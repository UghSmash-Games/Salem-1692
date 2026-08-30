/**
 * CharacterCard — the player's own Town Hall character: name, ability, and a short biography.
 *
 * Town Hall identity is PUBLIC (dealt face-up, read aloud at setup), so this is convenience, not
 * privileged information — it saves a player squinting at the host screen to remember what their
 * own card does.
 *
 * ⚠️ CACHES THE LAST NON-EMPTY NAME. The host nulls `townHall` when a player is eliminated
 * (Player.OnElimination clears the card), exactly as it does for the host display, which caches for
 * the same reason. Without this an eliminated player's card would blank out on the spectator screen
 * — losing information they are still entitled to, about a game they are still watching.
 *
 * Renders nothing when the player has no character (fewer than 8 players deals no Town Hall cards to
 * everyone, and nothing is dealt before setup completes).
 */

import { useEffect, useState } from 'react';
import { useMe } from '../store/selectors';
import { findCharacter } from '../data/townHallCharacters';

export function CharacterCard({ compact = false }: { compact?: boolean }) {
  const me = useMe();
  const [name, setName] = useState<string | null>(null);

  // Only ever move to a non-empty value; see the elimination note above.
  useEffect(() => {
    if (me?.townHall) setName(me.townHall);
  }, [me?.townHall]);

  const character = findCharacter(name);
  if (!name || !character) return null;

  return (
    <section
      className="rounded-md border border-parchment/25 bg-ink/60 px-4 py-3"
      data-testid="character-card"
    >
      <h3 className="text-xs uppercase tracking-wider text-parchment/50">Your character</h3>
      <p className="text-lg font-semibold text-candle" data-testid="character-name">
        {name}
      </p>
      <p className="mt-1 text-sm leading-snug text-parchment/85" data-testid="character-ability">
        {character.ability}
      </p>
      {!compact && (
        <p className="mt-2 text-xs italic leading-snug text-parchment/55" data-testid="character-bio">
          {character.bio}
        </p>
      )}
    </section>
  );
}
