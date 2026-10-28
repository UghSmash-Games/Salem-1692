/**
 * The mirror renders PUBLIC state only. We deliberately poison the store with private data (tryals +
 * role) that a mirror should never have, then assert the MirrorScreen does not render any of it.
 * Combined with useMirrorSocket never receiving private events, this is the client-side half of the
 * privacy rule.
 *
 * Since the ring landed, this also covers PARITY: the mirror must show the public facts the host
 * shows, because anything missing is an information asymmetry between players rather than a
 * cosmetic gap.
 */

import { describe, it, expect, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MirrorScreen } from './MirrorScreen';
import { useGameStore } from '../store/gameStore';

beforeEach(() => {
  useGameStore.getState().reset();
  useGameStore.getState().onMirrorJoined('ABCD');
  useGameStore.getState().applyGameStateUpdate({
    phase: 'day',
    whoseTurn: 'p0',
    players: [
      {
        playerId: 'p0',
        displayName: 'Alice',
        accusations: 2,
        accusationLimit: 7,
        eliminated: false,
        tryalTotal: 5,
        revealedTryals: [],
        handCount: 3,
        townHall: 'Tituba',
        statusCards: ['Asylum'],
      },
      {
        playerId: 'p1',
        displayName: 'Bob',
        accusations: 0,
        eliminated: true,
        tryalTotal: 5,
        revealedTryals: ['Not a Witch'],
        handCount: 0,
      },
      { playerId: 'p2', displayName: 'Cara', accusations: 0, eliminated: false, tryalTotal: 5 },
      { playerId: 'p3', displayName: 'Dan', accusations: 0, eliminated: false, tryalTotal: 5 },
    ],
    deckCount: 28,
    discardCount: 4,
    topDiscard: 'Evidence',
  });
});

describe('MirrorScreen privacy', () => {
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
  });

  it('does not render a "you" marker — mirrors have no player slot', () => {
    render(<MirrorScreen />);
    expect(screen.queryByText('(you)')).not.toBeInTheDocument();
  });

  it('draws face-down tryals from a COUNT, never from an identity', () => {
    render(<MirrorScreen />);
    // Alice has 5 tryals, none revealed -> five identical backs and no labels.
    const backs = screen.getAllByTestId('tryal-back');
    expect(backs.length).toBeGreaterThanOrEqual(5);
  });
});

describe('MirrorScreen parity', () => {
  it('seats every player in the ring', () => {
    render(<MirrorScreen />);
    expect(screen.getAllByTestId('mirror-seat')).toHaveLength(4);
    for (const name of ['Alice', 'Bob', 'Cara', 'Dan']) {
      expect(screen.getByText(name)).toBeInTheDocument();
    }
  });

  it('shows the header table line and phase', () => {
    render(<MirrorScreen />);
    expect(screen.getByTestId('table-line')).toHaveTextContent('TABLE ABCD · 4 SOULS');
    expect(screen.getByTestId('phase-pill')).toHaveTextContent('DAY');
  });

  it('shows the Meeting House tallies and deck counts', () => {
    render(<MirrorScreen />);
    expect(screen.getByTestId('meeting-house')).toBeInTheDocument();
    // Bob's revealed "Not a Witch" is one flip and ZERO witches.
    expect(screen.getByTestId('stat-flipped')).toHaveTextContent('1');
    expect(screen.getByTestId('stat-witches')).toHaveTextContent('0');
    expect(screen.getByTestId('stat-living')).toHaveTextContent('3');
    expect(screen.getByTestId('deck-count')).toHaveTextContent('28');
  });

  it('lists persistent cards in the IN EFFECT rail', () => {
    render(<MirrorScreen />);
    expect(screen.getByTestId('in-effect-row')).toHaveTextContent('Asylum');
    expect(screen.getByTestId('in-effect-row')).toHaveTextContent('ALICE');
  });

  it('marks whose turn it is', () => {
    render(<MirrorScreen />);
    expect(screen.getAllByTestId('seat-turn-ring')).toHaveLength(1);
  });

  it('stamps the eliminated seat', () => {
    render(<MirrorScreen />);
    expect(screen.getByTestId('seat-hanged')).toHaveTextContent('HANGED');
  });
});

describe('MirrorScreen reveal gating', () => {
  it('does NOT switch to game over while a reveal is still running', () => {
    // The host sends elimination_result and game_over back-to-back at revealAt. Gating on gameOver
    // alone cut the mirror's beat to ~0.2s while the host room saw the full linger.
    useGameStore.getState().applyPhaseResolve({ revealAt: Date.now() + 3000 });
    useGameStore.getState().applyGameOver({ winner: 'witches', tryals: {} });

    render(<MirrorScreen />);
    expect(screen.getByTestId('reveal-overlay')).toBeInTheDocument();
    expect(screen.queryByText(/witches win/i)).not.toBeInTheDocument();
  });

  it('shows game over once the reveal has cleared', () => {
    useGameStore.getState().applyGameOver({ winner: 'witches', tryals: {} });
    render(<MirrorScreen />);
    expect(screen.queryByTestId('mirror-seat')).toBeNull();
  });
});
