/**
 * Public deck + discard counts. Renders nothing if the host hasn't supplied
 * counts (they're optional, Unity-defined public state).
 */

interface Props {
  deckCount: number | null;
  discardCount: number | null;
}

export function DeckSummary({ deckCount, discardCount }: Props) {
  if (deckCount === null && discardCount === null) return null;

  return (
    <div className="flex gap-6" data-testid="deck-summary">
      {deckCount !== null && (
        <div className="flex flex-col items-center">
          <span className="text-2xl font-bold text-parchment">{deckCount}</span>
          <span className="text-xs uppercase tracking-wider text-parchment/60">
            Deck
          </span>
        </div>
      )}
      {discardCount !== null && (
        <div className="flex flex-col items-center">
          <span className="text-2xl font-bold text-parchment/70">
            {discardCount}
          </span>
          <span className="text-xs uppercase tracking-wider text-parchment/60">
            Discard
          </span>
        </div>
      )}
    </div>
  );
}
