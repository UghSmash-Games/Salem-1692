/**
 * Town Hall character reference for the phone.
 *
 * ⚠️ KEYED ON THE EXACT WIRE VALUE. `PublicPlayer.townHall` carries the card asset's `Name`, so the
 * keys below must match those strings character-for-character. Two differ from the C# enum spelling
 * and are easy to get wrong: the enum has `WillGrigs` and `WilliamsPhipps`, but the cards are named
 * **"Will Griggs"** and **"William Phipps"**.
 *
 * ⚠️ `ability` IS A PORT OF Unity's TownHallCard.GetRulesText() — the same static-copy arrangement
 * the host uses (HostCardSpriteRegistry.description is populated from that method). It is duplicated
 * rather than sent over the wire because it is reference copy, not game state, and the protocol's
 * standing rule is that the wire carries data while renderers carry prose.
 * **KEEP THE TWO IN STEP.** If an ability changes in C#, change it here. This is the same drift risk
 * as gameEventCopy.ts ↔ HostEventLog.Describe.
 *
 * Town Hall identity is PUBLIC (cards are dealt face-up and read aloud at setup), so none of this is
 * privileged information — it is shown to a player about their own character purely for convenience.
 *
 * 📝 `bio` is DRAFT historical copy written for this build, not sourced from the rulebook. These were
 * real people in the 1692 trials. Review and reword freely — nothing depends on the exact wording.
 */

export interface TownHallCharacter {
  ability: string;
  bio: string;
}

export const TOWN_HALL_CHARACTERS: Record<string, TownHallCharacter> = {
  'Abigail Williams': {
    ability:
      'If you place the final accusation on a tryal, you may discard all accusations in front of you.',
    bio: 'Niece of the village minister, and among the first to accuse.',
  },
  'Anne Putnam': {
    ability:
      'At the end of your turn, draw two cards for each tryal card you revealed during your turn.',
    bio: 'A young accuser who, years later, publicly apologised for her part.',
  },
  'Cotton Mather': {
    ability: 'Evidence cards played against you are worth only 1 accusation.',
    bio: 'A Boston minister whose writings on witchcraft shaped the trials.',
  },
  'George Burroughs': {
    ability: '8 total accusations must be played against you to reveal a Tryal.',
    bio: 'A former village minister; he recited the Lord’s Prayer at the gallows.',
  },
  'Giles Corey': {
    ability: 'If you draw 2 red cards on your turn, show the other players and draw a 3rd card.',
    bio: 'Refused to enter a plea, and was pressed to death with stones.',
  },
  'John Proctor': {
    ability:
      'When a player is eliminated, choose up to three cards from their hand to take. The rest are discarded.',
    bio: 'A farmer and tavern keeper who spoke openly against the trials.',
  },
  'Martha Corey': {
    ability: 'You have the same ability as the first living player to your right.',
    bio: 'A covenanted church member; her accusation shocked the village.',
  },
  'Mary Warren': {
    ability: 'You are immune to the ill effects of Matchmaker and Black Cat.',
    bio: 'Servant to the Proctors, and in turn both accuser and accused.',
  },
  'Rebecca Nurse': {
    ability: 'Each time a Tryal is revealed on another player (from accusations), draw 1 card.',
    bio: 'An elderly and widely respected churchwoman; her conviction caused an outcry.',
  },
  'Samuel Parris': {
    ability:
      'Twice per game, draw up to 2 cards from the discard pile instead of the deck. No Black cards.',
    bio: 'The village minister, in whose household the affliction began.',
  },
  'Sarah Good': {
    ability: 'Robbery and Arson cards have no effect on you and are discarded.',
    bio: 'A beggar with no home of her own; among the first three accused.',
  },
  'Thomas Danforth': {
    ability: 'When you accuse, the threshold is reduced by 1 (6th accusation triggers reveal).',
    bio: 'Deputy Governor of the colony, who presided over early examinations.',
  },
  Tituba: {
    ability: 'Once per game, on your turn before drawing, rearrange the deck for 60 seconds.',
    bio: 'An enslaved woman in the minister’s household, and the first to confess.',
  },
  'Will Griggs': {
    ability:
      'You may choose to use alibi cards as if they were witness cards, worth seven total accusations.',
    bio: 'The village doctor, who declared the afflicted girls bewitched.',
  },
  'William Phipps': {
    ability: 'Once per game, you may confess without revealing one of your Tryal cards.',
    bio: 'Governor of the colony; he created the court, and later dissolved it.',
  },
};

/** Look up a character by the wire name. Returns null for an unknown or absent card. */
export function findCharacter(name: string | null | undefined): TownHallCharacter | null {
  if (!name) return null;
  return TOWN_HALL_CHARACTERS[name] ?? null;
}
