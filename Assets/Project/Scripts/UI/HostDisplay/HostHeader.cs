using TMPro;
using UnityEngine;
using Salem.Networking; // PUBLIC DTOs + NetworkGameCoordinator (public lobby data) ONLY.

namespace Salem.UI.HostDisplay
{
    /// <summary>
    /// HOST (TV) header: room code + join/display URL + current phase label (7b; mirror blueprint:
    /// MirrorScreen header + its footer phase tag).
    ///
    /// Data sources, all public:
    ///  • Room code — <see cref="NetworkGameCoordinator.RoomCode"/> via OnRoomCodeAssigned (lobby data
    ///    shown to the whole room by design; explicitly allowed by the Phase-7 boundary).
    ///  • URL — a serialized string on this component (deployment-specific, not game state).
    ///  • Phase — the public <see cref="GameStateUpdateMsg.phase"/> field, handed in by the controller.
    /// </summary>
    public class HostHeader : MonoBehaviour
    {
        [Header("Lobby (optional — for a large standalone code/URL display)")]
        [SerializeField] private TMP_Text roomCodeText;   // e.g. "MAST"
        [SerializeField] private TMP_Text urlText;        // e.g. "salem.example.com/join"

        [Tooltip("Join/display URL shown next to the room code (deployment-specific).")]
        [SerializeField] private string displayUrl = "";

        [Header("In-game header")]
        [Tooltip("The combined subtitle, e.g. \"TABLE MAST · 12 SOULS\".")]
        [SerializeField] private TMP_Text tableLineText;
        [Tooltip("{0} = room code, {1} = seat count (living + dead).")]
        [SerializeField] private string tableLineFormat = "TABLE {0} · {1} SOULS";
        [Tooltip("Used before the game is dealt, when no seat count exists yet. {0} = room code.")]
        [SerializeField] private string tableLineFormatNoCount = "TABLE {0}";
        [SerializeField] private TMP_Text phaseText;      // e.g. "DAY"

        [Header("Phase pill")]
        [Tooltip("Optional ember dot beside the phase label. Pulses continuously, as in the design.")]
        [SerializeField] private CanvasGroup phaseDot;
        [SerializeField] private float dotPulseSeconds = 2.6f;
        [SerializeField, Range(0f, 1f)] private float dotMinAlpha = 0.5f;

        private NetworkGameCoordinator coordinator;
        private string roomCode = "";
        private int lastSoulCount;

        private void OnEnable()
        {
            // Same launch-time override as the lobby, so the two can never print different addresses
            // (they were independent Inspector fields, edited by hand, with nothing keeping them in
            // step). The override is a BASE url, so the join path is appended here.
            if (urlText != null)
            {
                var overrideBase = Salem.Networking.DeploymentConfig.ClientBaseUrlOverride();
                urlText.text = overrideBase != null ? overrideBase + "/join" : displayUrl;
            }

            coordinator = FindFirstObjectByType<NetworkGameCoordinator>();
            if (coordinator != null)
            {
                coordinator.OnRoomCodeAssigned += HandleRoomCode;
                // Late-enable: the code may already exist (e.g. this canvas activated post-lobby).
                if (!string.IsNullOrEmpty(coordinator.RoomCode)) HandleRoomCode(coordinator.RoomCode);
            }
        }

        private void OnDisable()
        {
            if (coordinator != null) coordinator.OnRoomCodeAssigned -= HandleRoomCode;
        }

        private void Update()
        {
            // Ember pulse on the phase dot. Unscaled so it keeps breathing while pauseOnGameEnd
            // has Time.timeScale at 0.
            if (phaseDot == null) return;
            phaseDot.alpha = Mathf.Lerp(dotMinAlpha, 1f,
                0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f /
                                        Mathf.Max(0.01f, dotPulseSeconds)));
        }

        private void HandleRoomCode(string code)
        {
            roomCode = code ?? "";
            if (roomCodeText != null) roomCodeText.text = roomCode;
            RenderTableLine(lastSoulCount);
        }

        /// <summary>
        /// Called by HostDisplayController on every public state broadcast. Everything here comes
        /// from the PUBLIC board: the phase name, and the seat count (players.Length) — which is the
        /// total dealt into the game, living and dead, matching the design's "12 SOULS".
        /// </summary>
        public void Render(GameStateUpdateMsg state)
        {
            if (state == null) return;

            if (phaseText != null)
                phaseText.text = string.IsNullOrEmpty(state.phase) ? "" : state.phase.ToUpperInvariant();

            lastSoulCount = state.players?.Length ?? 0;
            RenderTableLine(lastSoulCount);
        }

        private void RenderTableLine(int souls)
        {
            if (tableLineText == null) return;

            // The room code and the seat count arrive from DIFFERENT sources at different times —
            // the code from the lobby event, the count from the first public broadcast — so render
            // from the cached pair and fall back to the code alone until the game is dealt.
            tableLineText.text = souls > 0
                ? string.Format(tableLineFormat, roomCode, souls)
                : string.Format(tableLineFormatNoCount, roomCode);
        }
    }
}
