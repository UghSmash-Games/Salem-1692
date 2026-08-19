/**
 * Rules text for cards that sit in front of a player — the browser copy of
 * HostCardSpriteRegistry's `description` field, used by the mirror's IN EFFECT panel.
 *
 * STATIC COPY, NOT WIRE DATA. The host keeps this in the registry asset for the same reason: it is
 * reference text, not game state, and the protocol's standing rule is that the wire carries data
 * while renderers carry prose. Nothing here is broadcast.
 *
 * ⚠️ KEEP IN STEP WITH THE REGISTRY. The blue-card entries below are hand-authored in
 * `HostCardSpriteRegistry.asset` (docs/phase-7-editor-steps.md Stage 2, step 5) and are reproduced
 * verbatim. Town Hall abilities are NOT duplicated here — they already live in
 * `townHallCharacters.ts`, which is the single browser-side source for those.
 *
 * 🐛 The registry's John Proctor entry was stale (it described the pre-correction "take all blue
 * cards and their whole hand" rule) because the populator auto-filled it from GetRulesText BEFORE
 * that method was fixed, and the populator PRESERVES any non-empty description on re-run. Both the
 * asset and townHallCharacters.ts now carry the corrected wording.
 */

import { TOWN_HALL_CHARACTERS } from './townHallCharacters';

/** Blue / persistent cards, verbatim from the registry's hand-authored entries. */
const BLUE_CARD_DESCRIPTIONS: Record<string, string> = {
  Asylum: 'Recipient cannot be eliminated during the night',
  Piety: 'Doubles the accusations needed to reveal a tryal',
  Matchmaker: 'If one linked player is eliminated, both are',
  Stocks: "Skips this player's next turn",
  'Black Cat': 'Its holder reveals a tryal when conspiracy is drawn',
};

/**
 * Rules text for a card in front of a player, or null when we have none.
 *
 * Falls through to the Town Hall abilities so a character card sitting in `statusCards` resolves
 * without a second lookup at the call site — matching the host, where one registry serves both.
 */
export function cardDescription(label: string | null | undefined): string | null {
  if (!label) return null;
  if (BLUE_CARD_DESCRIPTIONS[label]) return BLUE_CARD_DESCRIPTIONS[label];
  return TOWN_HALL_CHARACTERS[label]?.ability ?? null;
}

/** Accent for the IN EFFECT row's left bar. Asylum is the one card with its own colour. */
export function cardAccentClass(label: string | null | undefined): string {
  return label === 'Asylum' ? 'bg-host-asylum' : 'bg-host-effect';
}
