/**
 * Card art for the mirror — the browser counterpart of Unity's HostCardSpriteRegistry.
 *
 * 🔴 NORMALIZATION MUST MATCH THE HOST EXACTLY: trim, lowercase, strip ALL whitespace. The host
 * needs this because the WIRE label and the ASSET name disagree in case and spacing — "Not a Witch"
 * comes from LabelFor, while the asset is "NOT A Witch". Getting it wrong there once blanked every
 * tryal on the host screen; getting it wrong here would blank them on the mirror instead, which is
 * exactly the parity failure this work exists to close.
 *
 * The files are the SAME .jpg assets Unity uses, copied to public/cards/ with normalized names — no
 * export step, and no second source of truth for the artwork itself.
 *
 * ⚠️ The key sets below are generated from what is actually on disk. An unknown label returns null
 * so the caller renders nothing, rather than emitting a 404 and a broken-image icon on a TV.
 *
 * ⛔ THE FACE-DOWN BACK IS ONE SHARED IMAGE (TRYAL_BACK). Every unrevealed tryal renders that same
 * file, so no code path can select art from a card whose identity the mirror was never sent. Keep it
 * that way — do not add a "face-down variant" keyed on anything.
 */

/** Same rule as HostCardSpriteRegistry.Normalize. */
export function normalizeCardLabel(label: string): string {
  return label.replace(/\s+/g, '').toLowerCase();
}

const CARD_KEYS = new Set<string>([
  'accusation',
  'alibi',
  'arson',
  'asylum',
  'back',
  'backing',
  'blackcat',
  'bless',
  'conspiracy',
  'constable',
  'curse',
  'evidence',
  'matchmaker',
  'night',
  'notawitch',
  'piety',
  'robbery',
  'scapegoat',
  'shilling',
  'stocks',
  'witch',
  'witness',
]);

const TOWN_HALL_KEYS = new Set<string>([
  'abigailwilliams',
  'anneputnam',
  'cottonmather',
  'georgeburroughs',
  'gilescorey',
  'johnproctor',
  'marthacorey',
  'marywarren',
  'rebeccanurse',
  'samuelparris',
  'sarahgood',
  'thomasdanforth',
  'tituba',
  'townhallbacking',
  'willgriggs',
  'williamphipps',
]);

/** The one shared face-down TRYAL image. */
export const TRYAL_BACK = '/cards/back.jpg';

/** The playing-card back, for the draw/discard stacks. Distinct art from the tryal back — the two
 *  decks are physically different cards, and the host's HostDeckView uses the playing-card back. */
export const DECK_BACK = '/cards/backing.jpg';

/** Art for a card or tryal label ("Evidence", "Not a Witch"), or null if we have none. */
export function cardArt(label: string | null | undefined): string | null {
  if (!label) return null;
  const key = normalizeCardLabel(label);
  return CARD_KEYS.has(key) ? `/cards/${key}.jpg` : null;
}

/** Portrait for a Town Hall character by its PRINTED name ("Will Griggs"), or null. */
export function townHallArt(name: string | null | undefined): string | null {
  if (!name) return null;
  const key = normalizeCardLabel(name);
  return TOWN_HALL_KEYS.has(key) ? `/cards/th-${key}.jpg` : null;
}
