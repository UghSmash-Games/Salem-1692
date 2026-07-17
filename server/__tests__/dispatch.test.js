'use strict';

const { createServer } = require('http');
const { Server } = require('socket.io');
const Client = require('socket.io-client');
const { registerDispatch } = require('../src/dispatch');
const { clearAll } = require('../src/rooms');

// ─── Test Helpers ──────────────────────────────────────────────

let io, httpServer, port;

/** Wait for a specific event on a socket, with timeout. */
function waitFor(socket, event, ms = 2000) {
  return new Promise((resolve, reject) => {
    const timer = setTimeout(() => reject(new Error(`Timeout waiting for "${event}"`)), ms);
    socket.once(event, (data) => {
      clearTimeout(timer);
      resolve(data);
    });
  });
}

/** Assert that an event does NOT fire within a window. */
function expectNoEvent(socket, event, ms = 300) {
  return new Promise((resolve, reject) => {
    const timer = setTimeout(() => {
      socket.off(event, handler);
      resolve();
    }, ms);
    const handler = (data) => {
      clearTimeout(timer);
      reject(new Error(`Unexpected "${event}" received with data: ${JSON.stringify(data)}`));
    };
    socket.on(event, handler);
  });
}

/** Create a connected client socket. */
function createClient() {
  const client = Client(`http://localhost:${port}`, {
    transports: ['websocket'],
    forceNew: true,
  });
  return client;
}

/** Wait for a client to be fully connected. */
function waitForConnect(client) {
  return new Promise((resolve) => {
    if (client.connected) return resolve();
    client.on('connect', resolve);
  });
}

// ─── Setup / Teardown ──────────────────────────────────────────

const clients = [];

function trackClient(client) {
  clients.push(client);
  return client;
}

beforeEach((done) => {
  clearAll();
  httpServer = createServer();
  io = new Server(httpServer);
  registerDispatch(io);
  httpServer.listen(0, () => {
    port = httpServer.address().port;
    done();
  });
});

afterEach((done) => {
  for (const c of clients) {
    if (c.connected) c.disconnect();
  }
  clients.length = 0;
  io.close(() => done());
});

// ─── Room Creation ─────────────────────────────────────────────

describe('room creation', () => {
  test('host creates a room and receives a code', async () => {
    const host = trackClient(createClient());
    await waitForConnect(host);

    host.emit('create_room');
    const data = await waitFor(host, 'room_created');

    expect(data.code).toMatch(/^[A-Z]{4}$/);
  });
});

// ─── Join Flows ────────────────────────────────────────────────

describe('join flows', () => {
  test('player joins and host is notified', async () => {
    const host = trackClient(createClient());
    await waitForConnect(host);

    host.emit('create_room');
    const { code } = await waitFor(host, 'room_created');

    const player = trackClient(createClient());
    await waitForConnect(player);

    const hostNotification = waitFor(host, 'player_joined');
    player.emit('join_room', { code, displayName: 'Alice' });

    const joined = await waitFor(player, 'joined');
    expect(joined.playerId).toBe('p0');
    expect(joined.roomCode).toBe(code);

    const notification = await hostNotification;
    expect(notification.playerId).toBe('p0');
    expect(notification.displayName).toBe('Alice');
  });

  test('mirror joins a room', async () => {
    const host = trackClient(createClient());
    await waitForConnect(host);

    host.emit('create_room');
    const { code } = await waitFor(host, 'room_created');

    const mirror = trackClient(createClient());
    await waitForConnect(mirror);

    mirror.emit('join_mirror', { code });
    const joined = await waitFor(mirror, 'joined');
    expect(joined.roomCode).toBe(code);
  });

  test('joining invalid room returns error', async () => {
    const player = trackClient(createClient());
    await waitForConnect(player);

    player.emit('join_room', { code: 'ZZZZ', displayName: 'Alice' });
    const err = await waitFor(player, 'error_msg');
    expect(err.message).toBe('Room not found');
  });
});

// ─── Player → Host Routing ────────────────────────────────────

