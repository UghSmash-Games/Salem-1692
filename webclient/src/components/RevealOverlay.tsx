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

/**
 * How long the outcome stays up after the reveal.
 * ⚠ MUST match HostRevealOverlay.lingerSeconds in Unity (Assets/Project/Scripts/UI/HostDisplay).
 * If these drift, one screen clears the reveal while the other is still showing it — the exact
 * desync the phase_resolve pattern exists to prevent.
 */
const REVEALED_LINGER_MS = 6000;

/**
 * `savedBy` is a LABEL from NightResolver ("constable" | "confession" | ""), NOT a playerId.
 *
 * This previously ran it through nameFor(), a player lookup that always failed and fell through to
 * printing the raw label — it read correctly by accident. Mapping it explicitly makes the contract
 * visible.
 *
 * The label form is deliberate and must stay: naming the saver would publish the CONSTABLE'S SECRET
 * IDENTITY to every player and mirror. See docs/protocol.md → elimination_result.
 */
function savedByPhrase(savedBy: string | null | undefined): string {
  switch (savedBy) {
    case 'constable':
      return 'saved by the constable';
    case 'confession':
      return 'saved by confession';
    default:
      return 'spared';
  }
}

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
                  {nameFor(lastElimination.playerId)} was {savedByPhrase(lastElimination.savedBy)}
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
