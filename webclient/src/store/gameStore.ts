/**
 * Zustand store — the server-driven source of truth for the phone client.
 *
 * Every server → client event is funneled into one of these slices by the
 * useGameSocket hook. Components subscribe to slices and render reactively.
 *
 * IMPORTANT: `prompt.acting` is stored here but MUST NOT drive any rendering
 * or timing decision. It exists only so the payload round-trips faithfully.
 */

import { create } from 'zustand';
import type {
  PublicPlayer,
  TryalCardView,
  PlayerRole,
  WitchVote,
  SecretPhaseType,
  GameStateUpdatePayload,
  PrivateStatePayload,
  SecretPhasePromptPayload,
  ActionRequestPayload,
  DeckRearrangeRequestPayload,
  CardPickRequestPayload,
  ConfirmRequestPayload,
  TargetRequestPayload,
  PhaseResolvePayload,
  PublicRevealPayload,
  EliminationResultPayload,
  GameOverPayload,
} from '../socket/types';

// ─── Slice shapes ─────────────────────────────────────────────────

export interface SessionSlice {
  connected: boolean;
  roomCode: string | null;
  playerId: string | null;
  displayName: string | null;
  /** Set when a join attempt fails (e.g. "Room not found"). */
  joinError: string | null;
}

export interface PrivateSlice {
  tryals: TryalCardView[];
  hand: string[];
  role: PlayerRole | null;
  /** Independent role truths — a player can be BOTH (evil constable). */
  isWitch: boolean;
  isConstable: boolean;
  /** Other witches' names — non-empty (for a witch) only after the dawn reveal. */
  fellowWitches: string[];
  /** Other witches' live tentative picks — non-empty (for a witch) only during a witch round. */
  witchVotes: WitchVote[];
}

export interface PublicBoardSlice {
  phase: string | null;
  whoseTurn: string | null;
  players: PublicPlayer[];
  deckCount: number | null;
  discardCount: number | null;
}

export interface PromptSlice {
  type: SecretPhaseType;
  targets: string[];
  /** Stored, never read by render/timing logic. */
  acting: boolean;
  /** Confess window only: this player is a William Phipps with a charge → show the "confess without
   *  revealing" button. Host-gated per-player (Town Hall identity is public). */
  canFakeConfess: boolean;
  submitted: boolean;
}

export interface ActionRequestSlice {
  actions: string[];
  /** Card names that can't be played right now (host-computed) — rendered greyed-out. */
  unplayableCards: string[];
}

export interface TargetRequestSlice {
  /** Machine code, e.g. "robbery_recipient". */
  prompt: string;
  /** Eligible public player ids. */
  targets: string[];
  /** Window in seconds — shown as a countdown. */
  seconds: number;
}

export interface DeckRearrangeSlice {
  /** Full deck labels, top→bottom (Tituba reorders these). */
  cards: string[];
  /** Rearrange window in seconds (rules value, 60) — shown as a countdown. */
  seconds: number;
}

export interface CardPickSlice {
  /** The pool of card labels to pick from (draft hand, or filtered discard pile). */
  cards: string[];
  /** 1-based index of this pick ("pick N of up to M"). */
  pickNumber: number;
  /** Max picks allowed (John: 3; Parris: 2). */
  totalPicks: number;
  /** Pick window in seconds — shown as a countdown. */
  seconds: number;
  /** When true, a "Done" button lets the picker stop early (submits index -1). */
  allowDone: boolean;
  /** Machine code for WHAT this pick is ("proctor_draft"/"parris_discard" = take; "curse_discard" =
   *  discard an opponent's blue card). Drives the screen copy; absent → generic take. */
  reason?: string;
}

export interface ConfirmSlice {
  /** Machine code for the decision, e.g. "abigail_discard". */
  prompt: string;
  /** Context card labels shown with the question. */
  items: string[];
  /** Numeric context (e.g. accusation total). */
  count: number;
  /** Window in seconds — shown as a countdown. */
  seconds: number;
}

