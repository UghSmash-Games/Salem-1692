/*
* AUTHOR: Ron Bresett
* NOTES:
*   Primary Purpose:
*   Controls one player's visual board on the table.
*
*   Responsibilities:
*   - Displays player name
*   - Displays hand count
*   - Displays Tryal card count
*   - Displays Town Hall card
*   - Displays eliminated status
*   - Allows this board to be clicked for targeting
*/

using System;
using System.Collections;
using System.Collections.Generic;
using Salem.Cards;
using Salem.Players;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

namespace Salem.UI
{
    public class PlayerBoardUI : MonoBehaviour
    {
        [Header("Player Info")]
        [SerializeField] private TMP_Text playerNameText;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private GameObject localPlayerBadge;
        [SerializeField] private GameObject eliminatedOverlay;
        [SerializeField] private GameObject turnIndicator;
        [SerializeField] private GameObject turnHighlight;
        [SerializeField] private GameObject targetHighlight;

        [Header("Card Slot Parents")]
        [SerializeField] private Transform handSlotParent;
        [SerializeField] private Transform tryalSlotParent;
        [SerializeField] private Transform statusCardSlotParent;
        [SerializeField] private Transform townHallSlotParent;

        [Header("Card UI Prefabs")]
        [SerializeField] private GameObject hiddenCardPrefab;
        [SerializeField] private GameObject cardUIPrefab;
        [SerializeField] private GameObject tryalCardPrefab;

        [Header("Interaction")]
        [SerializeField] private Button boardButton;

        public Player BoundPlayer => boundPlayer;
        private Player boundPlayer;

        private Coroutine pulseRoutine;

        public event Action<PlayerBoardUI, Player> OnBoardClicked;
        public event Action<Card, Player> OnHandCardClicked;
        public event Action<Player, int> OnTryalCardClicked;

        private readonly List<GameObject> spawnedCards = new();

        private void Awake()
        {
            if (boardButton != null)
            {
                boardButton.onClick.AddListener(HandleBoardClicked);
            }

            Clear();
        }

        private void OnDestroy()
        {
            if (boardButton != null)
            {
                boardButton.onClick.RemoveListener(HandleBoardClicked);
            }

            UnbindEvents();
        }

        public void Bind(Player player)
        {
            UnbindEvents();

            boundPlayer = player;

            if (boundPlayer == null)
            {
                Clear();
                return;
            }

            BindEvents();
            Refresh();
        }

        public void Refresh()
        {
            if (boundPlayer == null)
            {
                Clear();
                return;
            }

            SetText(playerNameText, boundPlayer.PlayerNameText);
            SetText(statusText, boundPlayer.IsEliminated ? "Eliminated" : "Active");

            SetActive(localPlayerBadge, boundPlayer.IsLocalPlayer);
            SetActive(eliminatedOverlay, boundPlayer.IsEliminated);

            RefreshCardSlots();
        }

        public void Clear()
        {
            ClearSpawnedCards();

            boundPlayer = null;

            SetText(playerNameText, "Empty Seat");
            SetText(statusText, "");

            SetActive(localPlayerBadge, false);
            SetActive(eliminatedOverlay, false);
            SetActive(turnIndicator, false);
            SetActive(targetHighlight, false);
        }

        public void SetTurnIndicator(bool isTurn)
        {
            /*Debug.Log(
                $"[PlayerBoardUI] SetTurnIndicator called for " +
                $"{boundPlayer?.PlayerNameText} | isTurn = {isTurn}"
            );*/
            SetActive(turnIndicator, isTurn);

            if (turnHighlight != null)
            {
                turnHighlight.SetActive(isTurn);
            }

            transform.localScale = isTurn
                ? new Vector3(1.05f, 1.05f, 1f)
                : Vector3.one;

            if (pulseRoutine != null)
            {
                StopCoroutine(pulseRoutine);
                pulseRoutine = null;
            }

            // isActiveAndEnabled guard: Networked_Game deactivates these legacy boards (the host
            // screen renders from HostDisplay instead), but TableLayoutController still iterates
            // them. Starting a coroutine on an inactive GameObject throws every turn.
            if (isTurn && isActiveAndEnabled)
            {
                pulseRoutine = StartCoroutine(PulseRoutine());
            }
            else
            {
                transform.localScale = Vector3.one;
            }
        }

        public void SetTargetHighlight(bool isHighlighted)
        {
            SetActive(targetHighlight, isHighlighted);
        }

        public void SetInteractable(bool canClick)
        {
            if (boardButton != null)
            {
                boardButton.interactable = canClick;
            }
        }

        private void RefreshCardSlots()
        {
            ClearSpawnedCards();

            SpawnHandSlots();
            SpawnTryalSlots();
            SpawnTownHallSlot();
            SpawnStatusCardSlots();
        }

        private void SpawnHandSlots()
        {
            FindHandSlotParent();

            if (boundPlayer.IsLocalPlayer && (boundPlayer.HandManager == null || handSlotParent == null))
            {
                Debug.LogWarning($"Local Player [PlayerBoardUI] Missing HandManager or handSlotParent for {boundPlayer.PlayerNameText}.");                return;
            }


            foreach (Card card in boundPlayer.HandManager.GetCards())
            {
                if (boundPlayer.IsLocalPlayer)
                {
                    SpawnVisibleCard(handSlotParent, card);
                }
                else
                {
                    SpawnHiddenCard(handSlotParent);
                }
            }
        }

