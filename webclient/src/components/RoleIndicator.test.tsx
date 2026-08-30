/**
 * The role indicator shows ALL roles a player holds. The key case is the
 * dual-role evil constable (witch AND constable), whose constable role must not
 * be hidden behind "Witch".
 */

import { describe, it, expect, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { RoleIndicator } from './RoleIndicator';
import { useGameStore } from '../store/gameStore';
import type { PrivateStatePayload } from '../socket/types';

function setup(priv: Partial<PrivateStatePayload>) {
  useGameStore.getState().reset();
  useGameStore.getState().applyPrivateState({
    playerId: 'p0',
    tryals: [],
    hand: [],
    role: 'townsperson',
    ...priv,
  });
}

describe('RoleIndicator', () => {
  beforeEach(() => useGameStore.getState().reset());

  it('shows BOTH roles for a dual-role evil constable', () => {
    setup({ role: 'witch', isWitch: true, isConstable: true });
    render(<RoleIndicator />);
    expect(screen.getByTestId('role-indicator')).toHaveTextContent('Witch + Constable');
  });

  it('shows Witch for a pure witch', () => {
    setup({ role: 'witch', isWitch: true, isConstable: false });
    render(<RoleIndicator />);
    expect(screen.getByTestId('role-indicator')).toHaveTextContent(/^Witch$/);
  });

  it('shows Constable for a pure constable', () => {
    setup({ role: 'constable', isWitch: false, isConstable: true });
    render(<RoleIndicator />);
    expect(screen.getByTestId('role-indicator')).toHaveTextContent(/^Constable$/);
  });

  it('shows Townsperson when holding neither role', () => {
    setup({ role: 'townsperson', isWitch: false, isConstable: false });
    render(<RoleIndicator />);
    expect(screen.getByTestId('role-indicator')).toHaveTextContent('Townsperson');
  });

  it('renders nothing before any private_state arrives', () => {
    useGameStore.getState().reset();
    render(<RoleIndicator />);
    expect(screen.queryByTestId('role-indicator')).not.toBeInTheDocument();
  });
});
