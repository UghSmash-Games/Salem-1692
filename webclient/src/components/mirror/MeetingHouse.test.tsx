/**
 * MeetingHouse — the three derived tallies and the deck/discard.
 *
 * The stats are derived on BOTH screens from the same public fields, so a divergence here would put
 * two different headline numbers in two rooms watching the same game. The witch count in particular
 * has a trap the host documents explicitly, and it is asserted below.
 */

import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MeetingHouse, deriveStats } from './MeetingHouse';
import type { PublicBoardSlice } from '../../store/gameStore';
import type { PublicPlayer } from '../../socket/types';

function p(over: Partial<PublicPlayer> = {}): PublicPlayer {
  return {
    playerId: 'x',
    displayName: 'X',
    accusations: 0,
    eliminated: false,
    ...over,
  };
}

/** The STORE slice shape the components actually receive. */
function state(over: Partial<PublicBoardSlice> = {}): PublicBoardSlice {
  return {
    phase: null,
    whoseTurn: null,
    players: [],
    deckCount: null,
    discardCount: null,
    topDiscard: null,
    ...over,
  };
}

describe('deriveStats', () => {
  it('counts "Not a Witch" as NOT a witch', () => {
    // ⚠ The trap: "Not a Witch" CONTAINS "Witch". A substring test would count every innocent
    // reveal as a witch and make the headline number nonsense.
    const s = deriveStats([
      p({ revealedTryals: ['Not a Witch', 'Not a Witch'] }),
      p({ revealedTryals: ['Witch'] }),
    ]);
    expect(s.witchesRevealed).toBe(1);
    expect(s.tryalsFlipped).toBe(3);
  });

  it('counts witch CARDS, not witch players', () => {
    // A player with two witch cards, one revealed, contributes 1 and is still alive. A second
    // reveal on the SAME player contributes another. Cards is the only public reading.
    const s = deriveStats([p({ revealedTryals: ['Witch', 'Witch'], eliminated: false })]);
    expect(s.witchesRevealed).toBe(2);
    expect(s.stillLiving).toBe(1);
  });

  it('counts the living, including players with nothing revealed', () => {
    const s = deriveStats([
      p({ eliminated: false }),
      p({ eliminated: true, revealedTryals: ['Witch'] }),
      p({ eliminated: false, revealedTryals: [] }),
    ]);
    expect(s.stillLiving).toBe(2);
  });

  it('is case-insensitive on the label', () => {
    expect(deriveStats([p({ revealedTryals: ['witch'] })]).witchesRevealed).toBe(1);
  });

  it('handles an empty board', () => {
    expect(deriveStats([])).toEqual({ witchesRevealed: 0, tryalsFlipped: 0, stillLiving: 0 });
  });
});

describe('MeetingHouse', () => {
  it('renders the three tallies', () => {
    render(
      <MeetingHouse
        state={state({
          players: [
            p({ revealedTryals: ['Witch'] }),
            p({ revealedTryals: ['Not a Witch'], eliminated: true }),
          ],
        })}
      />,
    );
    expect(screen.getByTestId('stat-witches')).toHaveTextContent('1');
    expect(screen.getByTestId('stat-flipped')).toHaveTextContent('2');
    expect(screen.getByTestId('stat-living')).toHaveTextContent('1');
  });

  it('shows deck and discard counts', () => {
    render(<MeetingHouse state={state({ deckCount: 37, discardCount: 12 })} />);
    expect(screen.getByTestId('deck-count')).toHaveTextContent('37');
    expect(screen.getByTestId('discard-count')).toHaveTextContent('12');
  });

  it('shows the face-up top discard with its name', () => {
    render(<MeetingHouse state={state({ topDiscard: 'Evidence' })} />);
    expect(screen.getByTestId('top-discard')).toHaveTextContent('Evidence');
  });

  it('shows an empty placeholder when the discard pile is empty', () => {
    render(<MeetingHouse state={state({ topDiscard: null })} />);
    expect(screen.getByTestId('discard-empty')).toBeInTheDocument();
  });
});
