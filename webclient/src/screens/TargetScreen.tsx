/**
 * Target screen — pick another PLAYER: the sub-target of a two-target card
 * (Robbery's recipient, Scapegoat's destination).
 *
 * The host sends the ELIGIBLE public player ids (never self, never the victim, never eliminated)
 * and re-verifies whatever comes back, so this screen just renders what it was given — it does not
 * compute eligibility itself.
 *
 * Not a masked secret phase: it's the acting player's own choice, routed to their socket as private
 * decision UI. On countdown expiry the host declines the play (the card is NOT consumed), so the
 * phone just clears without submitting.
 *
 * `PROMPT_COPY` is keyed by the machine code so future sub-target picks reuse this screen.
 */

import { useEffect, useMemo, useRef, useState } from 'react';
import { useGameStore } from '../store/gameStore';
import { sendTargetSubmit } from '../socket/socketClient';
import { PlayerTargetList } from '../components/PlayerTargetList';
import { RoleIndicator } from '../components/RoleIndicator';

const PROMPT_COPY: Record<string, { title: string; hint: string }> = {
  robbery_recipient: {
    title: 'Give the cards to…',
    hint: 'Their hand goes to the player you choose.',
  },
  scapegoat_recipient: {
    title: 'Move the cards to…',
    hint: 'The cards in front of them move to the player you choose.',
  },
};

const FALLBACK = { title: 'Choose a player', hint: '' };

export function TargetScreen() {
  const prompt = useGameStore((s) => s.targetRequest?.prompt ?? '');
  const targetIds = useGameStore((s) => s.targetRequest?.targets ?? []);
  const seconds = useGameStore((s) => s.targetRequest?.seconds ?? 30);
  const players = useGameStore((s) => s.publicBoard.players);
  const clearTargetRequest = useGameStore((s) => s.clearTargetRequest);

  const [selected, setSelected] = useState<string | null>(null);
  const [secondsLeft, setSecondsLeft] = useState(seconds);
  const resolvedRef = useRef(false);

  const copy = PROMPT_COPY[prompt] ?? FALLBACK;

  // Resolve the host's public ids to display names for rendering; keep the id for the answer.
  const options = useMemo(
    () =>
      targetIds.map((id) => ({
        id,
        name: players.find((p) => p.playerId === id)?.displayName ?? id,
      })),
    [targetIds, players],
  );

  const submit = () => {
    if (resolvedRef.current || selected === null) return;
    const picked = options.find((o) => o.name === selected);
    if (!picked) return;
    resolvedRef.current = true;
    sendTargetSubmit({ targetPlayerId: picked.id });
    clearTargetRequest();
  };

  // Host-owned window: a 1Hz countdown. On expiry the host declines the play (card kept), so we
  // just clear without submitting.
  useEffect(() => {
    const id = setInterval(() => setSecondsLeft((s) => Math.max(0, s - 1)), 1000);
    return () => clearInterval(id);
  }, []);

  useEffect(() => {
    if (secondsLeft === 0 && !resolvedRef.current) {
      resolvedRef.current = true;
      clearTargetRequest();
    }
  }, [secondsLeft, clearTargetRequest]);

  return (
    <div className="flex min-h-dvh flex-col gap-4 bg-ink px-6 py-8" data-testid="target-screen">
      <header className="flex items-center justify-between">
        <h2 className="text-xl font-semibold text-parchment" data-testid="target-title">
          {copy.title}
        </h2>
        <RoleIndicator />
      </header>

      {copy.hint && (
        <p className="text-center text-sm text-parchment/70" data-testid="target-hint">
          {copy.hint}
        </p>
      )}
      <p className="text-center text-sm text-parchment/70" data-testid="target-countdown">
        {secondsLeft}s
      </p>

      <PlayerTargetList
        targets={options.map((o) => o.name)}
        selected={selected}
        onSelect={setSelected}
      />

      <button
        type="button"
        disabled={selected === null}
        onClick={submit}
        data-testid="target-confirm"
        className="sticky bottom-0 mt-auto rounded-md bg-candle px-4 py-3 text-lg font-semibold text-ink disabled:opacity-40"
      >
        Confirm
      </button>
    </div>
  );
}
