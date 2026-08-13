/**
 * Tryal-pick screen — "which of their face-down tryal cards do you turn?"
 *
 * Shown to the player the rulebook gives the choice to: the accuser whose accusation crossed the
 * threshold, the Curse player who stripped a Piety at/over it, or the conspiracy drawer choosing on
 * the black-cat holder (p6). Before this existed all three silently picked at random for networked
 * players.
 *
 * 🔴 THE CHOICE IS BLIND, BY DESIGN. The host sends only a COUNT, so this renders `count` identical
 * card backs and submits an ORDINAL into that set. It has no card names to leak because it is never
 * told any — the same structural guarantee as the event log's closed kind vocabulary. Do NOT add a
 * label lookup here, and do not try to correlate a back with a real tryal slot.
 *
 * Also serves conspiracy step 2 (`conspiracy_pass`), where the card is TAKEN rather than turned and
 * EVERY player is asked at once. That one is simultaneous by the rulebook: the host sends all the
 * prompts in one frame on a shared window and moves no card until every answer is in. There is no
 * `acting` subset to hide — everyone picks — so submission timing cannot separate anyone by role,
 * which is why this reuses the plain prompt rather than the masked secret-phase machinery.
 *
 * Not a masked secret phase: for the reveal reasons the flip is public and only one player is asked,
 * so this is that player's private decision UI (same class as TargetScreen).
 *
 * ⚠ TIMEOUT DIFFERS FROM TargetScreen. There, expiry means "don't play the card" and the phone just
 * clears. Here the reveal is MANDATORY — the host turns a random face-down card if no answer
 * arrives — so expiry clears the screen but the flip still happens. The copy says so.
 */

import { useEffect, useRef, useState } from 'react';
import { useGameStore } from '../store/gameStore';
import { sendTryalPickSubmit } from '../socket/socketClient';
import { RoleIndicator } from '../components/RoleIndicator';

const REASON_COPY: Record<
  string,
  { title: string; hint: string; confirm?: string }
> = {
  accusation_reveal: {
    title: 'Turn a Tryal card',
    hint: 'The accusations against them are enough. Choose which card is revealed.',
  },
  piety_loss_reveal: {
    title: 'Their Piety is gone',
    hint: 'Without it the accusations against them suffice. Choose which card is revealed.',
  },
  conspiracy_reveal: {
    title: 'The black cat demands a Tryal',
    hint: 'You drew the conspiracy. Choose which of their cards is revealed.',
  },
  // The pass is the one reason where the card is TAKEN, not turned — everyone does this at once,
  // and the card stays face-down (only you will see what you took).
  conspiracy_pass: {
    title: 'Take a Tryal card',
    hint: 'Everyone chooses at the same time. Take one face-down card from the player on your left.',
    confirm: 'Take',
  },
};

const FALLBACK = { title: 'Turn a Tryal card', hint: '' };

export function TryalPickScreen() {
  const targetPlayerId = useGameStore((s) => s.tryalPick?.targetPlayerId ?? '');
  const count = useGameStore((s) => s.tryalPick?.count ?? 0);
  const seconds = useGameStore((s) => s.tryalPick?.seconds ?? 25);
  const reason = useGameStore((s) => s.tryalPick?.reason ?? '');
  const players = useGameStore((s) => s.publicBoard.players);
  const clearTryalPick = useGameStore((s) => s.clearTryalPick);

  const [selected, setSelected] = useState<number | null>(null);
  const [secondsLeft, setSecondsLeft] = useState(seconds);
  const resolvedRef = useRef(false);

  const copy = REASON_COPY[reason] ?? FALLBACK;
  const targetName =
    players.find((p) => p.playerId === targetPlayerId)?.displayName ?? 'them';

  const submit = () => {
    if (resolvedRef.current || selected === null) return;
    resolvedRef.current = true;
    sendTryalPickSubmit({ ordinal: selected });
    clearTryalPick();
  };

  useEffect(() => {
    const id = setInterval(() => setSecondsLeft((s) => Math.max(0, s - 1)), 1000);
    return () => clearInterval(id);
  }, []);

  // On expiry the host reveals a RANDOM face-down tryal — the flip is not cancelled, so this
  // just stops asking.
  useEffect(() => {
    if (secondsLeft === 0 && !resolvedRef.current) {
      resolvedRef.current = true;
      clearTryalPick();
    }
  }, [secondsLeft, clearTryalPick]);

  return (
    <div
      className="flex min-h-dvh flex-col gap-4 bg-ink px-6 py-8"
      data-testid="tryal-pick-screen"
    >
      <header className="flex items-center justify-between">
        <h2
          className="text-xl font-semibold text-parchment"
          data-testid="tryal-pick-title"
        >
          {copy.title}
        </h2>
        <RoleIndicator />
      </header>

      <p className="text-center text-sm text-parchment/70" data-testid="tryal-pick-hint">
        {copy.hint}
      </p>
      <p className="text-center text-sm text-parchment/70">
        <span className="font-semibold text-candle">{targetName}</span>
      </p>
      <p className="text-center text-sm text-parchment/70" data-testid="tryal-pick-countdown">
        {secondsLeft}s
      </p>

      {/* `count` identical backs — there is deliberately nothing to tell them apart. */}
      <div className="flex flex-wrap justify-center gap-3" data-testid="tryal-pick-options">
        {Array.from({ length: count }, (_, i) => (
          <button
            key={i}
            type="button"
            onClick={() => setSelected(i)}
            aria-label={`Face-down Tryal card ${i + 1}`}
            aria-pressed={selected === i}
            data-testid={`tryal-pick-option-${i}`}
            className={`h-28 w-20 rounded-md border-2 bg-ink/60 text-parchment/40 ${
              selected === i ? 'border-candle' : 'border-parchment/30'
            }`}
          >
            ?
          </button>
        ))}
      </div>

      <button
        type="button"
        disabled={selected === null}
        onClick={submit}
        data-testid="tryal-pick-confirm"
        className="mt-auto rounded-md bg-candle px-4 py-3 text-lg font-semibold text-ink disabled:opacity-40"
      >
        {copy.confirm ?? 'Reveal'}
      </button>
    </div>
  );
}
