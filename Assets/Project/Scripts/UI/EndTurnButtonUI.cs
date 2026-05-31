using Salem.GameFlow;
using UnityEngine;
using UnityEngine.UI;

namespace Salem.UI
{
    public class EndTurnButtonUI : MonoBehaviour
    {
        [SerializeField] private Button endTurnButton;
        [SerializeField] private GameTurnManager turnManager;

        private void Awake()
        {
            if (endTurnButton == null)
                endTurnButton = GetComponent<Button>();

            endTurnButton.onClick.AddListener(HandleEndTurnClicked);

            Hide();
        }

        private void OnDestroy()
        {
            endTurnButton.onClick.RemoveListener(HandleEndTurnClicked);
        }

        public void Show()
        {
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        private void HandleEndTurnClicked()
        {
            Debug.Log("[EndTurnButtonUI] End Turn clicked.");

            GameTurnManager.Instance.RequestEndTurn(
                GameTurnManager.Instance.CurrentPlayer
            );

            Hide();
        }
    }
}