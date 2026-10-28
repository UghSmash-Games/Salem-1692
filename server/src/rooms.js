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
 * @property {string|null} socketId    - null while the seat is reserved (player disconnected)
 * @property {string} displayName
 * @property {string} playerId  - e.g. 'p0', 'p1', 'p2'
 * @property {string} token     - seat secret; the ONLY thing that authorizes a rejoin
 * @property {boolean} connected
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

const crypto = require('crypto');

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
 * @returns {{ playerId: string, token: string }|null} The seat, or null if room not found
 */
function joinRoomAsPlayer(code, socketId, displayName) {
  const room = rooms.get(code);
  if (!room) return null;

  const playerId = `p${room.nextPlayerId++}`;
  // 🔴 The seat secret. playerId is PUBLIC (it is in every game_state_update), so it cannot be what
  // proves ownership of a seat — without this, any player could reclaim any seat and be sent that
  // seat's private_state. Cryptographically random, never broadcast, never sent to the host.
  const token = crypto.randomBytes(16).toString('hex');
  room.players.push({ socketId, displayName, playerId, token, connected: true });
  return { playerId, token };
}

/**
 * Reclaim a reserved seat on a NEW socket.
 *
 * Returns null for every failure — unknown room, unknown seat, wrong token — so the caller cannot
 * tell them apart and the event cannot be used to enumerate seats.
 *
 * @param {string} code
 * @param {string} playerId
 * @param {string} token
 * @param {string} socketId  - the new socket
 * @returns {{ playerId: string, displayName: string, token: string, previousSocketId: string|null }|null}
 */
function reclaimSeat(code, playerId, token, socketId) {
  const room = rooms.get(code);
  if (!room) return null;

  const entry = room.players.find(p => p.playerId === playerId);
  if (!entry) return null;

  // Constant-time compare: a length-safe equality check on a secret, so a timing difference cannot
  // leak how much of a guessed token was correct.
  const a = Buffer.from(String(token || ''), 'utf8');
  const b = Buffer.from(entry.token, 'utf8');
  if (a.length !== b.length || !crypto.timingSafeEqual(a, b)) return null;

  // Newest socket wins. A seat held by a live socket (second tab, or a drop the server has not
  // noticed yet) rebinds to the newcomer — one socket per seat, always, or private_state would fan
  // out to two devices.
  const previousSocketId = entry.socketId && entry.socketId !== socketId ? entry.socketId : null;

  entry.socketId = socketId;
  entry.connected = true;
  return { playerId, displayName: entry.displayName, token: entry.token, previousSocketId };
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
        ...room.players.filter(p => p.socketId).map(p => p.socketId),
        ...room.mirrors,
      ];
      rooms.delete(code);
      return { type: 'host', code, playerId: null, allSocketIds };
    }

    // Player disconnect — RESERVE the seat, never delete it.
    //
    // ⚠ This used to splice the entry out, which made reconnection impossible: the seat, its
    // playerId and its identity were gone the instant a phone locked its screen, and the returning
    // player could only come back as a stranger under a fresh id while the host still held their
    // tryals under the old one. The entry now survives with socketId null; the HOST decides what a
    // departure means (free the chair in the lobby, hold it mid-game), because the relay does not
    // know whether a game is running.
    const playerIdx = room.players.findIndex(p => p.socketId === socketId);
    if (playerIdx !== -1) {
      const entry = room.players[playerIdx];
      entry.socketId = null;
      entry.connected = false;
      return { type: 'player', code, playerId: entry.playerId, allSocketIds: [] };
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
    // Reserved seats have no socket — skip them rather than emitting to null.
    ...room.players.filter(p => p.socketId).map(p => p.socketId),
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
  reclaimSeat,
  joinRoomAsMirror,
  removeSocket,
  getRoom,
  getPlayerByPlayerId,
  getAllSocketIds,
  getPlayers,
  getMirrors,
  clearAll,
};
