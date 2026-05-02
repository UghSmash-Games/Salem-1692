using System.Linq;
using Salem.Cards;
using Salem.GameFlow;
using Salem.Players;
using UnityEngine;

namespace Salem.Data
{
    public static class TrialService
    {
        /// <summary>
        /// Fired when a player with two Witch Tryal cards reveals one but survives.
        /// All players should be notified that this player has a second Witch card.
        /// </summary>
        public static event System.Action<Player> OnDoubleWitchRevealed;
        public static void OnTrialCardRevealed(Player owner, TryalCard revealedCard, bool fromAccusation = false)
        {
            if (IsWitchCard(revealedCard))
            {
                // If the player still has another unrevealed Witch card, they survive
                bool hasAnotherWitch = owner.TryalCards.Any(c =>
                    c != revealedCard && c.TryalCardType == TryalCardType.Witch && !c.IsRevealed);

                if (hasAnotherWitch)
                {
                    Debug.Log($"[TrialService] {owner.PlayerNameText} revealed a Witch card but has a second Witch — not eliminated. All players notified.");
                    OnDoubleWitchRevealed?.Invoke(owner);
                    // Check if this was the last unrevealed Witch card in the game
                    // (townspeople win condition: all Witch Tryal cards revealed)
                    GameManager.Instance?.EvaluateEndGame();
                    // Do NOT eliminate; fall through to Rebecca Nurse check below
                }
                else
                {
                    PlayerService.Eliminate(owner, EliminationCause.WitchTrialRevealed);
                    return;
                }
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
            // Example; adapt to your data model
            // e.g., p.TrialCards is List<TrialCard>, each has IsRevealed
            return p.TryalCards != null && p.TryalCards.Count > 0 &&
                p.TryalCards.TrueForAll(tc => tc.IsRevealed);
        }

        private static bool IsWitchCard(TryalCard card)
        => card != null && card.TryalCardType == TryalCardType.Witch;
    }
}
