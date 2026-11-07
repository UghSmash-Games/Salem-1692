/**
 * Socket.io client singleton + typed emit helpers.
 *
 * The socket instance lives here (a module singleton), NOT in the Zustand
 * store or React tree. React components and the useGameSocket hook import
 * this module to send events; the hook wires incoming events into the store.
 *
 * Unlike the Unity client, the browser uses the official socket.io-client,
 * which transparently handles the Engine.io handshake, ping/pong, and framing.
 */

import { io, type Socket } from 'socket.io-client';
import { CLIENT_TO_SERVER } from './events';
import { PROTOCOL_VERSION } from './protocol';
import type {
  JoinRoomPayload,
  JoinMirrorPayload,
  RejoinRoomPayload,
  PlayerActionPayload,
  SecretPhaseSubmitPayload,
  ConfessPayload,
  DeckRearrangeSubmitPayload,
  CardPickSubmitPayload,
  ConfirmSubmitPayload,
  TargetSubmitPayload,
  TryalPickSubmitPayload,
} from './types';

const SERVER_URL =
  (import.meta.env.VITE_SERVER_URL as string | undefined) ?? 'http://localhost:3000';

export const socket: Socket = io(SERVER_URL, {
  transports: ['websocket'],
  autoConnect: false,
});

/** Open the connection if it isn't already open. */
export function connect(): void {
  if (!socket.connected) {
    socket.connect();
  }
}

/** Close the connection. */
export function disconnect(): void {
  if (socket.connected) {
    socket.disconnect();
  }
}

// ─── Typed emit helpers (the only client → server messages) ───────

export function joinRoom(payload: JoinRoomPayload): void {
  // The relay checks this against the host's and refuses a mismatch, so a stale host build cannot
  // half-work: see docs/protocol.md → Protocol version.
  socket.emit(CLIENT_TO_SERVER.JOIN_ROOM, { ...payload, protocolVersion: PROTOCOL_VERSION });
}

/**
 * Reclaim a seat after a socket drop. The token is the authorization — playerId alone is public and
 * proves nothing. A failure comes back as `error_msg`, at which point the stored seat is stale and
 * the phone falls back to a fresh join.
 */
export function rejoinRoom(payload: RejoinRoomPayload): void {
  socket.emit(CLIENT_TO_SERVER.REJOIN_ROOM, payload);
}

/** Join a room as a passive mirror display (public state only). */
export function joinMirror(payload: JoinMirrorPayload): void {
  // A mirror renders the same public payloads a phone does, so it gets the same check.
  socket.emit(CLIENT_TO_SERVER.JOIN_MIRROR, { ...payload, protocolVersion: PROTOCOL_VERSION });
}

export function sendPlayerAction(payload: PlayerActionPayload): void {
  socket.emit(CLIENT_TO_SERVER.PLAYER_ACTION, payload);
}

export function sendSecretPhaseSubmit(payload: SecretPhaseSubmitPayload): void {
  socket.emit(CLIENT_TO_SERVER.SECRET_PHASE_SUBMIT, payload);
}

export function sendConfess(payload: ConfessPayload): void {
  socket.emit(CLIENT_TO_SERVER.CONFESS, payload);
}

export function sendDeckRearrange(payload: DeckRearrangeSubmitPayload): void {
  socket.emit(CLIENT_TO_SERVER.DECK_REARRANGE_SUBMIT, payload);
}

export function sendCardPick(payload: CardPickSubmitPayload): void {
  socket.emit(CLIENT_TO_SERVER.CARD_PICK_SUBMIT, payload);
}

/** Answer a yes/no confirmation for this player's own optional ability choice. */
export function sendConfirm(payload: ConfirmSubmitPayload): void {
  socket.emit(CLIENT_TO_SERVER.CONFIRM_SUBMIT, payload);
}

/** Submit the chosen sub-target of a two-target card (Robbery/Scapegoat recipient). */
export function sendTargetSubmit(payload: TargetSubmitPayload): void {
  socket.emit(CLIENT_TO_SERVER.TARGET_SUBMIT, payload);
}

export function sendTryalPickSubmit(payload: TryalPickSubmitPayload): void {
  socket.emit(CLIENT_TO_SERVER.TRYAL_PICK_SUBMIT, payload);
}
