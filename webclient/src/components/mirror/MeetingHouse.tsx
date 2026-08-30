/**
 * MeetingHouse — the centre of the ring: the three tallies, the deck and discard, the face-up top
 * discard card, and the tryal legend. Browser port of HostTableStats + HostDeckView.
 *
 * 🔴 ALL THREE STATS ARE DERIVED, NOTHING NEW IS BROADCAST. That is the same property the host
 * relies on: a derived number cannot leak, because it can only restate what the seats already show.
 * Deriving them here from the SAME public fields — rather than sending a computed total — is also
 * what keeps the two screens from ever disagreeing about the headline numbers.
 *
 * The top discard card is public by the card rules (a discard pile is face-up at a table) and the
 * wire carries the TOP CARD'S NAME ONLY, never the ordered pile — the order would leak play history
 * and expose Samuel Parris' draw pool.
 */

import type { PublicBoardSlice } from '../../store/gameStore';
import type { PublicPlayer } from '../../socket/types';
import { cardArt, TRYAL_BACK, DECK_BACK } from '../../data/cardArt';

/**
 * ⚠ EXACT match, never `includes` — "Not a Witch" CONTAINS "Witch", so a substring test would count
 * every innocent reveal as a witch and make the headline number nonsense. This mirrors
 * HostTableStats.IsWitchLabel, which carries the same warning.
 */
function isWitchLabel(label: string): boolean {
  return label.toLowerCase() === 'witch';
}

export interface TableStats {
  /** Revealed witch CARDS, not witch players — see below. */
  witchesRevealed: number;
  tryalsFlipped: number;
  stillLiving: number;
}

/**
 * ⚠ `witchesRevealed` counts revealed witch CARDS, which is the only reading the public board
 * supports: a player holding two witch cards with one revealed contributes 1 and is still alive
 * (locked decision #5). Do not "fix" this into a player count.
 */
export function deriveStats(players: PublicPlayer[]): TableStats {
  let witchesRevealed = 0;
  let tryalsFlipped = 0;
  let stillLiving = 0;

  for (const p of players ?? []) {
    if (!p) continue;
    if (!p.eliminated) stillLiving += 1;

    const revealed = p.revealedTryals;
    if (!revealed) continue;

    tryalsFlipped += revealed.length;
    for (const label of revealed) {
      if (isWitchLabel(label)) witchesRevealed += 1;
    }
  }

  return { witchesRevealed, tryalsFlipped, stillLiving };
}

function Stat({ label, value, testId }: { label: string; value: number; testId: string }) {
  return (
    <div className="flex flex-col items-center">
      <span className="text-[4.4vh] font-semibold leading-none text-host-bright" data-testid={testId}>
        {value}
      </span>
      <span className="mt-[0.4vh] text-[1.3vh] uppercase tracking-[0.15em] text-host-parchment/70">
        {label}
      </span>
    </div>
  );
}

interface Props {
  /** The STORE slice, not the raw wire payload — that is what the mirror actually holds. */
  state: PublicBoardSlice;
}

export function MeetingHouse({ state }: Props) {
  const stats = deriveStats(state.players);
  const topDiscard = state.topDiscard ?? null;
  const topArt = cardArt(topDiscard);
  const deckCount = state.deckCount ?? 0;
  const discardCount = state.discardCount ?? 0;

  return (
    <section
      className="flex h-full flex-col items-center justify-center gap-[1.6vh] rounded-md border border-host-parchment/15 bg-host-ground/60 px-[1vw] py-[1.2vh]"
      data-testid="meeting-house"
    >
      <h2 className="text-[1.6vh] uppercase tracking-[0.3em] text-host-parchment/60">
        The Meeting House
      </h2>

      {/* Three derived tallies */}
      <div className="flex items-start gap-[2.2vw]">
        <Stat label="Witches Revealed" value={stats.witchesRevealed} testId="stat-witches" />
        <Stat label="Tryals Flipped" value={stats.tryalsFlipped} testId="stat-flipped" />
        <Stat label="Still Living" value={stats.stillLiving} testId="stat-living" />
      </div>

      {/* Draw and discard as actual PILES, matching the host's deck/discard stack images. The
          counts alone read as a scoreboard; the card backs read as a table. */}
      <div className="flex items-start justify-center gap-[3vw]">
        <figure className="flex flex-col items-center gap-[0.5vh]">
          {deckCount > 0 ? (
            <img src={DECK_BACK} alt="" className="h-[11vh] w-auto rounded-[3px] shadow-lg" />
          ) : (
            <div className="h-[11vh] w-[7.5vh] rounded-[3px] border border-dashed border-host-parchment/25" />
          )}
          <figcaption
            className="text-[1.4vh] uppercase tracking-widest text-host-parchment/70"
            data-testid="deck-count"
          >
            Draw · {deckCount}
          </figcaption>
        </figure>

        <figure className="flex flex-col items-center gap-[0.5vh]" data-testid="top-discard">
          {topDiscard && topArt ? (
            <img src={topArt} alt={topDiscard} className="h-[11vh] w-auto rounded-[3px] shadow-lg" />
          ) : (
            <div
              className="h-[11vh] w-[7.5vh] rounded-[3px] border border-dashed border-host-parchment/25"
              data-testid="discard-empty"
            />
          )}
          <figcaption
            className="text-[1.4vh] uppercase tracking-widest text-host-parchment/70"
            data-testid="discard-count"
          >
            Discard · {discardCount}
          </figcaption>
          {topDiscard && (
            <figcaption className="text-[1.2vh] uppercase tracking-wider text-host-parchment/55">
              {topDiscard}
            </figcaption>
          )}
        </figure>
      </div>

      {/* Legend — labels, so the card art is never the only way to read the board */}
      <div className="flex items-center gap-[1.2vw] text-[1.2vh] uppercase tracking-wider text-host-parchment/60">
        <span className="flex items-center gap-[0.3vw]">
          <img src={TRYAL_BACK} alt="" className="h-[2.6vh] w-auto rounded-[1px]" />
          Face Down
        </span>
        <span className="flex items-center gap-[0.3vw]">
          <img src={cardArt('Not a Witch') ?? ''} alt="" className="h-[2.6vh] w-auto rounded-[1px]" />
          Not a Witch
        </span>
        <span className="flex items-center gap-[0.3vw]">
          <img src={cardArt('Witch') ?? ''} alt="" className="h-[2.6vh] w-auto rounded-[1px]" />
          Witch
        </span>
      </div>
    </section>
  );
}
