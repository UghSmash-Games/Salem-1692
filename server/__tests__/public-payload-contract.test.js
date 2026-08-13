'use strict';

/**
 * PUBLIC PAYLOAD CONTRACT — permanent regression guard for the Phase 7 masking boundary.
 *
 * WHY THIS EXISTS
 * Before Phase 7, NetworkStateBroadcaster's PUBLIC builder never touched Player.TryalCards, so the
 * public path was *structurally* incapable of leaking tryal identities. Adding `tryalTotal` and
 * `revealedTryals` (so the host TV can draw real tryal art) changed that: the public builder now
 * reads that collection, and a single `.Where(t => t.IsRevealed)` inside
 * NetworkStateBroadcaster.BuildRevealedTryalLabels is the ENTIRE enforcement. The server relays
 * game_state_update verbatim (dispatch.js — host-role gate only, no field filtering), so there is
 * no server-side backstop. This file is the backstop.
 *
 * ⚠ SCOPE — READ BEFORE TRUSTING IT
 * These tests lock the WIRE CONTRACT (payload shape + relay behaviour). They CANNOT execute the
 * Unity C# builder — there is no play-mode harness in this project. So they catch:
 *   • a positional/faceUp shape creeping into the public DTO,
 *   • an unrevealed label riding along in a payload of this shape,
 *   • a new PublicPlayer field added without a privacy review (the allow-list test),
 *   • the payload reaching a mirror unfiltered.
 * They do NOT prove the C# builder filters correctly. That remains code-review-verified, the same
 * posture as the both-teams-lose and cascade-orphan edges (see docs/character-spec.md).
 */

const { createServer } = require('http');
const { Server } = require('socket.io');
const Client = require('socket.io-client');
const { registerDispatch } = require('../src/dispatch');
const { clearAll } = require('../src/rooms');

// ─── Harness (mirrors dispatch.test.js) ────────────────────────

let io, httpServer, port;
const clients = [];

function waitFor(socket, event, ms = 2000) {
  return new Promise((resolve, reject) => {
    const timer = setTimeout(() => reject(new Error(`Timeout waiting for "${event}"`)), ms);
    socket.once(event, (data) => {
      clearTimeout(timer);
      resolve(data);
    });
  });
}

/** Assert an event does NOT arrive within a window. */
function expectNoEvent(socket, event, ms = 300) {
  return new Promise((resolve, reject) => {
    const handler = (data) => {
      clearTimeout(timer);
      socket.off(event, handler);
      reject(new Error(`Unexpected "${event}" received: ${JSON.stringify(data)}`));
    };
    const timer = setTimeout(() => {
      socket.off(event, handler);
      resolve();
    }, ms);
    socket.on(event, handler);
  });
}

function createClient() {
  const client = Client(`http://localhost:${port}`, {
    transports: ['websocket'],
    forceNew: true,
  });
  clients.push(client);
  return client;
}

