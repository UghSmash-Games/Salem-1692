/**
 * MirrorSeat — the seat must show every public fact the host seat shows.
 *
 * A missing field here is not cosmetic: the mirror exists so a player who cannot see the host TV has
 * the SAME information as the people sitting at it. Anything the host renders and this does not is
 * an information asymmetry between players.
 */

import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MirrorSeat, stackCards } from './MirrorSeat';
import type { PublicPlayer } from '../../socket/types';

function player(over: Partial<PublicPlayer> = {}): PublicPlayer {
  return {
    playerId: 'p0',
    displayName: 'Alice',
    accusations: 0,
    eliminated: false,
    ...over,
  };
}

describe('stackCards', () => {
  it('groups by label and keeps first-appearance order', () => {
    // Order stability matters: the row must not reshuffle every time a card is added.
    expect(stackCards(['Evidence', 'Accusation', 'Evidence'])).toEqual([
      { label: 'Evidence', count: 2 },
      { label: 'Accusation', count: 1 },
    ]);
  });

  it('ignores empty labels', () => {
    expect(stackCards(['', 'Piety'])).toEqual([{ label: 'Piety', count: 1 }]);
  });
});

describe('MirrorSeat', () => {
  it('shows the PLAYER name primary and the character secondary', () => {
    render(<MirrorSeat player={player({ townHall: 'Tituba' })} isTurn={false} />);
    expect(screen.getByTestId('seat-name')).toHaveTextContent('Alice');
    expect(screen.getByTestId('seat-character')).toHaveTextContent('Tituba');
  });

  it('renders the host stat lines verbatim', () => {
    render(
      <MirrorSeat
        player={player({ handCount: 2, tryalTotal: 5, revealedTryals: ['Witch'], accusations: 3, accusationLimit: 7 })}
        isTurn={false}
      />,
    );
    expect(screen.getByTestId('seat-stats')).toHaveTextContent('2 IN HAND · 1/5');
    expect(screen.getByTestId('seat-accusations')).toHaveTextContent('ACCUSATIONS 3/7');
  });

  it('honours a DYNAMIC accusation limit (Piety 14, George Burroughs 8)', () => {
    render(<MirrorSeat player={player({ accusations: 9, accusationLimit: 14 })} isTurn={false} />);
    expect(screen.getByTestId('seat-accusations')).toHaveTextContent('ACCUSATIONS 9/14');
  });

  it('draws face-down tryals from tryalTotal, not a hardcoded 5', () => {
    // 10-12 players deal 3 tryals each; a hardcoded 5 was the PDF's mock data.
    render(
      <MirrorSeat player={player({ tryalTotal: 3, revealedTryals: ['Witch'] })} isTurn={false} />,
    );
    expect(screen.getAllByTestId('tryal-back')).toHaveLength(2);
  });

  it('shows no face-down backs when every tryal is revealed', () => {
    render(
      <MirrorSeat
        player={player({ tryalTotal: 2, revealedTryals: ['Witch', 'Not a Witch'] })}
        isTurn={false}
      />,
    );
    expect(screen.queryByTestId('tryal-back')).toBeNull();
  });

  it('stacks duplicate cards with a ×N badge instead of repeating images', () => {
    render(
      <MirrorSeat
        player={player({ accusationCards: ['Accusation', 'Accusation', 'Evidence'] })}
        isTurn={false}
      />,
    );
    const badges = screen.getAllByTestId('stack-count');
    expect(badges).toHaveLength(1);
    expect(badges[0]).toHaveTextContent('×2');
  });

  it('overflows on hidden TYPES beyond five', () => {
    render(
      <MirrorSeat
        player={player({
          accusationCards: ['Accusation', 'Evidence', 'Witness'],
          statusCards: ['Asylum', 'Piety', 'Matchmaker', 'Stocks'],
        })}
        isTurn={false}
      />,
    );
    expect(screen.getByTestId('seat-overflow')).toHaveTextContent('+2');
  });

  it('marks the active turn, and never on an eliminated seat', () => {
    const { rerender } = render(<MirrorSeat player={player()} isTurn />);
    expect(screen.getByTestId('seat-turn-ring')).toBeInTheDocument();

    rerender(<MirrorSeat player={player({ eliminated: true })} isTurn />);
    expect(screen.queryByTestId('seat-turn-ring')).toBeNull();
  });

  it('stamps HANGED with the WORD, not just a colour', () => {
    render(<MirrorSeat player={player({ eliminated: true })} isTurn={false} />);
    expect(screen.getByTestId('seat-hanged')).toHaveTextContent('HANGED');
  });

  it('writes the card name on each effect badge', () => {
    render(<MirrorSeat player={player({ statusCards: ['Asylum'] })} isTurn={false} />);
    expect(screen.getByTestId('seat-effects')).toHaveTextContent('Asylum');
  });

  it('KEEPS the character name after elimination, when townHall goes empty', () => {
    const { rerender } = render(
      <MirrorSeat player={player({ townHall: 'Giles Corey' })} isTurn={false} />,
    );
    expect(screen.getByTestId('seat-character')).toHaveTextContent('Giles Corey');

    // Player.OnElimination clears the card, so the field arrives empty from here on.
    rerender(
      <MirrorSeat player={player({ townHall: null, eliminated: true })} isTurn={false} />,
    );
    expect(screen.getByTestId('seat-character')).toHaveTextContent('Giles Corey');
  });

  it('renders a seat with no character at all (fewer than 8 players)', () => {
    render(<MirrorSeat player={player()} isTurn={false} />);
    expect(screen.queryByTestId('seat-character')).toBeNull();
    expect(screen.queryByTestId('seat-portrait')).toBeNull();
  });
});
