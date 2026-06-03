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
  SecretPhaseType,
  GameStateUpdatePayload,
  PrivateStatePayload,
  SecretPhasePromptPayload,
  ActionRequestPayload,
  PhaseResolvePayload,
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
}

export interface PublicBoardSlice {
  phase: string | null;
  whoseTurn: string | null;
  players: PublicPlayer[];
}

export interface PromptSlice {
  type: SecretPhaseType;
  targets: string[];
  /** Stored, never read by render/timing logic. */
  acting: boolean;
  submitted: boolean;
}

export interface ActionRequestSlice {
  actions: string[];
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
  reveal: { revealAt: number } | null;
  gameOver: GameOverSlice | null;

  // ── Connection / session ──
  setConnected: (connected: boolean) => void;
  beginJoin: (displayName: string) => void;
  onJoined: (playerId: string, roomCode: string) => void;
  setJoinError: (message: string) => void;
  reset: () => void;

  // ── Server event handlers ──
  applyGameStateUpdate: (data: GameStateUpdatePayload) => void;
  applyPrivateState: (data: PrivateStatePayload) => void;
  applySecretPhasePrompt: (data: SecretPhasePromptPayload) => void;
  applyActionRequest: (data: ActionRequestPayload) => void;
  applyPhaseResolve: (data: PhaseResolvePayload) => void;
  applyEliminationResult: (data: EliminationResultPayload) => void;
  applyGameOver: (data: GameOverPayload) => void;

  // ── Local UI transitions ──
  markPromptSubmitted: () => void;
}

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
};

const initialPublicBoard: PublicBoardSlice = {
  phase: null,
  whoseTurn: null,
  players: [],
};

export const useGameStore = create<GameStore>((set, get) => ({
  session: { ...initialSession },
  privateState: { ...initialPrivate },
  publicBoard: { ...initialPublicBoard },
  prompt: null,
  actionRequest: null,
  reveal: null,
  gameOver: null,

  setConnected: (connected) =>
    set((s) => ({ session: { ...s.session, connected } })),

  beginJoin: (displayName) =>
    set((s) => ({ session: { ...s.session, displayName, joinError: null } })),

  onJoined: (playerId, roomCode) =>
    set((s) => ({
      session: { ...s.session, playerId, roomCode, joinError: null },
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
      reveal: null,
      gameOver: null,
    }),

  applyGameStateUpdate: (data) =>
    set({
      publicBoard: {
        phase: data.phase ?? null,
        whoseTurn: data.whoseTurn ?? null,
        players: data.players ?? [],
      },
      // Advancing public state ends any active secret phase / action request.
      prompt: null,
      actionRequest: null,
    }),

  applyPrivateState: (data) =>
    set({
      privateState: {
        tryals: data.tryals ?? [],
        hand: data.hand ?? [],
        role: data.role ?? null,
      },
    }),

  applySecretPhasePrompt: (data) =>
    set({
      prompt: {
        type: data.prompt,
        targets: data.targets ?? [],
        acting: data.acting,
        submitted: false,
      },
      actionRequest: null,
    }),

  applyActionRequest: (data) =>
    set({
      actionRequest: { actions: data.actions ?? [] },
      prompt: null,
    }),

  applyPhaseResolve: (data) => set({ reveal: { revealAt: data.revealAt } }),

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
