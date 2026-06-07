/**
 * Privacy boundary test — the tryal card list only ever renders the cards it
 * is given (the current player's own). There is no code path through which
 * another player's tryals can reach it.
 */

import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { TryalCardList } from './TryalCardList';

describe('TryalCardList', () => {
  it('renders only the cards passed to it', () => {
    render(
      <TryalCardList
        tryals={[
          { label: 'Witch', faceUp: false },
          { label: 'Not a Witch', faceUp: true },
        ]}
      />,
    );
    const list = screen.getByTestId('tryal-card-list');
    expect(list).toHaveTextContent('Witch');
    expect(list).toHaveTextContent('Not a Witch');
    // Exactly two cards rendered — nothing injected from elsewhere.
    expect(list.children).toHaveLength(2);
  });

  it('marks revealed (face-up) cards', () => {
    render(<TryalCardList tryals={[{ label: 'Witch', faceUp: true }]} />);
    expect(screen.getByText('(revealed)')).toBeInTheDocument();
  });

  it('handles an empty hand of tryals', () => {
    render(<TryalCardList tryals={[]} />);
    expect(screen.getByText('No tryal cards.')).toBeInTheDocument();
  });
});
