
/*
* AUTHOR:
* REFERENCES:
* NOTES:
* TODO: [Planned improvements]
 * FIXME: [Known bugs or issues]
*/
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameCardUI : MonoBehaviour
{
    #region Vars
    public Image cardImage;
    //public TextMeshPro cardNameText;
    
    private Card card;
    #endregion

    #region Accessor Functions
    public void SetCard(Card newCard)
    {
        card = newCard;
        //cardNameText.text = card.Name;
        
        // Set card visuals if needed
    }
    #endregion
}
