/**
 * Secret phase masking screen — the heart of the identity-masking system.
 *
 * ┌─────────────────────────────────────────────────────────────────────┐
 * │ CRITICAL INVARIANT (see CLAUDE.md):                                   │
 * │ This screen and everything it renders depend ONLY on `prompt.type`    │
 * │ and `prompt.targets`. It must NEVER read `prompt.acting`. Acting and  │
 * │ non-acting players see a pixel-identical screen with identical timing.│
 * │ Every player submits; the server silently discards the non-acting     │
 * │ submissions. The phone shows the same confirmation either way.        │
 * └─────────────────────────────────────────────────────────────────────┘
 */

import { useState } from 'react';
import { useGameStore } from '../store/gameStore';
import { sendSecretPhaseSubmit } from '../socket/socketClient';
import { PlayerTargetList } from '../components/PlayerTargetList';
import { WaitingForOthers } from '../components/WaitingForOthers';
import type { SecretPhaseType } from '../socket/types';

const HEADERS: Record<SecretPhaseType, string> = {
  black_cat: 'Place the black cat',
  night_vote: 'Choose a player',
  constable_save: 'Protect a player',
};

export function SecretPhaseScreen() {
  const prompt = useGameStore((s) => s.prompt);
  const markSubmitted = useGameStore((s) => s.markPromptSubmitted);

  const [selected, setSelected] = useState<string | null>(null);

  if (!prompt) return null;

  // Once submitted, every player — acting or not — sees this identical state.
  if (prompt.submitted) {
    return (
      <div className="flex min-h-dvh flex-col items-center justify-center bg-ink px-6">
        <WaitingForOthers />
      </div>
    );
  }

  const handleSelect = (target: string) => {
    setSelected(target);
    // Submit immediately on tap; the confirmation is identical for everyone.
    sendSecretPhaseSubmit({ selection: target });
    markSubmitted();
  };

  return (
    <div className="flex min-h-dvh flex-col gap-6 bg-ink px-6 py-10">
      <h2 className="text-center text-2xl font-semibold text-parchment">
        {HEADERS[prompt.type]}
      </h2>
      <PlayerTargetList
        targets={prompt.targets}
        selected={selected}
        onSelect={handleSelect}
      />
    </div>
  );
}
