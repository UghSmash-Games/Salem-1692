/**
 * Secret phase masking screen — the heart of the identity-masking system.
 *
 * ┌─────────────────────────────────────────────────────────────────────┐
 * │ CRITICAL INVARIANT (see CLAUDE.md "Masking definition"):             │
 * │ The prompt header, target controls, the two-stage tentative→Confirm  │
 * │ flow, and timing are IDENTICAL for every player. This screen never    │
 * │ reads `prompt.acting`. Witch-only coordination (the fellow-witch      │
 * │ banner and the live ally tally) is PRIVATE data sourced from          │
 * │ privateState (role/fellowWitches/witchVotes) — the same class as      │
 * │ tryal cards — never from the prompt, never broadcast. The control     │
 * │ structure must stay identical regardless of role.                     │
 * └─────────────────────────────────────────────────────────────────────┘
 *
 * Two-stage submit: tapping a target sends a TENTATIVE pick (confirmed:false,
 * re-sendable); tapping Confirm sends the FINAL pick (confirmed:true). For
 * acting witches, tentative picks are relayed live to fellow witches (private).
 */

import { useState } from 'react';
import { useGameStore } from '../store/gameStore';
import { sendSecretPhaseSubmit } from '../socket/socketClient';
import { PlayerTargetList } from '../components/PlayerTargetList';
import { WaitingForOthers } from '../components/WaitingForOthers';
import { FellowWitchBanner } from '../components/FellowWitchBanner';
import { AllyTallyPanel } from '../components/AllyTallyPanel';
import type { SecretPhaseType } from '../socket/types';

const HEADERS: Record<SecretPhaseType, string> = {
  black_cat: 'Place the black cat',
  night_vote: 'Choose a player',
  constable_save: 'Protect a player',
};

export function SecretPhaseScreen() {
  const prompt = useGameStore((s) => s.prompt);
  const markSubmitted = useGameStore((s) => s.markPromptSubmitted);
  // Private (constable-only) info used to block an illegal self-protect on this
  // player's own device. The shared target list stays full + identical for all.
  const isConstable = useGameStore((s) => s.privateState.isConstable);
  const myName = useGameStore((s) => s.session.displayName);

  const [selected, setSelected] = useState<string | null>(null);

  if (!prompt) return null;

  // Post-confirm: identical waiting state for everyone (the banner is witch-only
  // private data, consistent with every other witch-facing screen).
  if (prompt.submitted) {
    return (
      <div className="flex min-h-dvh flex-col items-center justify-center gap-6 bg-ink px-6">
        <FellowWitchBanner />
        <WaitingForOthers />
      </div>
    );
  }

  // Stage 1 — tentative: re-sendable as the player changes their mind.
  const handleTentative = (target: string) => {
    setSelected(target);
    sendSecretPhaseSubmit({ selection: target, confirmed: false });
  };

  // The constable may not protect themselves (rulebook p7). Detected with private
  // role info on this device only; the shared prompt/target list is unchanged.
  const selfProtectViolation =
    prompt.type === 'constable_save' &&
    isConstable &&
    selected !== null &&
    selected === myName;

  // Stage 2 — confirm: finalizes. Identical confirmation flow for everyone.
  const handleConfirm = () => {
    if (selected === null || selfProtectViolation) return;
    sendSecretPhaseSubmit({ selection: selected, confirmed: true });
    markSubmitted();
  };

  return (
    <div className="flex min-h-dvh flex-col gap-6 bg-ink px-6 py-10">
      <FellowWitchBanner />
      <h2 className="text-center text-2xl font-semibold text-parchment">
        {HEADERS[prompt.type]}
      </h2>
      <PlayerTargetList
        targets={prompt.targets}
        selected={selected}
        onSelect={handleTentative}
      />
      <AllyTallyPanel ownPick={selected} />
      {selfProtectViolation && (
        <p className="text-center text-sm text-ember" role="alert">
          You can&apos;t protect yourself — choose another player.
        </p>
      )}
      <button
        type="button"
        disabled={selected === null || selfProtectViolation}
        onClick={handleConfirm}
        className="rounded-md bg-candle px-4 py-3 text-lg font-semibold text-ink transition-opacity disabled:opacity-40"
      >
        Confirm
      </button>
    </div>
  );
}
