/**
 * Confirm screen — a yes/no decision for THIS player's own optional ("may") ability.
 * Currently Abigail Williams: "If you place the final accusation on a tryal, you may
 * discard all accusations in front of you."
 *
 * NOT a masked secret phase: Town Hall identity is PUBLIC, so a holder-only prompt leaks
 * nothing (same class as the Tituba/Parris action buttons). It is routed to this one socket
 * because it is this player's private decision UI, not because the fact is secret.
 *
 * The host owns the window and defaults to YES on expiry (the choice is near-always
 * beneficial), so on countdown expiry the phone just clears — it does not submit.
 *
 * `PROMPT_COPY` is keyed by the machine code so future confirms reuse this screen.
 */

import { useEffect, useRef, useState } from 'react';
import { useGameStore } from '../store/gameStore';
import { sendConfirm } from '../socket/socketClient';
import { RoleIndicator } from '../components/RoleIndicator';

interface PromptCopy {
  title: string;
  /** Renders the context line above the card list. */
  detail: (count: number) => string;
  yes: string;
  no: string;
}

const PROMPT_COPY: Record<string, PromptCopy> = {
  abigail_discard: {
    title: 'Discard your accusations?',
    detail: (count) =>
      `You have ${count} accusation${count === 1 ? '' : 's'} in front of you.`,
    yes: 'Discard them',
    no: 'Keep them',
  },
  // Will Grigs: "You may choose to use alibi cards as if they were witness cards."
  // Yes = offensive Witness (+7 accusations on the target); No = the normal defensive Alibi
  // (removes accusations from the target). Opposite effects — the wording must be unambiguous.
  grigs_alibi_mode: {
    title: 'Use this Alibi as a Witness?',
    detail: () =>
      'Witness adds 7 accusations to your target. A normal Alibi removes accusations from them instead.',
    yes: 'Use as Witness (+7)',
    no: 'Use as Alibi (remove)',
  },
};

const FALLBACK: PromptCopy = {
  title: 'Use your ability?',
  detail: (count) => (count > 0 ? `${count} in front of you.` : ''),
  yes: 'Yes',
  no: 'No',
};

export function ConfirmScreen() {
  const prompt = useGameStore((s) => s.confirm?.prompt ?? '');
  const items = useGameStore((s) => s.confirm?.items ?? []);
  const count = useGameStore((s) => s.confirm?.count ?? 0);
  const seconds = useGameStore((s) => s.confirm?.seconds ?? 20);
  const clearConfirm = useGameStore((s) => s.clearConfirm);

  const [secondsLeft, setSecondsLeft] = useState(seconds);
  const resolvedRef = useRef(false);

  const copy = PROMPT_COPY[prompt] ?? FALLBACK;

  const answer = (confirmed: boolean) => {
    if (resolvedRef.current) return;
    resolvedRef.current = true;
    sendConfirm({ confirmed });
    clearConfirm();
  };

  // Host-owned window: a 1Hz countdown. On expiry the host applies its default, so we
  // just clear (no submit) to leave the screen.
  useEffect(() => {
    const id = setInterval(() => setSecondsLeft((s) => Math.max(0, s - 1)), 1000);
    return () => clearInterval(id);
  }, []);

  useEffect(() => {
    if (secondsLeft === 0 && !resolvedRef.current) {
      resolvedRef.current = true;
      clearConfirm();
    }
  }, [secondsLeft, clearConfirm]);

  return (
    <div className="flex min-h-dvh flex-col gap-4 bg-ink px-6 py-8" data-testid="confirm-screen">
      <header className="flex items-center justify-between">
        <h2 className="text-xl font-semibold text-parchment" data-testid="confirm-title">
          {copy.title}
        </h2>
        <RoleIndicator />
      </header>

      <p className="text-center text-sm text-parchment/70" data-testid="confirm-detail">
        {copy.detail(count)}
      </p>
      <p className="text-center text-sm text-parchment/70" data-testid="confirm-countdown">
        {secondsLeft}s
      </p>

      {items.length > 0 && (
        <ul className="flex flex-col gap-2" data-testid="confirm-items">
          {items.map((label, index) => (
            <li
              key={index}
              className="rounded-md border border-ember/40 bg-ember/10 px-3 py-2 text-sm text-parchment"
            >
              {label}
            </li>
          ))}
        </ul>
      )}

      <div className="sticky bottom-0 mt-auto flex flex-col gap-2 bg-ink pt-2">
        <button
          type="button"
          onClick={() => answer(true)}
          data-testid="confirm-yes"
          className="rounded-md bg-candle px-4 py-3 text-lg font-semibold text-ink"
        >
          {copy.yes}
        </button>
        <button
          type="button"
          onClick={() => answer(false)}
          data-testid="confirm-no"
          className="rounded-md border border-parchment/40 px-4 py-3 text-lg font-semibold text-parchment"
        >
          {copy.no}
        </button>
      </div>
    </div>
  );
}
