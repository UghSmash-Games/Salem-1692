using Salem.Cards;
using Salem.Players;
using UnityEngine;

namespace Salem.UI
{
    public class PlayerInputUI : MonoBehaviour
    {
        [SerializeField] private Transform handPanel;
        [SerializeField] private GameObject cardUIPrefab;

        private Player player;

        public void Initialize(Player p)
        {
            player = p;
            player.HandManager.OnHandChanged += UpdateHand;
            UpdateHand();
        }

        public void UpdateHand()
        {
            Debug.Log("Updating Player Hand UI");
            foreach (Transform child in handPanel) Destroy(child.gameObject);
            foreach (Card card in player.HandManager.Hand)
            {
                var obj = Instantiate(cardUIPrefab, handPanel);
                obj.GetComponent<GameCardUI>().SetCard(card);
            }
        }

        private void Oestroy()
        {
            if (player?.HandManager != null)
            {
                player.HandManager.OnHandChanged -= UpdateHand;
            }
        }
    }
}