describe('player → host forwarding', () => {
  test('player_action is forwarded to host with playerId', async () => {
    const host = trackClient(createClient());
    await waitForConnect(host);
    host.emit('create_room');
    const { code } = await waitFor(host, 'room_created');

    const player = trackClient(createClient());
    await waitForConnect(player);
    player.emit('join_room', { code, displayName: 'Alice' });
    await waitFor(player, 'joined');

    player.emit('player_action', { card: 'Accusation', targetPlayerId: 'p1' });
    const received = await waitFor(host, 'player_action');

    expect(received.playerId).toBe('p0');
    expect(received.card).toBe('Accusation');
    expect(received.targetPlayerId).toBe('p1');
  });

  test('secret_phase_submit is forwarded to host with playerId', async () => {
    const host = trackClient(createClient());
    await waitForConnect(host);
    host.emit('create_room');
    const { code } = await waitFor(host, 'room_created');

    const player = trackClient(createClient());
    await waitForConnect(player);
    player.emit('join_room', { code, displayName: 'Alice' });
    await waitFor(player, 'joined');

    player.emit('secret_phase_submit', { selection: 'p2' });
    const received = await waitFor(host, 'secret_phase_submit');

    expect(received.playerId).toBe('p0');
    expect(received.selection).toBe('p2');
  });

  test('confess is forwarded to host with playerId', async () => {
    const host = trackClient(createClient());
    await waitForConnect(host);
    host.emit('create_room');
    const { code } = await waitFor(host, 'room_created');

    const player = trackClient(createClient());
    await waitForConnect(player);
    player.emit('join_room', { code, displayName: 'Alice' });
    await waitFor(player, 'joined');

    player.emit('confess', { tryalIndex: 2 });
    const received = await waitFor(host, 'confess');

    expect(received.playerId).toBe('p0');
    expect(received.tryalIndex).toBe(2);
  });

  test('deck_rearrange_submit is forwarded to host with playerId', async () => {
    const host = trackClient(createClient());
    await waitForConnect(host);
    host.emit('create_room');
    const { code } = await waitFor(host, 'room_created');

    const player = trackClient(createClient());
    await waitForConnect(player);
    player.emit('join_room', { code, displayName: 'Alice' });
    await waitFor(player, 'joined');

    player.emit('deck_rearrange_submit', { order: [2, 0, 1], confirmed: true });
    const received = await waitFor(host, 'deck_rearrange_submit');

    expect(received.playerId).toBe('p0');
    expect(received.order).toEqual([2, 0, 1]);
    expect(received.confirmed).toBe(true);
  });

  test('deck_rearrange_submit from a mirror is silently ignored', async () => {
    const host = trackClient(createClient());
    await waitForConnect(host);
    host.emit('create_room');
    const { code } = await waitFor(host, 'room_created');

    const mirror = trackClient(createClient());
    await waitForConnect(mirror);
    mirror.emit('join_mirror', { code });
    await waitFor(mirror, 'joined');

    mirror.emit('deck_rearrange_submit', { order: [0, 1], confirmed: true });
    await expectNoEvent(host, 'deck_rearrange_submit');
  });

  test('card_pick_submit is forwarded to host with playerId', async () => {
    const host = trackClient(createClient());
    await waitForConnect(host);
    host.emit('create_room');
    const { code } = await waitFor(host, 'room_created');

    const player = trackClient(createClient());
    await waitForConnect(player);
    player.emit('join_room', { code, displayName: 'Alice' });
    await waitFor(player, 'joined');

    player.emit('card_pick_submit', { index: 2 });
    const received = await waitFor(host, 'card_pick_submit');

    expect(received.playerId).toBe('p0');
    expect(received.index).toBe(2);
  });

  test('confirm_submit is forwarded to host with playerId', async () => {
    const host = trackClient(createClient());
    await waitForConnect(host);
    host.emit('create_room');
    const { code } = await waitFor(host, 'room_created');

    const player = trackClient(createClient());
    await waitForConnect(player);
    player.emit('join_room', { code, displayName: 'Alice' });
    await waitFor(player, 'joined');

    player.emit('confirm_submit', { confirmed: true });
    const received = await waitFor(host, 'confirm_submit');

    expect(received.playerId).toBe('p0');
    expect(received.confirmed).toBe(true);
  });

  test('confirm_submit from a mirror is silently ignored', async () => {
    const host = trackClient(createClient());
    await waitForConnect(host);
    host.emit('create_room');
    const { code } = await waitFor(host, 'room_created');

    const mirror = trackClient(createClient());
    await waitForConnect(mirror);
    mirror.emit('join_mirror', { code });
    await waitFor(mirror, 'joined');

    mirror.emit('confirm_submit', { confirmed: true });
    await expectNoEvent(host, 'confirm_submit');
  });

  test('card_pick_submit from a mirror is silently ignored', async () => {
    const host = trackClient(createClient());
    await waitForConnect(host);
    host.emit('create_room');
    const { code } = await waitFor(host, 'room_created');

    const mirror = trackClient(createClient());
    await waitForConnect(mirror);
    mirror.emit('join_mirror', { code });
    await waitFor(mirror, 'joined');

    mirror.emit('card_pick_submit', { index: 0 });
    await expectNoEvent(host, 'card_pick_submit');
  });
});