        private void SpawnTryalSlots()
        {
            if (boundPlayer.TryalCards == null || tryalSlotParent == null)
                return;

            for (int i = 0; i < boundPlayer.TryalCards.Count; i++)
            {
                SpawnTryalCard(tryalSlotParent, boundPlayer.TryalCards[i], i);
            }
        }

        private void SpawnTryalCard(Transform parent, TryalCard tryalCard)
        {
            if (tryalCardPrefab == null || parent == null || tryalCard == null)
                return;

            GameObject cardObject = Instantiate(tryalCardPrefab, parent);
            spawnedCards.Add(cardObject);

            TryalCardUI tryalUI = cardObject.GetComponent<TryalCardUI>();
            if (tryalUI != null)
            {
                tryalUI.AssignCard(tryalCard);
            }
        }

        private void SpawnTryalCard(Transform parent, TryalCard tryalCard, int tryalIndex)
        {
            if (tryalCardPrefab == null || parent == null)
                return;

            GameObject cardObject = Instantiate(tryalCardPrefab, parent);
            spawnedCards.Add(cardObject);

            TryalCardUI tryalUI = cardObject.GetComponent<TryalCardUI>();
            if (tryalUI != null)
            {
                tryalUI.AssignCard(tryalCard);
            }

            Button button = cardObject.GetComponent<Button>();

            if (button == null)
            {
                button = cardObject.AddComponent<Button>();
            }

            int capturedIndex = tryalIndex;

            button.onClick.AddListener(() =>
            {
                Debug.Log($"[PlayerBoardUI] Tryal card clicked. Player: {boundPlayer.PlayerNameText}, Index: {capturedIndex}");
                OnTryalCardClicked?.Invoke(boundPlayer, capturedIndex);
            });
        }

        private void SpawnTownHallSlot()
        {
            if (townHallSlotParent == null) return;

            if (boundPlayer.townhallCard == null) return;
            
             TownHallCardUI townHallUI = townHallSlotParent.GetComponent<TownHallCardUI>();

            if (townHallUI == null)
            {
                Debug.LogWarning("[PlayerBoardUI] Missing TownHallCardUI on townHallSlotParent.");
                return;
            }

            townHallUI.Bind(boundPlayer.townhallCard);
        }

        private void SpawnStatusCardSlots()
        {
            if (statusCardSlotParent == null || boundPlayer.StatusCards == null)
                return;

            foreach (Card card in boundPlayer.StatusCards)
            {
                SpawnVisibleCard(statusCardSlotParent, card);
            }
        }

        private void SpawnHiddenCard(Transform parent)
        {
            if (hiddenCardPrefab == null || parent == null)
                return;

            GameObject cardObject = Instantiate(hiddenCardPrefab, parent);
            spawnedCards.Add(cardObject);
        }

        private void SpawnVisibleCard(Transform parent, Card card)
        {
            if (cardUIPrefab == null || parent == null || card == null)
                return;

            GameObject cardObject = Instantiate(cardUIPrefab, parent);
            spawnedCards.Add(cardObject);

            GameCardUI cardUI = cardObject.GetComponent<GameCardUI>();

            if (cardUI != null)
            {
                cardUI.SetCard(card, true, boundPlayer);
                cardUI.OnCardClicked += HandleHandCardClicked;
            }
        }

        private void HandleHandCardClicked(Card card, Player owner)
        {
            Debug.Log($"[PlayerBoardUI] Hand card clicked: {card.Name} by {owner.PlayerNameText}");

            OnHandCardClicked?.Invoke(card, owner);
        }

        private void ClearSpawnedCards()
        {
            foreach (GameObject card in spawnedCards)
            {
                if (card != null)
                {
                    GameCardUI cardUI = card.GetComponent<GameCardUI>();

                    if (cardUI != null)
                    {
                        cardUI.OnCardClicked -= HandleHandCardClicked;
                    }

                    Destroy(card);
                }
            }

            spawnedCards.Clear();
        }

        private void HandleBoardClicked()
        {
            if (boundPlayer == null)
                return;

            OnBoardClicked?.Invoke(this, boundPlayer);
        }

        private void BindEvents()
        {
            if (boundPlayer == null)
                return;

            boundPlayer.OnTryalCardsChanged += Refresh;
            boundPlayer.OnStatusCardsChanged += Refresh;

            if (boundPlayer.HandManager != null)
            {
                boundPlayer.HandManager.OnHandChanged += Refresh;
            }
        }

        private void UnbindEvents()
        {
            if (boundPlayer == null)
                return;

            boundPlayer.OnTryalCardsChanged -= Refresh;
            boundPlayer.OnStatusCardsChanged -= Refresh;
            
            if (boundPlayer.HandManager != null)
            {
                boundPlayer.HandManager.OnHandChanged -= Refresh;
            }
        }

        private static void SetText(TMP_Text text, string value)
        {
            if (text != null)
            {
                text.text = value;
            }
        }

        private static void SetActive(GameObject obj, bool active)
        {
            if (obj != null)
            {
                obj.SetActive(active);
            }
        }

        private void FindHandSlotParent()
        {
            if(handSlotParent != null) return;
            else if (boundPlayer.IsLocalPlayer)
            {
                handSlotParent = GameObject.Find("HandPanelUI-Container").transform;
            }
        }

        private IEnumerator PulseRoutine()
        {
            while (true)
            {
                float scale = 1f + Mathf.Sin(Time.time * 4f) * 0.02f;

                transform.localScale = new Vector3(scale, scale, 1f);

                yield return null;
            }
        }
    }
}