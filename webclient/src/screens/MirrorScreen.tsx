/**
 * MirrorScreen — the passive public display for a second room.
 *
 * 🔴 THIS IS A PARITY SURFACE, NOT A SUMMARY. Its purpose is that a player who cannot see the host
 * TV connects a device as `display` and sees EXACTLY what the people sitting at the host screen see.
 * Anything public the host renders and this does not is an information asymmetry between players —
 * a fairness bug in a social deduction game, not a cosmetic gap. See CLAUDE.md → MIRROR PARITY.
 *
 * Layout mirrors the host's Board (docs/phase-7-editor-steps.md Stage 5): a table pane holding the
 * rectangular ring around the Meeting House, with a fixed rail carrying IN EFFECT and the event log.
 * Seat placement uses the SAME locked geometry as Unity (see data/ringLayout.ts), so the same player
 * sits in the same chair on both screens.
 *
 * Renders PUBLIC state only. It imports no private components (no TryalCardList, HandList or
 * RoleIndicator) and its data comes solely from the public store slices fed by useMirrorSocket.
 *
 * Built for a TV: fixed viewport, no scrolling, sizes in viewport units.
 *
 * ⚠️ `h-screen` (100vh), NOT `h-dvh`. The dynamic viewport unit exists to cope with mobile browser
 * chrome that hides and shows — a TV has none, so it buys nothing here, and `dvh` is recent enough
 * (~2022) that smart-TV browsers routinely lack it. Unsupported, `h-dvh` yields an invalid height,
 * and with vh-sized children inside `overflow-hidden` the whole board collapses.
 */

import { useLayoutEffect, useRef, useState, type CSSProperties } from 'react';
import { useGameStore } from '../store/gameStore';
import { NightDawnOverlay } from '../components/NightDawnOverlay';
import { RevealOverlay } from '../components/RevealOverlay';
import { PublicRevealToast } from '../components/PublicRevealToast';
import { EventLog } from '../components/EventLog';
import { MirrorSeat } from '../components/mirror/MirrorSeat';
import { MeetingHouse } from '../components/mirror/MeetingHouse';
import { InEffectPanel } from '../components/mirror/InEffectPanel';
import { MirrorHeader } from '../components/mirror/MirrorHeader';
import { ringSlots, verticalSeatUnit, SEAT_GAP_VH } from '../data/ringLayout';
import { GameOverScreen } from './GameOverScreen';
import type { PublicPlayer } from '../socket/types';

/** One leg of the ring. Seats stretch to fill their share, exactly as the host's layout groups do,
 *  which is why a row of 2 and a row of 4 both span the same width. */
function Leg({
  indices,
  players,
  whoseTurn,
  vertical = false,
  unit = 1,
}: {
  indices: number[];
  players: PublicPlayer[];
  whoseTurn: string | null;
  vertical?: boolean;
  /** Seat scale for this leg, in vh, published to the seats as `--su`. 1 = full size. Only a
   *  vertical leg ever passes anything else — see verticalSeatUnit. */
  unit?: number;
}) {
  if (indices.length === 0) return null;
  return (
    <div
      className={`flex ${
        // ⚠ Vertical legs CENTRE their seats rather than stretching them. The middle band is tall
        // (it is the flex-1 row), so stretching left me with side seats several times the height of
        // the top/bottom ones, mostly empty. Centring keeps every seat the same size as its
        // neighbours around the ring, which is also what the host's layout groups produce.
        // ⚠ The vertical gap is a LITERAL 0.8vh, not `${SEAT_GAP_VH}vh` — Tailwind scans source
        // text, so a class built from a variable is never generated. Keep it equal to SEAT_GAP_VH,
        // which is what the fit arithmetic subtracts.
        vertical ? 'h-full flex-col justify-center gap-[0.8vh]' : 'w-full gap-[0.6vw]'
      }`}
      // The seats size themselves off --su, so shrinking a leg is one variable rather than a
      // second set of measurements inside MirrorSeat.
      style={{ '--su': `${unit}vh` } as CSSProperties}
      data-testid={vertical ? 'ring-leg-vertical' : 'ring-leg-horizontal'}
      data-seat-unit={unit}
    >
      {indices.map((i) => {
        const p = players[i];
        if (!p) return null;
        return (
          <div key={p.playerId} className={vertical ? 'min-w-0 shrink-0' : 'min-w-0 flex-1'}>
            <MirrorSeat player={p} isTurn={p.playerId === whoseTurn} />
          </div>
        );
      })}
    </div>
  );
}

