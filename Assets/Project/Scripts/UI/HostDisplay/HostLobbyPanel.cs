using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Salem.Networking; // PUBLIC DTOs + NetworkGameCoordinator lobby data ONLY.

// See the masking-boundary banner in HostTableView.cs: no file in this folder may reference Player /
// PlayerService / TryalCard / HandManager / StatusCards.

namespace Salem.UI.HostDisplay
{
    /// <summary>
    /// The pre-game lobby on the HOST (TV) screen: the room code, the two URLs people type into
    /// their phones and second-room browsers, and who has joined so far.
    ///
    /// This is the one screen whose whole job is to be READ ACROSS A ROOM. The room code and URLs
    /// are the only way anyone gets into the game, so they are the largest things on it.
    ///
    /// Data sources, all public by construction:
    ///  • Room code — <see cref="NetworkGameCoordinator.RoomCode"/> (lobby data, shown to the whole
    ///    room by design; the same source HostHeader already uses).
    ///  • Roster — <see cref="NetworkGameCoordinator.BuildLobbySeats"/>, a projection carrying only a
    ///    display name and an is-bot flag. Deliberately NOT the coordinator's `Seats` list, which is
    ///    IReadOnlyList&lt;Player&gt; and would breach this folder's boundary.
    ///  • URLs — serialized strings; deployment-specific, not game state.
    ///
    /// NOTHING private exists at lobby time anyway (no tryals dealt, no roles assigned), but the
    /// projection keeps that true by construction rather than by timing.
    ///
    /// Dismissal: hidden the moment <see cref="NetworkGameCoordinator.OnGameStarted"/> fires, which
    /// the coordinator raises BEFORE handing off to GamePhaseManager — so the panel is gone before
    /// the first public broadcast paints the board behind it. A late-enabled canvas re-checks
    /// <see cref="NetworkGameCoordinator.HasStarted"/> so it never reappears over a running game.
    /// </summary>
    public class HostLobbyPanel : MonoBehaviour
    {
        [Header("Root")]
        [Tooltip("Everything lobby-only. Switched off once the game starts.")]
        [SerializeField] private GameObject content;

        [Header("Room code")]
        [SerializeField] private TMP_Text roomCodeText;
        [Tooltip("Shown while waiting for the server to assign a code.")]
        [SerializeField] private string roomCodePlaceholder = "----";

        [Header("URLs")]
        [Tooltip("Host part only, e.g. \"salem.example.com\". The paths are appended below.")]
        [SerializeField] private string baseUrl = "";
        [Tooltip("{0} = baseUrl. Where PLAYERS join on their phones.")]
        [SerializeField] private string joinUrlFormat = "{0}/join";
        [Tooltip("{0} = baseUrl. Where a SECOND-ROOM display connects.")]
        [SerializeField] private string displayUrlFormat = "{0}/display";
        [SerializeField] private TMP_Text joinUrlText;
        [SerializeField] private TMP_Text displayUrlText;

        [Header("Roster")]
        [SerializeField] private Transform seatContainer;
        [Tooltip("Row prefab: a single TMP_Text. One per joined seat.")]
        [SerializeField] private TMP_Text seatRowPrefab;
        [Tooltip("{0} = joined count, {1} = minimum to begin.")]
        [SerializeField] private string waitingFormat = "{0} JOINED · {1} NEEDED TO BEGIN";
        [Tooltip("{0} = joined count. Shown once the minimum is met.")]
        [SerializeField] private string readyFormat = "{0} JOINED · READY TO BEGIN";
        [SerializeField] private TMP_Text statusText;
        [Tooltip("Suffix for a bot seat, so the room can see the table is being filled out.")]
        [SerializeField] private string aiSuffix = " (AI)";

        private NetworkGameCoordinator coordinator;
        private readonly List<TMP_Text> rows = new();

        private void OnEnable()
        {
            coordinator = FindFirstObjectByType<NetworkGameCoordinator>();
            if (coordinator == null)
            {
                Debug.LogWarning("[HostLobbyPanel] No NetworkGameCoordinator in scene — lobby hidden.");
                Hide();
                return;
            }

            coordinator.OnRoomCodeAssigned += HandleRoomCode;
            coordinator.OnRosterChanged += RenderRoster;
            coordinator.OnGameStarted += Hide;

            // Late-enable: the code and roster may already exist, and the game may already be
            // running (this canvas activated post-lobby) — in which case never show at all.
            if (coordinator.HasStarted) { Hide(); return; }

            if (content != null) content.SetActive(true);
            RenderUrls();
            HandleRoomCode(coordinator.RoomCode);
            RenderRoster();
        }

        private void OnDisable()
        {
            if (coordinator == null) return;
            coordinator.OnRoomCodeAssigned -= HandleRoomCode;
            coordinator.OnRosterChanged -= RenderRoster;
            coordinator.OnGameStarted -= Hide;
        }

        private void Hide()
        {
            if (content != null) content.SetActive(false);
        }

        private void HandleRoomCode(string code)
        {
            if (roomCodeText == null) return;
            roomCodeText.text = string.IsNullOrEmpty(code) ? roomCodePlaceholder : code;
        }

        private void RenderUrls()
        {
            // An unset baseUrl leaves both blank rather than printing "/join" on a TV, which would
            // read as a broken address people then try to type.
            bool haveBase = !string.IsNullOrWhiteSpace(baseUrl);
            if (joinUrlText != null)
                joinUrlText.text = haveBase ? string.Format(joinUrlFormat, baseUrl) : "";
            if (displayUrlText != null)
                displayUrlText.text = haveBase ? string.Format(displayUrlFormat, baseUrl) : "";
        }

        private void RenderRoster()
        {
            if (coordinator == null) return;

            var seats = coordinator.BuildLobbySeats();

            if (statusText != null)
            {
                int needed = Mathf.Max(0, coordinator.MinPlayers - seats.Count);
                statusText.text = needed > 0
                    ? string.Format(waitingFormat, seats.Count, needed)
                    : string.Format(readyFormat, seats.Count);
            }

            if (seatContainer == null || seatRowPrefab == null) return;

            // Pooled like HostTableView's seats: rows are reused and surplus hidden, so a player
            // leaving and rejoining doesn't churn the hierarchy.
            while (rows.Count < seats.Count)
                rows.Add(Instantiate(seatRowPrefab, seatContainer));

            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (row == null) continue;

                if (i >= seats.Count) { row.gameObject.SetActive(false); continue; }

                row.gameObject.SetActive(true);
                row.text = seats[i].IsAI ? seats[i].DisplayName + aiSuffix : seats[i].DisplayName;
            }
        }
    }
}
