'use strict';

const {
  generateRoomCode,
  createRoom,
  joinRoomAsPlayer,
  joinRoomAsMirror,
  reclaimSeat,
  removeSocket,
  getRoom,
  getPlayerByPlayerId,
  getAllSocketIds,
  getPlayers,
  getMirrors,
  clearAll,
} = require('../src/rooms');

beforeEach(() => {
  clearAll();
});

describe('generateRoomCode', () => {
  test('returns a 4-character uppercase string', () => {
    const code = generateRoomCode();
    expect(code).toMatch(/^[A-Z]{4}$/);
  });

  test('never returns a code that collides with a LIVE room', () => {
    // The real invariant. generateRoomCode() loops on `while (rooms.has(code))`, so a code can
    // never duplicate an ACTIVE room — that is what makes join-by-code unambiguous.
    //
    // ⚠ The previous version of this test called generateRoomCode() 100 times WITHOUT creating
    // rooms, so the dedup loop never engaged and it was really asserting "100 random draws are all
    // distinct". With 24^4 = 331,776 codes that is only ~98.5% likely
    // (birthday paradox: 1 - e^(-100*99/(2*331776)) ≈ 1.48%), so it failed roughly 1 run in 67.
    // Creating the rooms is what actually exercises the guarantee.
    const codes = new Set();
    for (let i = 0; i < 200; i++) {
      const code = createRoom(`host-socket-${i}`);
      expect(code).toMatch(/^[A-Z]{4}$/);
      expect(codes.has(code)).toBe(false); // deterministic: live rooms are never reused
      codes.add(code);
    }
    expect(codes.size).toBe(200);
  });

  test('reuses a code only after that room is gone', () => {
    // Complement to the above: the dedup set is LIVE rooms, not all-time history. A code freed by
    // a closed room is legitimately available again — otherwise the 331,776-code space would leak
    // over a long-running server.
    const code = createRoom('host-a');
    removeSocket('host-a');           // host leaving destroys the room
    expect(getRoom(code)).toBeUndefined();

    // The freed code is now allowed to come back; nothing asserts that it WILL (it is random).
    expect(() => createRoom('host-b')).not.toThrow();
  });
});

describe('createRoom', () => {
  test('creates a room and returns a valid code', () => {
    const code = createRoom('host-socket-1');
    expect(code).toMatch(/^[A-Z]{4}$/);
  });

  test('room is retrievable after creation', () => {
    const code = createRoom('host-socket-1');
    const room = getRoom(code);
    expect(room).toBeDefined();
    expect(room.hostSocketId).toBe('host-socket-1');
    expect(room.players).toEqual([]);
    expect(room.mirrors).toEqual([]);
  });

  test('returns undefined for nonexistent room', () => {
    expect(getRoom('ZZZZ')).toBeUndefined();
  });
});

describe('joinRoomAsPlayer', () => {
  test('adds a player and assigns incrementing IDs', () => {
    const code = createRoom('host-socket-1');

    const r1 = joinRoomAsPlayer(code, 'player-1', 'Alice');
    expect(r1.playerId).toBe('p0');

    const r2 = joinRoomAsPlayer(code, 'player-2', 'Bob');
    expect(r2.playerId).toBe('p1');

    // Each seat also gets its reconnection token — see the reclaimSeat suite.
    expect(r1.token).toMatch(/^[0-9a-f]{32}$/);

    const r3 = joinRoomAsPlayer(code, 'player-3', 'Carlos');
    expect(r3.playerId).toBe('p2');

    const players = getPlayers(code);
    expect(players).toHaveLength(3);
    expect(players[0]).toMatchObject({
      socketId: 'player-1', displayName: 'Alice', playerId: 'p0', connected: true,
    });
    expect(players[2]).toMatchObject({
      socketId: 'player-3', displayName: 'Carlos', playerId: 'p2', connected: true,
    });
  });

  test('returns null for invalid room code', () => {
    expect(joinRoomAsPlayer('NOPE', 'player-1', 'Alice')).toBeNull();
  });

  test('allows duplicate display names', () => {
    const code = createRoom('host-socket-1');
    const r1 = joinRoomAsPlayer(code, 'player-1', 'Alice');
    const r2 = joinRoomAsPlayer(code, 'player-2', 'Alice');
    expect(r1.playerId).not.toBe(r2.playerId);
  });
});

