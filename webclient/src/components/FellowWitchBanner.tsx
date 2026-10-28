import { useGameStore } from '../store/gameStore';

/**
 * Witch-only private banner showing the player's fellow witches. Rendered on
 * every witch-facing screen (Idle, Action, SecretPhase) so a witch can always
 * see their allies. Renders nothing for non-witches (and before the dawn reveal),
 * so it carries witch-private data on the private channel only — never broadcast.
 */
export function FellowWitchBanner() {
  const role = useGameStore((s) => s.privateState.role);
  const fellowWitches = useGameStore((s) => s.privateState.fellowWitches);

  if (role !== 'witch' || fellowWitches.length === 0) return null;

  return (
    <section
      className="flex flex-col gap-1 rounded-md border border-ember/50 bg-ember/10 px-3 py-2"
      data-testid="fellow-witches"
    >
      <h3 className="text-xs uppercase tracking-wider text-ember">
        Your fellow witches
      </h3>
      <p className="text-sm text-parchment">{fellowWitches.join(', ')}</p>
    </section>
  );
}
