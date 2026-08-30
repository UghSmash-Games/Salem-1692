/**
 * EventLog — the mirror's rendering of the public log.
 *
 * Copy-per-kind is covered in gameEventCopy.test.ts; this covers the component's own behaviour:
 * ordering, the rolling cap, dropping unknown kinds, and resolving ids to PLAYER names.
 */

import { describe, it, expect, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { EventLog } from './EventLog';
import { useGameStore } from '../store/gameStore';
import type { GameEventPayload } from '../socket/types';

function board() {
  useGameStore.getState().applyGameStateUpdate({
    phase: 'day',
    players: [
      { playerId: 'p0', displayName: 'Alice', accusations: 0, eliminated: false },
      { playerId: 'p1', displayName: 'Bob', accusations: 0, eliminated: false },
    ],
  });
}

function push(over: Partial<GameEventPayload>) {
  useGameStore.getState().appendGameEvent({
    kind: 'game_started',
    actorId: null,
    targetId: null,
    cardName: null,
    value: null,
    atMs: Date.now(),
    ...over,
  } as GameEventPayload);
}

describe('EventLog', () => {
  beforeEach(() => useGameStore.getState().reset());

  it('renders nothing before any event', () => {
    board();
    const { container } = render(<EventLog />);
    expect(container).toBeEmptyDOMElement();
  });

  it('names the PLAYER, resolved from the public board', () => {
    board();
    push({ kind: 'player_eliminated', targetId: 'p1' });
    render(<EventLog />);
    expect(screen.getByTestId('event-log')).toHaveTextContent('Bob is hanged.');
  });

  it('keeps oldest-first order, matching the host screen', () => {
    board();
    push({ kind: 'game_started' });
    push({ kind: 'phase_changed', value: 'Dawn' });
    render(<EventLog />);

    const entries = screen.getAllByTestId('event-log-entry');
    expect(entries).toHaveLength(2);
    expect(entries[0]).toHaveTextContent('The table is set');
    expect(entries[1]).toHaveTextContent('Dawn breaks');
  });

  it('drops unknown kinds without breaking the rest of the log', () => {
    board();
    push({ kind: 'game_started' });
    push({ kind: 'night_vote_cast' as never, targetId: 'p1' });
    push({ kind: 'player_eliminated', targetId: 'p1' });
    render(<EventLog />);

    const entries = screen.getAllByTestId('event-log-entry');
    expect(entries).toHaveLength(2);
    expect(screen.getByTestId('event-log')).not.toHaveTextContent('night_vote');
  });

  it('caps the stored log so a long game cannot grow it without bound', () => {
    board();
    for (let i = 0; i < 30; i++) push({ kind: 'player_eliminated', targetId: 'p1' });
    // The mirror is a display that never reloads; the store keeps a rolling window (14, matching
    // HostEventLog.maxEntries).
    expect(useGameStore.getState().eventLog.length).toBe(14);
  });
});