function waitForConnect(client) {
  return new Promise((resolve) => {
    if (client.connected) return resolve();
    client.on('connect', resolve);
  });
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

// ─── Fixtures ──────────────────────────────────────────────────

const TRYAL_LABELS = ['Witch', 'Not a Witch', 'Constable'];

/**
 * A representative public payload exactly as NetworkStateBroadcaster.BuildGameStateUpdate emits it.
 * "Goody Proctor" is the important seat: 3 tryals held, only 1 revealed — so 2 identities are
 * hidden and must appear NOWHERE on the wire.
 */
function buildPublicPayload() {
  return {
    phase: 'day',
    whoseTurn: 'p0',
    deckCount: 22,
    discardCount: 7,
    topDiscard: 'Accusation', // TOP CARD ONLY — never the ordered pile
    players: [
      {
        playerId: 'p0',
        displayName: 'Goody Proctor',
        accusations: 4,
        accusationLimit: 7,
        eliminated: false,
        statusCards: ['Piety'],
        accusationCards: ['Evidence', 'Accusation'],
        townHall: 'Abigail Williams',
        tryalTotal: 3,
        revealedTryals: ['Not a Witch'], // 2 of 3 still face-down
        handCount: 2,
      },
      {
        playerId: 'p1',
        displayName: 'Giles Corey',
        accusations: 0,
        accusationLimit: 7,
        eliminated: true,
        statusCards: [],
        accusationCards: [],
        townHall: null, // nulled at elimination — consumers cache the last non-empty value
        tryalTotal: 2,
        revealedTryals: ['Constable', 'Witch'], // fully revealed → eliminated
        handCount: 0,
      },
    ],
  };
}

/** Every key a PublicPlayer entry is allowed to carry. Adding one MUST be a deliberate act. */
const ALLOWED_PUBLIC_PLAYER_KEYS = [
  'playerId',
  'displayName',
  'accusations',
  'accusationLimit',
  'eliminated',
  'statusCards',
  'accusationCards',
  'townHall',
  'tryalTotal',
  'revealedTryals',
  'handCount',
];

/** Public player fields that must be a COUNT, never a collection. The type IS the privacy guard. */
const COUNT_ONLY_PUBLIC_FIELDS = ['handCount', 'tryalTotal', 'accusations', 'accusationLimit'];

/** Keys that would leak private state or face-down positions if they ever appeared publicly. */
const FORBIDDEN_PUBLIC_KEYS = [
  'tryals',        // the private TryalViewMsg array
  'hand',
  'role',
  'isWitch',
  'isConstable',
  'acting',
  'fellowWitches',
  'witchVotes',
  'faceUp',        // positional mirror of the private DTO
  'tryalIndex',
  'unrevealedTryals',
  'unrevealedCount',
];

// ─── The invariants, as callable predicates ────────────────────
//
// Extracted so they can be applied BOTH to a relayed good payload (must find nothing) and to
// deliberately corrupted payloads (must fire). The negative-control tests at the bottom are what
// prove these are real checks rather than assertions that happen to be green.

/**
 * Self-consistency violations detectable WITHOUT knowing ground truth — i.e. the checks that would
 * still work against a live payload from a real game.
 */
function selfConsistencyViolations(p) {
  const bad = [];

  if ((p.revealedTryals ?? []).length > (p.tryalTotal ?? 0)) {
    bad.push('more revealed labels than tryals held');
  }

  const labels = p.revealedTryals ?? [];
  const sorted = [...labels].sort();
  if (JSON.stringify(labels) !== JSON.stringify(sorted)) {
    bad.push('revealedTryals not canonically sorted (carries slot order)');
  }

  for (const label of labels) {
    if (!TRYAL_LABELS.includes(label)) bad.push(`not a real tryal label: ${label}`);
  }

  for (const key of Object.keys(p)) {
    if (!ALLOWED_PUBLIC_PLAYER_KEYS.includes(key)) bad.push(`unreviewed public field: ${key}`);
  }

  const overlap = (p.accusationCards ?? []).filter((c) => (p.statusCards ?? []).includes(c));
  if (overlap.length > 0) bad.push(`Red/Blue split overlaps: ${overlap.join(',')}`);

  // The TYPE is the privacy guard for these. handCount widened to a collection would leak the whole
  // hand; tryalTotal widened would leak identities. Catch the widening, not just the values.
  for (const field of COUNT_ONLY_PUBLIC_FIELDS) {
    if (field in p && typeof p[field] !== 'number') {
      bad.push(`${field} must be a count, got ${Array.isArray(p[field]) ? 'array' : typeof p[field]}`);
    }
  }

  return bad;
}

/** Violations at the payload root (not per-player). */
function payloadLevelViolations(payload) {
  const bad = [];

  // topDiscard is the TOP CARD ONLY. An array here means someone published the ordered pile, which
  // leaks play history beyond the table AND exposes Samuel Parris' discard-draw pool.
  if ('topDiscard' in payload && payload.topDiscard !== null &&
      typeof payload.topDiscard !== 'string') {
    bad.push(
      `topDiscard must be a single card name, got ${Array.isArray(payload.topDiscard) ? 'array (the PILE)' : typeof payload.topDiscard}`
    );
  }

  return bad;
}

/** Forbidden keys anywhere in the serialized payload. */
function forbiddenKeysIn(payload) {
  const serialized = JSON.stringify(payload);
  return FORBIDDEN_PUBLIC_KEYS.filter((key) => serialized.includes(`"${key}"`));
}

async function setUpRoomWithMirrorAndPlayer() {
  const host = createClient();
  await waitForConnect(host);
  host.emit('create_room');
  const { code } = await waitFor(host, 'room_created');

  const player = createClient();
  await waitForConnect(player);
  player.emit('join_room', { code, displayName: 'Alice' });
  await waitFor(player, 'joined');

  const mirror = createClient();
  await waitForConnect(mirror);
  mirror.emit('join_mirror', { code });
  await waitFor(mirror, 'joined');

  return { host, player, mirror };
}

// ─── Tests ─────────────────────────────────────────────────────

describe('public payload contract — tryal privacy', () => {
  test('no UNREVEALED tryal label reaches a player or a mirror', async () => {
    const { host, player, mirror } = await setUpRoomWithMirrorAndPlayer();

    const playerGot = waitFor(player, 'game_state_update');
    const mirrorGot = waitFor(mirror, 'game_state_update');
    host.emit('game_state_update', buildPublicPayload());

    for (const received of [await playerGot, await mirrorGot]) {
      const proctor = received.players.find((p) => p.playerId === 'p0');

      // The core invariant: fewer labels than cards means the rest stayed face-down.
      expect(proctor.revealedTryals.length).toBeLessThan(proctor.tryalTotal);

      // Every emitted label must be one that was genuinely revealed. If the `.Where(IsRevealed)`
      // in BuildRevealedTryalLabels were ever deleted, all 3 labels would appear here.
      expect(proctor.revealedTryals).toEqual(['Not a Witch']);
      expect(proctor.revealedTryals).toHaveLength(1);
    }
  });

  test('revealedTryals never exceeds tryalTotal for any player', async () => {
    const { host, player } = await setUpRoomWithMirrorAndPlayer();

    const playerGot = waitFor(player, 'game_state_update');
    host.emit('game_state_update', buildPublicPayload());
    const received = await playerGot;

    for (const p of received.players) {
      expect(p.revealedTryals.length).toBeLessThanOrEqual(p.tryalTotal);
    }
  });

  test('revealedTryals is canonically sorted — carries no slot-order information', async () => {
    const { host, player } = await setUpRoomWithMirrorAndPlayer();

    const playerGot = waitFor(player, 'game_state_update');
    host.emit('game_state_update', buildPublicPayload());
    const received = await playerGot;

    // Position-free by construction: tryals are APPENDED on receipt, so a Conspiracy giver who
    // knows the card they passed could otherwise pin it to an exact face-down slot.
    for (const p of received.players) {
      const sorted = [...p.revealedTryals].sort();
      expect(p.revealedTryals).toEqual(sorted);
    }
  });

  test('every emitted label is a real tryal label (no internal enum or index leakage)', async () => {
    const { host, player } = await setUpRoomWithMirrorAndPlayer();

    const playerGot = waitFor(player, 'game_state_update');
    host.emit('game_state_update', buildPublicPayload());
    const received = await playerGot;

    for (const p of received.players) {
      for (const label of p.revealedTryals) {
        expect(TRYAL_LABELS).toContain(label);
      }
    }
  });
});

describe('public payload contract — shape', () => {
  test('PublicPlayer carries no forbidden key', async () => {
    const { host, player, mirror } = await setUpRoomWithMirrorAndPlayer();

    const playerGot = waitFor(player, 'game_state_update');
    const mirrorGot = waitFor(mirror, 'game_state_update');
    host.emit('game_state_update', buildPublicPayload());

    for (const received of [await playerGot, await mirrorGot]) {
      const serialized = JSON.stringify(received);
      for (const key of FORBIDDEN_PUBLIC_KEYS) {
        expect(serialized).not.toContain(`"${key}"`);
      }
    }
  });

  test('PublicPlayer key set is locked — a new field must be a deliberate, reviewed act', async () => {
    const { host, player } = await setUpRoomWithMirrorAndPlayer();

    const playerGot = waitFor(player, 'game_state_update');
    host.emit('game_state_update', buildPublicPayload());
    const received = await playerGot;

    // If this fails because you ADDED a field: that is the point. Confirm the field is genuinely
    // public per the rulebook (see docs/phase-7-host-seat-design.md §7), then add it here.
    for (const p of received.players) {
      for (const key of Object.keys(p)) {
        expect(ALLOWED_PUBLIC_PLAYER_KEYS).toContain(key);
      }
    }
  });

  test('accusationCards and statusCards are disjoint — the Red/Blue split has no overlap', async () => {
    const { host, player } = await setUpRoomWithMirrorAndPlayer();

    const playerGot = waitFor(player, 'game_state_update');
    host.emit('game_state_update', buildPublicPayload());
    const received = await playerGot;

    // Both come from the ONE Player.StatusCards list, split by Card.CardColor. A card appearing in
    // both fields would mean the filters drifted out of complement.
    for (const p of received.players) {
      const overlap = (p.accusationCards ?? []).filter((c) => (p.statusCards ?? []).includes(c));
      expect(overlap).toEqual([]);
    }
  });

  test('the good payload passes every invariant (baseline for the negative controls)', async () => {
    const { host, player } = await setUpRoomWithMirrorAndPlayer();

    const playerGot = waitFor(player, 'game_state_update');
    host.emit('game_state_update', buildPublicPayload());
    const received = await playerGot;

    expect(forbiddenKeysIn(received)).toEqual([]);
    expect(payloadLevelViolations(received)).toEqual([]);
    for (const p of received.players) {
      expect(selfConsistencyViolations(p)).toEqual([]);
    }
  });

  test('handCount is a count and hand CONTENTS never reach the wire', async () => {
    const { host, player, mirror } = await setUpRoomWithMirrorAndPlayer();

    const playerGot = waitFor(player, 'game_state_update');
    const mirrorGot = waitFor(mirror, 'game_state_update');
    host.emit('game_state_update', buildPublicPayload());

    for (const received of [await playerGot, await mirrorGot]) {
      for (const p of received.players) {
        expect(typeof p.handCount).toBe('number');
      }
      // "hand" is already in FORBIDDEN_PUBLIC_KEYS; this asserts the pairing explicitly.
      expect(forbiddenKeysIn(received)).not.toContain('hand');
    }
  });

  test('topDiscard is a single card name, never the pile', async () => {
    const { host, player } = await setUpRoomWithMirrorAndPlayer();

    const playerGot = waitFor(player, 'game_state_update');
    host.emit('game_state_update', buildPublicPayload());
    const received = await playerGot;

    expect(typeof received.topDiscard).toBe('string');
    expect(Array.isArray(received.topDiscard)).toBe(false);
    expect(payloadLevelViolations(received)).toEqual([]);
  });
});

// ─── Event log contract ────────────────────────────────────────

/** The CLOSED vocabulary. Adding a kind here must be a deliberate, reviewed act. */
const ALLOWED_EVENT_KINDS = [
  'game_started',
  'phase_changed',
  'card_played',
  'tryal_revealed',
  'player_eliminated',
  'double_witch_revealed',
  'confession_revealed',
  'gavel_placed',
  'game_over',
];

/** Only these fields may ride on a log event. Note the absence of anything prose-shaped. */
const ALLOWED_EVENT_KEYS = ['kind', 'actorId', 'targetId', 'cardName', 'value', 'atMs'];

/** Field names that would (re)introduce a free-text channel. The renderer owns all prose. */
const FORBIDDEN_EVENT_KEYS = ['text', 'message', 'msg', 'description', 'body', 'label', 'prose'];

function buildGameEvent(overrides = {}) {
  return {
    kind: 'tryal_revealed',
    actorId: null,
    targetId: 'p0',
    cardName: null,
    value: 'Not a Witch',
    atMs: 1754049840000,
    ...overrides,
  };
}

function eventViolations(e) {
  const bad = [];

  if (!ALLOWED_EVENT_KINDS.includes(e.kind)) bad.push(`unreviewed event kind: ${e.kind}`);

  for (const key of Object.keys(e)) {
    if (!ALLOWED_EVENT_KEYS.includes(key)) bad.push(`unreviewed event field: ${key}`);
    if (FORBIDDEN_EVENT_KEYS.includes(key.toLowerCase())) bad.push(`free-text field: ${key}`);
  }

  if (typeof e.atMs !== 'number') bad.push('atMs must be epoch milliseconds (a number)');

  // `value` is a short enumerable label, never a sentence. A generous cap still rules out prose.
  if (e.value != null && String(e.value).length > 24) bad.push('value looks like prose, not a label');

  return bad;
}

describe('event log contract', () => {
  test('a well-formed event relays to players and mirrors', async () => {
    const { host, player, mirror } = await setUpRoomWithMirrorAndPlayer();

    const playerGot = waitFor(player, 'game_event');
    const mirrorGot = waitFor(mirror, 'game_event');
    host.emit('game_event', buildGameEvent());

    for (const received of [await playerGot, await mirrorGot]) {
      expect(eventViolations(received)).toEqual([]);
      expect(received.kind).toBe('tryal_revealed');
      expect(typeof received.atMs).toBe('number');
    }
  });

  test('a non-host cannot inject a log entry', async () => {
    const { player, mirror } = await setUpRoomWithMirrorAndPlayer();

    // A forged entry from a player must not reach anyone — the log is host-authored only.
    const nothing = expectNoEvent(mirror, 'game_event', 300);
    player.emit('game_event', buildGameEvent({ kind: 'game_over', value: 'witches' }));
    await nothing;
  });

  test('timestamps cross the wire as epoch ms, never preformatted', async () => {
    const { host, player } = await setUpRoomWithMirrorAndPlayer();

    const got = waitFor(player, 'game_event');
    host.emit('game_event', buildGameEvent());
    const received = await got;

    // "19:04" would bake in the host's timezone and break a mirror in another region.
    expect(typeof received.atMs).toBe('number');
    expect(String(received.atMs)).not.toMatch(/:/);
  });
});

// ─── NEGATIVE CONTROLS (mutation tests) ────────────────────────
//
// These prove the checks above are not vacuous. Each feeds a DELIBERATELY corrupted payload —
// simulating a specific regression — and asserts the corresponding invariant FIRES. If someone
// weakens an invariant into a no-op, the matching test here goes red.
//
// ⚠ Note what these can and cannot simulate. They corrupt the PAYLOAD, which is the only thing this
// suite can reach. Deleting the `.Where(t => t.IsRevealed)` in the C# BuildRevealedTryalLabels would
// NOT make this suite fail — the suite never executes Unity code. The C# filter stays
// code-review-verified; these controls prove the WIRE-CONTRACT guard is real.

describe('negative controls — each invariant actually fires', () => {
  test('leaked unrevealed label is caught by the ground-truth assertion', () => {
    // Simulates the C# `.Where(IsRevealed)` being deleted: all 3 of Proctor's labels ship,
    // including the 2 that were face-down.
    const leaky = buildPublicPayload();
    const proctor = leaky.players.find((p) => p.playerId === 'p0');
    proctor.revealedTryals = ['Not a Witch', 'Witch', 'Witch'];

    // ⚠ IMPORTANT — the count check ALONE does not catch this: 3 labels with tryalTotal 3 is
    // indistinguishable from a legitimately fully-revealed player. Only the fixture's known ground
    // truth catches it. This is precisely why the C# side needs code review, not just this suite.
    expect(proctor.revealedTryals.length).not.toBeLessThan(proctor.tryalTotal);
    expect(selfConsistencyViolations(proctor)).toEqual([]); // self-consistency is blind here

    // The assertion that DOES catch it (the one used in the privacy test above):
    expect(() => expect(proctor.revealedTryals).toEqual(['Not a Witch'])).toThrow();
  });

  test('more labels than tryals held is caught', () => {
    const leaky = buildPublicPayload();
    const proctor = leaky.players.find((p) => p.playerId === 'p0');
    proctor.revealedTryals = ['Constable', 'Not a Witch', 'Witch', 'Witch']; // 4 > tryalTotal 3

    expect(selfConsistencyViolations(proctor)).toContain('more revealed labels than tryals held');
  });

  test('slot-ordered (unsorted) revealedTryals is caught', () => {
    const leaky = buildPublicPayload();
    const corey = leaky.players.find((p) => p.playerId === 'p1');
    corey.revealedTryals = ['Witch', 'Constable']; // deal order, not canonical

    expect(selfConsistencyViolations(corey)).toContain(
      'revealedTryals not canonically sorted (carries slot order)'
    );
  });

  test('a positional faceUp mirror of the private DTO is caught', () => {
    const leaky = buildPublicPayload();
    const proctor = leaky.players.find((p) => p.playerId === 'p0');
    // The exact shape ruled out at design time: face-down slots become addressable.
    proctor.tryals = [
      { label: 'Not a Witch', faceUp: true },
      { label: 'Witch', faceUp: false },
      { label: 'Witch', faceUp: false },
    ];
    delete proctor.revealedTryals;

    expect(forbiddenKeysIn(leaky)).toEqual(expect.arrayContaining(['tryals', 'faceUp']));
    expect(selfConsistencyViolations(proctor)).toContain('unreviewed public field: tryals');
  });

  test('an unreviewed new public field is caught by the allow-list', () => {
    const leaky = buildPublicPayload();
    const proctor = leaky.players.find((p) => p.playerId === 'p0');
    proctor.townHallCharges = 1; // eligibility data — belongs on the per-socket channel, not here

    expect(selfConsistencyViolations(proctor)).toContain(
      'unreviewed public field: townHallCharges'
    );
  });

  test('private role/hand data anywhere in the payload is caught', () => {
    const leaky = buildPublicPayload();
    const proctor = leaky.players.find((p) => p.playerId === 'p0');
    proctor.role = 'witch';
    proctor.hand = ['Accusation', 'Alibi'];

    expect(forbiddenKeysIn(leaky)).toEqual(expect.arrayContaining(['role', 'hand']));
  });

  test('handCount widened into the actual hand is caught', () => {
    const leaky = buildPublicPayload();
    const proctor = leaky.players.find((p) => p.playerId === 'p0');
    // The total leak: someone "helpfully" sends the cards instead of the count.
    proctor.handCount = ['Accusation', 'Alibi'];

    expect(selfConsistencyViolations(proctor)).toContain('handCount must be a count, got array');
  });

  test('topDiscard widened into the whole discard pile is caught', () => {
    const leaky = buildPublicPayload();
    // Leaks play history beyond what the table sees, and hands Parris' draw pool to everyone.
    leaky.topDiscard = ['Accusation', 'Alibi', 'Evidence'];

    expect(payloadLevelViolations(leaky)).toContain(
      'topDiscard must be a single card name, got array (the PILE)'
    );
  });

  test('a secret-phase event kind is caught', () => {
    // The exact thing the closed vocabulary exists to prevent.
    const leaky = buildGameEvent({ kind: 'night_vote_cast', actorId: 'p0', targetId: 'p1' });

    expect(eventViolations(leaky)).toContain('unreviewed event kind: night_vote_cast');
  });

  test('a free-text field on a log event is caught', () => {
    // With a `text` field, any call site upstream could write "Alice voted for Bob" and the closed
    // vocabulary would be worthless.
    const leaky = buildGameEvent();
    leaky.text = 'Alice voted for Bob at night.';

    expect(eventViolations(leaky)).toEqual(
      expect.arrayContaining(['unreviewed event field: text', 'free-text field: text'])
    );
  });

  test('a prose-shaped value is caught', () => {
    const leaky = buildGameEvent({ value: 'Alice secretly voted to eliminate Bob' });

    expect(eventViolations(leaky)).toContain('value looks like prose, not a label');
  });

  test('a preformatted timestamp is caught', () => {
    const leaky = buildGameEvent({ atMs: '19:04' });

    expect(eventViolations(leaky)).toContain('atMs must be epoch milliseconds (a number)');
  });

  test('a drifted Red/Blue split (same card in both fields) is caught', () => {
    const leaky = buildPublicPayload();
    const proctor = leaky.players.find((p) => p.playerId === 'p0');
    proctor.statusCards = ['Piety', 'Evidence']; // Evidence is Red, already in accusationCards

    expect(selfConsistencyViolations(proctor)).toContain('Red/Blue split overlaps: Evidence');
  });
});
