using System.Collections;
using UnityEngine;

namespace Salem.Players
{
    /// <summary>
    /// How long an AI seat appears to think.
    ///
    /// **Why this exists.** An AI turn resolved in a single 1.5s beat: the seat lit up, a card
    /// appeared, the turn was gone. With three or four AI seats that is a blur — nobody can read
    /// what was played, let alone form an opinion about it, and a social deduction game played too
    /// fast to follow is a game nobody can deduce anything in. The pause is not decoration; the
    /// table needs time to SEE the move.
    ///
    /// 🔴 REALTIME, ALWAYS. <c>pauseOnGameEnd</c> sets <c>Time.timeScale = 0</c>, and a plain
    /// <c>WaitForSeconds</c> under a zero timescale never returns — an AI mid-pause when the game
    /// ended would hang its coroutine forever. The synchronized-reveal code waits in realtime for
    /// exactly this reason; so does everything here.
    ///
    /// ⚠ The jitter is MULTIPLICATIVE on the caller's base delay, which keeps `0` meaning `0` — the
    /// autopilot debug harness passes zero to run games as fast as it can, and that must not become
    /// "zero, plus a second".
    ///
    /// ⚠ Uses <see cref="UnityEngine.Random"/>, NOT the seeded game RNG. These are cosmetic draws;
    /// spending the gameplay stream on them would mean two runs from the same seed dealt different
    /// cards because an AI paused for a different fraction of a second.
    /// </summary>
    public static class AIPacing
    {
        /// <summary>The long pause before an AI commits to anything — reading the board.</summary>
        public static IEnumerator Think(float baseSeconds)
        {
            if (baseSeconds <= 0f) yield break;
            yield return new WaitForSecondsRealtime(baseSeconds * Random.Range(1.2f, 2.2f));
        }

        /// <summary>
        /// A short second pause after the decision and before the card lands, so a turn reads as
        /// two moments — considering, then acting — instead of one lump of dead time.
        /// </summary>
        public static IEnumerator Beat(float baseSeconds)
        {
            if (baseSeconds <= 0f) yield break;
            yield return new WaitForSecondsRealtime(baseSeconds * Random.Range(0.25f, 0.55f));
        }
    }
}
