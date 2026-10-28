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