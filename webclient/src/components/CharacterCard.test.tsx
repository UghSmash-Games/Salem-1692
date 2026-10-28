/**
 * CharacterCard — the player's own Town Hall character.
 *
 * The load-bearing behaviours: it keys on the EXACT wire name (two of which differ from the C# enum
 * spelling), and it keeps showing the character after elimination, when the host nulls `townHall`.
 */

import { describe, it, expect, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { CharacterCard } from './CharacterCard';
import { useGameStore } from '../store/gameStore';
import { TOWN_HALL_CHARACTERS } from '../data/townHallCharacters';

function board(townHall: string | null) {
  useGameStore.getState().onJoined('p0', 'ABCD');
  useGameStore.getState().applyGameStateUpdate({
    phase: 'day',
    players: [
      {
        playerId: 'p0',
        displayName: 'Alice',
        accusations: 0,
        eliminated: false,
        townHall,
      },
      { playerId: 'p1', displayName: 'Bob', accusations: 0, eliminated: false },
    ],
  });
}

describe('CharacterCard', () => {
  beforeEach(() => useGameStore.getState().reset());

  it('renders nothing when the player has no character', () => {
    board(null);
    const { container } = render(<CharacterCard />);
    expect(container).toBeEmptyDOMElement();
  });

  it('shows the name, ability and bio', () => {
    board('Tituba');
    render(<CharacterCard />);
    expect(screen.getByTestId('character-name')).toHaveTextContent('Tituba');
    expect(screen.getByTestId('character-ability')).toHaveTextContent('rearrange the deck');
    expect(screen.getByTestId('character-bio')).toBeInTheDocument();
  });

  it('hides the bio in compact mode but keeps the ability', () => {
    board('Tituba');
    render(<CharacterCard compact />);
    expect(screen.getByTestId('character-ability')).toBeInTheDocument();
    expect(screen.queryByTestId('character-bio')).toBeNull();
  });

  it('KEEPS the character after elimination, when the host nulls townHall', () => {
    // Player.OnElimination clears the card, so `townHall` goes empty on the wire. An eliminated
    // player is still watching and is still entitled to know what their own card was.
    board('Giles Corey');
    const { rerender } = render(<CharacterCard />);
    expect(screen.getByTestId('character-name')).toHaveTextContent('Giles Corey');

    board(null);
    rerender(<CharacterCard />);
    expect(screen.getByTestId('character-name')).toHaveTextContent('Giles Corey');
  });

  it('renders nothing for a name it does not know, rather than a blank card', () => {
    board('Judge Hathorne');
    const { container } = render(<CharacterCard />);
    expect(container).toBeEmptyDOMElement();
  });
});

describe('character data', () => {
  it('covers all 15 Town Hall cards', () => {
    expect(Object.keys(TOWN_HALL_CHARACTERS)).toHaveLength(15);
  });

  it('uses the CARD names, which differ from the C# enum for two of them', () => {
    // Enum: WillGrigs / WilliamsPhipps. Card asset Name: "Will Griggs" / "William Phipps".
    // The wire carries the card Name, so these are the keys that matter.
    expect(TOWN_HALL_CHARACTERS['Will Griggs']).toBeDefined();
    expect(TOWN_HALL_CHARACTERS['William Phipps']).toBeDefined();
    expect(TOWN_HALL_CHARACTERS['Will Grigs']).toBeUndefined();
    expect(TOWN_HALL_CHARACTERS['Williams Phipps']).toBeUndefined();
  });

  it('every entry has both an ability and a bio', () => {
    for (const [name, c] of Object.entries(TOWN_HALL_CHARACTERS)) {
      expect(c.ability.length, `${name} ability`).toBeGreaterThan(10);
      expect(c.bio.length, `${name} bio`).toBeGreaterThan(10);
    }
  });

  it("states John Proctor's CORRECTED ability, not the pre-correction one", () => {
    // He takes up to three from the HAND, by choice; cards in play are discarded, not taken.
    // Unity's GetRulesText carried the old wording and fed it to the host's IN EFFECT panel.
    const john = TOWN_HALL_CHARACTERS['John Proctor'].ability;
    expect(john).toMatch(/up to three/i);
    expect(john).not.toMatch(/blue cards/i);
  });
});
