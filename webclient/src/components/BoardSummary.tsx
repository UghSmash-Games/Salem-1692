/**
 * Public board summary. Shows only public information about every player:
 * accusation counts, eliminated status, the red accusation + blue status cards
 * in front of them, and whose turn it is. NEVER shows tryal card faces or roles.
 */

import type { PublicPlayer } from '../socket/types';

interface Props {
  players: PublicPlayer[];
  whoseTurn?: string | null;
  myPlayerId?: string | null;
}

export function BoardSummary({ players, whoseTurn, myPlayerId }: Props) {
  if (players.length === 0) {
    return (
      <p className="text-sm italic text-parchment/60">
        Waiting for the game to begin…
      </p>
    );
  }

  return (
    <ul className="flex flex-col gap-1" data-testid="board-summary">
      {players.map((p) => {
        const isTurn = p.playerId === whoseTurn;
        const isMe = p.playerId === myPlayerId;
        // Red accusation cards and blue status cards arrive as two fields but are one physical
        // pile in front of the player, so they render as one list. Same CONTENT as before the wire
        // split; ordering is now grouped (reds, then blues) rather than interleaved by play order.
        const cardsInFront = [
          ...(p.accusationCards ?? []),
          ...(p.statusCards ?? []),
        ];
        return (
          <li
            key={p.playerId}
            className={[
              'flex items-center justify-between rounded px-2 py-1 text-sm',
              isTurn ? 'bg-candle/20' : '',
              p.eliminated ? 'opacity-50 line-through' : '',
            ].join(' ')}
          >
            <span className="font-medium text-parchment">
              {p.displayName}
              {isMe && <span className="ml-1 text-xs text-candle">(you)</span>}
              {isTurn && !p.eliminated && (
                <span className="ml-1 text-xs text-candle">● turn</span>
              )}
            </span>
            <span className="flex items-center gap-2 text-xs text-parchment/80">
              {cardsInFront.length > 0 && (
                <span className="text-moss">{cardsInFront.join(', ')}</span>
              )}
              <span title="accusations">⚖ {p.accusations}</span>
            </span>
          </li>
        );
      })}
    </ul>
  );
}
