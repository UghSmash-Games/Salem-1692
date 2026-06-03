/**
 * The player's own hand. On the idle screen cards are display-only (names);
 * on the action screen they are selectable.
 */

interface Props {
  hand: string[];
  selectable?: boolean;
  selectedIndex?: number | null;
  onSelect?: (index: number) => void;
}

export function HandList({
  hand,
  selectable = false,
  selectedIndex = null,
  onSelect,
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
        return (
          <li key={i}>
            <button
              type="button"
              onClick={() => onSelect?.(i)}
              className={`${base} w-full ${
                isSelected
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
