using System.Linq;
using Salem.Cards;
using Salem.Deck;
using Salem.GameFlow;
using Salem.Players;
using UnityEngine;
using UnityEngine.UI;

namespace Salem.UI
{
    public class PlayerInputUI : MonoBehaviour
    {
        [SerializeField] private Transform handPanel;
        [SerializeField] private GameObject cardUIPrefab;
        [SerializeField] private TargetPickerUI targetPicker;
        [SerializeField] private Button drawCardButton;
        [SerializeField] private Button endTurnButton;
        [SerializeField] private DeckManager deckManager;

        [Header("Town Hall Ability Buttons")]
        [SerializeField] private Button drawFromDiscardButton;  // Samuel Parris
        [SerializeField] private Button titubaAbilityButton;    // Tituba

        private Player player;
        private bool isLocalPlayersTurn;
        private bool hasStartedPlayingThisTurn;

        public void Initialize(Player p)
        {
            //Debug.Log("Initializing Player");
            player = p;
            player.HandManager.OnHandChanged += UpdateHand;
            UpdateHand();

            if (!deckManager)
            {
                deckManager = FindFirstObjectByType<DeckManager>();
            }

            if (drawCardButton != null)
            {
                drawCardButton.onClick.AddListener(OnDrawCardClicked);
                drawCardButton.interactable = false;
            }

            if (endTurnButton != null)
            {
                endTurnButton.onClick.AddListener(OnEndTurnClicked);
                endTurnButton.interactable = false;
            }

            // Samuel Parris: draw from discard button
            if (drawFromDiscardButton != null)
            {
                drawFromDiscardButton.onClick.AddListener(OnDrawFromDiscardClicked);
                drawFromDiscardButton.interactable = false;
                drawFromDiscardButton.gameObject.SetActive(false);
            }

            // Tituba: rearrange deck button
            if (titubaAbilityButton != null)
            {
                titubaAbilityButton.onClick.AddListener(OnTitubaAbilityClicked);
                titubaAbilityButton.interactable = false;
                titubaAbilityButton.gameObject.SetActive(false);
            }

            if (GameTurnManager.Instance != null)
            {
                GameTurnManager.Instance.TurnStarted += HandleTurnStarted;
                GameTurnManager.Instance.TurnEnded += HandleTurnEnded;
                HandleTurnStarted(GameTurnManager.Instance.CurrentPlayer);
            }
        }

        private void SpawnCardUI(Card card)
        {
            var obj = Instantiate(cardUIPrefab, handPanel);
            var cardUI = obj.GetComponent<GameCardUI>();
            cardUI.SetCard(card, /* faceUp: */ true, player);   // local player sees their own cards

            var relay = obj.GetComponent<CardSelectionRelay>();
            relay.Init(card);
            relay.Clicked += OnCardClicked;
        }

        public void UpdateHand()
        {
            //Debug.Log("Updating Player Hand UI");
            foreach (Transform child in handPanel) Destroy(child.gameObject);
            foreach (Card card in player.HandManager.Hand)
                SpawnCardUI(card);
        }

        private void OnCardClicked(Card selected)
        {
            if (!isLocalPlayersTurn)
            {
                Debug.LogWarning("[PlayerInputUI] Ignoring card click because it is not this player's turn.");
                return;
            }

            if (GameTurnManager.Instance != null && !GameTurnManager.Instance.TryBeginPlayPhase(player))
            {
                return;
            }

            hasStartedPlayingThisTurn = true;

            if (drawCardButton != null)
            {
                drawCardButton.interactable = false;
            }

            var ac = selected as ActionCardSO;
            if (ac == null)
            {
                // Non-action cards shouldn’t be in hand, but guard anyway
                CardEffectManager.Instance.ExecuteCardEffect(selected, null);
                return;
            }

             // Will Griggs: Alibi cards can be used offensively as Witness (+7 accusations on target)
            if (ac.Op == Salem.Cards.ActionOp.Alibi && player.HasTownHall(Salem.Cards.TownhallName.WillGrigs))
            {
                // Offer choice: use target picker to select a target for offensive Alibi
                // If player skips/cancels, play defensively on self
                if (targetPicker != null)
                {
                    var alivePlayers = Salem.Data.PlayerService.GetAlivePlayers()
                        .Where(p => p != player).ToList();
                    targetPicker.Open(player, true, (primary, _) =>
                    {
                        if (primary != null)
                            CardEffectManager.Instance.ExecuteCardEffect(ac, primary);
                        else
                            CardEffectManager.Instance.ExecuteCardEffect(ac, null);
                    }, alivePlayers, true, "Will Griggs: Choose a target for +7 accusations, or close to use Alibi (-3 on self)");
                }
                else
                {
                    CardEffectManager.Instance.ExecuteCardEffect(ac, null);
                }
            }
            else if (ac.NeedsTarget)
            {
                bool two = ac.RequiresSecondTarget;
                // Open the picker; exclude the acting player by default
                targetPicker.OpenLegacy(player, two, (primary, secondary) =>
                {
                    ac.target = secondary; // second target, if any
                    CardEffectManager.Instance.ExecuteCardEffect(ac, primary);
                });
            }
            else
            {
                // No target required → play immediately
                CardEffectManager.Instance.ExecuteCardEffect(ac, null);
            }
        }

