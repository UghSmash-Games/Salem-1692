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
            // Rebecca Nurse: draw one card each time ANOTHER player loses a tryal BY ACCUSATIONS
            // (not by death or confession). Evaluated UP-FRONT — before the elimination early-returns
            // below — so it fires even when this same accusation reveal is ALSO the eliminating reveal
            // (the other player's last tryal, or their last/only Witch card). The tryal is still lost by
            // accusation; elimination is a consequence of that loss, not a separate category. Self is
            // excluded (p != owner): Rebecca does NOT draw when SHE loses a tryal. No team restriction,
            // no charge/cap — exactly one card per qualifying reveal.
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

                // Anne Putnam: tally this ACTUAL accusation reveal for her end-of-turn draw (2× count).
                // Counts only when it's the acting player's turn and the reveal is on someone else;
                // the turn manager owns that gating. See GameTurnManager.NotifyAccusationRevealOnOther.
                GameTurnManager.Instance?.NotifyAccusationRevealOnOther(owner);
            }

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
                    // Do NOT eliminate; the survivor continues (Rebecca's draw already ran up-front).
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
