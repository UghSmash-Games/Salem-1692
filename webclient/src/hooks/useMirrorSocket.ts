/**
 * useMirrorSocket — the mirror's ONLY socket listener hook, mounted once in
 * MirrorApp. It registers handlers for the public event set and nothing else.
 *
 * This is a deliberate defense-in-depth boundary: there is no listener here for
 * private_state, secret_phase_prompt, or action_request, so even a server bug
 * that broadcast one of those could not reach the mirror's store or UI. The
 * allowed set below mirrors protocol.md Privacy Rule 4.
 */

import { useEffect } from 'react';
import { socket, connect } from '../socket/socketClient';
import { SERVER_TO_CLIENT } from '../socket/events';
import { useGameStore } from '../store/gameStore';
import type {
  JoinedPayload,
  GameStateUpdatePayload,
  PhaseResolvePayload,
  EliminationResultPayload,
  GameOverPayload,
  ErrorMsgPayload,
} from '../socket/types';

/**
 * The complete set of events a mirror is allowed to handle. Exported so it can
 * be asserted in a test — the mirror must never grow a private-event listener.
 */
export const MIRROR_ALLOWED_EVENTS: readonly string[] = [
  'connect',
  'disconnect',
  SERVER_TO_CLIENT.JOINED,
  SERVER_TO_CLIENT.GAME_STATE_UPDATE,
  SERVER_TO_CLIENT.PHASE_RESOLVE,
  SERVER_TO_CLIENT.ELIMINATION_RESULT,
  SERVER_TO_CLIENT.GAME_OVER,
  SERVER_TO_CLIENT.ROOM_CLOSED,
  SERVER_TO_CLIENT.ERROR_MSG,
];

export function useMirrorSocket(): void {
  useEffect(() => {
    const onConnect = () => useGameStore.getState().setConnected(true);
    const onDisconnect = () => useGameStore.getState().setConnected(false);

    const onJoined = (data: JoinedPayload) =>
      useGameStore.getState().onMirrorJoined(data.roomCode);
    const onGameState = (data: GameStateUpdatePayload) =>
      useGameStore.getState().applyGameStateUpdate(data);
    const onPhaseResolve = (data: PhaseResolvePayload) =>
      useGameStore.getState().applyPhaseResolve(data);
    const onElimination = (data: EliminationResultPayload) =>
      useGameStore.getState().applyEliminationResult(data);
    const onGameOver = (data: GameOverPayload) =>
      useGameStore.getState().applyGameOver(data);
    const onRoomClosed = () => useGameStore.getState().reset();
    const onError = (data: ErrorMsgPayload) =>
      useGameStore.getState().setJoinError(data?.message ?? 'Unknown error');

    socket.on('connect', onConnect);
    socket.on('disconnect', onDisconnect);
    socket.on(SERVER_TO_CLIENT.JOINED, onJoined);
    socket.on(SERVER_TO_CLIENT.GAME_STATE_UPDATE, onGameState);
    socket.on(SERVER_TO_CLIENT.PHASE_RESOLVE, onPhaseResolve);
    socket.on(SERVER_TO_CLIENT.ELIMINATION_RESULT, onElimination);
    socket.on(SERVER_TO_CLIENT.GAME_OVER, onGameOver);
    socket.on(SERVER_TO_CLIENT.ROOM_CLOSED, onRoomClosed);
    socket.on(SERVER_TO_CLIENT.ERROR_MSG, onError);

    connect();

    return () => {
      socket.off('connect', onConnect);
      socket.off('disconnect', onDisconnect);
      socket.off(SERVER_TO_CLIENT.JOINED, onJoined);
      socket.off(SERVER_TO_CLIENT.GAME_STATE_UPDATE, onGameState);
      socket.off(SERVER_TO_CLIENT.PHASE_RESOLVE, onPhaseResolve);
      socket.off(SERVER_TO_CLIENT.ELIMINATION_RESULT, onElimination);
      socket.off(SERVER_TO_CLIENT.GAME_OVER, onGameOver);
      socket.off(SERVER_TO_CLIENT.ROOM_CLOSED, onRoomClosed);
      socket.off(SERVER_TO_CLIENT.ERROR_MSG, onError);
    };
  }, []);
}