describe('joinRoomAsMirror', () => {
  test('adds a mirror socket', () => {
    const code = createRoom('host-socket-1');
    const success = joinRoomAsMirror(code, 'mirror-1');
    expect(success).toBe(true);
    expect(getMirrors(code)).toEqual(['mirror-1']);
  });

  test('returns false for invalid room code', () => {
    expect(joinRoomAsMirror('NOPE', 'mirror-1')).toBe(false);
  });
});

describe('getPlayerByPlayerId', () => {
  test('finds a player by their assigned ID', () => {
    const code = createRoom('host-socket-1');
    joinRoomAsPlayer(code, 'player-1', 'Alice');
    joinRoomAsPlayer(code, 'player-2', 'Bob');

    const player = getPlayerByPlayerId(code, 'p1');
    expect(player).toBeDefined();
    expect(player.displayName).toBe('Bob');
    expect(player.socketId).toBe('player-2');
  });

  test('returns undefined for nonexistent playerId', () => {
    const code = createRoom('host-socket-1');
    expect(getPlayerByPlayerId(code, 'p99')).toBeUndefined();
  });
});

describe('getAllSocketIds', () => {
  test('returns all socket IDs in a room', () => {
    const code = createRoom('host-1');
    joinRoomAsPlayer(code, 'player-1', 'Alice');
    joinRoomAsMirror(code, 'mirror-1');

    const all = getAllSocketIds(code);
    expect(all).toContain('host-1');
    expect(all).toContain('player-1');
    expect(all).toContain('mirror-1');
    expect(all).toHaveLength(3);
  });

  test('returns empty array for nonexistent room', () => {
    expect(getAllSocketIds('NOPE')).toEqual([]);
  });
});

describe('removeSocket', () => {
  test('a departing player RESERVES their seat rather than losing it', () => {
    // ⚠ This test asserted the opposite until reconnection existed: the entry was spliced out, so a
    // locked phone screen destroyed the seat, its playerId and its identity, and the player could
    // only return as a stranger while the host still held their tryals under the old id.
    const code = createRoom('host-1');
    joinRoomAsPlayer(code, 'player-1', 'Alice');
    joinRoomAsPlayer(code, 'player-2', 'Bob');

    const result = removeSocket('player-1');
    expect(result.type).toBe('player');
    expect(result.playerId).toBe('p0');
    expect(result.code).toBe(code);

    // Both seats survive; Alice's is held with no socket on it.
    expect(getPlayers(code)).toHaveLength(2);
    const alice = getPlayers(code).find(p => p.playerId === 'p0');
    expect(alice.displayName).toBe('Alice');
    expect(alice.connected).toBe(false);
    expect(alice.socketId).toBeNull();
  });

  test('a reserved seat is skipped by socket routing, never emitted to as null', () => {
    const code = createRoom('host-1');
    joinRoomAsPlayer(code, 'player-1', 'Alice');
    joinRoomAsPlayer(code, 'player-2', 'Bob');
    removeSocket('player-1');

    const ids = getAllSocketIds(code);
    expect(ids).toContain('player-2');
    expect(ids).not.toContain('player-1');
    expect(ids).not.toContain(null);
  });

  test('a host leaving does not try to close a reserved seat', () => {
    const code = createRoom('host-1');
    joinRoomAsPlayer(code, 'player-1', 'Alice');
    removeSocket('player-1');

    const result = removeSocket('host-1');
    expect(result.type).toBe('host');
    expect(result.allSocketIds).not.toContain(null);
  });

  test('removing the host destroys the room and returns all socket IDs', () => {
    const code = createRoom('host-1');
    joinRoomAsPlayer(code, 'player-1', 'Alice');
    joinRoomAsMirror(code, 'mirror-1');

    const result = removeSocket('host-1');
    expect(result.type).toBe('host');
    expect(result.code).toBe(code);
    expect(result.allSocketIds).toContain('player-1');
    expect(result.allSocketIds).toContain('mirror-1');
    expect(result.allSocketIds).not.toContain('host-1');

    // Room should be gone
    expect(getRoom(code)).toBeUndefined();
  });

  test('removing a mirror removes it silently', () => {
    const code = createRoom('host-1');
    joinRoomAsMirror(code, 'mirror-1');

    const result = removeSocket('mirror-1');
    expect(result.type).toBe('mirror');
    expect(getMirrors(code)).toEqual([]);
  });

  test('removing an unknown socket returns null type', () => {
    const result = removeSocket('unknown-socket');
    expect(result.type).toBeNull();
  });
});

