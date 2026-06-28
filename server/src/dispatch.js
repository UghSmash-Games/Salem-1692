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
          // since the player already knows who they are
          targetSocket.emit('secret_phase_prompt', {
            prompt: entry.prompt,
            targets: entry.targets,
            acting: entry.acting,
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

    // Phase resolve → ALL clients in room (synchronized reveal timestamp)
    socket.on('phase_resolve', (data) => {
      if (socket.role !== 'host') return;
      // Broadcast to everyone else in the room
      socket.to(socket.roomCode).emit('phase_resolve', data);
      // Echo back to the host itself (for synchronized animation)
      socket.emit('phase_resolve', data);
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
 */
function forwardToHost(io, socket, event, data) {
  const room = getRoom(socket.roomCode);
  if (!room) return;

  const hostSocket = io.sockets.sockets.get(room.hostSocketId);
  if (!hostSocket) return;

  hostSocket.emit(event, {
    playerId: socket.playerId,
    ...(data || {}),
  });
}

module.exports = { registerDispatch };
