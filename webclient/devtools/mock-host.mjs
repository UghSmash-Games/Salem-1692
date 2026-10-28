/**
 * Dev-only mock host — stands in for the Unity game host so the phone client
 * can be driven end-to-end before Phase 4 wires up real game logic.
 *
 * Usage:
 *   1. Start the server:   cd server && npm run dev
 *   2. Run this host:      cd webclient && npm run mock-host
 *   3. It prints a ROOM CODE — join from two phones/tabs at /join.
 *   4. Type single-letter commands (then Enter) to drive the game:
 *
 *        s  game_state_update   (public board for all joined players)
 *        p  private_state       (sends each player their own tryals/hand/role;
 *                                first player is the witch, rest townspeople)
 *        v  secret_phase_prompt  night_vote — acting:true for the witch (p0),
 *                                acting:false for everyone else
 *        b  secret_phase_prompt  black_cat (dawn)
 *        a  action_request       to p0 (draw/play/confess)
 *        r  phase_resolve        reveal 3s out
 *        e  elimination_result   eliminate p1
 *        o  game_over            townspeople win, reveal tryals
 *        q  quit
 *
 * The server attaches each sender's playerId to inbound player messages, which
 * this host just logs.
 */

import { io } from 'socket.io-client';
import readline from 'node:readline';

const SERVER_URL = process.env.VITE_SERVER_URL || 'http://localhost:3000';

const socket = io(SERVER_URL, { transports: ['websocket'] });

/** playerId -> displayName, in join order. */
const players = new Map();

socket.on('connect', () => {
  console.log(`[mock-host] connected to ${SERVER_URL}`);
  socket.emit('create_room');
});

socket.on('room_created', ({ code }) => {
  console.log('\n══════════════════════════════════');
  console.log(`  ROOM CODE:  ${code}`);
  console.log('══════════════════════════════════');
  console.log('Join at http://localhost:5173/join, then use the commands below.\n');
  printHelp();
});

socket.on('player_joined', ({ playerId, displayName }) => {
  players.set(playerId, displayName);
  console.log(`[mock-host] player joined: ${playerId} (${displayName})`);
});

socket.on('player_left', ({ playerId }) => {
  console.log(`[mock-host] player left: ${playerId}`);
  players.delete(playerId);
});

socket.on('mirror_joined', () => {
  console.log('[mock-host] a mirror display connected');
});

// Log anything players send back so we can confirm acting:false is discarded
// upstream of game logic (here we just observe — Unity would gate on acting).
socket.on('player_action', (d) => console.log('[mock-host] player_action', d));
socket.on('secret_phase_submit', (d) =>
  console.log('[mock-host] secret_phase_submit', d),
);
socket.on('confess', (d) => console.log('[mock-host] confess', d));

function playerIds() {
  return [...players.keys()];
}

function publicPlayers() {
  return playerIds().map((playerId) => ({
    playerId,
    displayName: players.get(playerId),
    accusations: 0,
    eliminated: false,
    statusCards: [],
  }));
}

function sendGameState(phase = 'day') {
  const ids = playerIds();
  socket.emit('game_state_update', {
    phase,
    whoseTurn: ids[0] ?? null,
    players: publicPlayers(),
    deckCount: 28,
    discardCount: 4,
  });
  console.log(`[mock-host] → game_state_update (phase=${phase})`);
}

/**
 * Reveal sequence: broadcast a phase_resolve timestamp 3s out, then emit the
 * elimination_result at that moment. Two /display tabs side by side should
 * flip in unison because they schedule against the shared revealAt, not on
 * message receipt.
 */
function sendRevealSequence() {
  const ids = playerIds();
  const target = ids[1] ?? ids[0];
  const revealAt = Date.now() + 3000;
  socket.emit('phase_resolve', { revealAt });
  console.log('[mock-host] → phase_resolve (reveal in 3s)…');
  setTimeout(() => {
    if (!target) return;
    socket.emit('elimination_result', {
      playerId: target,
      eliminated: true,
      savedBy: null,
    });
    console.log(`[mock-host] → elimination_result: ${target} eliminated`);
  }, 3000);
}

