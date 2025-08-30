using System;
using System.Collections;
using System.Collections.Generic;
using Salem.Cards;
using Salem.Data;
using Salem.GameFlow;
using Salem.Players;
using TMPro;
using UnityEngine;

namespace Salem.UI
{
    public class PlayerStatusUI : MonoBehaviour
    {
        [SerializeField] private Transform statusCardPanel;
        [SerializeField] private GameObject cardPrefab;
        [SerializeField] private Transform tryalCardPanel;
        [SerializeField] private GameObject tryalCardPrefab;
        [SerializeField] private GameObject turnIndicator;
        [SerializeField] private Color originalColor;
        [SerializeField] private Color flashColor;
        public TextMeshProUGUI PlayerName;

        private Player player;
        private Player currentPlayer;
        private bool isTurnIndicatorActive;

        private void Awake()
        {
            PlayerName = GetComponentInChildren<TextMeshProUGUI>();
        }

        private void Start()
        {
            
        }

        private void Update()
        {
            TurnIndicatorEffects();
        }

        public void Initialize(Player p)
        {
            //Debug.Log("Status UI Initiazlize");
            player = p;
            //playerNameText.text = p.PlayerName;
            UpdateStatusCards(player.StatusCards);
            UpdateTryalCards();
        }

        public void UpdateStatusCards(List<Card> cards)
        {
            foreach (Transform child in statusCardPanel) Destroy(child.gameObject);

            foreach (Card card in cards)
            {
                GameObject uiCard = Instantiate(cardPrefab, statusCardPanel);
                // Optionally update visuals
                // uiCard.GetComponent<CardUI>()?.Setup(card);
            }
        }
        /*
        public void UpdateStatusCards()
        {
            foreach (Transform child in statusCardPanel) Destroy(child.gameObject);
            foreach (Card card in player.StatusCards)
            {
                var obj = Instantiate(cardPrefab, statusCardPanel);
                obj.GetComponent<GameCardUI>().SetCard(card);
            }
        }
        */

        public void UpdateTryalCards()
        {
            //Debug.Log($"[{player.PlayerName}] has {player.TryalCards.Count} Tryal cards.");

            // Determine a template transform to copy position and scale from if one exists.
            Vector3 tryalCardPosition = Vector3.zero;
            Vector3 tryalCardScale = Vector3.one;

             if (tryalCardPanel.childCount > 0)
            {
                Transform template = tryalCardPanel.GetChild(0);
                tryalCardPosition = template.localPosition;
                tryalCardScale = template.localScale;
            }

            foreach (Transform child in tryalCardPanel) Destroy(child.gameObject);
            foreach (TryalCard tc in player.TryalCards)
            {
                var obj = Instantiate(tryalCardPrefab, tryalCardPanel);
                Transform objTransform = obj.GetComponent<Transform>();
                objTransform.localPosition = tryalCardPosition;
                objTransform.localScale = tryalCardScale;
                obj.GetComponent<TryalCardUI>().AssignCard(tc);
            }
        }

        public void SetTurnActive(bool isActive)
        {
            turnIndicator.SetActive(isActive);
            //Debug.Log($"[PlayerStatusUI] Turn indicator {(isActive ? "ENABLED" : "DISABLED")} for player panel: {gameObject.name}");
        }


        private void TurnIndicatorEffects()
        {
            bool wasTurnIndicatorActive = isTurnIndicatorActive;
            UpdateCurrentPlayer();

            if (isTurnIndicatorActive && !wasTurnIndicatorActive)
            {
                FlashTurnStart();
            }

            if (isTurnIndicatorActive)
            {
                ScaleTurnIndicator();
            }
        }

        private void FlashTurnStart()
        {
            PlayerName.color = flashColor; // or animate via Lerp
            StartCoroutine(ResetNameColor());
        }

        private IEnumerator ResetNameColor()
        {
            yield return new WaitForSeconds(1.5f);
            PlayerName.color = originalColor;
        }

        private void ScaleTurnIndicator()
        {
            float scale = 1f + Mathf.PingPong(Time.time * 0.1f, .05f);
            turnIndicator.transform.localScale = Vector3.one * scale;
        }

        private void UpdateCurrentPlayer()
        {
            var players = PlayerService.GetAlivePlayers();
            if (GameTurnManager.CurrentPlayerIndex < players.Count)
            {
                currentPlayer = players[GameTurnManager.CurrentPlayerIndex];
            }
            isTurnIndicatorActive = turnIndicator.activeSelf;
        }
    }

}