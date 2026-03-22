using System.Linq;
using Salem.Cards;
using Salem.Players;
using UnityEngine;

namespace Salem.Data
{
    public static class TrialService
    {
        public static void OnTrialCardRevealed(Player owner, TryalCard revealedCard, bool fromAccusation = false)
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

            // Rebecca Nurse: draw one card each time a Tryal is revealed on another player
            // Only from accusations (not from death or confession)
            if (fromAccusation)
            {
                var nurse = PlayerService.GetAlivePlayers()
                    .FirstOrDefault(p => p != owner && p.HasTownHall(TownhallName.RebeccaNurse));
                if (nurse != null)
                {
                    var dm = Object.FindFirstObjectByType<Salem.Deck.DeckManager>();
                    dm?.DrawCard(nurse.HandManager);
                    Debug.Log($"[TownHall] Rebecca Nurse ({nurse.PlayerNameText}) draws a card from tryal reveal on {owner.PlayerNameText}.");
                }
            }
        }

        private static bool RevealedAllTrials(Player p)
        {
            return p.TryalCards != null && p.TryalCards.Count > 0 &&
                p.TryalCards.TrueForAll(tc => tc.IsRevealed);
        }

        private static bool IsWitchCard(TryalCard card)
        => card != null && card.TryalCardType == TryalCardType.Witch;
    }
}
