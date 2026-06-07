// TODO: Delete this file before Phase 2. It exists only to verify the
// NetworkManager Γåö server connection during Phase 1 development.

using UnityEngine;

namespace Salem.Networking
{
    public class NetworkConnectionTest : MonoBehaviour
    {
        [Header("Network Test")]
        [Tooltip("Automatically connect and create a room on Start.")]
        [SerializeField] private bool autoConnect = true;

        private void Start()
        {
            if (NetworkManager.Instance == null)
            {
                Debug.LogError("[NetworkTest] NetworkManager instance not found. " +
                               "Add a GameObject with NetworkManager to the scene.");
                return;
            }

            NetworkManager.Instance.OnConnectedToServer += () =>
            {
                Debug.Log("[NetworkTest] Connected to server");
                NetworkManager.Instance.CreateRoom();
            };

            NetworkManager.Instance.OnDisconnectedFromServer += () =>
            {
                Debug.Log("[NetworkTest] Disconnected from server");
            };

            NetworkManager.Instance.OnRoomCreated += (code) =>
            {
                Debug.Log($"[NetworkTest] Room created: {code}");
            };

            NetworkManager.Instance.OnPlayerJoined += (playerId, displayName) =>
            {
                Debug.Log($"[NetworkTest] Player joined: {playerId} ({displayName})");
            };

            if (autoConnect)
            {
                NetworkManager.Instance.ConnectToServer();
            }
        }
    }
}
