'use strict';

const {
  generateRoomCode,
  createRoom,
  joinRoomAsPlayer,
  joinRoomAsMirror,
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

  test('generates unique codes across many calls', () => {
    const codes = new Set();
    for (let i = 0; i < 100; i++) {
      codes.add(generateRoomCode());
    }
    // With 24^4 = 331,776 possible codes, 100 should all be unique
    expect(codes.size).toBe(100);
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
    expect(r1).toEqual({ playerId: 'p0' });

    const r2 = joinRoomAsPlayer(code, 'player-2', 'Bob');
    expect(r2).toEqual({ playerId: 'p1' });

    const r3 = joinRoomAsPlayer(code, 'player-3', 'Carlos');
    expect(r3).toEqual({ playerId: 'p2' });

    const players = getPlayers(code);
    expect(players).toHaveLength(3);
    expect(players[0]).toEqual({ socketId: 'player-1', displayName: 'Alice', playerId: 'p0' });
    expect(players[2]).toEqual({ socketId: 'player-3', displayName: 'Carlos', playerId: 'p2' });
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
  test('removing a player removes them from the room', () => {
    const code = createRoom('host-1');
    joinRoomAsPlayer(code, 'player-1', 'Alice');
    joinRoomAsPlayer(code, 'player-2', 'Bob');

    const result = removeSocket('player-1');
    expect(result.type).toBe('player');
    expect(result.playerId).toBe('p0');
    expect(result.code).toBe(code);

    expect(getPlayers(code)).toHaveLength(1);
    expect(getPlayers(code)[0].displayName).toBe('Bob');
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
