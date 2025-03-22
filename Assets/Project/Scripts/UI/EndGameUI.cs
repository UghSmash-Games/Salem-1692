/*
* AUTHOR:
* REFERENCES:
* NOTES:
* TODO: [Planned improvements]
 * FIXME: [Known bugs or issues]
*/
using System;
using UnityEngine;
using UnityEngine.UI;

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
