/**
 * Phase 10, item 18 — a player joins with the same display name as someone already seated.
 *
 * The host uniquifies names at join (`UniqueName` → "Cris (2)") because targets resolve BY NAME.
 * The phone must therefore show, and compare against, the host's name for itself — not the one the
 * player typed. The bug this locks: SecretPhaseScreen detects an illegal constable self-protect by
 * comparing the selected target against "my name", and for the duplicated player that comparison
 * could never match.
 */

import { describe, it, expect, beforeEach } from 'vitest';
import { useGameStore, selectMyDisplayName } from './gameStore';

beforeEach(() => {
  useGameStore.getState().reset();
});

/** Seat this phone as `playerId`, having typed `typed`, with `board` as the public roster. */
function seat(playerId: string, typed: string, board: { playerId: string; displayName: string }[]) {
  useGameStore.getState().beginJoin(typed);
  useGameStore.getState().onJoined(playerId, 'ABCD');
  useGameStore.getState().applyGameStateUpdate({
    phase: 'night',
    whoseTurn: null,
    players: board.map((p) => ({ ...p, accusations: 0, eliminated: false })),
    deckCount: 10,
    discardCount: 0,
  });
}

describe('the name this phone shows for itself', () => {
  it('uses the HOST name when it differs from the typed one', () => {
    seat('p1', 'Cris', [
      { playerId: 'p0', displayName: 'Cris' },
      { playerId: 'p1', displayName: 'Cris (2)' },
    ]);

    expect(selectMyDisplayName(useGameStore.getState())).toBe('Cris (2)');
  });

  it('matches the name in the target list, so a self-pick is detectable', () => {
    // The exact comparison SecretPhaseScreen makes for the constable self-protect guard. Against
    // the typed name this was false for the duplicated player: no warning, and the host then
    // silently placed no gavel — a wasted save that looked like a save.
    seat('p1', 'Cris', [
      { playerId: 'p0', displayName: 'Cris' },
      { playerId: 'p1', displayName: 'Cris (2)' },
    ]);

    const targetsFromHost = ['Cris', 'Cris (2)'];
    const myName = selectMyDisplayName(useGameStore.getState());

    expect(targetsFromHost).toContain(myName);
    expect(targetsFromHost.find((t) => t === myName)).toBe('Cris (2)');
  });

  it('is unchanged for the ordinary case of a unique name', () => {
    seat('p0', 'Cris', [{ playerId: 'p0', displayName: 'Cris' }]);
    expect(selectMyDisplayName(useGameStore.getState())).toBe('Cris');
  });

  it('falls back to the typed name before the first board arrives', () => {
    useGameStore.getState().beginJoin('Cris');
    useGameStore.getState().onJoined('p0', 'ABCD');

    expect(selectMyDisplayName(useGameStore.getState())).toBe('Cris');
  });

  it('survives a reconnect, where nothing typed a name at all', () => {
    // After a reload the store starts empty; the board is the only source of the name.
    useGameStore.getState().onJoined('p1', 'ABCD');
    useGameStore.getState().applyGameStateUpdate({
      phase: 'day',
      whoseTurn: null,
      players: [{ playerId: 'p1', displayName: 'Cris (2)', accusations: 0, eliminated: false }],
      deckCount: 10,
      discardCount: 0,
    });

    expect(selectMyDisplayName(useGameStore.getState())).toBe('Cris (2)');
  });
});
