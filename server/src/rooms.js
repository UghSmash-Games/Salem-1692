'use strict';

/**
 * Room state management for Salem 1692 multiplayer.
 *
 * Each room represents one active game session identified by a 4-letter code.
 * Rooms track the host socket, player sockets (with display names and IDs),
 * and mirror sockets. All state is in-memory — add Redis adapter later if
 * horizontal scaling is needed.
 */

/** @type {Map<string, Room>} */
const rooms = new Map();

/**
 * @typedef {Object} PlayerEntry
 * @property {string} socketId
 * @property {string} displayName
 * @property {string} playerId  - e.g. 'p0', 'p1', 'p2'
 */

/**
 * @typedef {Object} Room
 * @property {string}        code
 * @property {string}        hostSocketId
 * @property {PlayerEntry[]} players
 * @property {string[]}      mirrors       - socket IDs only
 * @property {number}        nextPlayerId  - counter for ID assignment
 * @property {number}        createdAt     - Date.now() at creation
 */

// Characters used for room codes (no ambiguous letters like O/0, I/1)
const CODE_CHARS = 'ABCDEFGHJKLMNPQRSTUVWXYZ';

/**
 * Generate a 4-letter room code that is not already in use.
 * @returns {string}
 */
function generateRoomCode() {
  let code;
  do {
    code = '';
    for (let i = 0; i < 4; i++) {
      code += CODE_CHARS[Math.floor(Math.random() * CODE_CHARS.length)];
    }
  } while (rooms.has(code));
  return code;
}

/**
 * Create a new room with the given socket as host.
 * @param {string} hostSocketId
 * @returns {string} The room code
 */
function createRoom(hostSocketId) {
  const code = generateRoomCode();
  rooms.set(code, {
    code,
    hostSocketId,
    players: [],
    mirrors: [],
    nextPlayerId: 0,
    createdAt: Date.now(),
  });
  return code;
}

/**
 * Add a player to an existing room.
 * @param {string} code         - Room code
 * @param {string} socketId     - Player's socket ID
 * @param {string} displayName  - Player's chosen display name
 * @returns {{ playerId: string }|null} The assigned player ID, or null if room not found
 */
function joinRoomAsPlayer(code, socketId, displayName) {
  const room = rooms.get(code);
  if (!room) return null;

  const playerId = `p${room.nextPlayerId++}`;
  room.players.push({ socketId, displayName, playerId });
  return { playerId };
}

/**
 * Add a mirror display to an existing room.
 * @param {string} code      - Room code
 * @param {string} socketId  - Mirror's socket ID
 * @returns {boolean} True if joined, false if room not found
 */
function joinRoomAsMirror(code, socketId) {
  const room = rooms.get(code);
  if (!room) return false;

  room.mirrors.push(socketId);
  return true;
}

/**
 * Remove a socket from its room. If the socket was the host, destroy the room.
 * @param {string} socketId
 * @returns {{ type: 'host'|'player'|'mirror'|null, code: string|null, playerId: string|null, allSocketIds: string[] }}
 */
function removeSocket(socketId) {
  for (const [code, room] of rooms) {
    // Host disconnect — destroy entire room
    if (room.hostSocketId === socketId) {
      const allSocketIds = [
        ...room.players.map(p => p.socketId),
        ...room.mirrors,
      ];
      rooms.delete(code);
      return { type: 'host', code, playerId: null, allSocketIds };
    }

    // Player disconnect
    const playerIdx = room.players.findIndex(p => p.socketId === socketId);
    if (playerIdx !== -1) {
      const playerId = room.players[playerIdx].playerId;
      room.players.splice(playerIdx, 1);
      return { type: 'player', code, playerId, allSocketIds: [] };
    }

    // Mirror disconnect
    const mirrorIdx = room.mirrors.indexOf(socketId);
    if (mirrorIdx !== -1) {
      room.mirrors.splice(mirrorIdx, 1);
      return { type: 'mirror', code, playerId: null, allSocketIds: [] };
    }
  }

  return { type: null, code: null, playerId: null, allSocketIds: [] };
}

/**
 * Get a room by code.
 * @param {string} code
 * @returns {Room|undefined}
 */
function getRoom(code) {
  return rooms.get(code);
}

/**
 * Find the player entry for a given playerId in a room.
 * @param {string} code
 * @param {string} playerId
 * @returns {PlayerEntry|undefined}
 */
function getPlayerByPlayerId(code, playerId) {
  const room = rooms.get(code);
  if (!room) return undefined;
  return room.players.find(p => p.playerId === playerId);
}

/**
 * Get all socket IDs in a room (host + players + mirrors).
 * @param {string} code
 * @returns {string[]}
 */
function getAllSocketIds(code) {
  const room = rooms.get(code);
  if (!room) return [];
  return [
    room.hostSocketId,
    ...room.players.map(p => p.socketId),
    ...room.mirrors,
  ];
}

/**
 * Get all player socket IDs in a room.
 * @param {string} code
 * @returns {PlayerEntry[]}
 */
function getPlayers(code) {
  const room = rooms.get(code);
  if (!room) return [];
  return room.players;
}

/**
 * Get all mirror socket IDs in a room.
 * @param {string} code
 * @returns {string[]}
 */
function getMirrors(code) {
  const room = rooms.get(code);
  if (!room) return [];
  return room.mirrors;
}

/**
 * Clear all rooms (for testing).
 */
function clearAll() {
  rooms.clear();
}

module.exports = {
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
};
