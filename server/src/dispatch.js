'use strict';

/**
 * Role-enforced Socket.io message dispatch for Salem 1692.
 *
 * This is the critical file for multiplayer correctness. It:
 * - Tags every socket with its role (host/mirror/player) at join time
 * - Silently ignores messages from wrong roles
 * - Routes host broadcasts to correct recipients based on event type
 * - NEVER sends private_state or secret_phase_prompt to mirror or host clients
 * - Forwards player actions only to the host socket in that room
 *
 * See docs/protocol.md for the full event specification.
 */

const {
  createRoom,
  joinRoomAsPlayer,
  reclaimSeat,
  joinRoomAsMirror,
  removeSocket,
  getRoom,
  getPlayerByPlayerId,
  getPlayers,
  getMirrors,
} = require('./rooms');

/**
 * Register all socket event handlers on the given Socket.io server.
 * @param {import('socket.io').Server} io
 */
function registerDispatch(io) {
  io.on('connection', (socket) => {
    // ─── Room Management ───────────────────────────────────────

    // Host (Unity) creates a room
    socket.on('create_room', () => {
      const code = createRoom(socket.id);
      socket.role = 'host';
      socket.roomCode = code;
      socket.join(code);
      socket.emit('room_created', { code });
    });

    // Player (phone browser) joins a room
    socket.on('join_room', (data) => {
      if (!data || !data.code || !data.displayName) return;

      const result = joinRoomAsPlayer(data.code, socket.id, data.displayName);
      if (!result) {
        socket.emit('error_msg', { message: 'Room not found' });
        return;
      }

      socket.role = 'player';
      socket.roomCode = data.code;
      socket.playerId = result.playerId;
      socket.join(data.code);

      socket.emit('joined', {
        playerId: result.playerId,
        roomCode: data.code,
        // Seat secret — this socket only. Never broadcast, never sent to the host or a mirror.
        token: result.token,
      });

      // Notify host of new player
      const room = getRoom(data.code);
      if (room) {
        const hostSocket = io.sockets.sockets.get(room.hostSocketId);
        if (hostSocket) {
          hostSocket.emit('player_joined', {
            playerId: result.playerId,
            displayName: data.displayName,
          });
        }
      }
    });

    // Player reclaims a seat they already hold, on a NEW socket.
    //
    // A phone loses its socket constantly in normal play — screen lock, backgrounded tab, wifi blip
    // — and socket.io reconnects with a fresh socket.id. Without this the seat was unrecoverable:
    // join_room always mints a new playerId, so the returning player became a stranger to a host
    // that still held their tryals under the old one.
    //
    // 🔴 THE TOKEN IS THE AUTHORIZATION, and it is checked inside reclaimSeat. playerId is PUBLIC
    // (every game_state_update carries it), so it can never be what proves a seat is yours —
    // without the secret, any player could reclaim any seat and be sent its private_state: another
    // player's tryal cards and role. Never soften this to a displayName match; names are public.
    socket.on('rejoin_room', (data) => {
      if (!data || !data.code || !data.playerId || !data.token) return;

      const result = reclaimSeat(data.code, data.playerId, data.token, socket.id);
      if (!result) {
        // One message AND one code for unknown room, unknown seat AND bad token — telling them
        // apart would turn this into an oracle for enumerating seats.
        socket.emit('error_msg', { message: 'Could not rejoin', code: 'rejoin_failed' });
        return;
      }

      // Evict whatever socket held the seat before (a second tab, or a drop the server has not
      // noticed yet). One socket per seat, always, or private_state fans out to two devices.
      if (result.previousSocketId) {
        const stale = io.sockets.sockets.get(result.previousSocketId);
        if (stale) {
          stale.role = null;
          stale.roomCode = null;
          stale.playerId = null;
          stale.leave(data.code);
          // `code` so the evicted phone can recognise this WITHOUT matching on prose: it must drop
          // its stored seat, or its next reconnect would snatch the seat back and the two devices
          // would trade it back and forth.
          stale.emit('error_msg', {
            message: 'Seat taken over on another device',
            code: 'seat_taken',
          });
        }
      }

      socket.role = 'player';
      socket.roomCode = data.code;
      socket.playerId = result.playerId;
      socket.join(data.code);

      socket.emit('joined', {
        playerId: result.playerId,
        roomCode: data.code,
        token: result.token,
      });

      // Tell the host the seat is live again. It re-sends this player's state — the phone came back
      // with an empty store and knows nothing: not its tryals, not its hand, not the prompt it may
      // be blocking the game on. No token here; the host never sees one.
      const room = getRoom(data.code);
      if (room) {
        const hostSocket = io.sockets.sockets.get(room.hostSocketId);
        if (hostSocket) {
          hostSocket.emit('player_rejoined', {
            playerId: result.playerId,
            displayName: result.displayName,
          });
        }
      }
    });

    // Mirror (passive display) joins a room
    socket.on('join_mirror', (data) => {
      if (!data || !data.code) return;

      const success = joinRoomAsMirror(data.code, socket.id);
      if (!success) {
        socket.emit('error_msg', { message: 'Room not found' });
        return;
      }

      socket.role = 'mirror';
      socket.roomCode = data.code;
      socket.join(data.code);

      socket.emit('joined', { roomCode: data.code });

      // Notify host (informational)
      const room = getRoom(data.code);
      if (room) {
        const hostSocket = io.sockets.sockets.get(room.hostSocketId);
        if (hostSocket) {
          hostSocket.emit('mirror_joined', {});
        }
      }
    });

    // ─── Host → Client Events (role-gated, routed by type) ────

    // Public state → all players + all mirrors (not back to host)
    socket.on('game_state_update', (data) => {
      if (socket.role !== 'host') return;
      socket.to(socket.roomCode).emit('game_state_update', data);
    });

    // Private state → one specific player ONLY
    socket.on('private_state', (data) => {
      if (socket.role !== 'host') return;
      if (!data || !data.playerId) return;

      const player = getPlayerByPlayerId(socket.roomCode, data.playerId);
      if (!player) return;

      const targetSocket = io.sockets.sockets.get(player.socketId);
      if (targetSocket) {
        targetSocket.emit('private_state', data);
      }
    });

    // Secret phase prompt → each player gets their own copy individually
    // Host sends: { prompts: [{ playerId, prompt, targets, acting }, ...] }
    // Server sends each player only their entry
    socket.on('secret_phase_prompt', (data) => {
      if (socket.role !== 'host') return;
      if (!data || !Array.isArray(data.prompts)) return;

      const players = getPlayers(socket.roomCode);
      for (const entry of data.prompts) {
        const player = players.find(p => p.playerId === entry.playerId);
        if (!player) continue;

        const targetSocket = io.sockets.sockets.get(player.socketId);
        if (targetSocket) {
          // Send only this player's data — strip playerId from the payload
          // since the player already knows who they are. canFakeConfess (William Phipps,
          // confess window) is per-player like acting — routed to this one socket only.
          targetSocket.emit('secret_phase_prompt', {
            prompt: entry.prompt,
            targets: entry.targets,
            acting: entry.acting,
            canFakeConfess: entry.canFakeConfess,
          });
        }
      }
    });

    // Action request → one specific player ONLY
    socket.on('action_request', (data) => {
      if (socket.role !== 'host') return;
      if (!data || !data.playerId) return;

      const player = getPlayerByPlayerId(socket.roomCode, data.playerId);
      if (!player) return;

      const targetSocket = io.sockets.sockets.get(player.socketId);
      if (targetSocket) {
        targetSocket.emit('action_request', data);
      }
    });

    // Deck rearrange request → one specific player ONLY (the Tituba holder).
    // Carries the full deck card list — private, never broadcast.
    socket.on('deck_rearrange_request', (data) => {
      if (socket.role !== 'host') return;
      if (!data || !data.playerId) return;

      const player = getPlayerByPlayerId(socket.roomCode, data.playerId);
      if (!player) return;

      const targetSocket = io.sockets.sockets.get(player.socketId);
      if (targetSocket) {
        targetSocket.emit('deck_rearrange_request', data);
      }
    });

    // Card pick request → one specific player ONLY (a John Proctor / Martha drafter).
    // Carries an eliminated player's hand card list — private, never broadcast.
    socket.on('card_pick_request', (data) => {
      if (socket.role !== 'host') return;
      if (!data || !data.playerId) return;

      const player = getPlayerByPlayerId(socket.roomCode, data.playerId);
      if (!player) return;

      const targetSocket = io.sockets.sockets.get(player.socketId);
      if (targetSocket) {
        targetSocket.emit('card_pick_request', data);
      }
    });

    // Target request → one specific player ONLY (pick the sub-target of a two-target card,
    // e.g. Robbery's recipient). Private decision UI — never broadcast.
    socket.on('target_request', (data) => {
      if (socket.role !== 'host') return;
      if (!data || !data.playerId) return;

      const player = getPlayerByPlayerId(socket.roomCode, data.playerId);
      if (!player) return;

      const targetSocket = io.sockets.sockets.get(player.socketId);
      if (targetSocket) {
        targetSocket.emit('target_request', data);
      }
    });

    // Tryal pick request → one specific player ONLY (the accuser / piety remover / conspiracy
    // drawer choosing WHICH face-down tryal to flip on another player).
    // ⛔ Carries a COUNT of face-down tryals, never their identities and never their slot
    // positions — the chooser is picking blind, exactly as at a physical table. Routed to one
    // socket because it is that player's private decision UI, not because the count is secret.
    socket.on('tryal_pick_request', (data) => {
      if (socket.role !== 'host') return;
      if (!data || !data.playerId) return;

      const player = getPlayerByPlayerId(socket.roomCode, data.playerId);
      if (!player) return;

      const targetSocket = io.sockets.sockets.get(player.socketId);
      if (targetSocket) {
        targetSocket.emit('tryal_pick_request', data);
      }
    });

    // Confirm request → one specific player ONLY (their own optional "may" choice,
    // e.g. Abigail Williams' discard). Private decision UI — never broadcast.
    socket.on('confirm_request', (data) => {
      if (socket.role !== 'host') return;
      if (!data || !data.playerId) return;

      const player = getPlayerByPlayerId(socket.roomCode, data.playerId);
      if (!player) return;

      const targetSocket = io.sockets.sockets.get(player.socketId);
      if (targetSocket) {
        targetSocket.emit('confirm_request', data);
      }
    });

    // Phase resolve → ALL clients in room (synchronized reveal timestamp)
    socket.on('phase_resolve', (data) => {
      if (socket.role !== 'host') return;
      // Broadcast to everyone else in the room
      socket.to(socket.roomCode).emit('phase_resolve', data);
      // Echo back to the host itself (for synchronized animation)
      socket.emit('phase_resolve', data);
    });

    // Public reveal → all players + all mirrors (public card-show, e.g. Giles Corey).
    // Carries only public card names; not echoed to host (host renders from its own model).
    socket.on('public_reveal', (data) => {
      if (socket.role !== 'host') return;
      socket.to(socket.roomCode).emit('public_reveal', data);
    });

    // Public event-log entry ("What Has Passed") → all players + all mirrors.
    // Carries a closed-vocabulary `kind`, public player ids, a public card name and a short
    // enumerable `value` — never prose, and never secret-phase content (the Unity-side
    // GameEventKind enum has no kind that could express one). Not echoed to the host, which
    // renders from its own send-event.
    socket.on('game_event', (data) => {
      if (socket.role !== 'host') return;
      socket.to(socket.roomCode).emit('game_event', data);
    });

    // Elimination result → ALL clients in room
    socket.on('elimination_result', (data) => {
      if (socket.role !== 'host') return;
      socket.to(socket.roomCode).emit('elimination_result', data);
    });

    // Game over → ALL clients in room
    socket.on('game_over', (data) => {
      if (socket.role !== 'host') return;
      socket.to(socket.roomCode).emit('game_over', data);
    });

    // ─── Player → Host Events (role-gated, forwarded with playerId) ─

    socket.on('player_action', (data) => {
      if (socket.role !== 'player') return;
      forwardToHost(io, socket, 'player_action', data);
    });

    socket.on('secret_phase_submit', (data) => {
      if (socket.role !== 'player') return;
      forwardToHost(io, socket, 'secret_phase_submit', data);
    });

    socket.on('confess', (data) => {
      if (socket.role !== 'player') return;
      forwardToHost(io, socket, 'confess', data);
    });

    socket.on('deck_rearrange_submit', (data) => {
      if (socket.role !== 'player') return;
      forwardToHost(io, socket, 'deck_rearrange_submit', data);
    });

    socket.on('card_pick_submit', (data) => {
      if (socket.role !== 'player') return;
      forwardToHost(io, socket, 'card_pick_submit', data);
    });

    socket.on('confirm_submit', (data) => {
      if (socket.role !== 'player') return;
      forwardToHost(io, socket, 'confirm_submit', data);
    });

    socket.on('target_submit', (data) => {
      if (socket.role !== 'player') return;
      forwardToHost(io, socket, 'target_submit', data);
    });

    socket.on('tryal_pick_submit', (data) => {
      if (socket.role !== 'player') return;
      forwardToHost(io, socket, 'tryal_pick_submit', data);
    });

    // ─── Disconnect ────────────────────────────────────────────

    socket.on('disconnect', () => {
      const result = removeSocket(socket.id);

      if (result.type === 'host') {
        // Host left — notify all remaining sockets that the room is closed
        for (const sid of result.allSocketIds) {
          const s = io.sockets.sockets.get(sid);
          if (s) {
            s.emit('room_closed', {});
            s.leave(result.code);
          }
        }
      } else if (result.type === 'player' && result.code) {
        // Player left — notify the host
        const room = getRoom(result.code);
        if (room) {
          const hostSocket = io.sockets.sockets.get(room.hostSocketId);
          if (hostSocket) {
            hostSocket.emit('player_left', { playerId: result.playerId });
          }
        }
      }
      // Mirror disconnect — no notification needed
    });
  });
}

/**
 * Forward a player event to the host socket in the same room.
 * Attaches the player's server-assigned playerId to the payload.
 *
 * SECURITY — field order is load-bearing: the client's `data` is spread FIRST so the trusted,
 * server-assigned `playerId` always wins. The reverse order let a client overwrite its own
 * playerId (e.g. `confirm_submit { confirmed: true, playerId: "<someone-else>" }`), which defeated
 * the host-side sender checks that compare `msg.playerId` against the expected player — those
 * checks ARE the authorization for confirm/target/card-pick/deck-rearrange submits. Never move
 * `playerId` above the spread.
 */
function forwardToHost(io, socket, event, data) {
  const room = getRoom(socket.roomCode);
  if (!room) return;

  const hostSocket = io.sockets.sockets.get(room.hostSocketId);
  if (!hostSocket) return;

  hostSocket.emit(event, {
    ...(data || {}),
    playerId: socket.playerId, // trusted id LAST — client cannot override it
  });
}

module.exports = { registerDispatch };
