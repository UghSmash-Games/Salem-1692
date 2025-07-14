using UnityEngine;
using Salem.Cards;
using Salem.Players;
using Salem.UI;

namespace Salem.GameFlow
{
    public class TargetingManager : MonoBehaviour
    {
        public static TargetingManager Instance;
        private Card currentCard;

        private void Awake() => Instance = this;

        public void BeginTargeting(Card card)
        {
            currentCard = card;
            Debug.Log($"[Targeting] Select a target for {card.Name}");

            // Visually highlight possible targets in UI
        }

        public void OnTargetSelected(Player target)
        {
            Debug.Log($"[Targeting] {target.PlayerNameText} selected for {currentCard.Name}");

            CardEffectManager.Instance.ExecuteCardEffect(currentCard, target);
            currentCard = null;
        }
    }
}