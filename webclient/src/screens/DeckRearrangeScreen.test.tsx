import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { render, screen, fireEvent, within, act } from '@testing-library/react';
import { DeckRearrangeScreen } from './DeckRearrangeScreen';
import { useGameStore } from '../store/gameStore';

const sendSpy = vi.fn();
vi.mock('../socket/socketClient', () => ({
  sendDeckRearrange: (payload: { order: number[]; confirmed: boolean }) => sendSpy(payload),
}));

function renderWith(cards: string[], seconds = 60) {
  useGameStore.getState().reset();
  useGameStore.getState().applyDeckRearrangeRequest({ cards, seconds });
  return render(<DeckRearrangeScreen />);
}

const rowLabels = () =>
  within(screen.getByTestId('deck-rearrange-list'))
    .getAllByRole('listitem')
    .map((li) => li.querySelector('span:nth-child(2)')?.textContent);

describe('DeckRearrangeScreen', () => {
  beforeEach(() => {
    sendSpy.mockClear();
    useGameStore.getState().reset();
  });

  it('renders the deck cards in the given top→bottom order', () => {
    renderWith(['Accusation', 'Night', 'Conspiracy']);
    expect(rowLabels()).toEqual(['Accusation', 'Night', 'Conspiracy']);
  });

  it('moving a row down updates the order and sends a TENTATIVE submit', () => {
    renderWith(['Accusation', 'Night', 'Conspiracy']);

    // Move row 1 (Accusation, original index 0) down → swaps positions 0 and 1.
    fireEvent.click(screen.getByLabelText('Move row 1 down'));

    expect(rowLabels()).toEqual(['Night', 'Accusation', 'Conspiracy']);
    expect(sendSpy).toHaveBeenCalledWith({ order: [1, 0, 2], confirmed: false });
  });

  it('Confirm sends the final reordered permutation', () => {
    renderWith(['Accusation', 'Night', 'Conspiracy']);

    fireEvent.click(screen.getByLabelText('Move row 3 up')); // Conspiracy (idx 2) up → [0,2,1]
    sendSpy.mockClear();

    fireEvent.click(screen.getByRole('button', { name: 'Confirm' }));
    expect(sendSpy).toHaveBeenCalledWith({ order: [0, 2, 1], confirmed: true });
  });

  describe('countdown', () => {
    beforeEach(() => vi.useFakeTimers());
    afterEach(() => vi.useRealTimers());

    it('renders a countdown and auto-submits the in-progress order at 0', () => {
      renderWith(['Accusation', 'Night'], 2);
      expect(screen.getByTestId('rearrange-countdown')).toHaveTextContent('2s');

      // Move a row, then let the window expire — her in-progress order must commit.
      fireEvent.click(screen.getByLabelText('Move row 1 down')); // → [1, 0]
      sendSpy.mockClear();

      act(() => {
        vi.advanceTimersByTime(2000);
      });
      expect(sendSpy).toHaveBeenCalledWith({ order: [1, 0], confirmed: true });
    });
  });
});
