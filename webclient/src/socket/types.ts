/**
 * TypeScript payload contracts for every Socket.io message the phone client
 * sends or receives. Mirrors docs/protocol.md.
 *
 * NOTE on the `acting` flag (SecretPhasePrompt): it is part of the payload so
 * the client can hold it, but it MUST NEVER influence rendering or timing.
 * See src/screens/SecretPhaseScreen.tsx and CLAUDE.md.
 */

// ─── Roles ────────────────────────────────────────────────────────

export type PlayerRole = 'witch' | 'townsperson' | 'constable';

// ─── Secret phase variants ────────────────────────────────────────

export type SecretPhaseType = 'black_cat' | 'night_vote' | 'constable_save' | 'confess';

// ─── Server → Client payloads ─────────────────────────────────────

export interface JoinedPayload {
  playerId: string;
  roomCode: string;
}

/** A single player's public-facing entry on the board. */
export interface PublicPlayer {
  playerId: string;
  displayName: string;
  accusations: number;
  /** Reveal threshold (base 7 / 8 George → ×2 Piety; NOT Danforth-adjusted). For an accusation-progress
   *  meter (count vs limit). Optional for contract parity with the host display. */
  accusationLimit?: number;
  eliminated: boolean;
  /** Public BLUE/persistent cards in front of them (names only), e.g. ["Asylum"], plus "Black Cat".
   *  Red accusation cards are in `accusationCards` — one list internally, split on the wire so the
   *  UI never needs card-colour (a rules concept) to render them. */
  statusCards?: string[];
  /** Public RED accusation cards in front of them, e.g. ["Evidence", "Accusation"]. Public by the
   *  card rules (played face-up); previously carried inside `statusCards`, not a new disclosure. */
  accusationCards?: string[];
  /** PRINTED Town Hall character display name, or null/empty when none. Town Hall identity is PUBLIC
   *  (dealt face-up, read aloud at setup). Card identity ONLY — never charges or ability eligibility.
   *  Not Martha Corey's copied name. Goes empty on elimination; cache the last non-empty value. */
  townHall?: string | null;
  /** COUNT of tryal cards held (revealed + unrevealed). No identities. Tabletop-visible.
   *  Unrevealed count is (tryalTotal - revealedTryals.length) — never sent as its own field. */
  tryalTotal?: number;
  /** Labels of ALREADY-REVEALED tryals only, in canonical sorted order — deliberately POSITION-FREE.
   *  ⚠ Never widen to a positional shape (placeholders for face-down slots, {label,index}, or a
   *  public mirror of the private TryalCardView's faceUp flag): tryals are APPENDED on receipt, so
   *  slot position would let a Conspiracy giver pin a card they know onto a specific face-down slot. */
  revealedTryals?: string[];
  /** COUNT of cards held. Hand SIZE is openly countable at a physical table; hand CONTENTS are
   *  private and arrive only in PrivateStatePayload.hand.
   *  ⚠ Must stay a number — widening this to string[] would be a total leak. */
  handCount?: number;
}

export interface GameStateUpdatePayload {
  phase?: string;
  whoseTurn?: string | null;
  players: PublicPlayer[];
  /** Public deck/discard counts (Unity-defined, optional). */
  deckCount?: number;
  discardCount?: number;
  /** Name of the TOP discard card, or null when the pile is empty. Public — the discard pile is
   *  face-up at a table. TOP CARD ONLY: the ordered pile would leak play history and expose
   *  Samuel Parris' discard-draw pool. */
  topDiscard?: string | null;
}

/** The CLOSED vocabulary of loggable events. Mirrors Unity's `GameEventKind`.
 *  ⛔ Never extend this with a secret-phase kind (night votes, constable saves, witch identities,
 *  black-cat placement, or a confession before it resolves). The closed set IS the privacy
 *  mechanism: the log cannot describe a secret action because no kind can express one. */
export type GameEventKind =
  | 'game_started'
  | 'phase_changed'
  | 'card_played'
  | 'tryal_revealed'
  | 'player_eliminated'
  | 'double_witch_revealed'
  | 'confession_revealed'
  | 'gavel_placed'
  | 'game_over';

/** One entry in the public "What Has Passed" log. Broadcast to all players + mirrors.
 *  PUBLIC DATA ONLY — ids and short enumerable labels, never prose. The RENDERER turns this into a
 *  sentence, which is why no free-text field exists here and none may be added. */
