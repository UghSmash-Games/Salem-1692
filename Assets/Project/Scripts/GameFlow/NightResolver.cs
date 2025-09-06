/*
* AUTHOR: Ron Bresett
* REFERENCES:
* NOTES:
*   Primary Purpose: Resolves logic when cards are played.
*   Responsibilities:
*        • Interpret effect type
*        • Trigger gameplay consequences
*   Access Requirements:
*        • DeckManager
*        • Player
*        • GameStateManager

* TODO: [Planned improvements]
* FIXME: [Known bugs or issues]
*/

using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using Salem.Data;
using Salem.Players;

namespace Salem.GameFlow
{
    public static class NightResolver
    {
        public static void Resolve(IRng rng, bool witchesCanTargetWitches = false)
        {
            var alive   = PlayerService.GetAlivePlayers();
            var witches = alive.Where(p => p.IsWitch && !p.IsEliminated).ToList();
            if (witches.Count == 0) return;

            // Eligible = alive, not eliminated, not protected by Asylum
            var eligible = alive.Where(p => !p.IsEliminated && !p.hasAsylum).ToList();

            // (Optional) If witches cannot target witches, filter them out here:
            if (!witchesCanTargetWitches)
                eligible = eligible.Where(p => !p.IsWitch).ToList();

            if (eligible.Count == 0) return;

            // Tally random votes (deterministic via IRng)
            var tally = eligible.ToDictionary(p => p, _ => 0);
            foreach (var w in witches)
            {
                var t = eligible[rng.NextInt(0, eligible.Count)];
                tally[t]++;
            }

            // Winner with deterministic tie-break
            int best = tally.Values.Max();
            var top  = tally.Where(kv => kv.Value == best).Select(kv => kv.Key).ToList();
            var victim = top[rng.NextInt(0, top.Count)];

            // Eliminate victim (reveal remaining Tryals)
            victim.EliminateNow();
        }
    }
}