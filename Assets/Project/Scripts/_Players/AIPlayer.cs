/*
* AUTHOR: Ron Bresett
* REFERENCES:
* NOTES:
*   Primary Purpose: AI behavior controller subclassing Player.
*   Responsibilities:
*        • Override input-based behavior
*        • Execute strategy logic
*   Access Requirements:
*        • GamePhaseManager
*        • HandManager

* TODO: Implement AI Behaviors
*    • Start with basic logic → suspicion → tactics

* FIXME: [Known bugs or issues]
*/
using System.Collections.Generic;
using UnityEngine;
namespace Salem.Players
{
    public class AIPlayer : Player
    {
        #region Vars
        private List<Card> Hand = new List<Card>();
        private Card card;
        private Player target;
        #endregion

        #region Accessor Functions
        public void TakeTurn()
        {
            // Placeholder for AI behavior
            DrawCards();
            //need to develop choosing card from hand and selecting target
            //PlayCards();
        }
        #endregion

        #region Helper Functions
        private void DrawCards()
        {
            // Logic for drawing cards
        }

        private void PlayCards()
        {
            foreach (Card i in Hand)
            {
                    if (IsValidPlay(card))
                {
                    //target = ChooseTarget(); // Basic target selection
                    PlayCard(card, target);
                    break; // Play one card per turn in this example
                }
                //GameTurnManager.Instance.EndTurn();
            }
        }

        private void PlayCard(Card card, Player target)
        {
            Debug.Log("Function Not Implemented");
            throw new System.NotImplementedException();
        }

        private bool IsValidPlay(Card card)
        {
            // Define simple rules for valid card plays
            return card.Type != "Black"; // Example: Skip black cards for now
        }
        
        /*
        private Player ChooseTarget()
        {
        // Simple targeting logic (can be expanded later)
        List<Player> potentialTargets = GameManager.Instance.GetActivePlayers();
        return potentialTargets[Random.Range(0, potentialTargets.Count)];
        }
        */
        #endregion

    }
}