export interface GameEventPayload {
  kind: GameEventKind;
  /** Public id of whoever acted, or null (e.g. phase_changed has no actor). */
  actorId?: string | null;
  /** Public id of whoever the event happened to, or null. */
  targetId?: string | null;
  /** Public card label, or null. Same visibility class as statusCards. */
  cardName?: string | null;
  /** SHORT enumerable detail whose meaning depends on `kind` — a tryal label, a phase name, an
   *  "n/limit" counter, a winner. Never a sentence. */
  value?: string | null;
  /** Epoch milliseconds stamped by the host. Format in the CLIENT's local time — a preformatted
   *  "19:04" would bake in the host's timezone and break a mirror in another region. */
  atMs: number;
}

/** A tryal card as shown to its owner. faceUp cards have been revealed publicly. */
export interface TryalCardView {
  /** Display label, e.g. "Witch", "Not a Witch", "Constable". */
  label: string;
  faceUp: boolean;
}

/** One other witch's live tentative pick during a witch round. */
export interface WitchVote {
  witch: string;
  /** tentative target name; "" = not yet picked. */
  target: string;
}

export interface PrivateStatePayload {
  playerId: string;
  tryals: TryalCardView[];
  hand: string[];
  role: PlayerRole;
  /** Independent role truths — a player can be BOTH (evil constable). */
  isWitch?: boolean;
  isConstable?: boolean;
  /** Names of the OTHER witches — present (for a witch) only after the dawn reveal. */
  fellowWitches?: string[];
  /** Live tentative picks of the OTHER witches — present (for a witch) only during a witch round. */
  witchVotes?: WitchVote[];
}

/** Delivered to each player individually; server strips the playerId. */
export interface SecretPhasePromptPayload {
  prompt: SecretPhaseType;
  targets: string[];
  acting: boolean;
  /** Confess window only: true ONLY on a William Phipps holder's own prompt (with a charge) — shows
   *  the "confess without revealing" button. Per-player like `acting` (Town Hall identity is public,
   *  so this is host-gated, not universal); never broadcast. */
  canFakeConfess?: boolean;
}

export interface ActionRequestPayload {
  playerId: string;
  /** Available top-level actions, e.g. ["draw", "play", "confess"]. */
  actions: string[];
  /** Card NAMES in hand that can't legally be played right now (host-computed; e.g.
   *  Robbery/Scapegoat with fewer than 3 players alive). Rendered greyed-out. */
  unplayableCards?: string[];
}

/** Pick another PLAYER — the sub-target of a two-target card (server → this one player only).
 *  `targets` are eligible PUBLIC player ids; resolve them to names via the public board. */
export interface TargetRequestPayload {
  /** Machine code, e.g. "robbery_recipient" | "scapegoat_recipient". */
  prompt: string;
  /** Eligible public player ids (host-computed: never self, never the victim, never eliminated). */
  targets: string[];
  /** The window in seconds — shown as a countdown. */
  seconds: number;
}

/** Pick WHICH face-down tryal turns on another player (server → this one player only) — the
 *  accuser at the threshold, the Curse player on a piety-loss reveal, or the conspiracy drawer.
 *
 *  🔴 DELIBERATELY CARRIES NO CARD DATA. Only a COUNT of face-down tryals: the chooser is picking
 *  blind among identical backs, exactly as at a physical table. The answer is an ORDINAL into that
 *  face-down subset; only the host knows which real tryal slot it maps to.
 *  ⛔ Never add labels here, and never expect real slot indices — tryals are appended on receipt,
 *  so a real index would let a Conspiracy giver pin a card they passed to an exact slot. */
export interface TryalPickRequestPayload {
  /** Whose tryals are being flipped — a PUBLIC player id; resolve to a name via the public board. */
  targetPlayerId: string;
  /** How many face-down tryals they may choose between (render this many identical backs). */
  count: number;
  /** The window in seconds — shown as a countdown. */
  seconds: number;
  /** Machine code: "accusation_reveal" | "piety_loss_reveal" | "conspiracy_reveal". */
  reason: string;
}

/** Tituba's deck-rearrange prompt (server → this one player only). */
export interface DeckRearrangeRequestPayload {
  /** Full deck labels, top→bottom (the cards she may reorder). */
  cards: string[];
  /** The rearrange window in seconds (rules value, 60) — shown as a countdown. */
  seconds: number;
}

