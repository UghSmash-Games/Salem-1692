/**
 * useGameSocket — mounted ONCE (in App). Registers every server → client
 * listener from the canonical event list and pipes each payload into the
 * Zustand store, then tears the listeners down on unmount.
 *
 * Components never attach socket listeners themselves; they read the store.
 */

import { useEffect } from 'react';
import { socket, connect } from '../socket/socketClient';
import { SERVER_TO_CLIENT } from '../socket/events';
import { useGameStore } from '../store/gameStore';
import type {
  JoinedPayload,
  GameStateUpdatePayload,
  PrivateStatePayload,
  SecretPhasePromptPayload,
  ActionRequestPayload,
  PhaseResolvePayload,
  EliminationResultPayload,
  GameOverPayload,
  ErrorMsgPayload,
} from '../socket/types';

const SESSION_KEY = 'salem.session';

export function useGameSocket(): void {
  useEffect(() => {
    const store = useGameStore.getState();

    const onConnect = () => store.setConnected(true);
    const onDisconnect = () => store.setConnected(false);

    const onJoined = (data: JoinedPayload) => {
      useGameStore.getState().onJoined(data.playerId, data.roomCode);
      // Persist for refresh-resilience (re-join handled by JoinScreen).
      const { displayName } = useGameStore.getState().session;
      sessionStorage.setItem(
        SESSION_KEY,
        JSON.stringify({ roomCode: data.roomCode, displayName }),
      );
    };

    const onGameState = (data: GameStateUpdatePayload) =>
      useGameStore.getState().applyGameStateUpdate(data);
    const onPrivate = (data: PrivateStatePayload) =>
      useGameStore.getState().applyPrivateState(data);
    const onSecretPrompt = (data: SecretPhasePromptPayload) =>
      useGameStore.getState().applySecretPhasePrompt(data);
    const onActionReq = (data: ActionRequestPayload) =>
      useGameStore.getState().applyActionRequest(data);
    const onPhaseResolve = (data: PhaseResolvePayload) =>
      useGameStore.getState().applyPhaseResolve(data);
    const onElimination = (data: EliminationResultPayload) =>
      useGameStore.getState().applyEliminationResult(data);
    const onGameOver = (data: GameOverPayload) =>
      useGameStore.getState().applyGameOver(data);

    const onRoomClosed = () => {
      sessionStorage.removeItem(SESSION_KEY);
      useGameStore.getState().reset();
    };
    const onError = (data: ErrorMsgPayload) =>
      useGameStore.getState().setJoinError(data?.message ?? 'Unknown error');

    socket.on('connect', onConnect);
    socket.on('disconnect', onDisconnect);
    socket.on(SERVER_TO_CLIENT.JOINED, onJoined);
    socket.on(SERVER_TO_CLIENT.GAME_STATE_UPDATE, onGameState);
    socket.on(SERVER_TO_CLIENT.PRIVATE_STATE, onPrivate);
    socket.on(SERVER_TO_CLIENT.SECRET_PHASE_PROMPT, onSecretPrompt);
    socket.on(SERVER_TO_CLIENT.ACTION_REQUEST, onActionReq);
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
      socket.off(SERVER_TO_CLIENT.PRIVATE_STATE, onPrivate);
      socket.off(SERVER_TO_CLIENT.SECRET_PHASE_PROMPT, onSecretPrompt);
      socket.off(SERVER_TO_CLIENT.ACTION_REQUEST, onActionReq);
      socket.off(SERVER_TO_CLIENT.PHASE_RESOLVE, onPhaseResolve);
      socket.off(SERVER_TO_CLIENT.ELIMINATION_RESULT, onElimination);
      socket.off(SERVER_TO_CLIENT.GAME_OVER, onGameOver);
      socket.off(SERVER_TO_CLIENT.ROOM_CLOSED, onRoomClosed);
      socket.off(SERVER_TO_CLIENT.ERROR_MSG, onError);
    };
  }, []);
}

export { SESSION_KEY };
