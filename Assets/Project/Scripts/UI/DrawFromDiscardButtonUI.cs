using Salem.GameFlow;
using Salem.Players;
using UnityEngine;
using UnityEngine.UI;

namespace Salem.UI
{
    public class DrawFromDiscardButtonUI : MonoBehaviour
    {
        [SerializeField] private Button drawFromDiscardButton;
        [SerializeField] private GameTurnManager turnManager;

        private void Awake()
        {
            if (drawFromDiscardButton == null)
                drawFromDiscardButton = GetComponent<Button>();

            drawFromDiscardButton.onClick.AddListener(HandleClicked);

            Hide();
        }

        private void OnDestroy()
        {
            if (drawFromDiscardButton != null)
                drawFromDiscardButton.onClick.RemoveListener(HandleClicked);
        }

        private void HandleClicked()
        {
            Player currentPlayer = turnManager.CurrentPlayer;

            Debug.Log($"[DrawFromDiscardButtonUI] Clicked for {currentPlayer?.PlayerNameText}");

            turnManager.TryDrawFromDiscard(currentPlayer);

            Hide();
        }

        public void Show()
        {
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}