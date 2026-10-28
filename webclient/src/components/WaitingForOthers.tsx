/**
 * The post-submission confirmation state, shared by every secret phase.
 *
 * CRITICAL: This is shown identically to acting and non-acting players, with
 * identical timing. It must never branch on role or the acting flag. A fast
 * submitter and a discarded submitter see exactly this same screen.
 */

export function WaitingForOthers() {
  return (
    <div
      className="flex flex-col items-center gap-4 text-center"
      data-testid="waiting-for-others"
    >
      <div className="h-10 w-10 animate-spin rounded-full border-4 border-parchment/30 border-t-candle" />
      <p className="text-lg text-parchment">Waiting for others…</p>
      <p className="text-sm text-parchment/60">Your choice has been recorded.</p>
    </div>
  );
}
