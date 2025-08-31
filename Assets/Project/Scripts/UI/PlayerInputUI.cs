using Salem.Cards;
using Salem.GameFlow;
using Salem.Players;
using UnityEngine;

namespace Salem.UI
{
    public class PlayerInputUI : MonoBehaviour
    {
        [SerializeField] private Transform handPanel;
        [SerializeField] private GameObject cardUIPrefab;
        [SerializeField] private TargetPickerUI targetPicker;

        private Player player;

        public void Initialize(Player p)
        {
            //Debug.Log("Initializing Player");
            player = p;
            player.HandManager.OnHandChanged += UpdateHand;
            UpdateHand();
        }

        private void SpawnCardUI(Card card)
        {
            var obj = Instantiate(cardUIPrefab, handPanel);
            obj.GetComponent<GameCardUI>().SetCard(card);

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
            var ac = selected as ActionCardSO;
            if (ac == null)
            {
                // Non-action cards shouldn’t be in hand, but guard anyway
                CardEffectManager.Instance.ExecuteCardEffect(selected, null);
                return;
            }

            if (ac.NeedsTarget)
            {
                bool two = ac.RequiresSecondTarget;
                // Open the picker; exclude the acting player by default
                targetPicker.Open(player, two, (primary, secondary) =>
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

        private void OnDestroy()
        {
            if (player?.HandManager != null)
            {
                player.HandManager.OnHandChanged -= UpdateHand;
            }
        }
    }
}