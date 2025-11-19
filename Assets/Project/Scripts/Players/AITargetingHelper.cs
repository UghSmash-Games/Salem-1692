using System.Linq;
using System.Collections.Generic;
using Salem.Data;

namespace Salem.Players
{
    public static class AITargetingHelper
    {
        public static Player SelectRandomTarget(Player self)
        {
            // Get all valid players
            var validTargets = PlayerService.All
                .Where(p => p != self && !p.IsEliminated)
                .ToList();

            if (validTargets.Count == 0)
            {
                UnityEngine.Debug.LogWarning("No valid AI targets available.");
                return null;
            }

            // Randomly pick one
            return validTargets[RNGService.Rng.NextInt(0, validTargets.Count)];
        }

        public static Player SelectSmartAccusationCountTarget(Player self)
        {
            var validTargets = PlayerService.All
                .Where(p => p != self && !p.IsEliminated && !p.hasAsylum)
                .OrderBy(p => p.currentAccusationCount)
                .ToList();

            return validTargets.FirstOrDefault();
        }
    }

}