describe('reclaimSeat — the token is the authorization', () => {
  test('the right token rebinds the seat to the new socket', () => {
    const code = createRoom('host-1');
    const { playerId, token } = joinRoomAsPlayer(code, 'phone-old', 'Alice');
    removeSocket('phone-old');

    const result = reclaimSeat(code, playerId, token, 'phone-new');
    expect(result).not.toBeNull();
    expect(result.playerId).toBe(playerId);
    expect(result.displayName).toBe('Alice');

    const seat = getPlayerByPlayerId(code, playerId);
    expect(seat.socketId).toBe('phone-new');
    expect(seat.connected).toBe(true);
  });

  test('a WRONG token cannot take a seat', () => {
    // The whole point. playerId is public — it is in every game_state_update — so if a guessed id
    // were enough, any player could reclaim any seat and be sent its private_state: another
    // player's tryal cards and role.
    const code = createRoom('host-1');
    const { playerId } = joinRoomAsPlayer(code, 'phone-1', 'Alice');
    removeSocket('phone-1');

    expect(reclaimSeat(code, playerId, 'not-the-token', 'attacker')).toBeNull();
    expect(reclaimSeat(code, playerId, '', 'attacker')).toBeNull();
    expect(reclaimSeat(code, playerId, undefined, 'attacker')).toBeNull();
    // A token of the RIGHT LENGTH but wrong content — the constant-time compare path.
    const wrongSameLength = 'f'.repeat(32);
    expect(reclaimSeat(code, playerId, wrongSameLength, 'attacker')).toBeNull();

    expect(getPlayerByPlayerId(code, playerId).socketId).toBeNull();
  });

  test("one seat's token does not open another seat", () => {
    const code = createRoom('host-1');
    joinRoomAsPlayer(code, 'phone-1', 'Alice');
    const bob = joinRoomAsPlayer(code, 'phone-2', 'Bob');

    expect(reclaimSeat(code, 'p0', bob.token, 'attacker')).toBeNull();
  });

  test('every failure looks the same — unknown room, unknown seat, bad token', () => {
    // Uninformative by design: distinguishing them would make this an oracle for enumerating seats.
    const code = createRoom('host-1');
    const { token } = joinRoomAsPlayer(code, 'phone-1', 'Alice');

    expect(reclaimSeat('ZZZZ', 'p0', token, 's')).toBeNull();
    expect(reclaimSeat(code, 'p99', token, 's')).toBeNull();
    expect(reclaimSeat(code, 'p0', 'wrong', 's')).toBeNull();
  });

  test('tokens are unguessable and unique per seat', () => {
    const code = createRoom('host-1');
    const a = joinRoomAsPlayer(code, 'phone-1', 'Alice');
    const b = joinRoomAsPlayer(code, 'phone-2', 'Bob');

    expect(a.token).toMatch(/^[0-9a-f]{32}$/);
    expect(a.token).not.toBe(b.token);
  });

  test('reclaiming a seat that is still live reports the socket to evict', () => {
    // Two devices cannot hold one seat, or private_state fans out to both.
    const code = createRoom('host-1');
    const { playerId, token } = joinRoomAsPlayer(code, 'phone-old', 'Alice');

    const result = reclaimSeat(code, playerId, token, 'phone-new');
    expect(result.previousSocketId).toBe('phone-old');
    expect(getPlayerByPlayerId(code, playerId).socketId).toBe('phone-new');
  });

  test('rejoining on the SAME socket evicts nothing', () => {
    const code = createRoom('host-1');
    const { playerId, token } = joinRoomAsPlayer(code, 'phone-1', 'Alice');

    const result = reclaimSeat(code, playerId, token, 'phone-1');
    expect(result.previousSocketId).toBeNull();
  });
});
