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
using System.Collections;
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
        //private IRng Rng => GameManager != null ? GameManager.Rng : _fallbackRng;
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

            yield return AITurnSequencer.ExecuteTurn(player, deckManager, aiThinkDelay, false);
        }

        public override void ApplyCardEffect(Card card)
        {
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
            // NOTE: no live caller — the AI runs through AITurnSequencer (TakeTurnOnce). Kept correct
            // anyway: pass the chosen `secondary` (it was computed above and previously dropped) and
            // only consume the card if the effect actually ran.
            if (CardEffectManager.Instance.ExecuteCardEffect(selectedCard, primary, secondary))
                HandManager.RemoveCard(selectedCard);
        }
        #endregion

        #region Helper Functions
        void OnEnable()
        {
            // Disable itself if this player is human
            if (player != null && player.IsHuman) enabled = false;
        }
        #endregion
    }
}