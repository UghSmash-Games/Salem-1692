using Salem.Cards;
using Salem.GameFlow;
using Salem.Players;
using UnityEngine;

namespace Salem.UI
{
    public class PlayerInputUI : MonoBehaviour
    {
        public static PlayerInputUI Instance { get; private set; }

        [SerializeField] private Transform handPanel;
        [SerializeField] private GameObject cardUIPrefab;

        private Player player;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        public void Initialize(Player p)
        {
            //Debug.Log("Initializing Player");
            player = p;
            player.HandManager.OnHandChanged += UpdateHand;
            UpdateHand();
        }

        public void UpdateHand()
        {
            //Debug.Log("Updating Player Hand UI");
            foreach (Transform child in handPanel) Destroy(child.gameObject);
            foreach (Card card in player.HandManager.Hand)
            {
                var obj = Instantiate(cardUIPrefab, handPanel);
                obj.GetComponent<GameCardUI>().SetCard(card);
            }
        }

        public void TryPlayCard(Card card)
        {
            if (!CanPlayCard(card))
            {
                Debug.Log("Card not playable!");
                return;
            }

            if (card.RequiresTarget)
            {
                TargetingManager.Instance.BeginTargeting(card);
            }
            else
            {
                CardEffectManager.Instance.ExecuteCardEffect(card, null);
            }
        }

private bool CanPlayCard(Card card)
{
    // placeholder — implement actual logic later
    return true;
}

        private void Destroy()
        {
            if (player?.HandManager != null)
            {
                player.HandManager.OnHandChanged -= UpdateHand;
            }
        }
    }
}