// ─── Role Enforcement ──────────────────────────────────────────

describe('role enforcement', () => {
  test('mirror cannot send player_action — silently ignored', async () => {
    const host = trackClient(createClient());
    await waitForConnect(host);
    host.emit('create_room');
    const { code } = await waitFor(host, 'room_created');

    const mirror = trackClient(createClient());
    await waitForConnect(mirror);
    mirror.emit('join_mirror', { code });
    await waitFor(mirror, 'joined');

    mirror.emit('player_action', { card: 'Accusation', targetPlayerId: 'p0' });

    // Host should NOT receive this
    await expectNoEvent(host, 'player_action');
  });

  test('host cannot send player_action as if it were a player', async () => {
    const host = trackClient(createClient());
    await waitForConnect(host);
    host.emit('create_room');
    await waitFor(host, 'room_created');

    // Host tries to emit player_action — should be silently ignored
    // (no one to receive it since the host IS the host)
    host.emit('player_action', { card: 'Accusation', targetPlayerId: 'p0' });

    // No crash, no error — just silently ignored
    await expectNoEvent(host, 'player_action');
  });
});

// ─── Host → Client Broadcasting ───────────────────────────────

describe('host → client broadcasting', () => {
  test('game_state_update reaches all players and mirrors', async () => {
    const host = trackClient(createClient());
    await waitForConnect(host);
    host.emit('create_room');
    const { code } = await waitFor(host, 'room_created');

    const player = trackClient(createClient());
    await waitForConnect(player);
    player.emit('join_room', { code, displayName: 'Alice' });
    await waitFor(player, 'joined');

    const mirror = trackClient(createClient());
    await waitForConnect(mirror);
    mirror.emit('join_mirror', { code });
    await waitFor(mirror, 'joined');

    const stateData = { turn: 'p0', accusations: {} };
    host.emit('game_state_update', stateData);

    const playerReceived = await waitFor(player, 'game_state_update');
    const mirrorReceived = await waitFor(mirror, 'game_state_update');

    expect(playerReceived.turn).toBe('p0');
    expect(mirrorReceived.turn).toBe('p0');
  });

  test('public_reveal reaches all players and mirrors (public card-show)', async () => {
    const host = trackClient(createClient());
    await waitForConnect(host);
    host.emit('create_room');
    const { code } = await waitFor(host, 'room_created');

    const player = trackClient(createClient());
    await waitForConnect(player);
    player.emit('join_room', { code, displayName: 'Alice' });
    await waitFor(player, 'joined');

    const mirror = trackClient(createClient());
    await waitForConnect(mirror);
    mirror.emit('join_mirror', { code });
    await waitFor(mirror, 'joined');

    const payload = { playerId: 'p0', cards: ['Evidence', 'Witness'], reason: 'giles_corey' };
    host.emit('public_reveal', payload);

    const pData = await waitFor(player, 'public_reveal');
    const mData = await waitFor(mirror, 'public_reveal');

    expect(pData.cards).toEqual(['Evidence', 'Witness']);
    expect(pData.reason).toBe('giles_corey');
    expect(mData.cards).toEqual(['Evidence', 'Witness']);
  });

  test('public_reveal from a player or mirror is silently ignored (host-only)', async () => {
    const host = trackClient(createClient());
    await waitForConnect(host);
    host.emit('create_room');
    const { code } = await waitFor(host, 'room_created');

    const player = trackClient(createClient());
    await waitForConnect(player);
    player.emit('join_room', { code, displayName: 'Alice' });
    await waitFor(player, 'joined');

    const mirror = trackClient(createClient());
    await waitForConnect(mirror);
    mirror.emit('join_mirror', { code });
    await waitFor(mirror, 'joined');

    // A non-host emitting public_reveal must not be relayed to anyone.
    player.emit('public_reveal', { playerId: 'p0', cards: ['Witness'], reason: 'spoof' });
    mirror.emit('public_reveal', { playerId: 'p0', cards: ['Witness'], reason: 'spoof' });
    await expectNoEvent(host, 'public_reveal');
    await expectNoEvent(mirror, 'public_reveal');
  });

  test('phase_resolve reaches all clients including host', async () => {
    const host = trackClient(createClient());
    await waitForConnect(host);
    host.emit('create_room');
    const { code } = await waitFor(host, 'room_created');

    const player = trackClient(createClient());
    await waitForConnect(player);
    player.emit('join_room', { code, displayName: 'Alice' });
    await waitFor(player, 'joined');

    const mirror = trackClient(createClient());
    await waitForConnect(mirror);
    mirror.emit('join_mirror', { code });
    await waitFor(mirror, 'joined');

    const revealAt = Date.now() + 3000;
    host.emit('phase_resolve', { revealAt });

    const pData = await waitFor(player, 'phase_resolve');
    const mData = await waitFor(mirror, 'phase_resolve');
    const hData = await waitFor(host, 'phase_resolve');

    expect(pData.revealAt).toBe(revealAt);
    expect(mData.revealAt).toBe(revealAt);
    expect(hData.revealAt).toBe(revealAt);
  });
});

