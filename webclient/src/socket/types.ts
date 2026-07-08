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
}

export interface ActionRequestPayload {
  playerId: string;
  /** Available top-level actions, e.g. ["draw", "play", "confess"]. */
  actions: string[];
}

/** Tituba's deck-rearrange prompt (server → this one player only). */
export interface DeckRearrangeRequestPayload {
  /** Full deck labels, top→bottom (the cards she may reorder). */
  cards: string[];
  /** The rearrange window in seconds (rules value, 60) — shown as a countdown. */
  seconds: number;
}

/** John Proctor / Martha card-draft pick prompt (server → this one drafter only). */
export interface CardPickRequestPayload {
  /** The draft pool — an eliminated player's hand labels. */
  cards: string[];
  /** 1-based index of this pick, for display ("pick N of up to 3"). */
  pickNumber: number;
  /** Max picks this drafter may take (3). */
  totalPicks: number;
  /** The pick window in seconds — shown as a countdown. */
  seconds: number;
}

export interface PhaseResolvePayload {
  /** UTC epoch ms at which all screens should trigger the reveal. */
  revealAt: number;
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
