/**
 * RulesSheet — the always-available rules reference.
 *
 * The behaviours worth locking are the ones that protect a live prompt: it must be reachable from
 * every in-game screen, and it must get out of the way when the screen changes, because several
 * prompts run on a host-owned deadline and a sheet left open over a countdown costs the player
 * their submission.
 */

import { describe, it, expect, beforeEach } from 'vitest';
import { render, screen, fireEvent, act } from '@testing-library/react';
import { RulesSheet } from './RulesSheet';
import { useGameStore } from '../store/gameStore';

function joined() {
  useGameStore.getState().onJoined('p0', 'ABCD');
  useGameStore.getState().applyGameStateUpdate({
    phase: 'day',
    players: [
      { playerId: 'p0', displayName: 'Alice', accusations: 0, eliminated: false },
      { playerId: 'p1', displayName: 'Bob', accusations: 0, eliminated: false },
    ],
  });
}

describe('RulesSheet', () => {
  beforeEach(() => useGameStore.getState().reset());

  it('is hidden before joining — nothing to reference yet', () => {
    const { container } = render(<RulesSheet />);
    expect(container).toBeEmptyDOMElement();
  });

  it('offers an affordance once in a game, and opens', () => {
    joined();
    render(<RulesSheet />);

    fireEvent.click(screen.getByTestId('rules-open'));
    expect(screen.getByTestId('rules-sheet')).toBeInTheDocument();
    expect(screen.getAllByTestId('rules-section').length).toBeGreaterThan(3);
  });

  it('closes on demand', () => {
    joined();
    render(<RulesSheet />);
    fireEvent.click(screen.getByTestId('rules-open'));
    fireEvent.click(screen.getByTestId('rules-close'));
    expect(screen.queryByTestId('rules-sheet')).toBeNull();
  });

  it('CLOSES ITSELF when a prompt arrives, so it cannot sit over a countdown', () => {
    joined();
    render(<RulesSheet />);
    fireEvent.click(screen.getByTestId('rules-open'));
    expect(screen.getByTestId('rules-sheet')).toBeInTheDocument();

    // A host-blocking prompt lands while the player is reading the rules.
    act(() => {
      useGameStore.getState().applyTryalPickRequest({
        targetPlayerId: 'p1',
        count: 3,
        seconds: 25,
        reason: 'accusation_reveal',
      });
    });

    expect(screen.queryByTestId('rules-sheet')).toBeNull();
    expect(screen.getByTestId('rules-open')).toBeInTheDocument();
  });

  it('stays reachable while a prompt is up', () => {
    // The point of not being a Screen: a live prompt must not make the rules unreachable.
    joined();
    act(() => {
      useGameStore.getState().applyTryalPickRequest({
        targetPlayerId: 'p1',
        count: 2,
        seconds: 25,
        reason: 'conspiracy_pass',
      });
    });
    render(<RulesSheet />);

    expect(screen.getByTestId('rules-open')).toBeInTheDocument();
    fireEvent.click(screen.getByTestId('rules-open'));
    expect(screen.getByTestId('rules-sheet')).toBeInTheDocument();
  });
});
