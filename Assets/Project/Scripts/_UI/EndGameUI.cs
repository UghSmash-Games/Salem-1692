/*
* AUTHOR:
* REFERENCES:
* NOTES:
*   Primary Purpose: Displays end screen and result summary.
*   Responsibilities:
*        • Show win/lose text
*        • Trigger game reset or quit
*   Access Requirements:
*        • GameManager
*        • GameStateManager

* TODO:
*    • Add OnRestart, OnQuit events

* FIXME: [Known bugs or issues]
*/
using System;
using UnityEngine;
using UnityEngine.UI;
using Salem.GameFlow;
using Salem.Managers.GameState;

namespace Salem.UI
{
    public class EndGameUI : MonoBehaviour
    {
        #region Vars
        public static event Action OnRestart;
        public static event Action OnQuit;

        [SerializeField] private Text resultText;
        [SerializeField] private GameObject endGamePanel;
        #endregion

        #region Accessor Functions
        public void Show(string result)
        {
            resultText.text = result;
            endGamePanel.SetActive(true);
        }

        public void Restart()
        {
            OnRestart?.Invoke();
        }

        public void Quit()
        {
            OnQuit?.Invoke();
        }
        #endregion
    }
}