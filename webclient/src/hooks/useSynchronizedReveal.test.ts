/**
 * The synchronized-reveal contract: the reveal fires based on the absolute
 * `revealAt` timestamp, not on when the component mounted or the message
 * arrived. Two screens scheduling against the same revealAt reveal together.
 */

import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { renderHook, act } from '@testing-library/react';
import { useSynchronizedReveal } from './useSynchronizedReveal';
import { useGameStore } from '../store/gameStore';

const BASE = 1_700_000_000_000;

describe('useSynchronizedReveal', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    vi.setSystemTime(BASE);
    useGameStore.getState().reset();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('is idle when there is no pending reveal', () => {
    const { result } = renderHook(() => useSynchronizedReveal());
    expect(result.current.phase).toBe('idle');
  });

  it('counts down then reveals exactly at revealAt', () => {
    useGameStore.getState().applyPhaseResolve({ revealAt: BASE + 3000 });
    const { result } = renderHook(() => useSynchronizedReveal());

    // Immediately counting, ~3s remaining.
    expect(result.current.phase).toBe('counting');
    expect(result.current.secondsRemaining).toBe(3);

    // Just before the moment — still counting.
    act(() => {
      vi.advanceTimersByTime(2999);
    });
    expect(result.current.phase).toBe('counting');

    // At revealAt — revealed.
    act(() => {
      vi.advanceTimersByTime(1);
    });
    expect(result.current.phase).toBe('revealed');
    expect(result.current.secondsRemaining).toBe(0);
  });

  it('reveals immediately when revealAt is already in the past', () => {
    useGameStore.getState().applyPhaseResolve({ revealAt: BASE - 1000 });
    const { result } = renderHook(() => useSynchronizedReveal());
    expect(result.current.phase).toBe('revealed');
  });

  it('does NOT reveal early for a late mount — timing tracks revealAt', () => {
    // Reveal was scheduled 3s out, but this screen mounts 1s "late".
    useGameStore.getState().applyPhaseResolve({ revealAt: BASE + 3000 });
    vi.setSystemTime(BASE + 1000);
    const { result } = renderHook(() => useSynchronizedReveal());

    expect(result.current.phase).toBe('counting');
    // Only 2s of real delay remain for this late-mounting screen.
    act(() => {
      vi.advanceTimersByTime(2000);
    });
    expect(result.current.phase).toBe('revealed');
  });
});
