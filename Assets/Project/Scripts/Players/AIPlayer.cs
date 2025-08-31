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
using Salem.GameFlow;
using Salem.Managers.Hands;
using Salem.Cards;
using System.Collections;
using System;
using Salem.Data;

namespace Salem.Players
{
    public class AIPlayer : Player
    {
        #region Vars
        [SerializeField] private float aiThinkDelay = 1.5f;
        private IRng rng;
        #endregion

        void Awake()
        {
            rng = new XorShiftRng((ulong)System.DateTime.UtcNow.Ticks);
        }

        #region Accessor Functions
        public void StartTurn(Action onComplete)
        {
            StartCoroutine(ExecuteAITurn(onComplete));
        }

        public override void ApplyCardEffect(Card card)
        {
            // Use generic version or expand later with smarter AI logic
            base.ApplyCardEffect(card);
        }

        public override Card SelectCard()
        {
            if (HandManager == null || HandManager.Hand.Count == 0)
            {
                Debug.LogWarning("[AI] No cards to select.");
                return null;
            }

            return HandManager.Hand[0];
        }

        public override void PerformTurnAction(Card selectedCard)
        {
            if (selectedCard == null)
            {
                return;
            }

            if (CardEffectManager.Instance == null)
            {
                Debug.LogError("CardEffectManager.Instance is null!");
                return;
            }

            Player target = null;
            if (selectedCard.RequiresTarget)
            {
                target = AITargetingHelper.SelectRandomTarget(this);
                if (target == null)
                {
                    Debug.LogWarning("[AI] No valid target found.");
                    return;
                }
            }

            CardEffectManager.Instance.ExecuteCardEffect(selectedCard, target);
            HandManager.RemoveCard(selectedCard);
        }

        public void TakeTurn()
        {
            // Placeholder for AI behavior
            DrawCards();
            //need to develop choosing card from hand and selecting target
            //PlayCards();
        }
        #endregion

        #region Helper Functions
        private IEnumerator ExecuteAITurn(Action onComplete)
        {
            //Sort Delay before Acting
            yield return new WaitForSeconds(aiThinkDelay);
            Debug.Log($"[AI] {PlayerNameText} is taking action...");

            Card chosenCard = SelectCard();

            // Example stub: play first card in hand if exists
            if (chosenCard != null)
            {
                PerformTurnAction(chosenCard);
            }

            // Optional delay after action
            yield return new WaitForSeconds(1f);

            onComplete?.Invoke();
        }
        private void DrawCards()
        {
            // Logic for drawing cards
        }

        private void PlayCards()
        {
            /*
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
            */
        }

        /*
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
        */
        
        /*
        private Player ChooseTarget()
        {
        // Simple targeting logic (can be expanded later)
        List<Player> potentialTargets = GameManager.Instance.GetActivePlayers();
        return potentialTargets[rng.NextInt(0, potentialTargets.Count)];
        }
        */
        #endregion

    }
}