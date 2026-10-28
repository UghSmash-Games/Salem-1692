/**
 * InEffectPanel + MirrorHeader.
 *
 * Both restate public facts the host already shows. The assertions worth keeping are the ones where
 * the two screens could silently diverge: which rows appear, and what "SOULS" counts.
 */

import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { InEffectPanel, buildEffectRows } from './InEffectPanel';
import { MirrorHeader } from './MirrorHeader';
import { cardDescription } from '../../data/cardDescriptions';
import type { PublicBoardSlice } from '../../store/gameStore';
import type { PublicPlayer } from '../../socket/types';

function p(over: Partial<PublicPlayer> = {}): PublicPlayer {
  return { playerId: 'x', displayName: 'X', accusations: 0, eliminated: false, ...over };
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

describe('buildEffectRows', () => {
  it('lists one row per persistent card, with the holder', () => {
    const rows = buildEffectRows([
      p({ displayName: 'Alice', statusCards: ['Asylum', 'Piety'] }),
      p({ displayName: 'Bob', statusCards: ['Matchmaker'] }),
    ]);
    expect(rows).toEqual([
      { card: 'Asylum', holder: 'ALICE' },
      { card: 'Piety', holder: 'ALICE' },
      { card: 'Matchmaker', holder: 'BOB' },
    ]);
  });

  it('SKIPS eliminated players, matching the host', () => {
    const rows = buildEffectRows([
      p({ displayName: 'Dead', statusCards: ['Asylum'], eliminated: true }),
      p({ displayName: 'Alive', statusCards: ['Piety'] }),
    ]);
    expect(rows).toEqual([{ card: 'Piety', holder: 'ALIVE' }]);
  });

  it('handles players with no cards', () => {
    expect(buildEffectRows([p(), p({ statusCards: [] })])).toEqual([]);
  });
});

describe('cardDescription', () => {
  it('gives the hand-authored blue-card text verbatim', () => {
    expect(cardDescription('Asylum')).toBe('Recipient cannot be eliminated during the night');
    expect(cardDescription('Black Cat')).toBe('Its holder reveals a tryal when conspiracy is drawn');
  });

  it('falls through to Town Hall abilities without a second lookup', () => {
    expect(cardDescription('Tituba')).toMatch(/rearrange the deck/i);
  });

  it("uses John Proctor's CORRECTED rule", () => {
    // The registry's cached copy was the pre-correction "take all blue cards + whole hand" text.
    const john = cardDescription('John Proctor') ?? '';
    expect(john).toMatch(/up to three/i);
    expect(john).not.toMatch(/blue cards/i);
  });

  it('returns null for a card with no rules text', () => {
    expect(cardDescription('Evidence')).toBeNull();
    expect(cardDescription(null)).toBeNull();
  });
});

describe('InEffectPanel', () => {
  it('renders card, holder and rules text', () => {
    render(<InEffectPanel state={state({ players: [p({ displayName: 'Alice', statusCards: ['Asylum'] })] })} />);
    const row = screen.getByTestId('in-effect-row');
    expect(row).toHaveTextContent('Asylum');
    expect(row).toHaveTextContent('ALICE');
    expect(row).toHaveTextContent(/cannot be eliminated during the night/i);
  });

  it('says so when nothing is in play', () => {
    render(<InEffectPanel state={state()} />);
    expect(screen.getByTestId('in-effect-empty')).toBeInTheDocument();
  });
});

describe('MirrorHeader', () => {
  it('counts SOULS as every seat dealt in, living AND dead', () => {
    render(
      <MirrorHeader
        roomCode="MAST"
        state={state({ players: [p(), p({ eliminated: true }), p({ eliminated: true })] })}
      />,
    );
    // 3 seats, only 1 alive — SOULS is the table size, not the survivor count.
    expect(screen.getByTestId('table-line')).toHaveTextContent('TABLE MAST · 3 SOULS');
  });

  it('falls back before the deal, when no seats exist yet', () => {
    render(<MirrorHeader roomCode="MAST" state={state()} />);
    expect(screen.getByTestId('table-line')).toHaveTextContent('TABLE MAST');
    expect(screen.getByTestId('table-line')).not.toHaveTextContent('SOULS');
  });

  it('shows the phase pill only once a phase exists', () => {
    const { rerender } = render(<MirrorHeader roomCode="X" state={state()} />);
    expect(screen.queryByTestId('phase-pill')).toBeNull();

    rerender(<MirrorHeader roomCode="X" state={state({ phase: 'night' })} />);
    expect(screen.getByTestId('phase-pill')).toHaveTextContent('NIGHT');
  });
});
