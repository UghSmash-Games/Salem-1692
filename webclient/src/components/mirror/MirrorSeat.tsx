/**
 * MirrorSeat — the browser port of Unity's HostPlayerSeat.
 *
 * Composition is the locked design (docs/phase-7-host-seat-design.md §2), top to bottom:
 *   1. Cards played in front of them, grouped by type with a ×N badge
 *   2. Portrait + PLAYER name (primary) + character name (secondary) + stats
 *   3. Tryal row — face-up art for revealed, the shared back for the rest
 * plus the ember turn ring, the HANGED overlay, and effect badges.
 *
 * 🔴 EVERY FIELD IS ALREADY PUBLIC. This renders only what the mirror is already sent
 * (PublicPlayer), so it adds no protocol surface and needs no privacy re-audit. The face-down
 * tryals are drawn from a COUNT — `tryalTotal` minus the revealed labels — so there is no hidden
 * identity here even in principle.
 *
 * ⚠️ TRYAL COUNT COMES FROM `tryalTotal`, NEVER A HARDCODED 5. The design PDF showed "1/5" on a
 * 12-seat board, which was mock data: the real distribution is 5 tryals at 4-7 players, 4 at 8-9,
 * 3 at 10-12.
 *
 * ⚠️ `townHall` GOES EMPTY ON ELIMINATION (the host nulls the card), so the last non-empty value is
 * cached — same as HostPlayerSeat and CharacterCard. Without it a hanged player's character name
 * would vanish from the board mid-game.
 *
 * Built for a TV: no hover states, no scrolling, sized in viewport units so the ring fits the
 * screen at any panel size.
 *
 * ⚠️ EVERY VERTICAL METRIC IS A MULTIPLE OF `--su` (the seat unit, default 1vh) so a leg can shrink
 * its seats by setting ONE variable — see ringLayout.verticalSeatUnit, which is what stops the side
 * seats from spilling over the top and bottom rows on a full 12-seat ring. Horizontal metrics stay
 * in vw: the sides are constrained in height, never in width. If you add a vertical size here, scale
 * it too, or it will not shrink with the rest of the seat.
 */

import { useEffect, useRef } from 'react';
import type { PublicPlayer } from '../../socket/types';
import { cardArt, townHallArt, TRYAL_BACK } from '../../data/cardArt';

/**
 * Cards in the played row, grouped by label with FIRST-APPEARANCE ORDER preserved, so the row stays
 * stable between broadcasts rather than reshuffling whenever a card is added.
 */
export function stackCards(labels: string[]): { label: string; count: number }[] {
  const out: { label: string; count: number }[] = [];
  for (const label of labels) {
    if (!label) continue;
    const found = out.find((s) => s.label === label);
    if (found) found.count += 1;
    else out.push({ label, count: 1 });
  }
  return out;
}

/** Matches the host: five slots, then a "+N" counting hidden TYPES (not hidden cards). */
const PLAYED_SLOTS = 5;

interface Props {
  player: PublicPlayer;
  isTurn: boolean;
}

