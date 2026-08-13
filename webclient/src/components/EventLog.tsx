/**
 * EventLog — the mirror's "What Has Passed" rail, the browser counterpart of Unity's HostEventLog.
 *
 * Renders PUBLIC data only. Every line is composed here from a closed `kind` vocabulary via
 * describeGameEvent; the wire carries no prose, so this component cannot display a secret even in
 * principle. Entries whose kind it does not recognise are dropped rather than guessed at.
 *
 * Timestamps are formatted from epoch ms in the VIEWER's local time — a mirror in another timezone
 * must not inherit the host's clock.
 *
 * Newest LAST, matching the host screen, so both rooms scan the same direction.
 */

import { useMemo } from 'react';
import { useGameStore } from '../store/gameStore';
import { describeGameEvent, formatEventTime } from './gameEventCopy';

export function EventLog() {
  const events = useGameStore((s) => s.eventLog);
  const players = useGameStore((s) => s.publicBoard.players);

  // Resolve ids to names from the PUBLIC board — the log names the PLAYER, never the character.
  const lines = useMemo(() => {
    const nameOf = (playerId: string | null | undefined) =>
      players.find((p) => p.playerId === playerId)?.displayName ?? '';

    return events
      .map((e, i) => ({
        key: `${e.atMs}-${i}`,
        time: formatEventTime(e.atMs),
        body: describeGameEvent(e, nameOf),
      }))
      .filter((l): l is { key: string; time: string; body: string } => !!l.body);
  }, [events, players]);

  if (lines.length === 0) return null;

  return (
    <section className="flex flex-col gap-2" data-testid="event-log">
      <h2 className="text-sm uppercase tracking-wider text-parchment/60">
        What Has Passed
      </h2>
      <ol className="flex flex-col gap-1">
        {lines.map((l) => (
          <li key={l.key} className="flex gap-3 text-sm" data-testid="event-log-entry">
            <span className="shrink-0 tabular-nums text-parchment/40">{l.time}</span>
            <span className="text-parchment/80">{l.body}</span>
          </li>
        ))}
      </ol>
    </section>
  );
}
