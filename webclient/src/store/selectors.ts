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
  | 'spectator'
  | 'game_over';

/**
 * Decide which screen to render from the current store slices.
 *
 * Priority order:
 *   game_over  > eliminated (spectator) > secret phase > action > idle > join
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