export function MirrorSeat({ player, isTurn }: Props) {
  // Cache the character name across elimination — see the note above.
  const lastTownHall = useRef<string | null>(null);
  useEffect(() => {
    if (player.townHall) lastTownHall.current = player.townHall;
  }, [player.townHall]);
  const townHall = player.townHall || lastTownHall.current;

  const revealed = player.revealedTryals ?? [];
  const total = player.tryalTotal ?? 0;
  const faceDown = Math.max(0, total - revealed.length);

  // The host's played row carries BOTH reds and blues — they are one physical pile in front of the
  // player — while the blues ALSO appear as effect badges. That duplication is deliberate there:
  // the card is what sits on the table, the badge is the at-a-glance effect.
  const stacks = stackCards([
    ...(player.accusationCards ?? []),
    ...(player.statusCards ?? []),
  ]);
  const shown = stacks.slice(0, PLAYED_SLOTS);
  const hiddenTypes = stacks.length - shown.length;

  const portrait = townHallArt(townHall);

  return (
    <article
      className="relative flex h-full flex-col gap-[calc(var(--su,1vh)*0.5)] rounded-md bg-host-parchment/5 px-[0.7vw] py-[calc(var(--su,1vh)*0.7)]"
      data-testid="mirror-seat"
      data-player={player.playerId}
    >
      {/* ── 1. Cards played in front of them ── */}
      <div className="flex min-h-[calc(var(--su,1vh)*5)] items-start gap-[0.4vw]" data-testid="seat-played">
        {shown.map((s) => {
          const art = cardArt(s.label);
          return (
            <div key={s.label} className="relative" title={s.label}>
              {art ? (
                <img src={art} alt={s.label} className="h-[calc(var(--su,1vh)*5)] w-auto rounded-[2px]" />
              ) : (
                <span className="text-[length:calc(var(--su,1vh)*1.4)] text-host-parchment/70">{s.label}</span>
              )}
              {s.count > 1 && (
                <span
                  className="absolute -bottom-1 -right-1 rounded-full bg-host-crimson px-[0.4vw] text-[length:calc(var(--su,1vh)*1.5)] font-semibold text-host-badge"
                  data-testid="stack-count"
                >
                  ×{s.count}
                </span>
              )}
            </div>
          );
        })}
        {hiddenTypes > 0 && (
          <span className="self-center text-[length:calc(var(--su,1vh)*1.5)] text-host-parchment/60" data-testid="seat-overflow">
            +{hiddenTypes}
          </span>
        )}
      </div>

      {/* ── 2. Identity ── */}
      <div className="flex items-start gap-[0.5vw]">
        {portrait && (
          <img
            src={portrait}
            alt=""
            className="h-[calc(var(--su,1vh)*9)] w-auto shrink-0 rounded-[2px]"
            data-testid="seat-portrait"
          />
        )}
        <div className="min-w-0 flex-1">
          <p
            className="truncate text-[length:calc(var(--su,1vh)*2.7)] font-semibold leading-tight text-host-parchment"
            data-testid="seat-name"
          >
            {player.displayName}
          </p>
          {townHall && (
            <p
              className="truncate text-[length:calc(var(--su,1vh)*1.8)] leading-tight text-host-parchment/70"
              data-testid="seat-character"
            >
              {townHall}
            </p>
          )}
          <p
            className="text-[length:calc(var(--su,1vh)*1.6)] uppercase tracking-wider text-host-parchment/70"
            data-testid="seat-stats"
          >
            {player.handCount ?? 0} IN HAND · {revealed.length}/{total}
          </p>
          <p
            className="text-[length:calc(var(--su,1vh)*1.6)] uppercase tracking-wider text-host-parchment/70"
            data-testid="seat-accusations"
          >
            ACCUSATIONS {player.accusations}/{player.accusationLimit ?? 7}
          </p>
        </div>
      </div>

      {/* ── 3. Tryals: revealed art, then the ONE shared back repeated ── */}
      <div className="mt-auto flex gap-[0.25vw]" data-testid="seat-tryals">
        {revealed.map((label, i) => {
          const art = cardArt(label);
          return art ? (
            <img key={`r${i}`} src={art} alt={label} className="h-[calc(var(--su,1vh)*7.5)] w-auto rounded-[2px]" />
          ) : (
            <span key={`r${i}`} className="text-[length:calc(var(--su,1vh)*1.4)] text-host-parchment">
              {label}
            </span>
          );
        })}
        {Array.from({ length: faceDown }, (_, i) => (
          <img
            key={`d${i}`}
            src={TRYAL_BACK}
            alt=""
            className="h-[calc(var(--su,1vh)*7.5)] w-auto rounded-[2px] opacity-90"
            data-testid="tryal-back"
          />
        ))}
      </div>

      {/* Effect badges — the card NAME is written on the badge, so colour is never the only
          carrier (the Phase 9 accessibility rule). */}
      {(player.statusCards?.length ?? 0) > 0 && (
        <div
          className="absolute -top-[calc(var(--su,1vh)*0.8)] left-[0.6vw] flex gap-[0.2vw]"
          data-testid="seat-effects"
        >
          {(player.statusCards ?? []).map((c) => (
            <span
              key={c}
              className={`rounded-full px-[0.6vw] py-[calc(var(--su,1vh)*0.2)] text-[length:calc(var(--su,1vh)*1.3)] uppercase tracking-wide text-host-bright ${
                c === 'Asylum' ? 'bg-host-asylum' : 'bg-host-effect'
              }`}
            >
              {c}
            </span>
          ))}
        </div>
      )}

      {/* Active turn — an outline is a SHAPE, present or absent, so it never depends on telling one
          hue from another. */}
      {isTurn && !player.eliminated && (
        <div
          className="pointer-events-none absolute inset-[-3px] rounded-md border-2 border-host-ember"
          data-testid="seat-turn-ring"
        />
      )}

      {/* Eliminated */}
      {player.eliminated && (
        <div
          className="pointer-events-none absolute inset-0 flex items-center justify-center rounded-md bg-[#080605]/80"
          data-testid="seat-hanged"
        >
          <span className="-rotate-[9deg] border border-host-crimson bg-[#140604]/70 px-[1vw] py-[calc(var(--su,1vh)*0.3)] text-[length:calc(var(--su,1vh)*3)] font-bold tracking-widest text-host-hanged">
            HANGED
          </span>
        </div>
      )}
    </article>
  );
}
