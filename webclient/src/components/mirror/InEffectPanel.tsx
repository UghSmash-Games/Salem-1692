/**
 * InEffectPanel — every persistent card currently in play, with its holder and rules text.
 * Browser port of HostInEffectPanel.
 *
 * Everything comes from the PUBLIC board: `statusCards` is already broadcast per player. The rules
 * text is static copy (see cardDescriptions.ts), so this panel adds nothing to the wire.
 *
 * ⚠️ SKIPS ELIMINATED PLAYERS, matching the host. Their cards are discarded on elimination so
 * `statusCards` should already be empty — the guard is belt-and-braces, not a behaviour change, and
 * keeping it means the two screens list the same rows even if a broadcast arrives mid-elimination.
 */

import type { PublicBoardSlice } from '../../store/gameStore';
import type { PublicPlayer } from '../../socket/types';
import { cardDescription, cardAccentClass } from '../../data/cardDescriptions';

export interface EffectRow {
  card: string;
  holder: string;
}

/** One row per persistent card in play, in board order. */
export function buildEffectRows(players: PublicPlayer[]): EffectRow[] {
  const rows: EffectRow[] = [];
  for (const p of players ?? []) {
    if (!p?.statusCards) continue;
    if (p.eliminated) continue;
    for (const card of p.statusCards) {
      if (!card) continue;
      rows.push({ card, holder: (p.displayName ?? '').toUpperCase() });
    }
  }
  return rows;
}

interface Props {
  state: PublicBoardSlice;
}

export function InEffectPanel({ state }: Props) {
  const rows = buildEffectRows(state.players);

  return (
    <section className="flex flex-col gap-[0.6vh]" data-testid="in-effect-panel">
      <h2 className="text-[1.7vh] uppercase tracking-[0.3em] text-host-parchment/60">In Effect</h2>

      {rows.length === 0 ? (
        <p className="text-[1.5vh] italic text-host-parchment/40" data-testid="in-effect-empty">
          Nothing in play.
        </p>
      ) : (
        <ul className="flex flex-col gap-[0.5vh]">
          {rows.map((r, i) => (
            <li
              key={`${r.holder}-${r.card}-${i}`}
              className="relative overflow-hidden rounded-[3px] bg-host-parchment/5 py-[0.7vh] pl-[0.8vw] pr-[0.6vw]"
              data-testid="in-effect-row"
            >
              {/* The accent bar is decorative — the card NAME below carries the meaning, so this
                  never depends on telling one colour from another. */}
              <span className={`absolute inset-y-0 left-0 w-[5px] ${cardAccentClass(r.card)}`} />
              <p className="text-[2vh] font-semibold text-host-bright" data-testid="in-effect-card">
                {r.card}
              </p>
              <p className="text-[1.4vh] uppercase tracking-wider text-host-parchment/60">
                {r.holder}
              </p>
              {cardDescription(r.card) && (
                <p className="mt-[0.3vh] text-[1.4vh] leading-snug text-host-parchment/75">
                  {cardDescription(r.card)}
                </p>
              )}
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
