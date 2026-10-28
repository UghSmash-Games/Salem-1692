/**
 * PublicRevealToast — a brief, non-blocking public announcement for `public_reveal`
 * events (e.g. Giles Corey showing two red cards). Public data only, already routed
 * to phones and mirrors; this just surfaces it.
 *
 * Deliberately reason-AGNOSTIC: the copy is composed generically from the actor's
 * name and the shown card names, so future public_reveal reasons reuse it without
 * new code. `REASON_VERB` is the only extension point — add a reason→verb entry to
 * customize phrasing; everything falls back to "shows".
 *
 * Style: a fixed top banner (NOT a full-screen takeover — this is informational, not
 * a dramatic beat). z-40 sits BELOW RevealOverlay (z-50) so an elimination reveal
 * always wins; pointer-events-none so it never intercepts taps. Auto-dismisses.
 */

import { useEffect } from 'react';
import { useGameStore } from '../store/gameStore';

const AUTO_DISMISS_MS = 4000;

/** reason → verb. Default 'shows'; add entries here to customize future reasons. */
const REASON_VERB: Record<string, string> = {
  default: 'shows',
};

export function PublicRevealToast() {
  const reveal = useGameStore((s) => s.lastPublicReveal);
  const players = useGameStore((s) => s.publicBoard.players);
  const clearPublicReveal = useGameStore((s) => s.clearPublicReveal);

  // Auto-dismiss; re-keyed on the reveal identity so a new event resets the timer.
  useEffect(() => {
    if (!reveal) return;
    const t = setTimeout(() => clearPublicReveal(), AUTO_DISMISS_MS);
    return () => clearTimeout(t);
  }, [reveal, clearPublicReveal]);

  if (!reveal) return null;

  const name =
    players.find((p) => p.playerId === reveal.playerId)?.displayName ??
    reveal.playerId ??
    'A player';
  const verb = REASON_VERB[reveal.reason] ?? REASON_VERB.default;
  const cards = (reveal.cards ?? []).join(' & ');

  return (
    <div
      className="pointer-events-none fixed inset-x-0 top-0 z-40 flex justify-center px-4 pt-3"
      data-testid="public-reveal-toast"
    >
      <div className="max-w-md rounded-md border border-candle/50 bg-ink/90 px-4 py-2 text-center shadow-lg motion-safe:animate-fadeIn">
        <p className="text-sm text-parchment">
          <span className="font-semibold text-candle">{name}</span> {verb}{' '}
          <span className="font-semibold text-parchment">{cards}</span>
        </p>
      </div>
    </div>
  );
}
