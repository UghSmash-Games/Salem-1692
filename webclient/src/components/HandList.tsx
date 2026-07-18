/**
 * The player's own hand. On the idle screen cards are display-only (names);
 * on the action screen they are selectable.
 */

interface Props {
  hand: string[];
  selectable?: boolean;
  selectedIndex?: number | null;
  onSelect?: (index: number) => void;
  /** Card NAMES that can't be played right now (host-computed, e.g. Robbery/Scapegoat
   *  with fewer than 3 players alive). Rendered greyed-out and non-selectable. */
  disabledCards?: string[];
}

export function HandList({
  hand,
  selectable = false,
  selectedIndex = null,
  onSelect,
  disabledCards = [],
}: Props) {
  if (hand.length === 0) {
    return <p className="text-sm italic text-parchment/60">No cards in hand.</p>;
  }

  return (
    <ul className="flex flex-col gap-2" data-testid="hand-list">
      {hand.map((card, i) => {
        const isSelected = selectable && selectedIndex === i;
        const base =
          'rounded-md border px-3 py-2 text-left text-sm transition-colors';
        if (!selectable) {
          return (
            <li
              key={i}
              className={`${base} border-parchment/30 bg-ink/30 text-parchment`}
            >
              {card}
            </li>
          );
        }
        const isDisabled = disabledCards.includes(card);
        return (
          <li key={i}>
            <button
              type="button"
              disabled={isDisabled}
              onClick={() => onSelect?.(i)}
              data-disabled={isDisabled || undefined}
              className={`${base} w-full ${
                isDisabled
                  ? 'cursor-not-allowed border-parchment/20 bg-ink/20 text-parchment/40'
                  : isSelected
                    ? 'border-candle bg-candle/30 text-parchment'
                    : 'border-parchment/30 bg-ink/30 text-parchment hover:border-candle/60'
              }`}
            >
              {card}
            </button>
          </li>
        );
      })}
    </ul>
  );
}
