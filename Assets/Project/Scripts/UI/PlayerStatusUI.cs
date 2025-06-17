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

        private Player player;
        private Player currentPlayer;
        private bool isTurnIndicatorActive;

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

            Transform tryalCardTransform;
            tryalCardTransform = tryalCardPanel.GetChild(0).transform;

            foreach (Transform child in tryalCardPanel) Destroy(child.gameObject);
            foreach (TryalCard tc in player.TryalCards)
            {
                var obj = Instantiate(tryalCardPrefab, tryalCardPanel);
                obj.GetComponent<Transform>().position = tryalCardTransform.position;
                obj.GetComponent<Transform>().localScale = tryalCardTransform.localScale;
                obj.GetComponent<TryalCardUI>().AssignCard(tc);
            }
        }

        public void SetTurnActive(bool isActive)
        {
            turnIndicator.SetActive(isActive);
            Debug.Log($"[PlayerStatusUI] Turn indicator {(isActive ? "ENABLED" : "DISABLED")} for player panel: {gameObject.name}");
        }


        private void TurnIndicatorEffects()
        {
            if (isTurnIndicatorActive)
            {
                Debug.Log("TurnIndicatorEffects Running");
                UpdateCurrentPlayer();
                FlashTurnStart();
                ScaleTurnIndicator();
            }
        }

        private void FlashTurnStart()
        {
            currentPlayer.PlayerName.color = flashColor; // or animate via Lerp
            StartCoroutine(ResetNameColor());
        }

        private IEnumerator ResetNameColor()
        {
            yield return new WaitForSeconds(1.5f);
            currentPlayer.PlayerName.color = originalColor;
        }

        private void ScaleTurnIndicator()
        {
            float scale = 1f + Mathf.PingPong(Time.time * 0.5f, 0.1f);
            turnIndicator.transform.localScale = Vector3.one * scale;
        }

        private void UpdateCurrentPlayer()
        {
            currentPlayer = PlayerService.All[GameTurnManager.CurrentPlayerIndex];
            isTurnIndicatorActive = turnIndicator.activeSelf;
        }
    }

}