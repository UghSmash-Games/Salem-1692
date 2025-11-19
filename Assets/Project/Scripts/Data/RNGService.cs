/*
* AUTHOR: Ron Bresett
* REFERENCES:
* NOTES:
* TODO: [Planned improvements]
* FIXME: [Known bugs or issues]
*/

using System;

namespace Salem.Data
{
    /// <summary>
    /// Provides a globally accessible pseudo-random number generator
    /// implementation used across the project. This helps decouple
    /// randomness from UnityEngine.Random for easier testing and
    /// future determinism support.
    /// </summary>
    public static class RNGService
    {
        private static readonly IRng rng = new XorShiftRng((ulong)DateTime.UtcNow.Ticks);
        public static IRng Rng => rng;
    }
}
