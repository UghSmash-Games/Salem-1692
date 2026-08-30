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

using System.Collections.Generic;
using System.Linq;
using Salem.Data;
using Salem.Players;
using UnityEngine;

namespace Salem.GameFlow
{
    public static class NightResolver
    {
       public class NightPlan
        {
            public Player ConstableTarget { get; set; }
            public Dictionary<Player, Player> WitchVotes { get; } = new();
            public HashSet<Player> Confessors { get; } = new();

            public void SetWitchVote(Player witch, Player target)
            {
                if (witch == null || target == null) return;
                WitchVotes[witch] = target;
            }
        }

        /// <summary>
        /// Outcome of a night resolution. Resolve no longer eliminates directly — it
        /// computes WHO the night targets and whether they die, so the caller can run
        /// the synchronized reveal (phase_resolve) at the elimination site.
        /// </summary>
        public struct NightOutcome
        {
            public Player Victim;        // targeted player, or null if no kill resolved
            public bool Eliminated;      // true → caller reveals tryals + eliminates
            public string SavedByLabel;  // "constable" / "confession" / "" when saved
        }

        public static NightOutcome Resolve(IRng rng, NightPlan plan = null, bool witchesCanTargetWitches = false)
        {
            plan ??= new NightPlan();

            var alive   = PlayerService.GetAlivePlayers();
            var witches = alive.Where(p => p.IsWitch && !p.IsEliminated).ToList();
            if (witches.Count == 0) return default;   // no kill resolved

            // Eligible = alive, not eliminated, not protected by Asylum
            var eligible = alive.Where(p => !p.IsEliminated && !p.hasAsylum).ToList();

            // (Optional) If witches cannot target witches, filter them out here:
            if (!witchesCanTargetWitches)
                eligible = eligible.Where(p => !p.IsWitch).ToList();

            if (eligible.Count == 0) return default;  // no eligible target

            // Tally votes (manual overrides where provided; otherwise deterministic RNG)
            var tally = eligible.ToDictionary(p => p, _ => 0);
            foreach (var w in witches)
            {
                Player target = null;

                if (plan.WitchVotes.TryGetValue(w, out var planned) && planned != null && eligible.Contains(planned))
                    target = planned;

                // Safety net only. As of Phase 4b every witch's vote is collected over
                // the network (GamePhaseManager.NightPhaseRoutine round 1), so WitchVotes
                // is fully populated and this random fallback is not hit in normal play —
                // it remains for an un-recorded/ineligible vote (e.g. pre-timeout edge).
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
                return new NightOutcome { Victim = victim, Eliminated = false, SavedByLabel = "constable" };
            }

            if (plan.Confessors.Contains(victim))
            {
                Debug.Log($"[NightResolver] {victim.PlayerNameText} confessed and is saved from the night kill.");
                return new NightOutcome { Victim = victim, Eliminated = false, SavedByLabel = "confession" };
            }

            // Victim dies. The caller performs the tryal reveal + elimination inside the
            // synchronized-reveal window (phase_resolve) so host + mirrors animate together.
            return new NightOutcome { Victim = victim, Eliminated = true };
        }
    }
}