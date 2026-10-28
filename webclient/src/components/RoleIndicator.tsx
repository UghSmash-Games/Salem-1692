import { useGameStore } from '../store/gameStore';

/**
 * Subtle indicator of the player's OWN role(s), from private_state. A player can
 * hold BOTH roles (an evil constable is a witch AND the constable), so we show
 * every role they hold — e.g. "Witch + Constable". Private to this device; never
 * broadcast, so it doesn't affect masking.
 */
export function RoleIndicator() {
  const isWitch = useGameStore((s) => s.privateState.isWitch);
  const isConstable = useGameStore((s) => s.privateState.isConstable);
  const role = useGameStore((s) => s.privateState.role);

  const parts: string[] = [];
  if (isWitch) parts.push('Witch');
  if (isConstable) parts.push('Constable');
  if (parts.length === 0) {
    if (role == null) return null; // no private_state yet
    parts.push('Townsperson');
  }

  const colorClass = isWitch
    ? 'text-ember'
    : isConstable
      ? 'text-candle'
      : 'text-parchment/70';

  return (
    <span
      className={`text-xs uppercase tracking-widest ${colorClass}`}
      data-testid="role-indicator"
    >
      {parts.join(' + ')}
    </span>
  );
}
