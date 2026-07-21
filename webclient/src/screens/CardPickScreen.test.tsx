import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { render, screen, fireEvent, within, act } from '@testing-library/react';
import { CardPickScreen } from './CardPickScreen';
import { useGameStore } from '../store/gameStore';

const sendSpy = vi.fn();
vi.mock('../socket/socketClient', () => ({
  sendCardPick: (payload: { index: number }) => sendSpy(payload),
}));

function renderWith(cards: string[], pickNumber = 1, totalPicks = 3, seconds = 45, allowDone = false) {
  useGameStore.getState().reset();
  useGameStore.getState().applyCardPickRequest({ cards, pickNumber, totalPicks, seconds, allowDone });
  return render(<CardPickScreen />);
}

const rowLabels = () =>
  within(screen.getByTestId('card-pick-list'))
    .getAllByRole('listitem')
    .map((li) => li.querySelector('span:nth-child(2)')?.textContent);

describe('CardPickScreen', () => {
  beforeEach(() => {
    sendSpy.mockClear();
    useGameStore.getState().reset();
  });

  it('renders the draft pool and the "pick N of up to 3" progress', () => {
    renderWith(['Accusation', 'Alibi', 'Asylum'], 2, 3);
    expect(rowLabels()).toEqual(['Accusation', 'Alibi', 'Asylum']);
    expect(screen.getByTestId('card-pick-progress')).toHaveTextContent('Pick 2 of up to 3');
  });

  it('curse_discard reason shows discard copy, not "take"', () => {
    useGameStore.getState().reset();
    useGameStore.getState().applyCardPickRequest({
      cards: ['Asylum', 'Piety'],
      pickNumber: 1,
      totalPicks: 1,
      seconds: 30,
      allowDone: false,
      reason: 'curse_discard',
    });
    render(<CardPickScreen />);
    expect(screen.getByRole('heading')).toHaveTextContent('Curse a card');
    expect(screen.getByTestId('card-pick-progress')).toHaveTextContent('Choose a blue card to discard');
    // the pool is still tappable by index
    expect(rowLabels()).toEqual(['Asylum', 'Piety']);
  });

  it('tapping a card submits its index and clears the screen', () => {
    renderWith(['Accusation', 'Alibi', 'Asylum']);

    // Tap the second card (index 1).
    fireEvent.click(screen.getByText('Alibi'));

    expect(sendSpy).toHaveBeenCalledWith({ index: 1 });
    expect(useGameStore.getState().cardPick).toBeNull();
  });

  it('no Done button unless allowDone (John draft is mandatory)', () => {
    renderWith(['Accusation', 'Alibi']); // allowDone defaults false
    expect(screen.queryByTestId('card-pick-done')).toBeNull();
  });

  it('Done button (allowDone) submits the -1 skip sentinel and clears', () => {
    renderWith(['Accusation', 'Alibi', 'Asylum'], 2, 2, 45, true);
    fireEvent.click(screen.getByTestId('card-pick-done'));
    expect(sendSpy).toHaveBeenCalledWith({ index: -1 });
    expect(useGameStore.getState().cardPick).toBeNull();
  });

  it('tapping the first card submits index 0 exactly once', () => {
    renderWith(['Accusation', 'Alibi']);
    fireEvent.click(screen.getByText('Accusation'));
    expect(sendSpy).toHaveBeenCalledTimes(1);
    expect(sendSpy).toHaveBeenCalledWith({ index: 0 });
    // The pick resolved and cleared the screen (real app unmounts to the next request/idle).
    expect(useGameStore.getState().cardPick).toBeNull();
  });

  describe('countdown', () => {
    beforeEach(() => vi.useFakeTimers());
    afterEach(() => vi.useRealTimers());

    it('renders a countdown and clears WITHOUT submitting at 0 (host safety-picks)', () => {
      renderWith(['Accusation', 'Alibi'], 1, 3, 2);
      expect(screen.getByTestId('card-pick-countdown')).toHaveTextContent('2s');

      act(() => {
        vi.advanceTimersByTime(2000);
      });
      expect(sendSpy).not.toHaveBeenCalled();
      expect(useGameStore.getState().cardPick).toBeNull();
    });
  });
});