export interface GameOverSlice {
  winner: 'witches' | 'townspeople';
  tryals: Record<string, TryalCardView[]>;
}

// ─── Store ────────────────────────────────────────────────────────

interface GameStore {
  session: SessionSlice;
  privateState: PrivateSlice;
  publicBoard: PublicBoardSlice;
  prompt: PromptSlice | null;
  actionRequest: ActionRequestSlice | null;
  deckRearrange: DeckRearrangeSlice | null;
  cardPick: CardPickSlice | null;
  confirm: ConfirmSlice | null;
  targetRequest: TargetRequestSlice | null;
  reveal: { revealAt: number } | null;
  /** The most recent elimination_result, for the synchronized reveal overlay. */
  lastElimination: EliminationResultPayload | null;
  /** The most recent public_reveal (e.g. Giles Corey showing two red cards), for a public toast. */
  lastPublicReveal: PublicRevealPayload | null;
  gameOver: GameOverSlice | null;

  // ── Connection / session ──
  setConnected: (connected: boolean) => void;
  beginJoin: (displayName: string) => void;
  onJoined: (playerId: string, roomCode: string) => void;
  onMirrorJoined: (roomCode: string) => void;
  setJoinError: (message: string) => void;
  reset: () => void;

  // ── Server event handlers ──
  applyGameStateUpdate: (data: GameStateUpdatePayload) => void;
  applyPrivateState: (data: PrivateStatePayload) => void;
  applySecretPhasePrompt: (data: SecretPhasePromptPayload) => void;
  applyActionRequest: (data: ActionRequestPayload) => void;
  applyDeckRearrangeRequest: (data: DeckRearrangeRequestPayload) => void;
  clearDeckRearrange: () => void;
  applyCardPickRequest: (data: CardPickRequestPayload) => void;
  clearCardPick: () => void;
  applyConfirmRequest: (data: ConfirmRequestPayload) => void;
  clearConfirm: () => void;
  applyTargetRequest: (data: TargetRequestPayload) => void;
  clearTargetRequest: () => void;
  applyPhaseResolve: (data: PhaseResolvePayload) => void;
  clearReveal: () => void;
  applyPublicReveal: (data: PublicRevealPayload) => void;
  clearPublicReveal: () => void;
  applyEliminationResult: (data: EliminationResultPayload) => void;
  applyGameOver: (data: GameOverPayload) => void;

  // ── Local UI transitions ──
  markPromptSubmitted: () => void;
}

// Phases during which a secret_phase_prompt can be active. A game_state_update
// whose phase is one of these is a board refresh DURING a secret phase and must
// NOT clear the prompt; any other phase means the secret phase has ended.
const SECRET_PHASE_NAMES = new Set(['dawn', 'night']);

const initialSession: SessionSlice = {
  connected: false,
  roomCode: null,
  playerId: null,
  displayName: null,
  joinError: null,
};

const initialPrivate: PrivateSlice = {
  tryals: [],
  hand: [],
  role: null,
  isWitch: false,
  isConstable: false,
  fellowWitches: [],
  witchVotes: [],
};

const initialPublicBoard: PublicBoardSlice = {
  phase: null,
  whoseTurn: null,
  players: [],
  deckCount: null,
  discardCount: null,
};