// ─── Privacy Isolation (Critical Tests) ────────────────────────

describe('privacy isolation', () => {
  test('private_state is sent ONLY to the target player', async () => {
    const host = trackClient(createClient());
    await waitForConnect(host);
    host.emit('create_room');
    const { code } = await waitFor(host, 'room_created');

    const player0 = trackClient(createClient());
    await waitForConnect(player0);
    player0.emit('join_room', { code, displayName: 'Alice' });
    await waitFor(player0, 'joined');

    const player1 = trackClient(createClient());
    await waitForConnect(player1);
    player1.emit('join_room', { code, displayName: 'Bob' });
    await waitFor(player1, 'joined');

    const mirror = trackClient(createClient());
    await waitForConnect(mirror);
    mirror.emit('join_mirror', { code });
    await waitFor(mirror, 'joined');

    // Host sends private_state for player p0
    host.emit('private_state', {
      playerId: 'p0',
      tryals: ['Witch', 'NotAWitch'],
      hand: ['Accusation'],
    });

    // p0 should receive it
    const p0Data = await waitFor(player0, 'private_state');
    expect(p0Data.tryals).toEqual(['Witch', 'NotAWitch']);

    // p1 and mirror should NOT receive it
    await expectNoEvent(player1, 'private_state');
    await expectNoEvent(mirror, 'private_state');
  });

  test('secret_phase_prompt delivers each player only their own data', async () => {
    const host = trackClient(createClient());
    await waitForConnect(host);
    host.emit('create_room');
    const { code } = await waitFor(host, 'room_created');

    const player0 = trackClient(createClient());
    await waitForConnect(player0);
    player0.emit('join_room', { code, displayName: 'Alice' });
    await waitFor(player0, 'joined');

    const player1 = trackClient(createClient());
    await waitForConnect(player1);
    player1.emit('join_room', { code, displayName: 'Bob' });
    await waitFor(player1, 'joined');

    const mirror = trackClient(createClient());
    await waitForConnect(mirror);
    mirror.emit('join_mirror', { code });
    await waitFor(mirror, 'joined');

    // Host sends secret_phase_prompt batch. canFakeConfess (William Phipps) is per-player like
    // acting — only p0's entry has it true.
    host.emit('secret_phase_prompt', {
      prompts: [
        { playerId: 'p0', prompt: 'night_vote', targets: ['Alice', 'Bob'], acting: true, canFakeConfess: true },
        { playerId: 'p1', prompt: 'night_vote', targets: ['Alice', 'Bob'], acting: false, canFakeConfess: false },
      ],
    });

    // p0 gets their prompt with acting: true and canFakeConfess: true (their own entry only)
    const p0Data = await waitFor(player0, 'secret_phase_prompt');
    expect(p0Data.acting).toBe(true);
    expect(p0Data.canFakeConfess).toBe(true);
    expect(p0Data.prompt).toBe('night_vote');
    expect(p0Data.playerId).toBeUndefined(); // playerId stripped

    // p1 gets their prompt with acting: false and canFakeConfess: false — never p0's flag
    const p1Data = await waitFor(player1, 'secret_phase_prompt');
    expect(p1Data.acting).toBe(false);
    expect(p1Data.canFakeConfess).toBe(false);
    expect(p1Data.prompt).toBe('night_vote');

    // Mirror gets NOTHING
    await expectNoEvent(mirror, 'secret_phase_prompt');
  });

  test('action_request is sent ONLY to the target player', async () => {
    const host = trackClient(createClient());
    await waitForConnect(host);
    host.emit('create_room');
    const { code } = await waitFor(host, 'room_created');

    const player0 = trackClient(createClient());
    await waitForConnect(player0);
    player0.emit('join_room', { code, displayName: 'Alice' });
    await waitFor(player0, 'joined');

    const player1 = trackClient(createClient());
    await waitForConnect(player1);
    player1.emit('join_room', { code, displayName: 'Bob' });
    await waitFor(player1, 'joined');

    host.emit('action_request', { playerId: 'p0', actions: ['draw', 'play'] });

    const p0Data = await waitFor(player0, 'action_request');
    expect(p0Data.actions).toEqual(['draw', 'play']);

    await expectNoEvent(player1, 'action_request');
  });

  test('deck_rearrange_request is sent ONLY to the target player (never others/mirror)', async () => {
    const host = trackClient(createClient());
    await waitForConnect(host);
    host.emit('create_room');
    const { code } = await waitFor(host, 'room_created');

    const player0 = trackClient(createClient());
    await waitForConnect(player0);
    player0.emit('join_room', { code, displayName: 'Alice' });
    await waitFor(player0, 'joined');

    const player1 = trackClient(createClient());
    await waitForConnect(player1);
    player1.emit('join_room', { code, displayName: 'Bob' });
    await waitFor(player1, 'joined');

    const mirror = trackClient(createClient());
    await waitForConnect(mirror);
    mirror.emit('join_mirror', { code });
    await waitFor(mirror, 'joined');

    host.emit('deck_rearrange_request', {
      playerId: 'p0',
      cards: ['Accusation', 'Night', 'Conspiracy'],
      seconds: 60,
    });

    const p0Data = await waitFor(player0, 'deck_rearrange_request');
    expect(p0Data.cards).toEqual(['Accusation', 'Night', 'Conspiracy']);
    expect(p0Data.seconds).toBe(60);

    // The deck card list must never reach another player or a mirror.
    await expectNoEvent(player1, 'deck_rearrange_request');
    await expectNoEvent(mirror, 'deck_rearrange_request');
  });

  test('card_pick_request is sent ONLY to the target player (never others/mirror)', async () => {
    const host = trackClient(createClient());
    await waitForConnect(host);
    host.emit('create_room');
    const { code } = await waitFor(host, 'room_created');

    const player0 = trackClient(createClient());
    await waitForConnect(player0);
    player0.emit('join_room', { code, displayName: 'Alice' });
    await waitFor(player0, 'joined');

    const player1 = trackClient(createClient());
    await waitForConnect(player1);
    player1.emit('join_room', { code, displayName: 'Bob' });
    await waitFor(player1, 'joined');

    const mirror = trackClient(createClient());
    await waitForConnect(mirror);
    mirror.emit('join_mirror', { code });
    await waitFor(mirror, 'joined');

    host.emit('card_pick_request', {
      playerId: 'p0',
      cards: ['Accusation', 'Alibi', 'Asylum'],
      pickNumber: 1,
      totalPicks: 3,
      seconds: 45,
    });

    const p0Data = await waitFor(player0, 'card_pick_request');
    expect(p0Data.cards).toEqual(['Accusation', 'Alibi', 'Asylum']);
    expect(p0Data.pickNumber).toBe(1);
    expect(p0Data.totalPicks).toBe(3);

    // The eliminated player's hand list must never reach another player or a mirror.
    await expectNoEvent(player1, 'card_pick_request');
    await expectNoEvent(mirror, 'card_pick_request');
  });

  test('confirm_request is sent ONLY to the target player (never others/mirror)', async () => {
    const host = trackClient(createClient());
    await waitForConnect(host);
    host.emit('create_room');
    const { code } = await waitFor(host, 'room_created');

    const player0 = trackClient(createClient());
    await waitForConnect(player0);
    player0.emit('join_room', { code, displayName: 'Alice' });
    await waitFor(player0, 'joined');

    const player1 = trackClient(createClient());
    await waitForConnect(player1);
    player1.emit('join_room', { code, displayName: 'Bob' });
    await waitFor(player1, 'joined');

    const mirror = trackClient(createClient());
    await waitForConnect(mirror);
    mirror.emit('join_mirror', { code });
    await waitFor(mirror, 'joined');

    host.emit('confirm_request', {
      playerId: 'p0',
      prompt: 'abigail_discard',
      items: ['Evidence', 'Accusation'],
      count: 4,
      seconds: 20,
    });

    const p0Data = await waitFor(player0, 'confirm_request');
    expect(p0Data.prompt).toBe('abigail_discard');
    expect(p0Data.items).toEqual(['Evidence', 'Accusation']);
    expect(p0Data.count).toBe(4);

    // A player's own decision prompt must never reach another player or a mirror.
    await expectNoEvent(player1, 'confirm_request');
    await expectNoEvent(mirror, 'confirm_request');
  });
});

