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
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Salem.Cards;
using Salem.Data;
using Salem.Deck;
using Salem.GameFlow;
using Salem.Managers.Hands;
using UnityEngine;

namespace Salem.Players
{
    [RequireComponent(typeof(Player))]
    public class AIPlayer : Player
    {
        #region Vars
        [SerializeField] private float aiThinkDelay = 1.5f;
        [SerializeField] private Player player;
        [SerializeField] private GameManager GameManager;
        [SerializeField] private DeckManager deckManager;
        private IRng Rng => GameManager != null ? GameManager.Rng : _fallbackRng;
        private readonly IRng _fallbackRng = new XorShiftRng(1UL); // only if GM missing
        #endregion

        void OnValidate()
        {
            if (!player) player = GetComponent<Player>();
            if (!GameManager) GameManager = FindFirstObjectByType<GameManager>();
            if (!deckManager) deckManager = FindFirstObjectByType<DeckManager>();
        }
        void Awake()
        {
            if (!GameManager) Debug.LogError("[AI Player] Missing GameManager reference for RNG.");
            if (!deckManager) deckManager = FindFirstObjectByType<DeckManager>();
        }

        #region Accessor Functions
        public IEnumerator TakeTurnOnce()
        {
            //Debug.Log("Starting AI Turn Logic");
            if (player.IsHuman) yield break;

            yield return new WaitForSeconds(aiThinkDelay);

            var hand = player.HandManager.GetCards();
            if (hand == null || hand.Count == 0)
            {
                // Nothing to play -> Draw Cards
                if (!GameTurnManager.Instance.TryDrawTwoCards(player))
                {
                    DrawTwoCards();
                    GameTurnManager.Instance.RequestEndTurn(player);
                }
            }

            // Pick a playable ACTION card deterministically
            var actions = hand.OfType<ActionCardSO>().ToList();
            if (!GameTurnManager.Instance.TryDrawTwoCards(player))
                {
                    DrawTwoCards();
                    GameTurnManager.Instance.RequestEndTurn(player);
                }

            var card = actions[RNGService.Rng.NextInt(0, actions.Count)];


            // naive: pick first playable card and a legal target
            if (card == null)
            {
                if (!GameTurnManager.Instance.TryDrawTwoCards(player))
                {
                    DrawTwoCards();
                    GameTurnManager.Instance.RequestEndTurn(player);
                }
                yield break;
            }

            if (!GameTurnManager.Instance.TryBeginPlayPhase(player))
            {
                GameTurnManager.Instance.RequestEndTurn(player);
            }

            PerformTurnAction(card);
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

        public override void PerformTurnAction(ActionCardSO selectedCard)
        {
            if (selectedCard == null)
            {
                Debug.Log("No Selected Card");
                return;
            }

            if (CardEffectManager.Instance == null)
            {
                Debug.LogError("CardEffectManager.Instance is null!");
                return;
            }

            Player primary = null;
            Player secondary = null;
            if (selectedCard.RequiresTarget)
            {
                primary = AITargetingHelper.SelectRandomTarget(this);
                if (primary == null)
                {
                    Debug.LogWarning("[AI] No valid target found.");
                    return;
                }
            }
            if (selectedCard.RequiresSecondTarget)
            {
                secondary = AITargetingHelper.SelectRandomTarget(this);
                if (secondary == null || secondary == primary)
                {
                    Debug.LogWarning("[AI] No valid target found.");
                    return;
                }
            }
            Debug.Log("Playing Card");
            CardEffectManager.Instance.ExecuteCardEffect(selectedCard, primary);
            HandManager.RemoveCard(selectedCard);
        }
        #endregion

        #region Helper Functions

        private void DrawTwoCards()
        {
            if (deckManager == null)
            {
                Debug.LogError("[AI] DeckManager reference missing; cannot draw.");
                return;
            }

            if (player == null || player.HandManager == null)
            {
                Debug.LogError("[AI] Player or HandManager missing; cannot draw.");
                return;
            }

            deckManager.DrawMultipleCards(player.HandManager, 2);
        }

        void OnEnable()
        {
            // Disable itself if this player is human
            if (player != null && player.IsHuman) enabled = false;
        }
        #endregion
    }
}