export function MirrorScreen() {
  const publicBoard = useGameStore((s) => s.publicBoard);
  const { players, whoseTurn } = publicBoard;
  const roomCode = useGameStore((s) => s.session.roomCode);
  const gameOver = useGameStore((s) => s.gameOver);
  const reveal = useGameStore((s) => s.reveal);

  const ring = ringSlots(players.length);
  const sides = Math.max(ring.left.length, ring.right.length);

  // The side seats shrink to whatever height the horizontal rows leave over, so a full 12-seat ring
  // fits 100vh instead of spilling over the top and bottom rows. Measured rather than computed from
  // the seat's own metrics — see verticalSeatUnit for why an estimate was not good enough.
  const bandRef = useRef<HTMLDivElement>(null);
  const rowRef = useRef<HTMLDivElement>(null);
  const [sideUnit, setSideUnit] = useState(1);

  useLayoutEffect(() => {
    const measure = () => {
      const seat = rowRef.current?.querySelector('[data-testid="mirror-seat"]');
      const seatPx = seat instanceof HTMLElement ? seat.offsetHeight : 0;
      const bandPx = bandRef.current?.clientHeight ?? 0;
      const gapPx = (SEAT_GAP_VH / 100) * window.innerHeight;
      setSideUnit(verticalSeatUnit(sides, seatPx, bandPx, gapPx));
    };

    measure();
    // vh-sized content means every metric changes with the viewport, so re-measure on resize.
    window.addEventListener('resize', measure);
    return () => window.removeEventListener('resize', measure);
    // Seat height changes with the seat COUNT (tryals per player) as well as the leg counts, so the
    // player count is the dependency that covers both.
  }, [sides, players.length]);

  // ⚠ Every hook above the early return below: React requires an unchanging hook order, and the
  // game-over branch would otherwise skip them.
  // Reuse the player game-over screen — it shows only public/revealed data.
  //
  // ⚠ WAIT FOR THE REVEAL TO FINISH FIRST. At revealAt the host sends elimination_result and
  // game_over back-to-back in the same synchronous block, so switching on `gameOver` alone
  // unmounted <RevealOverlay/> after ~one network hop — the mirror room saw a fifth of a second
  // of the beat while the host room watched the full linger. That is the biggest reveal in the
  // game (the kill that ends it) and the exact desync phase_resolve exists to prevent.
  //
  // RevealOverlay clears `reveal` once its linger elapses, so gating on it keeps both rooms on
  // the same schedule. When a game ends with no reveal in flight (e.g. the last witch tryal
  // turned by accusation), `reveal` is already null and this shows immediately, as before.
  if (gameOver && !reveal) return <GameOverScreen />;

  return (
    <div className="relative flex h-screen w-full gap-[0.8vw] overflow-hidden bg-host-ground px-[1vw] py-[1.2vh]">
      {/* ── Table pane ── */}
      <div className="flex min-w-0 flex-1 flex-col gap-[1vh]">
        <MirrorHeader roomCode={roomCode} state={publicBoard} />

        {/* rowRef measures a full-size seat: horizontal legs are never shrunk. */}
        <div ref={rowRef}>
          <Leg indices={ring.top} players={players} whoseTurn={whoseTurn} />
        </div>

        {/* Middle band — the host's 1fr / 2.1fr / 1fr split. */}
        <div ref={bandRef} className="flex min-h-0 flex-1 gap-[0.8vw]">
          <div className="flex-[1]">
            <Leg indices={ring.left} players={players} whoseTurn={whoseTurn} vertical unit={sideUnit} />
          </div>
          <div className="flex-[1.7]">
            <MeetingHouse state={publicBoard} />
          </div>
          <div className="flex-[1]">
            <Leg indices={ring.right} players={players} whoseTurn={whoseTurn} vertical unit={sideUnit} />
          </div>
        </div>

        <Leg indices={ring.bottom} players={players} whoseTurn={whoseTurn} />
      </div>

      {/* ── Rail: what is in play, and what has passed ── */}
      <aside className="flex w-[21vw] shrink-0 flex-col gap-[1.4vh] overflow-hidden">
        <InEffectPanel state={publicBoard} />
        <div className="min-h-0 flex-1 overflow-hidden">
          <EventLog />
        </div>
      </aside>

      {/* Overlays — phase comes from PUBLIC state; reveal from the timestamp. */}
      <NightDawnOverlay phase={publicBoard.phase} />
      <RevealOverlay />
      {/* Non-blocking public announcement (e.g. Giles Corey); sits below RevealOverlay. */}
      <PublicRevealToast />
    </div>
  );
}
