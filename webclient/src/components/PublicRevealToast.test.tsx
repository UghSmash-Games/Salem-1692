/**
 * PublicRevealToast — surfaces a public_reveal event (actor name + card names),
 * resolves the name from the public board, stays reason-agnostic, and renders
 * nothing when there is no pending reveal.
 */

import { describe, it, expect, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { PublicRevealToast } from './PublicRevealToast';
import { useGameStore } from '../store/gameStore';

function seedBoard() {
  useGameStore.getState().applyGameStateUpdate({
    players: [
      { playerId: 'p0', displayName: 'Giles', accusations: 0, eliminated: false },
      { playerId: 'p1', displayName: 'Bob', accusations: 0, eliminated: false },
    ],
  });
}

describe('PublicRevealToast', () => {
  beforeEach(() => {
    useGameStore.getState().reset();
  });

  it('renders the actor name and shown card names', () => {
    seedBoard();
    useGameStore.getState().applyPublicReveal({
      playerId: 'p0',
      cards: ['Evidence', 'Witness'],
      reason: 'giles_corey',
    });

    render(<PublicRevealToast />);
    const toast = screen.getByTestId('public-reveal-toast');
    expect(toast).toHaveTextContent('Giles');
    expect(toast).toHaveTextContent('Evidence & Witness');
    // Reason-agnostic default verb — no Giles-specific copy.
    expect(toast).toHaveTextContent('shows');
  });

  it('falls back to the playerId when the name is unknown', () => {
    // No board seeded → name can't be resolved.
    useGameStore.getState().applyPublicReveal({
      playerId: 'ghost99',
      cards: ['Accusation'],
      reason: 'some_future_reason',
    });

    render(<PublicRevealToast />);
    expect(screen.getByTestId('public-reveal-toast')).toHaveTextContent('ghost99');
  });

  it('renders nothing when there is no pending reveal', () => {
    render(<PublicRevealToast />);
    expect(screen.queryByTestId('public-reveal-toast')).not.toBeInTheDocument();
  });
});
