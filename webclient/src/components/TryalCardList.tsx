/**
 * Renders ONLY the current player's own tryal cards (from privateState).
 * It never receives or renders another player's tryals — the data simply
 * isn't in this slice. This is a hard privacy boundary.
 */

import type { TryalCardView } from '../socket/types';

interface Props {
  tryals: TryalCardView[];
}

export function TryalCardList({ tryals }: Props) {
  if (tryals.length === 0) {
    return <p className="text-sm italic text-parchment/60">No tryal cards.</p>;
  }

  return (
    <div className="flex flex-wrap gap-2" data-testid="tryal-card-list">
      {tryals.map((card, i) => (
        <div
          key={i}
          className={[
            'rounded-md border px-3 py-2 text-sm font-semibold shadow',
            card.faceUp
              ? 'border-ember bg-ember/20 text-ember'
              : 'border-candle/60 bg-ink/40 text-parchment',
          ].join(' ')}
        >
          {card.label}
          {card.faceUp && (
            <span className="ml-1 text-xs font-normal opacity-70">
              (revealed)
            </span>
          )}
        </div>
      ))}
    </div>
  );
}
