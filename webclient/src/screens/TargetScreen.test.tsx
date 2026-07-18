/**
 * TargetScreen — picking the sub-target of a two-target card (Robbery/Scapegoat recipient).
 * Locks: it renders ONLY the host-supplied eligible ids (resolved to names), submits the
 * chosen ID (not the display name), and the countdown clears WITHOUT submitting (the host
 * declines the play and keeps the card).
 */

import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { render, screen, fireEvent, within, act } from '@testing-library/react';
import { TargetScreen } from './TargetScreen';
import { useGameStore } from '../store/gameStore';

const sendSpy = vi.fn();
vi.mock('../socket/socketClient', () => ({
  sendTargetSubmit: (payload: { targetPlayerId: string }) => sendSpy(payload),
}));

function seed(prompt = 'robbery_recipient', targets = ['p1', 'p2'], seconds = 30) {
  useGameStore.getState().reset();
  useGameStore.getState().applyGameStateUpdate({
    players: [
      { playerId: 'p0', displayName: 'Alice', accusations: 0, eliminated: false },
      { playerId: 'p1', displayName: 'Bob', accusations: 0, eliminated: false },
      { playerId: 'p2', displayName: 'Carol', accusations: 0, eliminated: false },
      { playerId: 'p3', displayName: 'Dave', accusations: 0, eliminated: false },
    ],
  });
  useGameStore.getState().applyTargetRequest({ prompt, targets, seconds });
  return render(<TargetScreen />);
}

const optionNames = () =>
  within(screen.getByTestId('target-list'))
    .getAllByRole('button')
    .map((b) => b.textContent);

describe('TargetScreen', () => {
  beforeEach(() => {
    sendSpy.mockClear();
    useGameStore.getState().reset();
  });

  it('renders only the host-supplied eligible players, resolved to names', () => {
    seed('robbery_recipient', ['p1', 'p2']);
    // p0 (self) and p3 are NOT eligible — the host excluded them; the screen must not invent them.
    expect(optionNames()).toEqual(['Bob', 'Carol']);
    expect(screen.getByTestId('target-title')).toHaveTextContent('Give the cards to…');
  });

  it('submits the chosen public ID, not the display name', () => {
    seed('robbery_recipient', ['p1', 'p2']);
    fireEvent.click(screen.getByText('Carol'));
    fireEvent.click(screen.getByTestId('target-confirm'));

    expect(sendSpy).toHaveBeenCalledTimes(1);
    expect(sendSpy).toHaveBeenCalledWith({ targetPlayerId: 'p2' });
    expect(useGameStore.getState().targetRequest).toBeNull();
  });

  it('cannot confirm before choosing', () => {
    seed();
    expect(screen.getByTestId('target-confirm')).toBeDisabled();
    fireEvent.click(screen.getByTestId('target-confirm'));
    expect(sendSpy).not.toHaveBeenCalled();
  });

  it('uses scapegoat copy for the scapegoat prompt code', () => {
    seed('scapegoat_recipient', ['p1']);
    expect(screen.getByTestId('target-title')).toHaveTextContent('Move the cards to…');
  });

  it('falls back to generic copy for an unknown prompt code', () => {
    seed('some_future_pick', ['p1']);
    expect(screen.getByTestId('target-title')).toHaveTextContent('Choose a player');
  });

  it('falls back to the raw id when a name cannot be resolved', () => {
    useGameStore.getState().reset();
    useGameStore.getState().applyTargetRequest({
      prompt: 'robbery_recipient',
      targets: ['ghost9'],
      seconds: 30,
    });
    render(<TargetScreen />);
    expect(optionNames()).toEqual(['ghost9']);
  });

  describe('countdown', () => {
    beforeEach(() => vi.useFakeTimers());
    afterEach(() => vi.useRealTimers());

    it('clears WITHOUT submitting at 0 (host declines; card is kept)', () => {
      seed('robbery_recipient', ['p1', 'p2'], 2);
      expect(screen.getByTestId('target-countdown')).toHaveTextContent('2s');

      act(() => {
        vi.advanceTimersByTime(2000);
      });

      expect(sendSpy).not.toHaveBeenCalled();
      expect(useGameStore.getState().targetRequest).toBeNull();
    });
  });
});
