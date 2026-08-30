/**
 * Tests for the derived screen selector — the client's screen state machine.
 */

import { describe, it, expect, beforeEach } from 'vitest';
import { renderHook } from '@testing-library/react';
import { useGameStore } from './gameStore';
import { useCurrentScreen } from './selectors';

function joinAs(playerId: string) {
  useGameStore.getState().onJoined(playerId, 'ABCD');
}

describe('useCurrentScreen', () => {
  beforeEach(() => {
    useGameStore.getState().reset();
  });

  it('starts on the join screen', () => {
    const { result } = renderHook(() => useCurrentScreen());
    expect(result.current).toBe('join');
  });

  it('shows idle after joining', () => {
    joinAs('p0');
    const { result } = renderHook(() => useCurrentScreen());
    expect(result.current).toBe('idle');
  });

  it('shows the secret phase when a prompt is active', () => {
    joinAs('p0');
    useGameStore.getState().applySecretPhasePrompt({
      prompt: 'night_vote',
      targets: ['Alice'],
      acting: false,
    });
    const { result } = renderHook(() => useCurrentScreen());
    expect(result.current).toBe('secret_phase');
  });

  it('shows the action screen when an action is requested', () => {
    joinAs('p0');
    useGameStore.getState().applyActionRequest({ playerId: 'p0', actions: ['draw', 'play'] });
    const { result } = renderHook(() => useCurrentScreen());
    expect(result.current).toBe('action');
  });

  it('an action_request arriving mid-draft does NOT clobber the card pick', () => {
    // Regression: John Proctor's turn starting while his elimination draft is still open used to
    // yank the pick menu away and show "Draw 2 / play cards". The draft outranks the turn screen.
    joinAs('p0');
    useGameStore.getState().applyCardPickRequest({
      cards: ['Accusation', 'Alibi'],
      pickNumber: 1,
      totalPicks: 3,
      seconds: 45,
      allowDone: true,
      reason: 'proctor_draft',
    });
    useGameStore.getState().applyActionRequest({ playerId: 'p0', actions: ['draw', 'play'] });

    const { result, rerender } = renderHook(() => useCurrentScreen());
    expect(result.current).toBe('card_pick');
    expect(useGameStore.getState().cardPick).not.toBeNull();
    // The turn prompt is retained, just queued behind the draft.
    expect(useGameStore.getState().actionRequest).not.toBeNull();

    // Once the draft resolves, the waiting turn screen takes over.
    useGameStore.getState().clearCardPick();
    rerender();
    expect(result.current).toBe('action');
  });

  it('a draft firing mid-turn returns to the action screen when it resolves', () => {
    // Regression: John played a card that eliminated someone, so his own draft took over his turn
    // screen. When the draft finished the phone fell to idle and stuck there until the turn timer
    // fired, because the card_pick had wiped the pending actionRequest.
    joinAs('p0');
    useGameStore.getState().applyActionRequest({ playerId: 'p0', actions: ['draw', 'play'] });
    useGameStore.getState().applyCardPickRequest({
      cards: ['Accusation', 'Alibi'],
      pickNumber: 1,
      totalPicks: 3,
      seconds: 45,
      allowDone: true,
      reason: 'proctor_draft',
    });

    const { result, rerender } = renderHook(() => useCurrentScreen());
    expect(result.current).toBe('card_pick');

    useGameStore.getState().clearCardPick();
    rerender();
    expect(result.current).toBe('action'); // back to the turn, not stranded on idle
  });

  it('a board tick during my own turn does not wipe my action prompt', () => {
    // The draft produces a flurry of game_state_update broadcasts; those must not drop a live turn.
    joinAs('p0');
    useGameStore.getState().applyActionRequest({ playerId: 'p0', actions: ['draw', 'play'] });
    useGameStore.getState().applyGameStateUpdate({
      whoseTurn: 'p0',
      players: [{ playerId: 'p0', displayName: 'Me', accusations: 0, eliminated: false }],
    });
    const { result } = renderHook(() => useCurrentScreen());
    expect(result.current).toBe('action');
  });

  it('a board tick that moves the turn away DOES clear my action prompt', () => {
    joinAs('p0');
    useGameStore.getState().applyActionRequest({ playerId: 'p0', actions: ['draw', 'play'] });
    useGameStore.getState().applyGameStateUpdate({
      whoseTurn: 'p1',
      players: [{ playerId: 'p0', displayName: 'Me', accusations: 0, eliminated: false }],
    });
    const { result } = renderHook(() => useCurrentScreen());
    expect(result.current).toBe('idle');
  });

  it('switches to spectator when this player is eliminated', () => {
    joinAs('p0');
    useGameStore.getState().applyGameStateUpdate({
      players: [
        { playerId: 'p0', displayName: 'Me', accusations: 0, eliminated: false },
      ],
    });
    useGameStore.getState().applyEliminationResult({
      playerId: 'p0',
      eliminated: true,
      savedBy: null,
    });
    const { result } = renderHook(() => useCurrentScreen());
    expect(result.current).toBe('spectator');
  });

  it('game over takes priority over everything', () => {
    joinAs('p0');
    useGameStore.getState().applyGameOver({ winner: 'townspeople', tryals: {} });
    const { result } = renderHook(() => useCurrentScreen());
    expect(result.current).toBe('game_over');
  });

  it('a board refresh DURING a secret phase does NOT clear the prompt', () => {
    // Regression guard for the dawn race: a game_state_update whose phase is a
    // secret phase (or has no phase) must keep an active prompt on screen.
    joinAs('p0');
    useGameStore.getState().applySecretPhasePrompt({
      prompt: 'black_cat',
      targets: ['Alice'],
      acting: true,
    });
    useGameStore.getState().applyGameStateUpdate({ phase: 'dawn', players: [] });
    let screen = renderHook(() => useCurrentScreen());
    expect(screen.result.current).toBe('secret_phase');
    screen.unmount();

    // A no-phase board tick also keeps it (conservative).
    useGameStore.getState().applyGameStateUpdate({ players: [] });
    screen = renderHook(() => useCurrentScreen());
    expect(screen.result.current).toBe('secret_phase');
  });

  it('a phase change OUT of a secret phase clears the prompt', () => {
    joinAs('p0');
    useGameStore.getState().applySecretPhasePrompt({
      prompt: 'black_cat',
      targets: ['Alice'],
      acting: true,
    });
    useGameStore.getState().applyGameStateUpdate({ phase: 'day', players: [] });
    const { result } = renderHook(() => useCurrentScreen());
    expect(result.current).toBe('idle');
  });
});
