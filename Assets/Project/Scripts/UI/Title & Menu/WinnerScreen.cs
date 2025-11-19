using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;


public class WinnerScreen : MonoBehaviour
{
    public GameObject[] winnerText = new GameObject[2];
    public GameObject[] playerPanels = new GameObject[11];

    public enum Winner
    {
        WitchesWin = 0,
        VillagersWin = 1
    }
    private Winner winner;
    private int numOfWinningPlayers = 0;

    void Awake()
    {

    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (Application.isEditor) { winner = Winner.WitchesWin; } //DEBUG
        if (Application.isEditor) { ActivePlayerPanels(2); } //DEBUG

        SetWinnerText();
        SetPlayerPanels();
    }

    // Update is called once per frame
    void Update()
    {

    }

    void SetPlayerPanels()
    {
        for (int i = 0; i < numOfWinningPlayers && i < playerPanels.Length; i++)
        {
            // Placeholder code to set avatar and name, replace with actual data retrieval
            Sprite avatar = null; // Replace with actual avatar retrieval logic
            string playerName = "Player" + (i + 1); // Replace with actual player name retrieval logic
            SetSteamAvatar(playerPanels[i], avatar);
            SetSteamID(playerPanels[i], playerName);
        }
    }

    void SetSteamAvatar(GameObject playerPanel, Sprite avatar)
    {
        Image playerPanelImage = playerPanel.transform.Find("SteamAvatar").GetComponent<Image>();
        if (playerPanelImage != null)
            playerPanelImage.sprite = avatar;
        else
            Debug.LogError("PlayerPanel does not have an Image component named 'SteamAvatar'.");
    }

    void SetSteamID(GameObject playerPanel, string playerName)
    {
        TMP_Text playerPanelText = playerPanel.transform.Find("SteamID").GetComponent<TMP_Text>();
        if (playerPanelText != null)
            playerPanelText.text = playerName;
        else
            Debug.LogError("PlayerPanel does not have a Text component named 'SteamID'.");
    }

    void SetWinnerText()
    {
        switch (winner)
        {
            case Winner.WitchesWin:
                winnerText[(int)Winner.WitchesWin].SetActive(true);
                break;
            case Winner.VillagersWin:
                winnerText[(int)Winner.VillagersWin].SetActive(true);
                break;
            default:
                Debug.LogError("Invalid winner state.");
                break;
        }
    }

    // Method needs to be called first to set the winner from another script that needs to be WinnerScreen.Winner type
    public void SetWinner(WinnerScreen.Winner gameWinner) { winner = gameWinner; }

    public void LoadMainMenu() { SceneManager.LoadScene("Main_Menu"); }

    // Method needs to be called first to activate player panels based on the number of winning players (1 to 11)
    public void ActivePlayerPanels(int winningPlayers)
    {
        numOfWinningPlayers = winningPlayers;
        if (numOfWinningPlayers < 0 || numOfWinningPlayers > playerPanels.Length)
        {
            Debug.LogError("Number of winning players is out of range.");
            return;
        }
        for (int i = 0; i < numOfWinningPlayers && i < playerPanels.Length; i++)
        {
            playerPanels[i].SetActive(true);
        }
    }
}

