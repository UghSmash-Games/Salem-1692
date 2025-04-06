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
        UpdateTryalCardVisual(assignedCard);
    }

    public void UpdateTryalCardVisual(TryalCard card)
    {
        if (card.IsRevealed)
        { 
            CardImage.sprite = card.RevealedCardImage;
        }
        else
        {
            CardImage.sprite = card.HiddenCardImage;
        }
    }
    #endregion
}
