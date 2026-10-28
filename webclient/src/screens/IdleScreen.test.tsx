/**
 * The fellow-witches reveal renders ONLY for a witch who has been given fellow
 * witches (i.e. after the dawn reveal). A non-witch never sees it, and a witch
 * with no fellows (pre-reveal / lone witch) sees nothing.
 */

import { describe, it, expect, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { IdleScreen } from './IdleScreen';
import { useGameStore } from '../store/gameStore';
import type { PrivateStatePayload } from '../socket/types';

function setup(partial: Partial<PrivateStatePayload>) {
  useGameStore.getState().reset();
  useGameStore.getState().beginJoin('Alice');
  useGameStore.getState().onJoined('p0', 'ABCD');
  useGameStore.getState().applyPrivateState({
    playerId: 'p0',
    tryals: [],
    hand: [],
    role: 'townsperson',
    ...partial,
  });
}

describe('IdleScreen fellow-witches reveal', () => {
  beforeEach(() => {
    useGameStore.getState().reset();
  });

  it('shows fellow witches to a witch once revealed', () => {
    setup({ role: 'witch', fellowWitches: ['Bob', 'Carol'] });
    render(<IdleScreen />);
    const banner = screen.getByTestId('fellow-witches');
    expect(banner).toHaveTextContent('Bob, Carol');
  });

  it('does NOT show the banner to a non-witch (even if a list somehow arrived)', () => {
    setup({ role: 'townsperson', fellowWitches: ['Bob'] });
    render(<IdleScreen />);
    expect(screen.queryByTestId('fellow-witches')).not.toBeInTheDocument();
  });

  it('does NOT show the banner to a witch before the reveal (empty list)', () => {
    setup({ role: 'witch', fellowWitches: [] });
    render(<IdleScreen />);
    expect(screen.queryByTestId('fellow-witches')).not.toBeInTheDocument();
  });
});