/** A card-pick prompt (server → this one player only): John Proctor draft OR Samuel Parris discard-pick. */
export interface CardPickRequestPayload {
  /** The pool of card labels to pick from (draft hand, or filtered discard pile). */
  cards: string[];
  /** 1-based index of this pick, for display ("pick N of up to M"). */
  pickNumber: number;
  /** Max picks allowed (John: 3; Parris: 2). */
  totalPicks: number;
  /** The pick window in seconds — shown as a countdown. */
  seconds: number;
  /** When true, the picker may decline / stop early (an "up to N" pick, e.g. Parris) — a Done button
   *  that submits index -1 is shown. False/absent for a mandatory pick (John's draft). */
  allowDone?: boolean;
  /** Machine code for WHAT this pick is, so the phone can phrase it correctly:
   *  "proctor_draft" / "parris_discard" (taking a card) vs "curse_discard" (discarding an opponent's
   *  blue card). Absent → treated as a generic take. */
  reason?: string;
}

/** A yes/no confirmation for this player's OWN optional ("may") ability choice
 *  (server → this one player only). NOT a masked secret phase — Town Hall identity is public,
 *  so a holder-only prompt leaks nothing; it's routed to one socket as private decision UI. */
export interface ConfirmRequestPayload {
  /** Machine code for the decision, e.g. "abigail_discard" — mapped to copy on the client. */
  prompt: string;
  /** Context card labels (e.g. her red cards in front of her). */
  items: string[];
  /** Numeric context (e.g. accusation total — differs from items.length: Evidence=3, Witness=7). */
  count: number;
  /** The window in seconds — shown as a countdown. */
  seconds: number;
}

export interface PhaseResolvePayload {
  /** UTC epoch ms at which all screens should trigger the reveal. */
  revealAt: number;
}

/** A public one-shot card-show to the whole table (e.g. Giles Corey's two red cards).
 *  PUBLIC — card names only, never tryals/role/hand. Same visibility class as statusCards. */
export interface PublicRevealPayload {
  /** The actor's public id. */
  playerId: string;
  /** Shown card labels (names only), e.g. ["Evidence", "Witness"]. */
  cards: string[];
  /** Machine code for the trigger, e.g. "giles_corey" — used to phrase the toast. */
  reason: string;
}

export interface EliminationResultPayload {
  playerId: string;
  eliminated: boolean;
  /** Empty string / null when no one saved the target. */
  savedBy: string | null;
}

export interface GameOverPayload {
  winner: 'witches' | 'townspeople';
  /** All players' tryals, revealed at game end. */
  tryals: Record<string, TryalCardView[]>;
}

export interface ErrorMsgPayload {
  message: string;
}

// ─── Client → Server payloads ─────────────────────────────────────

export interface JoinRoomPayload {
  code: string;
  displayName: string;
}

export interface JoinMirrorPayload {
  code: string;
}

export interface PlayerActionPayload {
  card: string;
  targetPlayerId: string;
}

export interface SecretPhaseSubmitPayload {
  selection: string;
  /** false = tentative pick (may change); true = final. */
  confirmed: boolean;
}

export interface ConfessPayload {
  tryalIndex: number;
}

/** Tituba's reordered deck (this player → host). */
export interface DeckRearrangeSubmitPayload {
  /** A permutation of the original deck indices (top→bottom). */
  order: number[];
  /** false = tentative in-progress order; true = final (Confirm or countdown expiry). */
  confirmed: boolean;
}

/** A John Proctor / Martha drafter's single card pick (this player → host). */
export interface CardPickSubmitPayload {
  /** The chosen card's index into the pool from CardPickRequestPayload. */
  index: number;
}

/** The answer to a ConfirmRequestPayload (this player → host). Single-stage. */
export interface ConfirmSubmitPayload {
  confirmed: boolean;
}

/** The chosen sub-target (this player → host). Single-stage; the host re-validates the id. */
export interface TargetSubmitPayload {
  targetPlayerId: string;
}

/** Answer to a TryalPickRequestPayload. `ordinal` indexes the FACE-DOWN subset the host described
 *  (0..count-1) — NOT a real tryal slot. The host owns that mapping and re-validates the range. */
export interface TryalPickSubmitPayload {
  ordinal: number;
}
