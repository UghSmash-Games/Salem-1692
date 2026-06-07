/**
 * Subtle indicator of the player's OWN role, sourced from private_state.
 * Only ever shown on the player's own device.
 */

import type { PlayerRole } from '../socket/types';

interface Props {
  role: PlayerRole | null;
}

const ROLE_LABEL: Record<PlayerRole, string> = {
  witch: 'Witch',
  townsperson: 'Townsperson',
  constable: 'Constable',
};

const ROLE_CLASS: Record<PlayerRole, string> = {
  witch: 'text-ember',
  townsperson: 'text-parchment/70',
  constable: 'text-candle',
};

export function RoleIndicator({ role }: Props) {
  if (!role) return null;
  return (
    <span
      className={`text-xs uppercase tracking-widest ${ROLE_CLASS[role]}`}
      data-testid="role-indicator"
    >
      {ROLE_LABEL[role]}
    </span>
  );
}
