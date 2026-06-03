/**
 * Reusable target picker. Renders a list of selectable targets.
 *
 * Used by both the action screen and the secret phase masking screen, so its
 * rendering depends ONLY on the targets passed in — never on role/acting.
 */

interface Props {
  targets: string[];
  selected: string | null;
  onSelect: (target: string) => void;
  disabled?: boolean;
}

export function PlayerTargetList({
  targets,
  selected,
  onSelect,
  disabled = false,
}: Props) {
  return (
    <ul className="flex flex-col gap-2" data-testid="target-list">
      {targets.map((target) => {
        const isSelected = selected === target;
        return (
          <li key={target}>
            <button
              type="button"
              disabled={disabled}
              onClick={() => onSelect(target)}
              className={[
                'w-full rounded-md border px-4 py-3 text-center text-base font-medium transition-colors',
                isSelected
                  ? 'border-candle bg-candle/30 text-parchment'
                  : 'border-parchment/40 bg-ink/40 text-parchment hover:border-candle/60',
                disabled ? 'cursor-not-allowed opacity-50' : '',
              ].join(' ')}
            >
              {target}
            </button>
          </li>
        );
      })}
    </ul>
  );
}
