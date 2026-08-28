/**
 * useGameSocket — mounted ONCE (in App). Registers every server → client
 * listener from the canonical event list and pipes each payload into the
 * Zustand store, then tears the listeners down on unmount.
 *
 * Components never attach socket listeners themselves; they read the store.
 */

import { useEffect } from 'react';
import { socket, connect, rejoinRoom } from '../socket/socketClient';
import { loadSeat, saveSeat, clearSeat } from '../socket/seatSession';
import { SERVER_TO_CLIENT } from '../socket/events';
import { useGameStore } from '../store/gameStore';
import type {
  JoinedPayload,
  GameStateUpdatePayload,
  PrivateStatePayload,
  SecretPhasePromptPayload,
  ActionRequestPayload,
  DeckRearrangeRequestPayload,
  CardPickRequestPayload,
  ConfirmRequestPayload,
  TargetRequestPayload,
  TryalPickRequestPayload,
  PhaseResolvePayload,
  PublicRevealPayload,
  EliminationResultPayload,
  GameOverPayload,
  ErrorMsgPayload,
} from '../socket/types';

export function useGameSocket(): void {
  useEffect(() => {
    const store = useGameStore.getState();

    /**
     * 🔴 THE RECONNECTION ENTRY POINT. socket.io reconnects the transport by itself, but it comes
     * back with a NEW socket.id, and the server keyed the seat to the old one — so every `connect`
     * has to re-present the seat. This fires on the first connect too, which is what restores a
     * player who reloaded the page or whose phone killed the backgrounded tab.
     *
     * Deliberately unconditional on why we connected: a drop the user never noticed and a full page
     * reload are indistinguishable here, and both need the same reclaim.
     */
    const onConnect = () => {
      store.setConnected(true);

      const seat = loadSeat();
      if (seat) {
        rejoinRoom({ code: seat.roomCode, playerId: seat.playerId, token: seat.token });
      }
    };
    const onDisconnect = () => store.setConnected(false);

    const onJoined = (data: JoinedPayload) => {
      useGameStore.getState().onJoined(data.playerId, data.roomCode);

      // ⚠ RESTORE THE NAME ON A RECONNECT. A fresh join sets displayName from the join form, but a
      // reconnect never passes through that form — the store starts empty, so the phone rendered a
      // BLANK name over its own tryals until the player noticed. The stored seat is the only place
      // the name survives a reload, and writing the seat back with the empty store value would
      // erase it for good.
      const stored = loadSeat();
      const displayName = useGameStore.getState().session.displayName ?? stored?.displayName ?? null;
      if (displayName && !useGameStore.getState().session.displayName) {
        useGameStore.getState().beginJoin(displayName);
      }

      // Remember the seat so the next connect can reclaim it. The token is a credential — stored,
      // never rendered or logged. A rejoin returns the same token, so this also refreshes it.
      if (data.token) {
        saveSeat({
          roomCode: data.roomCode,
          playerId: data.playerId,
          token: data.token,
          displayName,
        });
      }
    };

    const onGameState = (data: GameStateUpdatePayload) =>
      useGameStore.getState().applyGameStateUpdate(data);
    const onPrivate = (data: PrivateStatePayload) =>
      useGameStore.getState().applyPrivateState(data);
    const onSecretPrompt = (data: SecretPhasePromptPayload) =>
      useGameStore.getState().applySecretPhasePrompt(data);
    const onActionReq = (data: ActionRequestPayload) =>
      useGameStore.getState().applyActionRequest(data);
    const onDeckRearrange = (data: DeckRearrangeRequestPayload) =>
      useGameStore.getState().applyDeckRearrangeRequest(data);
    const onCardPick = (data: CardPickRequestPayload) =>
      useGameStore.getState().applyCardPickRequest(data);
    const onConfirmRequest = (data: ConfirmRequestPayload) =>
      useGameStore.getState().applyConfirmRequest(data);
    const onTargetRequest = (data: TargetRequestPayload) =>
      useGameStore.getState().applyTargetRequest(data);
    const onTryalPickRequest = (data: TryalPickRequestPayload) =>
      useGameStore.getState().applyTryalPickRequest(data);
    const onPhaseResolve = (data: PhaseResolvePayload) =>
      useGameStore.getState().applyPhaseResolve(data);
    const onPublicReveal = (data: PublicRevealPayload) =>
      useGameStore.getState().applyPublicReveal(data);
    const onElimination = (data: EliminationResultPayload) =>
      useGameStore.getState().applyEliminationResult(data);
    const onGameOver = (data: GameOverPayload) =>
      useGameStore.getState().applyGameOver(data);

    const onRoomClosed = () => {
      // The game is over as far as this phone is concerned — the seat can never be reclaimed.
      clearSeat();
      useGameStore.getState().reset();
    };

    const onError = (data: ErrorMsgPayload) => {
      const message = data?.message ?? 'Unknown error';

      if (data?.code === 'seat_taken') {
        // Another device holds this seat now. Drop ours and stand down — keeping it would make the
        // next reconnect snatch the seat back, and the two phones would trade it back and forth.
        clearSeat();
        useGameStore.getState().reset();          // clears joinError too, so set it AFTER
        useGameStore.getState().setJoinError(message);
        return;
      }

      if (data?.code === 'rejoin_failed') {
        // The stored seat is stale — host restarted, or the room was recycled. Forget it, or every
        // later connect replays the same dead credential and never reaches the join screen.
        clearSeat();
      }

      useGameStore.getState().setJoinError(message);
    };

    socket.on('connect', onConnect);
    socket.on('disconnect', onDisconnect);
    socket.on(SERVER_TO_CLIENT.JOINED, onJoined);
    socket.on(SERVER_TO_CLIENT.GAME_STATE_UPDATE, onGameState);
    socket.on(SERVER_TO_CLIENT.PRIVATE_STATE, onPrivate);
    socket.on(SERVER_TO_CLIENT.SECRET_PHASE_PROMPT, onSecretPrompt);
    socket.on(SERVER_TO_CLIENT.ACTION_REQUEST, onActionReq);
    socket.on(SERVER_TO_CLIENT.DECK_REARRANGE_REQUEST, onDeckRearrange);
    socket.on(SERVER_TO_CLIENT.CARD_PICK_REQUEST, onCardPick);
    socket.on(SERVER_TO_CLIENT.CONFIRM_REQUEST, onConfirmRequest);
    socket.on(SERVER_TO_CLIENT.TARGET_REQUEST, onTargetRequest);
    socket.on(SERVER_TO_CLIENT.TRYAL_PICK_REQUEST, onTryalPickRequest);
    socket.on(SERVER_TO_CLIENT.PHASE_RESOLVE, onPhaseResolve);
    socket.on(SERVER_TO_CLIENT.PUBLIC_REVEAL, onPublicReveal);
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
      socket.off(SERVER_TO_CLIENT.DECK_REARRANGE_REQUEST, onDeckRearrange);
      socket.off(SERVER_TO_CLIENT.CARD_PICK_REQUEST, onCardPick);
      socket.off(SERVER_TO_CLIENT.CONFIRM_REQUEST, onConfirmRequest);
      socket.off(SERVER_TO_CLIENT.TARGET_REQUEST, onTargetRequest);
      socket.off(SERVER_TO_CLIENT.TRYAL_PICK_REQUEST, onTryalPickRequest);
      socket.off(SERVER_TO_CLIENT.PHASE_RESOLVE, onPhaseResolve);
      socket.off(SERVER_TO_CLIENT.PUBLIC_REVEAL, onPublicReveal);
      socket.off(SERVER_TO_CLIENT.ELIMINATION_RESULT, onElimination);
      socket.off(SERVER_TO_CLIENT.GAME_OVER, onGameOver);
      socket.off(SERVER_TO_CLIENT.ROOM_CLOSED, onRoomClosed);
      socket.off(SERVER_TO_CLIENT.ERROR_MSG, onError);
    };
  }, []);
}
