/**
 * Tituba's deck-rearrange screen — shown only to the Tituba holder (private channel).
 *
 * She sees the whole deck top→bottom and reorders it with ↑/↓ buttons (no drag lib).
 * A 60-second countdown (the card's rules window) runs; each move sends a TENTATIVE
 * order so the host always has her in-progress arrangement, and Confirm OR the countdown
 * hitting 0 sends the FINAL order. The host owns the authoritative deadline and applies
 * the latest order it received — so letting the timer expire commits her work, not a discard.
 */

import { useEffect, useRef, useState } from 'react';
import { useGameStore } from '../store/gameStore';
import { sendDeckRearrange } from '../socket/socketClient';
import { RoleIndicator } from '../components/RoleIndicator';

export function DeckRearrangeScreen() {
  const cards = useGameStore((s) => s.deckRearrange?.cards ?? []);
  const seconds = useGameStore((s) => s.deckRearrange?.seconds ?? 60);
  const clearDeckRearrange = useGameStore((s) => s.clearDeckRearrange);

  // order[i] = the ORIGINAL deck index now sitting at position i (top→bottom).
  const [order, setOrder] = useState<number[]>(() => cards.map((_, i) => i));
  const [secondsLeft, setSecondsLeft] = useState(seconds);

  // Keep the latest order reachable from the countdown callback without re-arming it.
  const orderRef = useRef(order);
  orderRef.current = order;
  const submittedRef = useRef(false);

  const submitFinal = () => {
    if (submittedRef.current) return;
    submittedRef.current = true;
    sendDeckRearrange({ order: orderRef.current, confirmed: true });
    clearDeckRearrange();
  };

  // The rules 60s window: a 1Hz countdown…
  useEffect(() => {
    const id = setInterval(() => setSecondsLeft((s) => Math.max(0, s - 1)), 1000);
    return () => clearInterval(id);
  }, []);

  // …and on expiry, commit her in-progress order (not a discard).
  useEffect(() => {
    if (secondsLeft === 0) submitFinal();
    // submitFinal reads refs; any render's copy is correct.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [secondsLeft]);

  // Swap the row at `pos` with its neighbour and send a tentative order.
  const move = (pos: number, delta: number) => {
    const next = pos + delta;
    if (next < 0 || next >= order.length) return;
    const newOrder = order.slice();
    [newOrder[pos], newOrder[next]] = [newOrder[next], newOrder[pos]];
    setOrder(newOrder);
    sendDeckRearrange({ order: newOrder, confirmed: false });
  };

  return (
    <div className="flex min-h-dvh flex-col gap-4 bg-ink px-6 py-8">
      <header className="flex items-center justify-between">
        <h2 className="text-xl font-semibold text-parchment">Rearrange the deck</h2>
        <RoleIndicator />
      </header>

      <p className="text-center text-sm text-parchment/70" data-testid="rearrange-countdown">
        {secondsLeft}s
      </p>

      <ul className="flex flex-col gap-2" data-testid="deck-rearrange-list">
        {order.map((cardIdx, pos) => (
          <li
            key={pos}
            className="flex items-center gap-2 rounded-md border border-parchment/30 bg-ink/40 px-3 py-2"
          >
            <span className="w-6 text-right text-xs text-parchment/50">{pos + 1}</span>
            <span className="flex-1 text-parchment">{cards[cardIdx]}</span>
            <button
              type="button"
              aria-label={`Move row ${pos + 1} up`}
              disabled={pos === 0}
              onClick={() => move(pos, -1)}
              className="rounded border border-parchment/40 px-2 py-1 text-parchment disabled:opacity-30"
            >
              ↑
            </button>
            <button
              type="button"
              aria-label={`Move row ${pos + 1} down`}
              disabled={pos === order.length - 1}
              onClick={() => move(pos, 1)}
              className="rounded border border-parchment/40 px-2 py-1 text-parchment disabled:opacity-30"
            >
              ↓
            </button>
          </li>
        ))}
      </ul>

      <button
        type="button"
        onClick={submitFinal}
        className="mt-auto rounded-md bg-candle px-4 py-3 text-lg font-semibold text-ink"
      >
        Confirm
      </button>
    </div>
  );
}
