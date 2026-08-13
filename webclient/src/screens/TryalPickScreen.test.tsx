/**
 * TryalPickScreen — "which of their face-down tryals do you turn?"
 *
 * The load-bearing property is that the choice is BLIND: the payload carries a COUNT and nothing
 * else about the cards, and the answer is an ordinal into that face-down subset. These tests lock
 * that the screen renders exactly `count` indistinguishable options and submits an ordinal — if
 * someone later threads card labels through, the identity assertion here fails.
 */

import { describe, it, expect, beforeEach, vi, afterEach } from 'vitest';
import { render, screen, fireEvent, renderHook } from '@testing-library/react';
import { TryalPickScreen } from './TryalPickScreen';
import { useGameStore } from '../store/gameStore';
import { useCurrentScreen } from '../store/selectors';
import * as socketClient from '../socket/socketClient';
import type { TryalPickRequestPayload } from '../socket/types';

function arm(overrides: Partial<TryalPickRequestPayload> = {}) {
  useGameStore.getState().applyGameStateUpdate({
    phase: 'day',
    players: [
      { playerId: 'p0', displayName: 'Alice', accusations: 0, eliminated: false },
      { playerId: 'p1', displayName: 'Bob', accusations: 7, eliminated: false },
    ],
  });
  useGameStore.getState().applyTryalPickRequest({
    targetPlayerId: 'p1',
    count: 3,
    seconds: 25,
    reason: 'accusation_reveal',
    ...overrides,
  });
}

describe('TryalPickScreen', () => {
  beforeEach(() => useGameStore.getState().reset());
  afterEach(() => vi.restoreAllMocks());

  it('renders exactly `count` identical face-down options', () => {
    arm({ count: 3 });
    render(<TryalPickScreen />);

    const options = screen.getByTestId('tryal-pick-options');
    expect(options.children).toHaveLength(3);

    // Indistinguishable: every option renders the same text. Nothing on screen can hint at which
    // card is which, because the client is never told.
    const labels = Array.from(options.children).map((c) => c.textContent);
    expect(new Set(labels).size).toBe(1);
  });

  it('names the player whose tryal turns, resolved from the public board', () => {
    arm();
    render(<TryalPickScreen />);
    expect(screen.getByTestId('tryal-pick-screen')).toHaveTextContent('Bob');
  });

  it('submits the chosen ORDINAL, not a card identity', () => {
    const spy = vi.spyOn(socketClient, 'sendTryalPickSubmit').mockImplementation(() => {});
    arm({ count: 3 });
    render(<TryalPickScreen />);

    fireEvent.click(screen.getByTestId('tryal-pick-option-1'));
    fireEvent.click(screen.getByTestId('tryal-pick-confirm'));

    expect(spy).toHaveBeenCalledWith({ ordinal: 1 });
    // Submitting resolves the prompt so the phone leaves the screen.
    expect(useGameStore.getState().tryalPick).toBeNull();
  });

  it('cannot confirm before an option is chosen', () => {
    const spy = vi.spyOn(socketClient, 'sendTryalPickSubmit').mockImplementation(() => {});
    arm();
    render(<TryalPickScreen />);

    fireEvent.click(screen.getByTestId('tryal-pick-confirm'));
    expect(spy).not.toHaveBeenCalled();
  });

  it('uses reason-specific copy', () => {
    arm({ reason: 'conspiracy_reveal' });
    render(<TryalPickScreen />);
    expect(screen.getByTestId('tryal-pick-title')).toHaveTextContent(/black cat/i);
  });

  it('frames the conspiracy pass as TAKING a card, simultaneously', () => {
    // The pass is the one reason where the card is taken rather than turned, and where every player
    // is prompted at once — copy that said "reveal" here would describe the wrong game action.
    arm({ reason: 'conspiracy_pass' });
    render(<TryalPickScreen />);

    expect(screen.getByTestId('tryal-pick-title')).toHaveTextContent(/take/i);
    expect(screen.getByTestId('tryal-pick-hint')).toHaveTextContent(/same time/i);
    expect(screen.getByTestId('tryal-pick-hint')).toHaveTextContent(/left/i);
    expect(screen.getByTestId('tryal-pick-confirm')).toHaveTextContent('Take');
  });

  it('still submits an ordinal for the pass — no card identity anywhere', () => {
    const spy = vi.spyOn(socketClient, 'sendTryalPickSubmit').mockImplementation(() => {});
    arm({ reason: 'conspiracy_pass', count: 4 });
    render(<TryalPickScreen />);

    expect(screen.getByTestId('tryal-pick-options').children).toHaveLength(4);
    fireEvent.click(screen.getByTestId('tryal-pick-option-3'));
    fireEvent.click(screen.getByTestId('tryal-pick-confirm'));

    expect(spy).toHaveBeenCalledWith({ ordinal: 3 });
  });

  it('outranks the turn screen if both are somehow live', () => {
    // The host resolves the pick inside RunTurn BEFORE sending the next action_request, so these
    // should not overlap in practice. If they ever do, the blocking mandatory reveal must win —
    // otherwise the phone shows a turn prompt while the host waits on an answer it will never get.
    useGameStore.getState().onJoined('p0', 'ABCD');
    useGameStore.setState({
      actionRequest: { actions: ['draw'], unplayableCards: [] },
      tryalPick: {
        targetPlayerId: 'p1',
        count: 2,
        seconds: 25,
        reason: 'accusation_reveal',
      },
    });

    const { result } = renderHook(() => useCurrentScreen());
    expect(result.current).toBe('tryal_pick');
  });
});
