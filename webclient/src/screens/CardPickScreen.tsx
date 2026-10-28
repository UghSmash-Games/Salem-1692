/**
 * Card-pick screen — shown to a drafter (private channel). Serves two abilities:
 *  - John Proctor / Martha draft (mandatory pick from a dead player's hand), and
 *  - Samuel Parris discard-pick ("up to N" — `allowDone` shows a Done button that submits index -1).
 *
 * The host issues a fresh request per pick; this screen handles a SINGLE pick, then clears and reopens
 * when the next request arrives. A countdown shows the host-owned window — on expiry the host resolves
 * (John safety-picks; Parris stops), so the phone just clears (no submit).
 *
 * NOT a masked secret phase: the pick's existence is public, only the card identities are private
 * (routed to this one socket, like the deck-rearrange pool).
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
  const allowDone = useGameStore((s) => s.cardPick?.allowDone ?? false);
  const reason = useGameStore((s) => s.cardPick?.reason);
  const clearCardPick = useGameStore((s) => s.clearCardPick);

  // Copy per pick context. Curse discards an OPPONENT'S blue card (not a take); John/Parris take.
  const isCurse = reason === 'curse_discard';
  const heading = isCurse ? 'Curse a card' : 'Take a card';

  const [secondsLeft, setSecondsLeft] = useState(seconds);
  const resolvedRef = useRef(false);

  const pick = (index: number) => {
    if (resolvedRef.current) return;
    resolvedRef.current = true;
    sendCardPick({ index });
    clearCardPick(); // reopens when the host sends the next pick request
  };

  // "Done / take fewer" — only for "up to N" picks (allowDone). Submits the -1 skip sentinel,
  // which the host interprets as "stop picking, take what I have."
  const done = () => pick(-1);

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
        <h2 className="text-xl font-semibold text-parchment">{heading}</h2>
        <RoleIndicator />
      </header>

      {isCurse ? (
        <p className="text-center text-sm text-parchment/70" data-testid="card-pick-progress">
          Choose a blue card to discard
        </p>
      ) : (
        <p className="text-center text-sm text-parchment/70" data-testid="card-pick-progress">
          Pick {pickNumber} of up to {totalPicks}
        </p>
      )}
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

      {allowDone && (
        <button
          type="button"
          onClick={done}
          data-testid="card-pick-done"
          className="sticky bottom-0 mt-auto rounded-md border border-parchment/40 bg-ink px-4 py-3 text-lg font-semibold text-parchment"
        >
          Done
        </button>
      )}
    </div>
  );
}
