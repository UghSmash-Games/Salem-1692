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
        public static void Resolve(IRng rng)
        {
            var alive = PlayerService.GetAlivePlayers();
            var witches = alive.Where(p => p.IsWitch && !p.IsEliminated).ToList();

            if (witches.Count == 0) return;

            // Collect votes (AI placeholder: random eligible target)
            var eligible = alive.Where(p => !p.IsEliminated && !p.hasAsylum).ToList();
            if (eligible.Count == 0) return;

            var tally = new Dictionary<Player,int>();
            foreach (var p in eligible) tally[p] = 0;

            foreach (var w in witches)
            {
                // You can bias against other witches here if your rules require
                var t = eligible[rng.NextInt(0, eligible.Count)];
                tally[t]++;
            }

            // Winner; deterministic tie break
            int best = tally.Values.Max();
            var top = tally.Where(kv => kv.Value == best).Select(kv => kv.Key).ToList();
            var victim = top[rng.NextInt(0, top.Count)];

            // Eliminate victim (reveal remaining Tryals)
            victim.EliminateNow();
        }
    }
}