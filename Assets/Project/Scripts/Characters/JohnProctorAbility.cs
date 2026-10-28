using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Salem.Cards;
using Salem.Data;
using Salem.Players;

namespace Salem.Characters
{
    /// <summary>
    /// John Proctor (#5): when any player is eliminated, John — or a Martha whose effective ability is
    /// John — takes UP TO 3 cards from the eliminated player's HAND and the rest are discarded
    /// (rulebook-corrected: hand only; status/in-play cards are eliminated separately in
    /// <see cref="Player.OnElimination"/>). When BOTH John and an effectively-John Martha are alive they
    /// draft the hand, taking turns picking ONE card each, John first, capped at 3 each, alternating
    /// until the pool is exhausted; leftovers are discarded.
    ///
    /// Reached via the dispatcher's serialized draft queue, so it may run async (networked picks) and
    /// re-entrant eliminations (matchmaker cascade) are handled one draft at a time. The pool is the
    /// hand that <see cref="Player.OnElimination"/> LEFT IN PLACE because a drafter was alive.
    /// </summary>
    public class JohnProctorAbility : ICharacterAbility, IOnPlayerEliminated
    {
        public TownhallName Name => TownhallName.JohnProctor;

        private const int MaxPicksEach = 3;
        private const float PickTimeoutSeconds = 45f;

        public IEnumerator OnPlayerEliminated(Player dead, EliminationCause cause)
        {
            var dm = UnityEngine.Object.FindFirstObjectByType<Salem.Deck.DeckManager>();

            // Pool = the dead player's HAND. OnElimination left it in place iff a drafter was alive;
            // otherwise it already discarded the hand and this pool is empty (no-op).
            var pool = new List<Card>(dead.HandManager.GetCards());
            if (pool.Count == 0) yield break;
            dead.HandManager.ClearHand(); // ownership moves into the local pool now

            // Drafters alive NOW (recomputed at draft time — a drafter may have died in a cascade since
            // the elimination), John FIRST.
            var drafters = BuildDrafters(dead);
            if (drafters.Count == 0)
            {
                // Every drafter died before the draft ran (e.g. a matchmaker cascade took John). The
                // hand OnElimination left dangling is discarded here — the load-bearing orphan fallback.
                foreach (var c in pool) if (dm != null) dm.AddToDiscardPile(c);
                yield break;
            }

            var taken = new Dictionary<Player, int>();
            foreach (var d in drafters) taken[d] = 0;
            // A drafter who voluntarily stops early (the "up to three" Done button) is parked here so the
            // alternation SKIPS them while the OTHER drafter keeps picking. Card text: "choose UP TO three
            // cards to take" — taking fewer is a real, rulebook-granted choice (same shape as Samuel
            // Parris' "up to 2").
            var stopped = new HashSet<Player>();

            // Alternate one pick at a time, John first, until the pool empties or every drafter has either
            // reached 3 or chosen to stop.
            int turn = 0;
            while (pool.Count > 0 && drafters.Any(d => taken[d] < MaxPicksEach && !stopped.Contains(d)))
            {
                var drafter = drafters[turn % drafters.Count];
                turn++;
                if (taken[drafter] >= MaxPicksEach || stopped.Contains(drafter)) continue; // done — pass

                int idx = 0;
                if (drafter is AIPlayer)
                {
                    idx = 0; // AI drafters always take the top (cards are pure advantage — never decline)
                }
                else
                {
                    int picked = -1;
                    yield return drafter.Input.RequestCardPick(
                        drafter, pool, taken[drafter] + 1, MaxPicksEach, PickTimeoutSeconds,
                        allowDone: true, // "up to three" — the Done button lets a human drafter stop early
                        reason: "proctor_draft",
                        chosen => picked = chosen);

                    // Negative = Done (-1 from the Done button) OR no submit before the timeout. For an
                    // "up to N" pick both mean "this drafter takes no more" (same semantics as Parris);
                    // the draft still resolves and the other drafter keeps going.
                    if (picked < 0) { stopped.Add(drafter); continue; }
                    idx = picked < pool.Count ? picked : 0; // defensive: out-of-range → take the top
                }

                var card = pool[idx];
                pool.RemoveAt(idx);
                drafter.HandManager.AddCard(card);
                taken[drafter]++;
            }

            // Take up to 3, discard the rest.
            foreach (var c in pool) if (dm != null) dm.AddToDiscardPile(c);
        }

        /// <summary>
        /// Living drafters in pick order: the real John first, then any Martha whose effective ability
        /// is John. (A Martha is only effectively-John while a John lives to her right, so the real John
        /// is always present whenever a Martha qualifies.)
        /// </summary>
        private static List<Player> BuildDrafters(Player dead)
        {
            var alive = PlayerService.GetAlivePlayers();
            var result = new List<Player>();

            var realJohn = alive.FirstOrDefault(p => p != dead && p.townhallCard != null &&
                                                     p.townhallCard.CardName == TownhallName.JohnProctor);
            if (realJohn != null) result.Add(realJohn);

            foreach (var p in alive)
                if (p != dead && p != realJohn && p.townhallCard != null &&
                    p.townhallCard.CardName == TownhallName.MarthaCorey &&
                    p.GetEffectiveTownHallName() == TownhallName.JohnProctor)
                    result.Add(p);

            return result;
        }
    }
}
