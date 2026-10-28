import { useGameStore } from '../store/gameStore';

interface Props {
  /** This player's own tentative pick (local state), or null if none yet. */
  ownPick: string | null;
}

/**
 * The live tally region of the secret-phase screen. STRUCTURALLY identical for
 * every player: everyone sees their own tentative pick ("You → X"). Witches
 * additionally see fellow witches' live tentative picks (from private_state
 * witchVotes); non-witches' witchVotes is always empty, so they see only their
 * own line. The differing lines are witch-private data (same class as tryals),
 * never broadcast — the control structure is the same for all.
 */
export function AllyTallyPanel({ ownPick }: Props) {
  const witchVotes = useGameStore((s) => s.privateState.witchVotes);

  return (
    <section
      className="flex flex-col gap-1 rounded-md border border-parchment/20 bg-ink/40 px-3 py-2"
      data-testid="ally-tally"
    >
      <p className="text-sm text-parchment">You → {ownPick ?? '—'}</p>
      {witchVotes.map((v) => (
        <p key={v.witch} className="text-sm text-parchment/80">
          {v.witch} → {v.target ? v.target : '—'}
        </p>
      ))}
    </section>
  );
}
