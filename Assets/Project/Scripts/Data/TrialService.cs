using Salem.Cards;
using Salem.Players;

namespace Salem.Data
{
    public static class TrialService
    {
        public static void OnTrialCardRevealed(Player owner, TryalCard revealedCard)
        {
            if (IsWitchCard(revealedCard))
            {
                PlayerService.Eliminate(owner, EliminationCause.WitchTrialRevealed);
                return;
            }

            if (RevealedAllTrials(owner))
            {
                PlayerService.Eliminate(owner, EliminationCause.AllTrialsRevealed);
                return;
            }
        }

        private static bool RevealedAllTrials(Player p)
        {
            // Example; adapt to your data model
            // e.g., p.TrialCards is List<TrialCard>, each has IsRevealed
            return p.TryalCards != null && p.TryalCards.Count > 0 &&
                p.TryalCards.TrueForAll(tc => tc.IsRevealed);
        }

        private static bool IsWitchCard(TryalCard card)
        => card != null && card.TryalCardType == TryalCardType.Witch;
    }
}
