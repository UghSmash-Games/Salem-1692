/**
 * Atmospheric full-screen overlay shown on the mirror during secret phases.
 *
 * The mirror learns the phase from PUBLIC game_state_update (`phase` field),
 * never from a secret prompt — mirrors never receive secret_phase_prompt.
 */

interface Props {
  phase: string | null;
}

const ATMOSPHERE: Record<string, { title: string; sub: string; tint: string }> = {
  night: {
    title: 'Night falls over Salem',
    sub: 'The town sleeps. Dark deeds are afoot…',
    tint: 'from-ink via-ink to-black',
  },
  dawn: {
    title: 'Dawn breaks',
    sub: 'A black cat prowls in the half-light…',
    tint: 'from-ink via-ink to-ember/30',
  },
};

export function NightDawnOverlay({ phase }: Props) {
  const key = phase?.toLowerCase() ?? '';
  const atmosphere = ATMOSPHERE[key];
  if (!atmosphere) return null;

  return (
    <div
      className={`fixed inset-0 z-40 flex flex-col items-center justify-center gap-3 bg-gradient-to-b ${atmosphere.tint} px-6 text-center`}
      data-testid="night-dawn-overlay"
    >
      <div className="animate-pulse text-5xl">🌙</div>
      <h2 className="text-3xl font-semibold text-parchment">{atmosphere.title}</h2>
      <p className="text-parchment/70">{atmosphere.sub}</p>
    </div>
  );
}
