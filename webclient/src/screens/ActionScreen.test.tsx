import { describe, it, expect, beforeEach, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { ActionScreen } from './ActionScreen';
import { useGameStore } from '../store/gameStore';

const playerActionSpy = vi.fn();
vi.mock('../socket/socketClient', () => ({
  sendPlayerAction: (payload: { card: string; targetPlayerId: string }) =>
    playerActionSpy(payload),
  sendConfess: vi.fn(),
}));

function renderWith(actions: string[]) {
  useGameStore.getState().reset();
  useGameStore.getState().applyActionRequest({ playerId: 'p0', actions });
  return render(<ActionScreen />);
}

describe('ActionScreen — Tituba rearrange action', () => {
  beforeEach(() => {
    playerActionSpy.mockClear();
    useGameStore.getState().reset();
  });

  it('renders the "Rearrange the Deck" button when "tituba" is in actions', () => {
    renderWith(['tituba', 'draw', 'play']);
    expect(
      screen.getByRole('button', { name: 'Rearrange the Deck' }),
    ).toBeInTheDocument();
  });

  it('does NOT render the rearrange button when "tituba" is absent', () => {
    renderWith(['draw', 'play']);
    expect(
      screen.queryByRole('button', { name: 'Rearrange the Deck' }),
    ).not.toBeInTheDocument();
  });

  it('clicking "Rearrange the Deck" sends player_action {card:"tituba"}', () => {
    renderWith(['tituba', 'draw', 'play']);
    fireEvent.click(screen.getByRole('button', { name: 'Rearrange the Deck' }));
    expect(playerActionSpy).toHaveBeenCalledWith({ card: 'tituba', targetPlayerId: '' });
  });
});
