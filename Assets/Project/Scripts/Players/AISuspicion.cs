using System.Collections.Generic;
using System.Linq;
using Salem.Cards;
using Salem.Data;
using Salem.GameFlow;
using UnityEngine;

namespace Salem.Players
{
    /// <summary>
    /// A ledger of who has done what to whom, and the suspicion an AI seat derives from it.
    ///
    /// 🔴 EVERYTHING IN HERE IS PUBLIC INFORMATION. The ledger is built from exactly one event —
    /// <see cref="CardEffectManager.OnCardPlayedDetail"/> — which fires on a successful play and
    /// carries (who played it, which card, on whom). Every human at the table watches those same
    /// three facts: the card is announced, the red cards sit face-up in front of the target, and the
    /// event log carries it to every phone and mirror. So this is not a back channel; it is the AI
    /// finally *remembering* what everyone else already saw.
    ///
    /// ⚠️ That is the whole constraint, and it is what makes a suspicion model safe to build at all.
    /// A model fed from <c>TryalCards</c> or another seat's <c>IsWitch</c> would be indistinguishable
    /// from this one in behaviour — right up to the point it started being eerily correct — so the
    /// rule in <see cref="AIKnowledge"/> applies in full here. See that file's banner.
    ///
    /// **The model.** Salem's social read is that witches attack townspeople and shield each other.
    /// So an AI asks one question about every other seat: *is this person acting against me and mine?*
    ///   • Harm aimed at me is the strongest signal — a townsperson knows for certain that **it**
    ///     is not a witch, so pressure on itself is more likely malice than deduction.
    ///   • Harm aimed at a KNOWN ALLY counts too, which is what makes a witch's read different in
    ///     kind rather than just better: she knows her coven, so she can see who is hunting it.
    ///   • Help aimed at me or mine counts the other way.
    ///   • One association term: helping someone who is attacking me is itself mildly damning.
    ///
    /// A townsperson's allies set is just itself, so it gets the honest, limited read a person in
    /// that seat would have — "who keeps coming after me" — and nothing more.
    /// </summary>
    public static class AISuspicion
    {
        // actor → subject → total weight of hostile acts (and a parallel table for helpful ones).
        // Keyed by Player reference, so it MUST be cleared between games: see ResetForNewGame.
        private static readonly Dictionary<Player, Dictionary<Player, float>> _harm = new();
        private static readonly Dictionary<Player, Dictionary<Player, float>> _help = new();

        // ─── Act weighting ────────────────────────────────────────

        /// <summary>
        /// How hostile a card is toward the player it is played on, 0 for cards that are not.
        /// Weighted by how much the act actually costs the target: a Witness is seven accusations
        /// and a far louder statement of intent than a single Accusation.
        /// </summary>
        private static float HarmWeight(ActionOp op) => op switch
        {
            ActionOp.Witness => 3f,
            ActionOp.Evidence => 2f,
            ActionOp.Accusation => 1f,
            ActionOp.Arson => 2f,
            ActionOp.Robbery => 2f,
            ActionOp.Curse => 2f,
            ActionOp.Stocks => 1.5f,
            ActionOp.BlackCat => 2f,
            ActionOp.Matchmaker => 0.5f,
            _ => 0f,
        };

        /// <summary>How helpful a card is toward the player it is played on. Mirrors
        /// <see cref="AIKnowledge.IsBeneficialToTarget"/> — these are the three gifts.</summary>
        private static float HelpWeight(ActionOp op) => op switch
        {
            ActionOp.Alibi => 2f,
            ActionOp.Asylum => 2f,
            ActionOp.Piety => 2f,
            _ => 0f,
        };

        // ─── Recording ────────────────────────────────────────────

        private static bool _hooked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Hook()
        {
            // -= before += so an Editor domain reload cannot leave two subscriptions counting every
            // card twice. The event is static and so is this class, so there is no scene object to
            // leak; the DICTIONARIES are the thing that needs clearing, and ResetForNewGame does it.
            CardEffectManager.OnCardPlayedDetail -= Record;
            CardEffectManager.OnCardPlayedDetail += Record;
            _hooked = true;
        }

        private static void Record(Player source, Card card, Player target)
        {
            if (!_hooked) return;
            if (source == null || target == null || source == target) return;
            if (card is not ActionCardSO action) return;

            // ⚠ Only the PRIMARY target is recorded, because that is all the event carries. Robbery
            // and Scapegoat also have a recipient, and that recipient's gain is public too — but
            // adding it would mean widening OnCardPlayedDetail, whose narrow shape is a privacy
            // guarantee elsewhere (see GameEventEmitter). The primary target is the loud half of
            // both cards anyway: the robbed player and the one buried in someone else's accusations.
            float harm = HarmWeight(action.Op);
            if (harm > 0f) Add(_harm, source, target, harm);

            float help = HelpWeight(action.Op);
            if (help > 0f) Add(_help, source, target, help);
        }

