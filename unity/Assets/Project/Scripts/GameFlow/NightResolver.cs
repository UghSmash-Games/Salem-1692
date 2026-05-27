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
       public class NightPlan
        {
            public Player ConstableTarget { get; set; }
            public Dictionary<Player, Player> WitchVotes { get; } = new();

            public void SetWitchVote(Player witch, Player target)
            {
                if (witch == null || target == null) return;
                WitchVotes[witch] = target;
            }
        }

        public static void Resolve(IRng rng, NightPlan plan = null, bool witchesCanTargetWitches = false)
        {
            plan ??= new NightPlan();

            var alive   = PlayerService.GetAlivePlayers();
            var witches = alive.Where(p => p.IsWitch && !p.IsEliminated).ToList();
            if (witches.Count == 0) return;

            // Eligible = alive, not eliminated, not protected by Asylum
            var eligible = alive.Where(p => !p.IsEliminated && !p.hasAsylum).ToList();

            // (Optional) If witches cannot target witches, filter them out here:
            if (!witchesCanTargetWitches)
                eligible = eligible.Where(p => !p.IsWitch).ToList();

            if (eligible.Count == 0) return;

            // Tally votes (manual overrides where provided; otherwise deterministic RNG)
            var tally = eligible.ToDictionary(p => p, _ => 0);
            foreach (var w in witches)
            {
                Player target = null;

                if (plan.WitchVotes.TryGetValue(w, out var planned) && planned != null && eligible.Contains(planned))
                    target = planned;

                if (target == null)
                    target = eligible[RNGService.Rng.NextInt(0, eligible.Count)];

                tally[target]++;
            }

            // Winner with deterministic tie-break
            int best = tally.Values.Max();
            var top  = tally.Where(kv => kv.Value == best).Select(kv => kv.Key).ToList();
            var victim = top[rng.NextInt(0, top.Count)];

            if (plan.ConstableTarget != null && victim == plan.ConstableTarget)
            {
                Debug.Log($"[NightResolver] Constable protected {victim.PlayerNameText}. No elimination tonight.");
                return;
            }

            // Eliminate victim (reveal remaining Tryals)
            //victim.EliminateNow();
            PlayerService.Eliminate(victim, EliminationCause.NightKill);
        }
    }
}