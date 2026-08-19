/**
 * Tests for reveal arming — specifically that a new synchronized reveal never inherits the
 * PREVIOUS reveal's outcome.
 *
 * `elimination_result` is optional. A beat can turn a tryal and kill no one: a confession-only
 * night, or conspiracy step 1 (the drawer flips one card belonging to the black-cat holder,
 * rulebook p6). RevealOverlay falls back to `lastElimination` when none arrives, so a stale value
 * would make the mirror announce a death that is several rounds old — while the host, which nulls
 * its own copy on phase_resolve, shows the truth. The two screens disagreeing about whether anyone
 * died is the exact failure the phase_resolve pattern exists to prevent.
 */

import { describe, it, expect, beforeEach } from 'vitest';
import { useGameStore } from './gameStore';

describe('reveal arming', () => {
  beforeEach(() => {
    useGameStore.getState().reset();
  });

  it('records an elimination outcome for the reveal that produced it', () => {
    useGameStore.getState().applyPhaseResolve({ revealAt: Date.now() + 3000 });
    useGameStore
      .getState()
      .applyEliminationResult({ playerId: 'p1', eliminated: true, savedBy: '' });

    expect(useGameStore.getState().lastElimination).toEqual({
      playerId: 'p1',
      eliminated: true,
      savedBy: '',
    });
  });

  it('clears the previous outcome when a new reveal is armed', () => {
    // Night one: someone dies.
    useGameStore.getState().applyPhaseResolve({ revealAt: Date.now() + 3000 });
    useGameStore
      .getState()
      .applyEliminationResult({ playerId: 'p1', eliminated: true, savedBy: '' });
    useGameStore.getState().clearReveal();

    // A later beat with NO elimination_result — e.g. conspiracy step 1.
    useGameStore.getState().applyPhaseResolve({ revealAt: Date.now() + 3000 });

    expect(useGameStore.getState().lastElimination).toBeNull();
  });

  it('keeps the board eliminated-flag even though the outcome is cleared', () => {
    // The reset must not un-kill anyone: `lastElimination` drives the reveal COPY only, while the
    // board's own eliminated flag is public state that has to persist.
    useGameStore.getState().applyGameStateUpdate({
      phase: 'day',
      players: [
        { playerId: 'p1', displayName: 'Alice', accusations: 0, eliminated: false },
        { playerId: 'p2', displayName: 'Bob', accusations: 0, eliminated: false },
      ],
    });

    useGameStore.getState().applyPhaseResolve({ revealAt: Date.now() + 3000 });
    useGameStore
      .getState()
      .applyEliminationResult({ playerId: 'p1', eliminated: true, savedBy: '' });
    useGameStore.getState().applyPhaseResolve({ revealAt: Date.now() + 3000 });

    const alice = useGameStore
      .getState()
      .publicBoard.players.find((p) => p.playerId === 'p1');
    expect(alice?.eliminated).toBe(true);
    expect(useGameStore.getState().lastElimination).toBeNull();
  });
});

describe('public board', () => {
  it('KEEPS topDiscard — the store used to drop it silently', () => {
    // topDiscard rides on game_state_update and drives the Meeting House's face-up discard card.
    // The slice omitted the field entirely, so the mirror could never have rendered it and the
    // failure would have looked like missing art rather than a dropped field.
    useGameStore.getState().applyGameStateUpdate({
      players: [],
      topDiscard: 'Evidence',
    });
    expect(useGameStore.getState().publicBoard.topDiscard).toBe('Evidence');
  });

  it('clears topDiscard when the pile empties', () => {
    useGameStore.getState().applyGameStateUpdate({ players: [], topDiscard: 'Evidence' });
    useGameStore.getState().applyGameStateUpdate({ players: [] });
    expect(useGameStore.getState().publicBoard.topDiscard).toBeNull();
  });
});
