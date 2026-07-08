/**
 * John Proctor / Martha card-draft screen — shown only to a drafter (private channel).
 *
 * The drafter sees an eliminated player's hand (the draft pool) and taps ONE card to take.
 * The host issues a fresh request for each pick, alternating between John and Martha (John
 * first), up to 3 each; this screen therefore handles a SINGLE pick, then clears and reopens
 * when the next request arrives. A countdown shows the host-owned pick window — if it expires
 * the host safety-picks, so the phone just clears (no submit).
 *
 * NOT a masked secret phase: the draft's existence is public, only the card identities are
 * private (routed to this one socket, like the deck-rearrange pool).
 */

import { useEffect, useRef, useState } from 'react';
import { useGameStore } from '../store/gameStore';
import { sendCardPick } from '../socket/socketClient';
import { RoleIndicator } from '../components/RoleIndicator';

export function CardPickScreen() {
  const cards = useGameStore((s) => s.cardPick?.cards ?? []);
  const pickNumber = useGameStore((s) => s.cardPick?.pickNumber ?? 1);
  const totalPicks = useGameStore((s) => s.cardPick?.totalPicks ?? 3);
  const seconds = useGameStore((s) => s.cardPick?.seconds ?? 45);
  const clearCardPick = useGameStore((s) => s.clearCardPick);

  const [secondsLeft, setSecondsLeft] = useState(seconds);
  const resolvedRef = useRef(false);

  const pick = (index: number) => {
    if (resolvedRef.current) return;
    resolvedRef.current = true;
    sendCardPick({ index });
    clearCardPick(); // reopens when the host sends the next pick request
  };

  // Host-owned pick window: a 1Hz countdown. On expiry the host safety-picks, so we just
  // clear (no submit) to leave the screen.
  useEffect(() => {
    const id = setInterval(() => setSecondsLeft((s) => Math.max(0, s - 1)), 1000);
    return () => clearInterval(id);
  }, []);

  useEffect(() => {
    if (secondsLeft === 0 && !resolvedRef.current) {
      resolvedRef.current = true;
      clearCardPick();
    }
  }, [secondsLeft, clearCardPick]);

  return (
    <div className="flex min-h-dvh flex-col gap-4 bg-ink px-6 py-8">
      <header className="flex items-center justify-between">
        <h2 className="text-xl font-semibold text-parchment">Take a card</h2>
        <RoleIndicator />
      </header>

      <p className="text-center text-sm text-parchment/70" data-testid="card-pick-progress">
        Pick {pickNumber} of up to {totalPicks}
      </p>
      <p className="text-center text-sm text-parchment/70" data-testid="card-pick-countdown">
        {secondsLeft}s
      </p>

      <ul className="flex flex-col gap-2" data-testid="card-pick-list">
        {cards.map((label, index) => (
          <li key={index}>
            <button
              type="button"
              onClick={() => pick(index)}
              className="flex w-full items-center gap-2 rounded-md border border-parchment/30 bg-ink/40 px-3 py-3 text-left text-parchment"
            >
              <span className="w-6 text-right text-xs text-parchment/50">{index + 1}</span>
              <span className="flex-1">{label}</span>
            </button>
          </li>
        ))}
      </ul>
    </div>
  );
}