        private void OnDrawCardClicked()
        {
            if (player == null)
            {
                Debug.LogError("[PlayerInputUI] Cannot draw without an assigned player.");
                return;
            }

            if (!isLocalPlayersTurn)
            {
                Debug.LogWarning("[PlayerInputUI] Draw button pressed outside of the local player's turn.");
                return;
            }

            if (hasStartedPlayingThisTurn)
            {
                Debug.LogWarning("[PlayerInputUI] Cannot draw after starting to play cards this turn.");
                return;
            }

            if (deckManager == null)
            {
                deckManager = FindFirstObjectByType<DeckManager>();
            }

            if (deckManager == null)
            {
                Debug.LogError("[PlayerInputUI] DeckManager reference missing; ensure it is assigned in the inspector.");
                return;
            }

            if (GameTurnManager.Instance != null)
            {
                if (!GameTurnManager.Instance.TryDrawTwoCards(player))
                {
                    Debug.LogWarning("[PlayerInputUI] Draw action was rejected by the turn manager.");
                    return;
                }
            }
            else
            {
                deckManager.DrawMultipleCards(player.HandManager, 2);
            }

            if (drawCardButton != null)
            {
                drawCardButton.interactable = false;
            }

            if (endTurnButton != null)
            {
                endTurnButton.interactable = false;
            }
        }

        private void OnEndTurnClicked()
        {
            if (!isLocalPlayersTurn)
            {
                return;
            }

            if (GameTurnManager.Instance != null)
            {
                GameTurnManager.Instance.RequestEndTurn(player);
            }

            if (endTurnButton != null)
            {
                endTurnButton.interactable = false;
            }
        }

         private void OnDrawFromDiscardClicked()
        {
            if (player == null || !isLocalPlayersTurn) return;

            if (GameTurnManager.Instance != null)
            {
                if (GameTurnManager.Instance.TryDrawFromDiscard(player))
                {
                    SetTownHallButtonsInteractable(false);
                }
            }
        }

        private void OnTitubaAbilityClicked()
        {
            if (player == null || !isLocalPlayersTurn) return;

            if (GameTurnManager.Instance != null)
            {
                if (GameTurnManager.Instance.TryUseTitubaAbility(player))
                {
                    SetTownHallButtonsInteractable(false);
                }
            }
        }

        private void SetTownHallButtonsInteractable(bool interactable)
        {
            if (drawFromDiscardButton != null)
                drawFromDiscardButton.interactable = interactable;
            if (titubaAbilityButton != null)
                titubaAbilityButton.interactable = interactable;
        }

        private void UpdateTownHallButtons(bool isMyTurn)
        {
            // Samuel Parris: show draw from discard button when he has charges
            if (drawFromDiscardButton != null)
            {
                bool showParris = isMyTurn && player.HasTownHall(TownhallName.SamuelParris) && player.townHallAbilityCharges > 0;
                drawFromDiscardButton.gameObject.SetActive(showParris);
                drawFromDiscardButton.interactable = showParris;
            }

            // Tituba: show rearrange deck button when she has charges
            if (titubaAbilityButton != null)
            {
                bool showTituba = isMyTurn && player.HasTownHall(TownhallName.Tituba) && player.townHallAbilityCharges > 0;
                titubaAbilityButton.gameObject.SetActive(showTituba);
                titubaAbilityButton.interactable = showTituba;
            }
        }

        private void HandleTurnStarted(Player activePlayer)
        {
            if (player == null)
            {
                return;
            }

            isLocalPlayersTurn = player != null && activePlayer == player;
            if (isLocalPlayersTurn)
            {
                hasStartedPlayingThisTurn = false;
                if (drawCardButton != null)
                {
                    drawCardButton.interactable = true;
                }
                if (endTurnButton != null)
                {
                    endTurnButton.interactable = true;
                }
            }
            else
            {
                if (drawCardButton != null)
                {
                    drawCardButton.interactable = false;
                }
                if (endTurnButton != null)
                {
                    endTurnButton.interactable = false;
                }
            }

            UpdateTownHallButtons(isLocalPlayersTurn);
        }

        private void HandleTurnEnded(Player activePlayer)
        {
            if (player == null)
            {
                return;
            }

            if (activePlayer == player)
            {
                isLocalPlayersTurn = false;
                hasStartedPlayingThisTurn = false;
                if (drawCardButton != null)
                {
                    drawCardButton.interactable = false;
                }
                if (endTurnButton != null)
                {
                    endTurnButton.interactable = false;
                }
            }
        }

        private void OnDestroy()
        {
            if (player?.HandManager != null)
            {
                player.HandManager.OnHandChanged -= UpdateHand;
            }

            if (drawCardButton != null)
            {
                drawCardButton.onClick.RemoveListener(OnDrawCardClicked);
            }

            if (endTurnButton != null)
            {
                endTurnButton.onClick.RemoveListener(OnEndTurnClicked);
            }

            if (drawFromDiscardButton != null)
            {
                drawFromDiscardButton.onClick.RemoveListener(OnDrawFromDiscardClicked);
            }

            if (titubaAbilityButton != null)
            {
                titubaAbilityButton.onClick.RemoveListener(OnTitubaAbilityClicked);
            }

            if (GameTurnManager.Instance != null)
            {
                GameTurnManager.Instance.TurnStarted -= HandleTurnStarted;
                GameTurnManager.Instance.TurnEnded -= HandleTurnEnded;
            }
        }
    }
}