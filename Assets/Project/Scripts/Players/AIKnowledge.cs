using System.Collections.Generic;
using System.Linq;
using Salem.Cards;
using Salem.Data;
using Salem.GameFlow;

namespace Salem.Players
{
    /// <summary>
    /// What an AI seat is allowed to know, and the choices it makes from that.
    ///
    /// 🔴 THE AI MUST NOT CHEAT. It runs inside the host, so every tryal card, every role and the
    /// deck order are all one property access away — and an AI that quietly read them would be both
    /// unbeatable and undetectable, which in a social deduction game is the worst possible bug. It is
    /// not enough for the AI to *play* fairly; it has to be structurally unable to see what a player
    /// in its seat could not.
    ///
    /// So an AI may use exactly three things:
    ///   1. The PUBLIC board — accusation counts, who is eliminated, cards played in front of people.
    ///   2. Its OWN private state — its hand, its tryals, its role.
    ///   3. Fellow-witch identities, IF it is a witch AND the witches have been revealed to each
    ///      other at dawn. That is knowledge the rulebook grants; see <see cref="KnowsFellowWitches"/>.
    ///
    /// ⚠️ Nothing here may read another seat's `TryalCards`, another seat's `IsWitch` outside the
    /// fellow-witch rule, another seat's hand, or the deck. If a future heuristic needs one of those,
    /// it is the heuristic that is wrong.
    ///
    /// **What this fixes.** The AI was not merely unsophisticated, it played AGAINST ITSELF: every
    /// target was uniform random, so a witch regularly voted to kill its own partner at night, and
    /// any seat might hand Asylum or Piety — cards that purely benefit the recipient — to the player
    /// it was trying to beat. Those read to a human as the game being broken, not as a weak opponent.
    /// Making a mistake a person could also make is fine; a witch murdering her own coven is not.
    /// </summary>
    public static class AIKnowledge
    {
        /// <summary>
        /// Cards whose whole effect HELPS whoever they are played on: Alibi removes accusations,
        /// Asylum grants night immunity, Piety doubles the reveal threshold. Playing one of these on
        /// an opponent is a gift, which is exactly what random targeting kept doing.
        /// </summary>
        public static bool IsBeneficialToTarget(ActionOp op) =>
            op == ActionOp.Alibi || op == ActionOp.Asylum || op == ActionOp.Piety;

        /// <summary>
        /// True once this AI is a witch AND dawn has revealed the witches to each other.
        ///
        /// ⚠ The timing gate is the point. Before dawn a witch does not know her coven, so letting
        /// the AI act on it earlier would be reading the future — the same class of cheat as reading
        /// a tryal card, just harder to notice.
        /// </summary>
        public static bool KnowsFellowWitches(Player self) =>
            self != null && self.IsWitch &&
            GamePhaseManager.Instance != null && GamePhaseManager.Instance.WitchesRevealed;

        /// <summary>Another witch this AI legitimately knows about. False for every non-witch AI.</summary>
        public static bool IsKnownAlly(Player self, Player other) =>
            other != null && other != self && KnowsFellowWitches(self) && other.IsWitch;

        /// <summary>Alive seats other than this one — the base set every choice narrows.</summary>
        public static List<Player> LegalTargets(Player self) =>
            PlayerService.All.Where(p => p != null && p != self && !p.IsEliminated).ToList();

        /// <summary>
        /// Someone to do something unpleasant to: an accusation, Stocks, Arson, a Curse.
        ///
        /// Only one rule, deliberately — never a known ally. Beyond that it stays random, because a
        /// townsperson genuinely cannot tell a witch from a neighbour, and an AI that always picked
        /// the "best" target would be reading something it should not have.
        /// </summary>
        public static Player PickHarmfulTarget(Player self)
        {
            var pool = LegalTargets(self).Where(p => !IsKnownAlly(self, p)).ToList();
            return Random(pool);
        }

        /// <summary>
        /// Someone to help. A witch helps her coven; anyone else helps whoever is closest to being
        /// revealed, which is the one coherent reading available from public information alone.
        ///
        /// A townsperson handing Piety to a witch is still possible here, and that is correct — the
        /// information to avoid it does not exist in that seat. It is a mistake a person would make.
        /// </summary>
        public static Player PickBeneficialTarget(Player self)
        {
            var pool = LegalTargets(self);
            if (pool.Count == 0) return null;

            var allies = pool.Where(p => IsKnownAlly(self, p)).ToList();
            if (allies.Count > 0) return MostAccused(allies);

            return MostAccused(pool);
        }

        /// <summary>
        /// The witches' night victim.
        ///
        /// 🔴 Never a fellow witch. This is the single worst thing the old random pick did: in a
        /// five-player game with two witches, an AI witch voted to murder her own partner roughly a
        /// quarter of the time, and the table watched the witches lose to themselves.
        /// </summary>
        public static Player PickNightVictim(Player self)
        {
            var pool = LegalTargets(self).Where(p => !IsKnownAlly(self, p)).ToList();
            return Random(pool);
        }

        /// <summary>
        /// Where the black cat goes at dawn. Never onto a fellow witch — its holder reveals a tryal
        /// when conspiracy is drawn, which is a cost, not a gift.
        /// </summary>
        public static Player PickBlackCatTarget(Player self)
        {
            var pool = LegalTargets(self).Where(p => !IsKnownAlly(self, p)).ToList();
            return Random(pool);
        }

        /// <summary>
        /// Who the constable protects. Public information only — the constable has no idea who the
        /// witches are, so it shields whoever is under the most public pressure.
        ///
        /// ⚠ Never itself: the rulebook forbids it (p7) and the host voids a self-pick anyway, which
        /// would silently waste the gavel.
        /// </summary>
        public static Player PickProtectTarget(Player self) => MostAccused(LegalTargets(self));

        // ─── helpers ──────────────────────────────────────────────

        /// <summary>Most publicly accused, ties broken randomly so the AI is not predictable.</summary>
        private static Player MostAccused(List<Player> pool)
        {
            if (pool == null || pool.Count == 0) return null;

            int best = pool.Max(p => p.currentAccusationCount);
            return Random(pool.Where(p => p.currentAccusationCount == best).ToList());
        }

        private static Player Random(List<Player> pool) =>
            pool == null || pool.Count == 0 ? null : pool[RNGService.Rng.NextInt(0, pool.Count)];
    }
}
