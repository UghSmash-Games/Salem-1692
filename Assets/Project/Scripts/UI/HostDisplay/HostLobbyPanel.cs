using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Salem.Data;      // TimerSettings only — a host-owned pace value, not game state.
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

        [Header("Host controls (LOBBY ONLY — see the docblock)")]
        [SerializeField] private Button startButton;
        [Tooltip("Reason the Start button is disabled, e.g. \"Waiting for 2 more players\".")]
        [SerializeField] private TMP_Text startBlockedText;
        [SerializeField] private Toggle fillWithAIToggle;
        [SerializeField] private Button targetMinusButton;
        [SerializeField] private Button targetPlusButton;
        [Tooltip("{0} = target seat count. Shown beside the +/- buttons.")]
        [SerializeField] private string targetCountFormat = "TABLE OF {0}";
        [SerializeField] private TMP_Text targetCountText;
        [Tooltip("Row holding the AI fill controls — hidden entirely when AI fill is off.")]
        [SerializeField] private GameObject aiFillRow;

        [Header("Pace (timer lengths)")]
        [Tooltip("Cycles Normal -> Relaxed -> Extended. One GLOBAL multiplier on every player-facing " +
                 "deadline; see TimerSettings for why it must never be per-player.")]
        [SerializeField] private Button paceButton;
        [Tooltip("{0} = pace name, {1} = multiplier, e.g. \"PACE: RELAXED (1.5x)\".")]
        [SerializeField] private string paceFormat = "PACE: {0} ({1}x)";
        [SerializeField] private TMP_Text paceText;

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
            coordinator.OnLobbySettingsChanged += RenderRoster;
            coordinator.OnGameStarted += Hide;

            if (startButton != null) startButton.onClick.AddListener(HandleStartClicked);
            if (fillWithAIToggle != null) fillWithAIToggle.onValueChanged.AddListener(HandleFillWithAIChanged);
            if (targetMinusButton != null) targetMinusButton.onClick.AddListener(() => NudgeTarget(-1));
            if (targetPlusButton != null) targetPlusButton.onClick.AddListener(() => NudgeTarget(+1));
            if (paceButton != null) paceButton.onClick.AddListener(CyclePace);

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
            if (startButton != null) startButton.onClick.RemoveListener(HandleStartClicked);
            if (fillWithAIToggle != null) fillWithAIToggle.onValueChanged.RemoveListener(HandleFillWithAIChanged);
            if (targetMinusButton != null) targetMinusButton.onClick.RemoveAllListeners();
            if (targetPlusButton != null) targetPlusButton.onClick.RemoveAllListeners();
            if (paceButton != null) paceButton.onClick.RemoveListener(CyclePace);

            if (coordinator == null) return;
            coordinator.OnRoomCodeAssigned -= HandleRoomCode;
            coordinator.OnRosterChanged -= RenderRoster;
            coordinator.OnLobbySettingsChanged -= RenderRoster;
            coordinator.OnGameStarted -= Hide;
        }

        // ─── Host controls ─────────────────────────────────────────

        /// <summary>
        /// The ONE place the game is started from the screen. Guarded twice over: the button is
        /// disabled unless <see cref="NetworkGameCoordinator.CanStart"/> allows it, and StartGame
        /// re-checks the same predicate and is idempotent — so a double-click, or a click that races
        /// a player leaving, cannot deal the game twice or start it short-handed.
        /// </summary>
        private void HandleStartClicked()
        {
            if (coordinator == null) return;
            coordinator.StartGame();
        }

        private void HandleFillWithAIChanged(bool value)
        {
            if (coordinator == null) return;
            coordinator.SetFillWithAI(value);
            // The coordinator raises OnLobbySettingsChanged, which repaints via RenderRoster.
        }

        /// <summary>
        /// Cycles the global pace. Deliberately a single cycling button, not a per-timer editor:
        /// the individual windows are balanced against each other (every prompt sits under the Day
        /// idle timer), so scaling them together preserves those relationships, while editing one
        /// could let a prompt outlive the turn that owns it.
        /// </summary>
        private void CyclePace()
        {
            var next = TimerSettings.Current switch
            {
                TimerSettings.Pace.Normal => TimerSettings.Pace.Relaxed,
                TimerSettings.Pace.Relaxed => TimerSettings.Pace.Extended,
                _ => TimerSettings.Pace.Normal,
            };
            TimerSettings.SetPace(next);
            RenderHostControls();
        }

        private void NudgeTarget(int delta)
        {
            if (coordinator == null) return;
            coordinator.SetTargetPlayerCount(coordinator.TargetPlayerCount + delta);
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

        /// <summary>
        /// Repaints the host controls from the coordinator. Called on every roster change AND every
        /// settings change, because "can we start?" depends on both: a player leaving can invalidate
        /// a Start that was legal a second ago.
        /// </summary>
        private void RenderHostControls()
        {
            if (coordinator == null) return;

            bool canStart = coordinator.CanStart(out string reason);

            if (startButton != null) startButton.interactable = canStart;
            if (startBlockedText != null)
            {
                startBlockedText.text = reason;
                startBlockedText.gameObject.SetActive(!canStart);
            }

            // Toggle reflects state without re-entering the setter (SetIsOnWithoutNotify), so a
            // repaint triggered BY the toggle cannot loop back into another settings change.
            if (fillWithAIToggle != null && fillWithAIToggle.isOn != coordinator.FillWithAI)
                fillWithAIToggle.SetIsOnWithoutNotify(coordinator.FillWithAI);

            // The target-count row is meaningless with AI fill off — hide it rather than grey it,
            // so the lobby reads as one decision ("fill the table?") and not two.
            if (aiFillRow != null) aiFillRow.SetActive(coordinator.FillWithAI);

            if (targetCountText != null)
                targetCountText.text = string.Format(targetCountFormat, coordinator.TargetPlayerCount);

            // Clamp feedback: the bounds are the rulebook's (TryalDistribution covers 4–12 only), so
            // the buttons go dead at the edges instead of silently no-op'ing.
            if (targetMinusButton != null)
                targetMinusButton.interactable = coordinator.TargetPlayerCount > coordinator.MinPlayers;
            if (targetPlusButton != null)
                targetPlusButton.interactable = coordinator.TargetPlayerCount < coordinator.MaxPlayers;

            if (paceText != null)
                paceText.text = string.Format(paceFormat,
                    TimerSettings.Current.ToString().ToUpperInvariant(),
                    TimerSettings.Multiplier.ToString("0.#"));
            if (paceButton != null) paceButton.interactable = !TimerSettings.Locked;
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

            RenderHostControls();

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
