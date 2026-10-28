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
              aria-pressed={isSelected}
              data-selected={isSelected || undefined}
              className={[
                'flex w-full items-center justify-center gap-2 rounded-md px-4 py-3 text-center text-base font-medium transition-colors',
                // Border WIDTH carries selection alongside hue, so the state survives any colour
                // vision. See the check mark below.
                isSelected
                  ? 'border-2 border-candle bg-candle/30 text-parchment'
                  : 'border border-parchment/40 bg-ink/40 text-parchment hover:border-candle/60',
                disabled ? 'cursor-not-allowed opacity-50' : '',
              ].join(' ')}
            >
              {/* ✓ is the non-colour carrier. Selection here is confirmed under a countdown on the
                  secret-phase and target screens, so "which one did I pick?" must never depend on
                  telling candle from parchment. The mark is aria-hidden because aria-pressed
                  already conveys the state to assistive tech. */}
              <span aria-hidden className="w-3 text-candle">
                {isSelected ? '✓' : ''}
              </span>
              {target}
            </button>
          </li>
        );
      })}
    </ul>
  );
}