// ─── Room Cleanup ──────────────────────────────────────────────

describe('room cleanup', () => {
  test('host disconnect sends room_closed to all clients', async () => {
    const host = trackClient(createClient());
    await waitForConnect(host);
    host.emit('create_room');
    const { code } = await waitFor(host, 'room_created');

    const player = trackClient(createClient());
    await waitForConnect(player);
    player.emit('join_room', { code, displayName: 'Alice' });
    await waitFor(player, 'joined');

    const mirror = trackClient(createClient());
    await waitForConnect(mirror);
    mirror.emit('join_mirror', { code });
    await waitFor(mirror, 'joined');

    const playerClosed = waitFor(player, 'room_closed');
    const mirrorClosed = waitFor(mirror, 'room_closed');

    host.disconnect();

    await playerClosed;
    await mirrorClosed;
  });

  test('player disconnect notifies host with player_left', async () => {
    const host = trackClient(createClient());
    await waitForConnect(host);
    host.emit('create_room');
    const { code } = await waitFor(host, 'room_created');

    const player = trackClient(createClient());
    await waitForConnect(player);
    player.emit('join_room', { code, displayName: 'Alice' });
    await waitFor(player, 'joined');

    const leftNotification = waitFor(host, 'player_left');
    player.disconnect();

    const data = await leftNotification;
    expect(data.playerId).toBe('p0');
  });
});
