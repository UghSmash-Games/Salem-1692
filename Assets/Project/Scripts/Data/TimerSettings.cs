using System;

namespace Salem.Data
{
    /// <summary>
    /// The host-owned pace setting: ONE multiplier applied to every player-facing deadline in the
    /// game. Chosen in the lobby ("for players who need more time", Phase 9).
    ///
    /// 🔴 A SINGLE GLOBAL MULTIPLIER IS THE MASKING-SAFE SHAPE, AND THE ONLY ONE.
    /// Phase 4c fixed a real timing leak by making every secret-phase window a SHARED deadline: an
    /// observer must not be able to tell who acted from how long the phase took. A per-player or
    /// per-accessibility-profile timer would reintroduce exactly that leak — the phase would end
    /// when the slowest ACTING player finished, or one player's phone would behave differently from
    /// the rest.
    /// ⛔ NEVER add a per-player override, and never let this value vary by role, by seat, or by
    /// which prompt is showing. It scales the window for EVERYONE or not at all.
    ///
    /// LOCKED ONCE THE GAME BEGINS. Changing a window mid-phase would move a deadline players are
    /// already racing, and could resolve a round early. NetworkGameCoordinator.StartGame locks it.
    /// </summary>
    public static class TimerSettings
    {
        public enum Pace
        {
            Normal,
            Relaxed,
            Extended,
        }

        /// <summary>Multiplier per pace. Deliberately coarse — three legible choices on a TV beat a
        /// slider nobody can read across a room.</summary>
        public static float MultiplierFor(Pace pace) => pace switch
        {
            Pace.Relaxed => 1.5f,
            Pace.Extended => 2f,
            _ => 1f,
        };

        public static Pace Current { get; private set; } = Pace.Normal;

        /// <summary>True once the game has begun; further changes are refused.</summary>
        public static bool Locked { get; private set; }

        /// <summary>Raised when the pace changes, so the lobby panel can repaint.</summary>
        public static event Action OnChanged;

        public static float Multiplier => MultiplierFor(Current);

        /// <summary>
        /// Scale a configured window. Call this at the point the deadline is USED, not where it is
        /// declared, so the serialized Inspector values keep meaning "the Normal-pace duration".
        /// </summary>
        public static float Scale(float seconds) => seconds * Multiplier;

        public static void SetPace(Pace pace)
        {
            if (Locked || Current == pace) return;
            Current = pace;
            OnChanged?.Invoke();
        }

        /// <summary>Called by NetworkGameCoordinator.StartGame — see the locking note above.</summary>
        public static void Lock() => Locked = true;

        /// <summary>
        /// Statics survive a domain reload in the Editor and would leak a previous game's pace (and
        /// its lock) into the next one. Called when a new lobby starts.
        /// </summary>
        public static void ResetForNewGame()
        {
            Locked = false;
            Current = Pace.Normal;
            OnChanged?.Invoke();
        }
    }
}
