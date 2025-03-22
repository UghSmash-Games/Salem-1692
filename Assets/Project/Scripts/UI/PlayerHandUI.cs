/*
* AUTHOR: Ron Bresett
* REFERENCES:
* NOTES:
* TODO: []
 * FIXME: [Known bugs or issues]
*/
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PlayerHandUI : MonoBehaviour
{
    #region Vars 
    [SerializeField] private Transform handPanel;
    [SerializeField] private Transform tryalCardContainer;
    [SerializeField] private GameObject cardUIPrefab;
    [SerializeField] private GameObject tryalCardPrefab;

    private List<GameCardUI> cardUIElements = new List<GameCardUI>();
    #endregion

    #region Accessor Functions
    //Called in GameManger at Start, Will need to be called when Cards are used
    public void UpdateHand(List<Card> hand)
    {
        //Reset Hand
        foreach (Transform child in handPanel) Destroy(child.gameObject);
        cardUIElements.Clear();

        //Re-Populate Hand
        foreach (Card card in hand)
        {
            GameObject cardGO = Instantiate(cardUIPrefab, handPanel);
            GameCardUI cardUI = cardGO.GetComponent<GameCardUI>();
            cardUI.SetCard(card);
            cardUIElements.Add(cardUI);
        }
    }

    //Called In Game Manager At Start
    public void PopulateTryalCards(Player player)
    {
        // Clear existing cards
        foreach (Transform child in tryalCardContainer.transform)
        {
            Destroy(child.gameObject);
        }

        // Instantiate new Tryal Cards
        foreach (TryalCard card in player.TryalCards)
        {
            GameObject tryalCardUI = Instantiate(tryalCardPrefab, tryalCardContainer.transform);
            tryalCardUI.GetComponent<TryalCardUI>().AssignCard(card);
        }
    }
    #endregion
}
