/**
 * RevealOverlay — what the MIRROR shows during a synchronized beat.
 *
 * The case that matters here is a reveal with NO elimination_result. Accusation-threshold and
 * piety-loss flips kill no one, and they are the most common reveals in the game, so the fallback
 * copy is what a mirror room sees most often. It must describe what actually turned rather than
 * imply a death.
 */

import { describe, it, expect, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { RevealOverlay } from './RevealOverlay';
import { useGameStore } from '../store/gameStore';
import type { GameEventPayload } from '../socket/types';

function board() {
  useGameStore.getState().applyGameStateUpdate({
    phase: 'day',
    players: [
      { playerId: 'p0', displayName: 'Alice', accusations: 0, eliminated: false },
      { playerId: 'p1', displayName: 'Bob', accusations: 7, eliminated: false },
    ],
  });
}

/** Arm a reveal whose instant has already passed, so the overlay is in its 'revealed' phase. */
function armResolved() {
  useGameStore.getState().applyPhaseResolve({ revealAt: Date.now() - 100 });
}

function pushEvent(over: Partial<GameEventPayload>) {
  useGameStore.getState().appendGameEvent({
    kind: 'tryal_revealed',
    actorId: null,
    targetId: 'p1',
    cardName: null,
    value: 'Witch',
    atMs: Date.now(),
    ...over,
  } as GameEventPayload);
}

describe('RevealOverlay', () => {
  beforeEach(() => useGameStore.getState().reset());

  it('renders nothing when idle', () => {
    board();
    const { container } = render(<RevealOverlay />);
    expect(container).toBeEmptyDOMElement();
  });

  it('shows the elimination outcome when one arrives', () => {
    board();
    armResolved();
    useGameStore
      .getState()
      .applyEliminationResult({ playerId: 'p1', eliminated: true, savedBy: '' });

    render(<RevealOverlay />);
    expect(screen.getByTestId('reveal-overlay')).toHaveTextContent('Bob was eliminated');
  });

  it('describes what TURNED when the reveal carries no outcome', () => {
    board();
    armResolved();
    pushEvent({ kind: 'tryal_revealed', targetId: 'p1', value: 'Witch' });

    render(<RevealOverlay />);
    expect(screen.getByTestId('reveal-beat-line')).toHaveTextContent(
      "Bob's Tryal card is turned: Witch.",
    );
    // "The deed is done" over an accusation flip implies a death that did not happen.
    expect(screen.getByTestId('reveal-overlay')).not.toHaveTextContent('The deed is done');
  });

  it('ignores events unrelated to the flip', () => {
    board();
    armResolved();
    pushEvent({ kind: 'card_played', actorId: 'p0', targetId: 'p1', cardName: 'Accusation' });

    render(<RevealOverlay />);
    // A card_played landing inside the window is not part of the beat — it would read as noise
    // mid-drama, so the generic line is correct here.
    expect(screen.queryByTestId('reveal-beat-line')).toBeNull();
    expect(screen.getByTestId('reveal-overlay')).toHaveTextContent('The deed is done');
  });

  it('scopes events to THIS beat — a new reveal does not inherit the last one', () => {
    board();
    armResolved();
    pushEvent({ kind: 'tryal_revealed', targetId: 'p1', value: 'Witch' });
    useGameStore.getState().clearReveal();

    // A later beat with nothing of its own must not re-show the earlier flip.
    armResolved();
    render(<RevealOverlay />);
    expect(screen.queryByTestId('reveal-beat-line')).toBeNull();
  });

  it('does not collect events while no reveal is armed', () => {
    board();
    pushEvent({ kind: 'tryal_revealed', targetId: 'p1', value: 'Witch' });
    expect(useGameStore.getState().revealEvents).toHaveLength(0);
    // …but the permanent log still records it.
    expect(useGameStore.getState().eventLog).toHaveLength(1);
  });
});
