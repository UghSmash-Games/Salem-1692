using Salem.Cards;
using Salem.Players;
using TMPro;
using UnityEngine;

namespace Salem.UI
{
    public class PlayerStatusUI : MonoBehaviour
    {
        [SerializeField] private Transform statusCardPanel;
        [SerializeField] private Transform tryalCardPanel;
        [SerializeField] private TextMeshProUGUI playerNameText;
        [SerializeField] private GameObject cardPrefab;
        [SerializeField] private GameObject tryalCardPrefab;

        private Player player;

        public void Initialize(Player p)
        {
            player = p;
            //playerNameText.text = p.PlayerName;
            UpdateStatusCards();
            UpdateTryalCards();
        }

        public void UpdateStatusCards()
        {
            foreach (Transform child in statusCardPanel) Destroy(child.gameObject);
            foreach (Card card in player.StatusCards)
            {
                var obj = Instantiate(cardPrefab, statusCardPanel);
                obj.GetComponent<GameCardUI>().SetCard(card);
            }
        }

        public void UpdateTryalCards()
        {
            Debug.Log($"[{player.PlayerName}] has {player.TryalCards.Count} Tryal cards.");

            foreach (Transform child in tryalCardPanel) Destroy(child.gameObject);
            foreach (TryalCard tc in player.TryalCards)
            {
                var obj = Instantiate(tryalCardPrefab, tryalCardPanel);
                obj.GetComponent<TryalCardUI>().AssignCard(tc);
            }
        }
    }

}