        private static void Add(Dictionary<Player, Dictionary<Player, float>> table,
                                Player actor, Player subject, float amount)
        {
            if (!table.TryGetValue(actor, out var row))
            {
                row = new Dictionary<Player, float>();
                table[actor] = row;
            }
            row.TryGetValue(subject, out float had);
            row[subject] = had + amount;
        }

        private static float Get(Dictionary<Player, Dictionary<Player, float>> table,
                                 Player actor, Player subject) =>
            actor != null && subject != null
            && table.TryGetValue(actor, out var row)
            && row.TryGetValue(subject, out float v) ? v : 0f;

        /// <summary>
        /// Forget the finished game. Called by AppLifecycle.RestartGame before the scene reloads.
        ///
        /// ⚠ Not optional. These tables key on <see cref="Player"/> references, so without this they
        /// would hold destroyed seats from the previous game alive, and a new game would start with
        /// grudges nobody at the table had earned.
        /// </summary>
        public static void ResetForNewGame()
        {
            _harm.Clear();
            _help.Clear();
        }

        // ─── Reading ──────────────────────────────────────────────

        /// <summary>
        /// How much <paramref name="self"/> should believe <paramref name="other"/> is working
        /// against it. Positive is hostile, negative is friendly, zero is "no evidence either way".
        ///
        /// Deliberately unbounded and unnormalized: callers turn it into a weight, and clamping here
        /// would throw away the difference between one stray accusation and a sustained campaign.
        /// </summary>
        public static float Hostility(Player self, Player other)
        {
            if (self == null || other == null || self == other) return 0f;

            float score = 0f;

            // Acts against me. The heaviest term, because this is the one judgement a seat can make
            // from a position of certainty — it knows its own role.
            score += Get(_harm, other, self) * 1.0f;
            score -= Get(_help, other, self) * 0.8f;

            // Acts against people I KNOW are on my side. Empty for every townsperson; for a witch
            // after dawn, this is how she spots whoever is hunting her coven.
            foreach (var ally in PlayerService.All)
            {
                if (ally == null || ally == self || !AIKnowledge.IsKnownAlly(self, ally)) continue;
                score += Get(_harm, other, ally) * 0.6f;
                score -= Get(_help, other, ally) * 0.5f;
            }

            // Association: propping up someone who is coming after me reads as coordination. Weak on
            // purpose — at a real table this is a hunch, not a tell, and an AI that treated it as
            // proof would chase phantoms.
            foreach (var third in PlayerService.All)
            {
                if (third == null || third == self || third == other) continue;
                if (Get(_harm, third, self) <= 0f) continue;
                score += Get(_help, other, third) * 0.3f;
            }

            return score;
        }

        /// <summary>
        /// Pick from <paramref name="pool"/>, preferring high scores but never settling into the
        /// single "best" answer.
        ///
        /// ⚠ The randomness is a feature, not a concession. An AI that always played the optimum
        /// would be readable after two rounds — the table would learn that being targeted means the
        /// AI has a reason — and it would also claim a confidence the public board does not support.
        /// Every legal target keeps a real chance via <paramref name="floor"/>.
        /// </summary>
        public static Player WeightedPick(IReadOnlyList<Player> pool, System.Func<Player, float> score,
                                          float floor = 1f)
        {
            if (pool == null || pool.Count == 0) return null;
            if (pool.Count == 1) return pool[0];

            var weights = new float[pool.Count];
            float total = 0f;
            for (int i = 0; i < pool.Count; i++)
            {
                weights[i] = Mathf.Max(floor, floor + score(pool[i]));
                total += weights[i];
            }

            if (total <= 0f) return pool[RNGService.Rng.NextInt(0, pool.Count)];

            // NextInt is integer-only, so roll in thousandths rather than reaching for a second RNG.
            float roll = RNGService.Rng.NextInt(0, 1000) / 1000f * total;
            for (int i = 0; i < pool.Count; i++)
            {
                roll -= weights[i];
                if (roll <= 0f) return pool[i];
            }
            return pool[pool.Count - 1];
        }

        /// <summary>Debug aid: the table as this seat reads it, most-suspected first.</summary>
        public static string Describe(Player self)
        {
            if (self == null) return "(no seat)";
            var rows = PlayerService.All
                .Where(p => p != null && p != self && !p.IsEliminated)
                .OrderByDescending(p => Hostility(self, p))
                .Select(p => $"{p.PlayerNameText} {Hostility(self, p):+0.0;-0.0;0}");
            return $"{self.PlayerNameText} suspects: {string.Join(", ", rows)}";
        }
    }
}
