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

  it('a new game_state_update clears an active secret phase prompt', () => {
    joinAs('p0');
    useGameStore.getState().applySecretPhasePrompt({
      prompt: 'black_cat',
      targets: ['Alice'],
      acting: true,
    });
    useGameStore.getState().applyGameStateUpdate({ players: [] });
    const { result } = renderHook(() => useCurrentScreen());
    expect(result.current).toBe('idle');
  });
});
