/**
 * Derived state selectors. The server never tells the client which screen to
 * show — the screen is derived from the latest events. Unity stays authoritative.
 */

import { useGameStore } from './gameStore';

export type Screen =
  | 'join'
  | 'idle'
  | 'action'
  | 'secret_phase'
  | 'deck_rearrange'
  | 'card_pick'
  | 'confirm'
  | 'target'
  | 'spectator'
  | 'game_over';

/**
 * Decide which screen to render from the current store slices.
 *
 * Priority order:
 *   game_over  > eliminated (spectator) > secret phase > confirm > target
 *              > card pick > deck rearrange > action > idle > join
 */
export function useCurrentScreen(): Screen {
  return useGameStore((s): Screen => {
    if (s.gameOver) return 'game_over';

    const joined = !!s.session.roomCode && !!s.session.playerId;
    if (!joined) return 'join';

    const me = s.publicBoard.players.find(
      (p) => p.playerId === s.session.playerId,
    );
    if (me?.eliminated) return 'spectator';

    if (s.prompt) return 'secret_phase';
    // A confirm / sub-target pick is a blocking mid-turn decision the host is actively awaiting
    // before the play resolves — so both outrank the turn screens below them.
    if (s.confirm) return 'confirm';
    if (s.targetRequest) return 'target';
    // The John/Martha draft can arrive while the drafter is mid-turn (someone
    // else was eliminated), so it takes precedence over their own action/rearrange.
    if (s.cardPick) return 'card_pick';
    if (s.deckRearrange) return 'deck_rearrange';
    if (s.actionRequest) return 'action';

    return 'idle';
  });
}

/** The current player's own public board entry (or undefined pre-game). */
export function useMe() {
  return useGameStore((s) =>
    s.publicBoard.players.find((p) => p.playerId === s.session.playerId),
  );
}
