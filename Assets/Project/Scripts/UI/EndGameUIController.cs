/*
* AUTHOR: Ron Bresett
* REFERENCES:
* NOTES:
* TODO: [Planned improvements]
* FIXME: [Known bugs or issues]
*/

using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Salem.GameFlow;
using Salem.Data;

namespace Salem.UI
{
    public class EndGameUIController : MonoBehaviour
    {
        [Header("Root")]
        [SerializeField] private GameObject rootPanel; // EndGameUI Panel (set inactive in scene)

        [Header("Winner Banner")]
        [SerializeField] private TMP_Text winnerText;

        [Header("Winners List")]
        [SerializeField] private Transform winnersContainer; // parent for entries
        [SerializeField] private GameObject winnerEntryPrefab; // WinningPlayerEntry prefab

        [Header("Buttons")]
        [SerializeField] private Button backToMainMenuButton;
        [SerializeField] private Button quitButton;

        [Header("Strings")]
        [SerializeField] private string villagersWinText = "Villagers Win!";
        [SerializeField] private string witchesWinText = "Witches Win!";
        [SerializeField] private string drawText = "It's a Draw";

        private bool initialized;

        private void Awake()
        {
            if (rootPanel != null)
                rootPanel.SetActive(false);

            if (backToMainMenuButton != null)
                backToMainMenuButton.onClick.AddListener(BackToMainMenu);

            if (quitButton != null)
                quitButton.onClick.AddListener(QuitGame);
        }

        private void OnEnable()
        {
            // subscribe when enabled
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnGameEnded += HandleGameEnded;
                initialized = true;
            }
            else
            {
                Debug.LogWarning("[EndGameUI] GameManager not present at OnEnable()");
            }
        }

        private void OnDisable()
        {
            // unsubscribe
            if (initialized && GameManager.Instance != null)
            {
                GameManager.Instance.OnGameEnded -= HandleGameEnded;
            }
            initialized = false;
        }

        private void OnDestroy()
        {
            if (backToMainMenuButton != null)
                backToMainMenuButton.onClick.RemoveListener(BackToMainMenu);

            if (quitButton != null)
                quitButton.onClick.RemoveListener(QuitGame);
        }

        private void HandleGameEnded(EndGameResult result)
        {
            if (rootPanel != null) rootPanel.SetActive(true);

            // Winner banner
            switch (result.WinningTeam)
            {
                case Team.Villagers:
                    winnerText.text = villagersWinText;
                    break;
                case Team.Witches:
                    winnerText.text = witchesWinText;
                    break;
                default:
                    winnerText.text = drawText;
                    break;
            }

            // Clear previous children
            for (int i = winnersContainer.childCount - 1; i >= 0; i--)
            {
                Destroy(winnersContainer.GetChild(i).gameObject);
            }

            // Populate winners
            foreach (var p in result.Winners)
            {
                GameObject entryObj = Instantiate(winnerEntryPrefab, winnersContainer);

                WinningPlayerEntryUI entryUI = entryObj.GetComponent<WinningPlayerEntryUI>();

                if (entryUI == null)
                {
                    Debug.LogError("[EndGameUIController] Winner entry prefab is missing WinningPlayerEntryUI.");
                    return;
                }

                entryUI.Bind(p);
            }

            // Optional: also show result.Reason somewhere if you want
            // e.g., a small TMP_Text for "All witches eliminated" etc.
        }

        public void BackToMainMenu()
        {
            // Unpause
            Time.timeScale = 1f;
            UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
        }

        public void QuitGame()
        {
            // Unpause not needed, but harmless
            Time.timeScale = 1f;
            Application.Quit();
            #if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
            #endif
        }
    }

}