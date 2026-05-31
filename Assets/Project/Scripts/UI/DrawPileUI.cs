/*
* AUTHOR: Ron Bresett
* REFERENCES:
* NOTES:
*   Primary Purpose: 
*   Responsibilities:
*        • 
*   Access Requirements:
*        • 
* TODO:
*    • 
* FIXME: [Known bugs or issues]
*/

using Salem.GameFlow;
using Salem.Players;
using UnityEngine;
using UnityEngine.UI;

namespace Salem.UI
{
    public class DrawPileUI : MonoBehaviour
{
    [SerializeField] private Button drawButton;
    [SerializeField] private GameTurnManager turnManager;

    private void Awake()
    {
        if (drawButton == null)
        {
            drawButton = GetComponent<Button>();
        }

        if (drawButton != null)
        {
            drawButton.onClick.AddListener(HandleDrawClicked);
            //Debug.Log("[DrawPileUI] Draw button listener added.");
        }
        else
        {
            Debug.LogError("[DrawPileUI] No Button found.");
        }
    }

    private void OnDestroy()
    {
        drawButton.onClick.RemoveListener(HandleDrawClicked);
    }

    private void HandleDrawClicked()
    {
        Debug.Log("[DrawPileUI] BUTTON CLICKED");

        if (turnManager == null)
        {
            Debug.LogError("[DrawPileUI] TurnManager is missing.");
            return;
        }

        turnManager.TryDrawTwoCards(turnManager.CurrentPlayer);
    }
}
}