function sendPrivateStates() {
  playerIds().forEach((playerId, idx) => {
    const isWitch = idx === 0;
    socket.emit('private_state', {
      playerId,
      tryals: isWitch
        ? [
            { label: 'Witch', faceUp: false },
            { label: 'Not a Witch', faceUp: false },
          ]
        : [
            { label: 'Not a Witch', faceUp: false },
            { label: 'Not a Witch', faceUp: false },
          ],
      hand: ['Accusation', 'Alibi'],
      role: isWitch ? 'witch' : 'townsperson',
    });
  });
  console.log('[mock-host] → private_state to each player (p0 is the witch)');
}

function sendSecretPhase(promptType) {
  const names = publicPlayers().map((p) => p.displayName);
  const prompts = playerIds().map((playerId, idx) => ({
    playerId,
    prompt: promptType,
    targets: names,
    acting: idx === 0, // only p0 (the witch) is acting
  }));
  socket.emit('secret_phase_prompt', { prompts });
  console.log(
    `[mock-host] → secret_phase_prompt (${promptType}); acting=true only for p0`,
  );
}

function sendActionRequest() {
  const ids = playerIds();
  if (ids.length === 0) return;
  socket.emit('action_request', {
    playerId: ids[0],
    actions: ['draw', 'play', 'confess'],
  });
  console.log(`[mock-host] → action_request to ${ids[0]}`);
}

function sendPhaseResolve() {
  socket.emit('phase_resolve', { revealAt: Date.now() + 3000 });
  console.log('[mock-host] → phase_resolve (reveal in 3s)');
}

function sendElimination() {
  const ids = playerIds();
  const target = ids[1] ?? ids[0];
  if (!target) return;
  socket.emit('elimination_result', {
    playerId: target,
    eliminated: true,
    savedBy: null,
  });
  console.log(`[mock-host] → elimination_result: ${target} eliminated`);
}

function sendGameOver() {
  const tryals = {};
  playerIds().forEach((playerId, idx) => {
    tryals[playerId] =
      idx === 0
        ? [{ label: 'Witch', faceUp: true }]
        : [{ label: 'Not a Witch', faceUp: true }];
  });
  socket.emit('game_over', { winner: 'townspeople', tryals });
  console.log('[mock-host] → game_over (townspeople win)');
}

function printHelp() {
  console.log(
    [
      'Commands:',
      '  s  game_state_update      p  private_state',
      '  v  night_vote prompt      b  black_cat (dawn) prompt',
      '  a  action_request (p0)    r  phase_resolve',
      '  e  eliminate p1           o  game_over',
      '  ── mirror (/display) ──',
      '  n  game_state phase=night (overlay on)',
      '  d  game_state phase=day   (overlay off)',
      '  x  reveal sequence: phase_resolve + elimination in sync',
      '  h  help                   q  quit',
    ].join('\n'),
  );
}

const rl = readline.createInterface({ input: process.stdin });
rl.on('line', (line) => {
  const cmd = line.trim().toLowerCase();
  switch (cmd) {
    case 's': sendGameState(); break;
    case 'p': sendPrivateStates(); break;
    case 'v': sendSecretPhase('night_vote'); break;
    case 'b': sendSecretPhase('black_cat'); break;
    case 'a': sendActionRequest(); break;
    case 'r': sendPhaseResolve(); break;
    case 'e': sendElimination(); break;
    case 'o': sendGameOver(); break;
    case 'n': sendGameState('night'); break;
    case 'd': sendGameState('day'); break;
    case 'x': sendRevealSequence(); break;
    case 'h': printHelp(); break;
    case 'q':
      socket.disconnect();
      process.exit(0);
      break;
    default:
      if (cmd) console.log(`[mock-host] unknown command: "${cmd}" (h for help)`);
  }
});

socket.on('disconnect', () => console.log('[mock-host] disconnected'));
socket.on('connect_error', (err) =>
  console.error('[mock-host] connect_error:', err.message),
);
