/*
* AUTHOR: Ron Bresett
* REFERENCES:
* NOTES:
* TODO: [Planned improvements]
 * FIXME: [Known bugs or issues]
*/
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TryalCardUI : MonoBehaviour
{

    #region Vars
    //public TextMeshPro cardText;
    [SerializeField] private Image CardImage;
    private TryalCard assignedCard;
    #endregion

    #region Accessor Functions
    public void AssignCard(TryalCard card)
    {
        assignedCard = card;
        UpdateCardVisual(assignedCard);
    }

    public void UpdateCardVisual(TryalCard card)
    {
        if (card.IsRevealed)
        { 
            CardImage.sprite = card.RevealedCardImage;
            /*
            switch (card.TryalCardType)
            {
                case TryalCardType.Witch:
                    CardImage.sprite = card.RevealedSprite_Witch;
                    break;
                case TryalCardType.NotAWitch:
                    CardImage.sprite = card.RevealedSprite_NotAWitch;
                    break;
                case TryalCardType.Constable:
                    CardImage.sprite = card.RevealedSprite_Constable;
                    break;
            }
            */
        }
        else
        {
            CardImage.sprite = card.HiddenCardImage;
        }
    }
    #endregion
}
