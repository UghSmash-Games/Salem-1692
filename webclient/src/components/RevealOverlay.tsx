/**
 * RevealOverlay — the synchronized dramatic beat on the mirror.
 *
 * While the server-coordinated countdown runs it shows the count; at the
 * shared `revealAt` moment it flips to the reveal animation and shows the
 * latest elimination_result. The timing comes entirely from
 * useSynchronizedReveal (i.e. from `revealAt`), never from message receipt.
 */

import { useEffect } from 'react';
import { useGameStore } from '../store/gameStore';
import { useSynchronizedReveal } from '../hooks/useSynchronizedReveal';

const REVEALED_LINGER_MS = 4000;

export function RevealOverlay() {
  const { phase, secondsRemaining } = useSynchronizedReveal();
  const lastElimination = useGameStore((s) => s.lastElimination);
  const players = useGameStore((s) => s.publicBoard.players);
  const clearReveal = useGameStore((s) => s.clearReveal);

  // Once revealed, linger briefly so viewers see the outcome, then dismiss.
  useEffect(() => {
    if (phase !== 'revealed') return;
    const t = setTimeout(() => clearReveal(), REVEALED_LINGER_MS);
    return () => clearTimeout(t);
  }, [phase, clearReveal]);

  if (phase === 'idle') return null;

  const nameFor = (playerId: string | null | undefined) =>
    players.find((p) => p.playerId === playerId)?.displayName ?? playerId ?? '';

  return (
    <div
      className="fixed inset-0 z-50 flex flex-col items-center justify-center bg-black/80 px-6 text-center"
      data-testid="reveal-overlay"
      data-phase={phase}
    >
      {phase === 'counting' && (
        <div className="flex flex-col items-center gap-4">
          <p className="text-sm uppercase tracking-[0.3em] text-parchment/60">
            The town holds its breath…
          </p>
          <div
            className="text-7xl font-bold text-candle"
            data-testid="reveal-countdown"
          >
            {secondsRemaining}
          </div>
        </div>
      )}

      {phase === 'revealed' && (
        <div className="flex flex-col items-center gap-3 motion-safe:animate-fadeIn">
          {lastElimination ? (
            lastElimination.eliminated ? (
              <>
                <div className="text-6xl">⚰️</div>
                <h2 className="text-3xl font-bold text-ember">
                  {nameFor(lastElimination.playerId)} was eliminated
                </h2>
              </>
            ) : (
              <>
                <div className="text-6xl">🛡️</div>
                <h2 className="text-3xl font-bold text-candle">
                  {nameFor(lastElimination.playerId)} was saved
                  {lastElimination.savedBy
                    ? ` by ${nameFor(lastElimination.savedBy)}`
                    : ''}
                </h2>
              </>
            )
          ) : (
            <h2 className="text-3xl font-bold text-parchment">The deed is done</h2>
          )}
        </div>
      )}
    </div>
  );
}
