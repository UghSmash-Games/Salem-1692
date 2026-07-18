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
  eliminated: boolean;
  /** Public blue cards in front of them (names only), e.g. ["Asylum"]. */
  statusCards?: string[];
}

export interface GameStateUpdatePayload {
  phase?: string;
  whoseTurn?: string | null;
  players: PublicPlayer[];
  /** Public deck/discard counts (Unity-defined, optional). */
  deckCount?: number;
  discardCount?: number;
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