export const useGameStore = create<GameStore>((set, get) => ({
  session: { ...initialSession },
  privateState: { ...initialPrivate },
  publicBoard: { ...initialPublicBoard },
  prompt: null,
  actionRequest: null,
  deckRearrange: null,
  cardPick: null,
  confirm: null,
  targetRequest: null,
  reveal: null,
  lastElimination: null,
  lastPublicReveal: null,
  gameOver: null,

  setConnected: (connected) =>
    set((s) => ({ session: { ...s.session, connected } })),

  beginJoin: (displayName) =>
    set((s) => ({ session: { ...s.session, displayName, joinError: null } })),

  onJoined: (playerId, roomCode) =>
    set((s) => ({
      session: { ...s.session, playerId, roomCode, joinError: null },
    })),

  // Mirror clients have a room but no player slot (playerId stays null).
  onMirrorJoined: (roomCode) =>
    set((s) => ({
      session: { ...s.session, roomCode, playerId: null, joinError: null },
    })),

  setJoinError: (message) =>
    set((s) => ({ session: { ...s.session, joinError: message } })),

  reset: () =>
    set({
      session: { ...initialSession },
      privateState: { ...initialPrivate },
      publicBoard: { ...initialPublicBoard },
      prompt: null,
      actionRequest: null,
      deckRearrange: null,
      cardPick: null,
      confirm: null,
      targetRequest: null,
      reveal: null,
      lastElimination: null,
      lastPublicReveal: null,
      gameOver: null,
    }),

  applyGameStateUpdate: (data) =>
    set((s) => {
      const newPhase = data.phase ?? null;
      // Clear an active secret-phase prompt ONLY when the phase has moved out of a
      // secret phase (the phase genuinely ended). A routine board refresh during a
      // secret phase — including the phase-entry update — must NOT wipe a freshly
      // set prompt. That race was dropping every phone off the SecretPhaseScreen.
      const inSecretPhase =
        newPhase != null && SECRET_PHASE_NAMES.has(newPhase.toLowerCase());
      const secretPhaseEnded = !!s.prompt && newPhase != null && !inSecretPhase;

      // actionRequest is Day-only. Clear it ONLY when it can no longer be valid — the turn moved to
      // another player, or we entered a secret (night/dawn) phase. Clearing it on EVERY board tick
      // (the old 4a behavior) rested on the assumption that the host "always re-sends after a board
      // tick"; it does not — it re-sends once per turn-loop iteration. So any unrelated broadcast
      // (the flurry of eliminations/reveals during a John Proctor draft) wiped a LIVE turn prompt and
      // stranded the phone on idle until the turn timer fired. Same race, same shape, as the prompt
      // guard above.
      const turnMovedAway =
        data.whoseTurn != null && data.whoseTurn !== s.session.playerId;
      const dropAction = inSecretPhase || turnMovedAway;

      return {
        publicBoard: {
          phase: newPhase,
          whoseTurn: data.whoseTurn ?? null,
          players: data.players ?? [],
          deckCount: data.deckCount ?? null,
          discardCount: data.discardCount ?? null,
        },
        // Prompt cleared only when the secret phase ended (above). actionRequest
        // is Day-only and is always re-sent after a board tick if the turn
        // continues, so clearing it on every update remains safe (4a behavior).
        ...(secretPhaseEnded ? { prompt: null } : {}),
        ...(dropAction ? { actionRequest: null } : {}),
      };
    }),

  applyPrivateState: (data) =>
    set({
      privateState: {
        tryals: data.tryals ?? [],
        hand: data.hand ?? [],
        role: data.role ?? null,
        isWitch: data.isWitch ?? false,
        isConstable: data.isConstable ?? false,
        fellowWitches: data.fellowWitches ?? [],
        witchVotes: data.witchVotes ?? [],
      },
    }),

  applySecretPhasePrompt: (data) =>
    set({
      prompt: {
        type: data.prompt,
        targets: data.targets ?? [],
        acting: data.acting,
        canFakeConfess: data.canFakeConfess ?? false,
        submitted: false,
      },
      actionRequest: null,
      deckRearrange: null,
      cardPick: null,
      confirm: null,
      targetRequest: null,
    }),

  applyActionRequest: (data) =>
    set({
      actionRequest: {
        actions: data.actions ?? [],
        unplayableCards: data.unplayableCards ?? [],
      },
      prompt: null,
      // After a rearrange, the host re-prompts the turn action — leave the
      // rearrange screen for the action screen.
      deckRearrange: null,
      // NOTE: cardPick is deliberately NOT cleared here. A John/Martha draft can still be open when
      // the drafter's own turn begins (someone was eliminated just before it), and the selector gives
      // cardPick precedence over actionRequest for exactly that case. Clearing it here would defeat
      // that precedence and yank the half-finished draft off the phone. The stored actionRequest just
      // waits behind the draft; when the draft resolves (clearCardPick) the action screen appears.
      confirm: null,
      targetRequest: null,
    }),

  // Tituba's deck rearrange — mutually exclusive with prompt/actionRequest.
  applyDeckRearrangeRequest: (data) =>
    set({
      deckRearrange: { cards: data.cards ?? [], seconds: data.seconds ?? 60 },
      prompt: null,
      actionRequest: null,
      cardPick: null,
      confirm: null,
      targetRequest: null,
    }),

  clearDeckRearrange: () => set({ deckRearrange: null }),

  // John Proctor / Martha card draft — mutually exclusive with prompt/action/rearrange.
  applyCardPickRequest: (data) =>
    set({
      cardPick: {
        cards: data.cards ?? [],
        pickNumber: data.pickNumber ?? 1,
        totalPicks: data.totalPicks ?? 3,
        seconds: data.seconds ?? 45,
        allowDone: data.allowDone ?? false,
        reason: data.reason,
      },
      prompt: null,
      // NOTE: actionRequest is deliberately NOT cleared here — the mirror of the guard in
      // applyActionRequest. A draft can fire DURING the drafter's own turn (their card play
      // eliminated someone), and the turn prompt must survive underneath it so the action screen
      // returns when the draft resolves. Clearing it here stranded the phone on the idle screen
      // until the host's turn timer fired.
      deckRearrange: null,
      confirm: null,
      targetRequest: null,
    }),

  clearCardPick: () => set({ cardPick: null }),

  // A yes/no confirmation for this player's own optional ability choice (e.g. Abigail's
  // discard) — mutually exclusive with the other prompts, like cardPick.
  applyConfirmRequest: (data) =>
    set({
      confirm: {
        prompt: data.prompt,
        items: data.items ?? [],
        count: data.count ?? 0,
        seconds: data.seconds ?? 20,
      },
      prompt: null,
      actionRequest: null,
      deckRearrange: null,
      cardPick: null,
      targetRequest: null,
    }),

  clearConfirm: () => set({ confirm: null }),

  // Sub-target pick for a two-target card (Robbery/Scapegoat recipient). Mutually exclusive with
  // the other prompts — the host is blocking on this answer before the play resolves.
  applyTargetRequest: (data) =>
    set({
      targetRequest: {
        prompt: data.prompt,
        targets: data.targets ?? [],
        seconds: data.seconds ?? 30,
      },
      prompt: null,
      actionRequest: null,
      deckRearrange: null,
      cardPick: null,
      confirm: null,
    }),

  clearTargetRequest: () => set({ targetRequest: null }),

  applyPhaseResolve: (data) => set({ reveal: { revealAt: data.revealAt } }),

  clearReveal: () => set({ reveal: null }),

  // Public card-show (e.g. Giles Corey). Store-only; a UI toast reads it and clears it.
  applyPublicReveal: (data) => set({ lastPublicReveal: data }),

  clearPublicReveal: () => set({ lastPublicReveal: null }),

  applyEliminationResult: (data) => {
    const { playerId } = get().session;
    set((s) => ({
      publicBoard: {
        ...s.publicBoard,
        players: s.publicBoard.players.map((p) =>
          p.playerId === data.playerId
            ? { ...p, eliminated: data.eliminated }
            : p,
        ),
      },
      lastElimination: data,
      // If this elimination targets me, drop any pending prompt/action.
      prompt: data.playerId === playerId ? null : s.prompt,
      actionRequest: data.playerId === playerId ? null : s.actionRequest,
    }));
  },

  applyGameOver: (data) =>
    set({ gameOver: { winner: data.winner, tryals: data.tryals ?? {} } }),

  markPromptSubmitted: () =>
    set((s) => (s.prompt ? { prompt: { ...s.prompt, submitted: true } } : {})),
}));
