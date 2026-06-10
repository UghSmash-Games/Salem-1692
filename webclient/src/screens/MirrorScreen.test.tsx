/**
 * The mirror renders PUBLIC state only. We deliberately poison the store with
 * private data (tryals + role) that a mirror should never have, then assert the
 * MirrorScreen does not render any of it. Combined with useMirrorSocket never
 * receiving private events, this is the client-side half of the privacy rule.
 */

import { describe, it, expect, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MirrorScreen } from './MirrorScreen';
import { useGameStore } from '../store/gameStore';

describe('MirrorScreen privacy', () => {
  beforeEach(() => {
    useGameStore.getState().reset();
    useGameStore.getState().onMirrorJoined('ABCD');
    useGameStore.getState().applyGameStateUpdate({
      phase: 'day',
      whoseTurn: 'p0',
      players: [
        { playerId: 'p0', displayName: 'Alice', accusations: 2, eliminated: false },
        { playerId: 'p1', displayName: 'Bob', accusations: 0, eliminated: true },
      ],
      deckCount: 28,
      discardCount: 4,
    });
  });

  it('renders the public board', () => {
    render(<MirrorScreen />);
    expect(screen.getByTestId('board-summary')).toBeInTheDocument();
    expect(screen.getByText('Alice')).toBeInTheDocument();
    expect(screen.getByTestId('deck-summary')).toBeInTheDocument();
  });

  it('renders no private components even if private data is in the store', () => {
    // Poison the store as if a private_state had somehow landed here.
    useGameStore.getState().applyPrivateState({
      playerId: 'p0',
      tryals: [{ label: 'Witch', faceUp: false }],
      hand: ['Accusation'],
      role: 'witch',
    });

    render(<MirrorScreen />);

    expect(screen.queryByTestId('tryal-card-list')).not.toBeInTheDocument();
    expect(screen.queryByTestId('hand-list')).not.toBeInTheDocument();
    expect(screen.queryByTestId('role-indicator')).not.toBeInTheDocument();
    // The private tryal label must not leak into the DOM.
    expect(screen.queryByText('Witch')).not.toBeInTheDocument();
  });

  it('does not render a "you" marker — mirrors have no player slot', () => {
    render(<MirrorScreen />);
    expect(screen.queryByText('(you)')).not.toBeInTheDocument();
  });
});
