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
import { RoleIndicator } from '../components/RoleIndicator';
import type { SecretPhaseType } from '../socket/types';

const HEADERS: Record<SecretPhaseType, string> = {
  black_cat: 'Place the black cat',
  night_vote: 'Choose a player',
  constable_save: 'Protect a player',
  confess: 'Confess?',
};

// Selection sentinel for "don't confess" (matches the host's ConfessSkip).
const CONFESS_SKIP = 'skip';

export function SecretPhaseScreen() {
  const prompt = useGameStore((s) => s.prompt);
  const markSubmitted = useGameStore((s) => s.markPromptSubmitted);
  // Private (constable-only) info used to block an illegal self-protect on this
  // player's own device. The shared target list stays full + identical for all.
  const isConstable = useGameStore((s) => s.privateState.isConstable);
  const myName = useGameStore((s) => s.session.displayName);
  // Own tryals (private) — used only for the confess window, where each player
  // confesses one of their OWN face-down cards. Same class of private data as the
  // witch tally; differs per phone legitimately and is never broadcast.
  const myTryals = useGameStore((s) => s.privateState.tryals);

  const [selected, setSelected] = useState<string | null>(null);

  if (!prompt) return null;

  // Post-confirm: identical waiting state for everyone (the banner is witch-only
  // private data, consistent with every other witch-facing screen).
  if (prompt.submitted) {
    return (
      <div className="flex min-h-dvh flex-col items-center justify-center gap-6 bg-ink px-6">
        {/* Private role overlay — own device only, never part of the masked region. */}
        <RoleIndicator />
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

  const isConfess = prompt.type === 'confess';

  // Confess options: each of the player's OWN face-down tryals (selection = its index)
  // plus a "don't confess" choice (selection = CONFESS_SKIP). Confessing is a public
  // act, so there is no role to hide here — every phone shows this same structure; only
  // the private card labels differ (same class as tryals).
  const faceDownTryals = myTryals
    .map((card, index) => ({ card, index }))
    .filter(({ card }) => !card.faceUp);

  return (
    <div className="flex min-h-dvh flex-col gap-6 bg-ink px-6 py-10">
      {/* Private role overlay (own device only) — sits ABOVE the masked prompt region
          so it never alters the prompt header / controls structure that must stay
          identical for every player. Same private class as FellowWitchBanner. */}
      <header className="flex items-center justify-end">
        <RoleIndicator />
      </header>
      <FellowWitchBanner />
      <h2 className="text-center text-2xl font-semibold text-parchment">
        {HEADERS[prompt.type]}
      </h2>

      {isConfess ? (
        <ul className="flex flex-col gap-2" data-testid="confess-options">
          {faceDownTryals.map(({ card, index }) => {
            const value = String(index);
            return (
              <li key={index}>
                <button
                  type="button"
                  onClick={() => handleTentative(value)}
                  className={[
                    'w-full rounded-md border px-4 py-3 text-center transition-colors',
                    selected === value
                      ? 'border-ember bg-ember/30 text-parchment'
                      : 'border-parchment/40 bg-ink/40 text-parchment hover:border-ember/60',
                  ].join(' ')}
                >
                  {card.label}
                </button>
              </li>
            );
          })}
          <li>
            <button
              type="button"
              onClick={() => handleTentative(CONFESS_SKIP)}
              className={[
                'w-full rounded-md border px-4 py-3 text-center transition-colors',
                selected === CONFESS_SKIP
                  ? 'border-ember bg-ember/30 text-parchment'
                  : 'border-parchment/40 bg-ink/40 text-parchment hover:border-ember/60',
              ].join(' ')}
            >
              Don&apos;t confess
            </button>
          </li>
        </ul>
      ) : (
        <>
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
        </>
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
