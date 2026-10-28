/**
 * useSynchronizedReveal — turns a server `phase_resolve` timestamp into a
 * reveal animation that fires at the same wall-clock moment on every screen.
 *
 * CRITICAL (CLAUDE.md): the reveal must trigger on `revealAt - Date.now()`,
 * NOT on message receipt. Two mirrors with different network latency both
 * schedule against the same absolute `revealAt`, so they animate in unison.
 * Clock-skew correction between devices is out of scope (assumes synced clocks).
 */

import { useEffect, useRef, useState } from 'react';
import { useGameStore } from '../store/gameStore';

export type RevealPhase = 'idle' | 'counting' | 'revealed';

export interface RevealState {
  phase: RevealPhase;
  /** Whole seconds until reveal while counting (0 once revealed). */
  secondsRemaining: number;
}

export function useSynchronizedReveal(): RevealState {
  const revealAt = useGameStore((s) => s.reveal?.revealAt ?? null);

  const [phase, setPhase] = useState<RevealPhase>('idle');
  const [secondsRemaining, setSecondsRemaining] = useState(0);
  const timerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const tickRef = useRef<ReturnType<typeof setInterval> | null>(null);

  useEffect(() => {
    // Always clear any prior schedule when revealAt changes / unmounts.
    const clearAll = () => {
      if (timerRef.current) clearTimeout(timerRef.current);
      if (tickRef.current) clearInterval(tickRef.current);
      timerRef.current = null;
      tickRef.current = null;
    };

    if (revealAt === null) {
      clearAll();
      setPhase('idle');
      setSecondsRemaining(0);
      return clearAll;
    }

    const delay = revealAt - Date.now();

    if (delay <= 0) {
      // Joined late or high latency: the moment has already passed.
      clearAll();
      setPhase('revealed');
      setSecondsRemaining(0);
      return clearAll;
    }

    setPhase('counting');
    setSecondsRemaining(Math.ceil(delay / 1000));

    // Fire the reveal exactly at the wall-clock moment.
    timerRef.current = setTimeout(() => {
      setPhase('revealed');
      setSecondsRemaining(0);
      if (tickRef.current) clearInterval(tickRef.current);
      tickRef.current = null;
    }, delay);

    // Update the on-screen countdown each second, recomputed from revealAt.
    tickRef.current = setInterval(() => {
      const remaining = revealAt - Date.now();
      setSecondsRemaining(Math.max(0, Math.ceil(remaining / 1000)));
    }, 250);

    return clearAll;
  }, [revealAt]);

  return { phase, secondsRemaining };
}
