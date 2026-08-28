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

  test('a client CANNOT spoof playerId on a forwarded submit (server id wins)', async () => {
    // Regression: forwardToHost used to spread client data AFTER playerId, letting a client
    // overwrite it. The host's sender checks compare msg.playerId to the expected player, so a
    // spoof would let ANY player answer another player's confirm/target prompt.
    const host = trackClient(createClient());
    await waitForConnect(host);
    host.emit('create_room');
    const { code } = await waitFor(host, 'room_created');

    const player0 = trackClient(createClient());
    await waitForConnect(player0);
    player0.emit('join_room', { code, displayName: 'Alice' });
    await waitFor(player0, 'joined'); // p0

    const player1 = trackClient(createClient());
    await waitForConnect(player1);
    player1.emit('join_room', { code, displayName: 'Bob' });
    await waitFor(player1, 'joined'); // p1

    // p1 tries to answer AS p0.
    player1.emit('confirm_submit', { confirmed: true, playerId: 'p0' });
    const received = await waitFor(host, 'confirm_submit');

    // The server's own id for the sender must win — not the spoofed one.
    expect(received.playerId).toBe('p1');
    expect(received.confirmed).toBe(true);
  });

  test('target_submit is forwarded to host with playerId', async () => {
    const host = trackClient(createClient());
    await waitForConnect(host);
    host.emit('create_room');
    const { code } = await waitFor(host, 'room_created');

    const player = trackClient(createClient());
    await waitForConnect(player);
    player.emit('join_room', { code, displayName: 'Alice' });
    await waitFor(player, 'joined');

    player.emit('target_submit', { targetPlayerId: 'p2' });
    const received = await waitFor(host, 'target_submit');

    expect(received.playerId).toBe('p0');
    expect(received.targetPlayerId).toBe('p2');
  });

  test('target_submit from a mirror is silently ignored', async () => {
    const host = trackClient(createClient());
    await waitForConnect(host);
    host.emit('create_room');
    const { code } = await waitFor(host, 'room_created');

    const mirror = trackClient(createClient());
    await waitForConnect(mirror);
    mirror.emit('join_mirror', { code });
    await waitFor(mirror, 'joined');

    mirror.emit('target_submit', { targetPlayerId: 'p1' });
    await expectNoEvent(host, 'target_submit');
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

  test('target_request is sent ONLY to the target player (never others/mirror)', async () => {
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

    host.emit('target_request', {
      playerId: 'p0',
      prompt: 'robbery_recipient',
      targets: ['p1', 'p2'],
      seconds: 30,
    });

    const p0Data = await waitFor(player0, 'target_request');
    expect(p0Data.prompt).toBe('robbery_recipient');
    expect(p0Data.targets).toEqual(['p1', 'p2']);

    // The acting player's own decision prompt must never reach another player or a mirror.
    await expectNoEvent(player1, 'target_request');
    await expectNoEvent(mirror, 'target_request');
  });

  test('tryal_pick_request is sent ONLY to the chooser, and carries no card identities', async () => {
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

    host.emit('tryal_pick_request', {
      playerId: 'p0',
      targetPlayerId: 'p1',
      count: 3,
      seconds: 25,
      reason: 'accusation_reveal',
    });

    const p0Data = await waitFor(player0, 'tryal_pick_request');
    expect(p0Data.targetPlayerId).toBe('p1');
    expect(p0Data.count).toBe(3);
    expect(p0Data.reason).toBe('accusation_reveal');

    // 🔴 The whole point of the shape: a COUNT, never the cards and never their slot positions.
    // If someone adds labels or real indices to the payload, this fails.
    expect(p0Data.labels).toBeUndefined();
    expect(p0Data.cards).toBeUndefined();
    expect(p0Data.tryals).toBeUndefined();
    expect(p0Data.indices).toBeUndefined();

    // The chooser's own decision prompt must never reach the player being revealed, or a mirror.
    await expectNoEvent(player1, 'tryal_pick_request');
    await expectNoEvent(mirror, 'tryal_pick_request');
  });

  test('tryal_pick_submit reaches the host with a trusted playerId the client cannot spoof', async () => {
    const host = trackClient(createClient());
    await waitForConnect(host);
    host.emit('create_room');
    const { code } = await waitFor(host, 'room_created');

    const player0 = trackClient(createClient());
    await waitForConnect(player0);
    player0.emit('join_room', { code, displayName: 'Alice' });
    const joined0 = await waitFor(player0, 'joined');

    // Spoof attempt: claim to be another seat. The trusted id is spread LAST server-side, so the
    // host's `msg.playerId == expected` check — the ONLY authorization on this family of prompts —
    // still sees the real sender.
    player0.emit('tryal_pick_submit', { ordinal: 1, playerId: 'p9' });

    const hostData = await waitFor(host, 'tryal_pick_submit');
    expect(hostData.ordinal).toBe(1);
    expect(hostData.playerId).toBe(joined0.playerId);
    expect(hostData.playerId).not.toBe('p9');
  });

  test('a mirror cannot send tryal_pick_submit', async () => {
    const host = trackClient(createClient());
    await waitForConnect(host);
    host.emit('create_room');
    const { code } = await waitFor(host, 'room_created');

    const mirror = trackClient(createClient());
    await waitForConnect(mirror);
    mirror.emit('join_mirror', { code });
    await waitFor(mirror, 'joined');

    mirror.emit('tryal_pick_submit', { ordinal: 0 });

    await expectNoEvent(host, 'tryal_pick_submit');
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

// ─── Reconnection ──────────────────────────────────────────────

describe('rejoin_room', () => {
  /** Host + one joined player, returned with the seat's credentials. */
  async function seatOnePlayer() {
    const host = trackClient(createClient());
    await waitForConnect(host);
    host.emit('create_room');
    const { code } = await waitFor(host, 'room_created');

    const phone = trackClient(createClient());
    await waitForConnect(phone);
    phone.emit('join_room', { code, displayName: 'Alice' });
    const joined = await waitFor(phone, 'joined');

    return { host, phone, code, playerId: joined.playerId, token: joined.token };
  }

  test('a fresh join issues a seat token', async () => {
    const { token } = await seatOnePlayer();
    expect(token).toMatch(/^[0-9a-f]{32}$/);
  });

  test('the host is never told the token', async () => {
    // It is the one credential that lets a socket be handed another player's private_state.
    const host = trackClient(createClient());
    await waitForConnect(host);
    host.emit('create_room');
    const { code } = await waitFor(host, 'room_created');

    const phone = trackClient(createClient());
    await waitForConnect(phone);
    const notification = waitFor(host, 'player_joined');
    phone.emit('join_room', { code, displayName: 'Alice' });

    const payload = await notification;
    expect(payload).not.toHaveProperty('token');
  });

  test('a dropped player reclaims their seat on a new socket', async () => {
    const { host, phone, code, playerId, token } = await seatOnePlayer();

    // The phone drops — screen lock, wifi blip, tab reload.
    const left = waitFor(host, 'player_left');
    phone.disconnect();
    await left;

    const returning = trackClient(createClient());
    await waitForConnect(returning);
    const rejoinNotice = waitFor(host, 'player_rejoined');
    returning.emit('rejoin_room', { code, playerId, token });

    const joined = await waitFor(returning, 'joined');
    expect(joined.playerId).toBe(playerId);   // the SAME seat, not a new one
    expect(joined.roomCode).toBe(code);

    const notice = await rejoinNotice;
    expect(notice.playerId).toBe(playerId);
    expect(notice.displayName).toBe('Alice');
    expect(notice).not.toHaveProperty('token');
  });

  test('private_state follows the seat to the NEW socket', async () => {
    // The functional heart of reconnection: the host addresses playerId, and the relay must now
    // resolve that to the socket the player is actually holding.
    const { host, phone, code, playerId, token } = await seatOnePlayer();

    phone.disconnect();
    await waitFor(host, 'player_left');

    const returning = trackClient(createClient());
    await waitForConnect(returning);
    returning.emit('rejoin_room', { code, playerId, token });
    await waitFor(returning, 'joined');

    host.emit('private_state', { playerId, tryals: [{ label: 'Witch' }], hand: ['Alibi'] });
    const priv = await waitFor(returning, 'private_state');
    expect(priv.tryals[0].label).toBe('Witch');
  });

  test('a WRONG token cannot steal a seat, and leaks nothing', async () => {
    const { host, phone, code, playerId } = await seatOnePlayer();

    phone.disconnect();
    await waitFor(host, 'player_left');

    const attacker = trackClient(createClient());
    await waitForConnect(attacker);
    const noRejoin = expectNoEvent(host, 'player_rejoined');
    attacker.emit('rejoin_room', { code, playerId, token: 'f'.repeat(32) });

    const err = await waitFor(attacker, 'error_msg');
    expect(err.message).toBe('Could not rejoin');
    await noRejoin;

    // And the seat's private state must not reach them.
    const noPrivate = expectNoEvent(attacker, 'private_state');
    host.emit('private_state', { playerId, tryals: [{ label: 'Witch' }] });
    await noPrivate;
  });

  test('an unknown room and an unknown seat report the SAME failure as a bad token', async () => {
    // Uninformative by design — otherwise this is an oracle for enumerating seats.
    const { code, playerId, token } = await seatOnePlayer();

    const probe = trackClient(createClient());
    await waitForConnect(probe);

    probe.emit('rejoin_room', { code: 'ZZZZ', playerId, token });
    expect((await waitFor(probe, 'error_msg')).message).toBe('Could not rejoin');

    probe.emit('rejoin_room', { code, playerId: 'p99', token });
    expect((await waitFor(probe, 'error_msg')).message).toBe('Could not rejoin');

    probe.emit('rejoin_room', { code, playerId, token: 'nope' });
    expect((await waitFor(probe, 'error_msg')).message).toBe('Could not rejoin');
  });

  test('a takeover leaves exactly ONE socket on the seat', async () => {
    // Two live devices on one seat would fan private_state out to both.
    const { host, phone, code, playerId, token } = await seatOnePlayer();

    const second = trackClient(createClient());
    await waitForConnect(second);
    // ⚠ Listen BEFORE the takeover: the server evicts the old socket before it answers the new
    // one, so a listener registered after `joined` has already missed the notice.
    const evicted = waitFor(phone, 'error_msg');
    second.emit('rejoin_room', { code, playerId, token });
    await waitFor(second, 'joined');

    // The original is told, and stops receiving the seat's private state.
    const err = await evicted;
    expect(err.message).toBe('Seat taken over on another device');

    const noPrivate = expectNoEvent(phone, 'private_state');
    host.emit('private_state', { playerId, tryals: [{ label: 'Not a Witch' }] });
    await waitFor(second, 'private_state');
    await noPrivate;
  });

  test('an evicted socket loses its player role and cannot act', async () => {
    const { host, phone, code, playerId, token } = await seatOnePlayer();

    const second = trackClient(createClient());
    await waitForConnect(second);
    const evicted = waitFor(phone, 'error_msg');   // see the note above — listen first
    second.emit('rejoin_room', { code, playerId, token });
    await waitFor(second, 'joined');
    await evicted;

    const noAction = expectNoEvent(host, 'player_action');
    phone.emit('player_action', { card: 'Accusation', targetPlayerId: 'p1' });
    await noAction;
  });

  test('a reclaimed seat can act again', async () => {
    const { host, phone, code, playerId, token } = await seatOnePlayer();

    phone.disconnect();
    await waitFor(host, 'player_left');

    const returning = trackClient(createClient());
    await waitForConnect(returning);
    returning.emit('rejoin_room', { code, playerId, token });
    await waitFor(returning, 'joined');

    const action = waitFor(host, 'player_action');
    returning.emit('player_action', { card: 'Accusation', targetPlayerId: 'p1' });

    const received = await action;
    expect(received.playerId).toBe(playerId);  // server-attached, still trusted
    expect(received.card).toBe('Accusation');
  });

  test('a malformed rejoin is ignored, not crashed on', async () => {
    const probe = trackClient(createClient());
    await waitForConnect(probe);

    const noJoin = expectNoEvent(probe, 'joined');
    probe.emit('rejoin_room', {});
    probe.emit('rejoin_room', { code: 'ABCD' });
    probe.emit('rejoin_room', null);
    await noJoin;
  });
});
