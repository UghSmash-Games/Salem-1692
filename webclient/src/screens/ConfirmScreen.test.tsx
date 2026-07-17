/**
 * ConfirmScreen — the yes/no decision for a player's own optional ("may") ability.
 * Locks: the copy is driven by the prompt CODE (reusable), Yes/No submit the right
 * boolean, and the host-owned countdown clears WITHOUT submitting (host applies its
 * own default — for Abigail, "clear").
 */

import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { render, screen, fireEvent, within, act } from '@testing-library/react';
import { ConfirmScreen } from './ConfirmScreen';
import { useGameStore } from '../store/gameStore';

const sendSpy = vi.fn();
vi.mock('../socket/socketClient', () => ({
  sendConfirm: (payload: { confirmed: boolean }) => sendSpy(payload),
}));

function renderWith(
  prompt = 'abigail_discard',
  items = ['Evidence', 'Accusation'],
  count = 4,
  seconds = 20,
) {
  useGameStore.getState().reset();
  useGameStore.getState().applyConfirmRequest({ prompt, items, count, seconds });
  return render(<ConfirmScreen />);
}

describe('ConfirmScreen', () => {
  beforeEach(() => {
    sendSpy.mockClear();
    useGameStore.getState().reset();
  });

  it('renders the prompt copy, the numeric context, and the context items', () => {
    renderWith();

    expect(screen.getByTestId('confirm-title')).toHaveTextContent('Discard your accusations?');
    // count (4) is the accusation TOTAL — deliberately not items.length (Evidence = 3).
    expect(screen.getByTestId('confirm-detail')).toHaveTextContent('4 accusations');

    const labels = within(screen.getByTestId('confirm-items'))
      .getAllByRole('listitem')
      .map((li) => li.textContent);
    expect(labels).toEqual(['Evidence', 'Accusation']);
  });

  it('Yes submits confirmed:true and clears', () => {
    renderWith();
    fireEvent.click(screen.getByTestId('confirm-yes'));

    expect(sendSpy).toHaveBeenCalledTimes(1);
    expect(sendSpy).toHaveBeenCalledWith({ confirmed: true });
    expect(useGameStore.getState().confirm).toBeNull();
  });

  it('No submits confirmed:false and clears', () => {
    renderWith();
    fireEvent.click(screen.getByTestId('confirm-no'));

    expect(sendSpy).toHaveBeenCalledTimes(1);
    expect(sendSpy).toHaveBeenCalledWith({ confirmed: false });
    expect(useGameStore.getState().confirm).toBeNull();
  });

  it('falls back to generic copy for an unknown prompt code (reusable screen)', () => {
    renderWith('some_future_choice', [], 0);
    expect(screen.getByTestId('confirm-title')).toHaveTextContent('Use your ability?');
    expect(screen.getByTestId('confirm-yes')).toHaveTextContent('Yes');
    expect(screen.getByTestId('confirm-no')).toHaveTextContent('No');
  });

  it('singularizes the accusation count', () => {
    renderWith('abigail_discard', ['Accusation'], 1);
    expect(screen.getByTestId('confirm-detail')).toHaveTextContent('1 accusation in front of you');
  });

  describe('countdown', () => {
    beforeEach(() => vi.useFakeTimers());
    afterEach(() => vi.useRealTimers());

    it('clears WITHOUT submitting at 0 (the host applies its own default)', () => {
      renderWith('abigail_discard', ['Accusation'], 1, 2);
      expect(screen.getByTestId('confirm-countdown')).toHaveTextContent('2s');

      act(() => {
        vi.advanceTimersByTime(2000);
      });

      expect(sendSpy).not.toHaveBeenCalled();
      expect(useGameStore.getState().confirm).toBeNull();
    });
  });
});
