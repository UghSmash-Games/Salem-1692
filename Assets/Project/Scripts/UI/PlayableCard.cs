using UnityEngine;
using UnityEngine.EventSystems;
using Salem.Cards;

namespace Salem.UI
{
    public class PlayableCard : MonoBehaviour, IPointerClickHandler
    {
        public Card LinkedCard;

        public void OnPointerClick(PointerEventData eventData)
        {
            Debug.Log($"Clicked card: {LinkedCard.Name}");
            PlayerInputUI.Instance.TryPlayCard(LinkedCard);
        }
    }
}

