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
    [SerializeField] private Image CardImage;
    [SerializeField] private Sprite HiddenSprite;
    [SerializeField] private Sprite RevealedSprite_Witch;
    [SerializeField] private Sprite RevealedSprite_NotAWitch;
    [SerializeField] private Sprite RevealedSprite_Constable;

    //public TextMeshPro cardText;
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
            switch (card.TryalCardType)
            {
                case TryalCardType.Witch:
                    CardImage.sprite = RevealedSprite_Witch;
                    break;
                case TryalCardType.NotAWitch:
                    CardImage.sprite = RevealedSprite_NotAWitch;
                    break;
                case TryalCardType.Constable:
                    CardImage.sprite = RevealedSprite_Constable;
                    break;
            }
        }
        else
        {
            CardImage.sprite = HiddenSprite;
        }
    }
    #endregion
}
