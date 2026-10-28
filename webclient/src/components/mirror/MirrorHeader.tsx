/**
 * MirrorHeader — title, "TABLE {code} · {n} SOULS", and the pulsing phase pill.
 * Browser port of HostHeader.
 *
 * ⚠️ SOULS COUNTS EVERY SEAT DEALT INTO THE GAME, living AND dead — `players.length`, not the number
 * still alive. That matches HostHeader and the locked design's "12 SOULS"; the living count is a
 * separate stat in the Meeting House, and conflating the two would put different numbers on the two
 * screens.
 *
 * Falls back to "TABLE {code}" before the deal, when no seats exist yet — the room code arrives from
 * the lobby, the seat count only on the first public broadcast, so the two are never available at
 * the same moment.
 */

import type { PublicBoardSlice } from '../../store/gameStore';

interface Props {
  roomCode: string | null;
  state: PublicBoardSlice;
}

export function MirrorHeader({ roomCode, state }: Props) {
  const souls = state.players?.length ?? 0;
  const code = roomCode ?? '';
  const tableLine = souls > 0 ? `TABLE ${code} · ${souls} SOULS` : `TABLE ${code}`;
  const phase = state.phase ? state.phase.toUpperCase() : '';

  return (
    <header className="flex items-baseline justify-between" data-testid="mirror-header">
      <div className="flex items-baseline gap-[1vw]">
        <h1 className="text-[3.4vh] font-semibold tracking-wide text-host-bright">Salem, 1692</h1>
        <span
          className="text-[1.6vh] uppercase tracking-[0.25em] text-host-parchment/70"
          data-testid="table-line"
        >
          {tableLine}
        </span>
      </div>

      {phase && (
        <span
          className="flex items-center gap-[0.4vw] rounded-full border border-host-parchment/25 px-[1vw] py-[0.4vh] text-[1.7vh] uppercase tracking-[0.25em] text-host-parchment/85"
          data-testid="phase-pill"
        >
          {/* The ember dot breathes, as on the host. Purely decorative — the phase WORD beside it
              is what carries the meaning. */}
          <span className="h-[1.2vh] w-[1.2vh] rounded-full bg-host-ember motion-safe:animate-pulse" />
          {phase}
        </span>
      )}
    </header>
  );
}
