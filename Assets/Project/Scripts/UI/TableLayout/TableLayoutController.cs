/*
* AUTHOR: Ron Bresett
* REFERENCES:
* NOTES:
*   Primary Purpose: Controls Layout of Player Boards
*   Responsibilities:
*   Access Requirements:
* TODO: [Planned improvements]
* FIXME: [Known bugs or issues]
*/
using UnityEngine;
using System;
using System.Collections.Generic;
using Salem.Players;
using Salem.Cards;
using Salem.GameFlow;

namespace Salem.UI
{
    [ExecuteAlways]
    public class TableLayoutController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private RectTransform tableArea;          // The middle area (stretch in the center)
        [SerializeField] private RectTransform playerContainer; 
        [SerializeField] private GameObject playerBoardPrefab;   // Where PlayerBoardUI instances live

        [Header("Local Player Placement")]
        [Tooltip("Normalized position within tableArea. (0.5, 0) is bottom-center.")]
        [SerializeField] private Vector2 localAnchorNormalized = new Vector2(0.5f, 0.08f);
        [SerializeField] private float localScale = 1.15f;

        [Header("Remote Player Arc")]
        [Tooltip("Ellipse radii as a fraction of tableArea size.")]
        [SerializeField] private Vector2 ellipseRadiiNormalized = new Vector2(0.45f, 0.34f);

        [Tooltip("Arc start/end angles in degrees. 180 is left, 0 is right, 90 is up, 270 is down.")]
        [SerializeField] private float arcStartDeg = 210f;
        [SerializeField] private float arcEndDeg = -30f;

        [Tooltip("Pushes the arc up/down inside the table area.")]
        [SerializeField] private Vector2 arcCenterOffsetNormalized = new Vector2(0.0f, 0.18f);

        [Header("Scaling by Player Count")]
        [SerializeField] private float remoteScale_4to6 = 1.00f;
        [SerializeField] private float remoteScale_7to9 = 0.90f;
        [SerializeField] private float remoteScale_10to12 = 0.80f;

        [Header("Seating / Ordering")]
        [Tooltip("If true, remote players are laid out in the order given (left->right across the arc).")]
        [SerializeField] private bool keepInputOrder = true;

        [Tooltip("If false, remote players will be centered on the arc (symmetrical).")]
        [SerializeField] private bool centerPlayersOnArc = true;

        [SerializeField] private EndTurnButtonUI endTurnButtonUI;

        // Public: call SetPlayers() when the roster changes.
        private readonly List<PlayerSeat> _seats = new();
        private string _localPlayerId;

        // Track changes to re-layout automatically when the screen/table changes.
        private Vector2 _lastTableSize;
        private int _lastPlayerCount;

        //Targeting Section
        private Card selectedCard;
        private Player selectedCardOwner;
        private bool isSelectingTarget;

        //Tryal Targeting Section
        private bool isSelectingTryal;
        private Player selectedTryalPlayer;
        private Action<int> pendingTryalCallback;

        private readonly Dictionary<Player, PlayerBoardUI> playerBoards = new();

        private Action<Player> pendingTargetCallback;
        private Func<Player, bool> pendingTargetValidator;

        [Serializable]
        public class PlayerSeat
        {
            public string playerId;
            [Tooltip("Drop PlayerBoardUI reference here.")]
            public RectTransform board;     // PlayerBoardUI root RectTransform
            public bool isLocal;
        }

        private void Reset()
        {
            if (tableArea == null) tableArea = GetComponent<RectTransform>();
            if (playerContainer == null) playerContainer = tableArea;
        }

        private void OnEnable()
        {
            ForceLayout();
        }

        private void Update()
        {
            // In play mode, only re-layout if something important changed.
            // In edit mode (ExecuteAlways), this keeps the preview updated.
            if (tableArea == null) return;

            Vector2 size = tableArea.rect.size;
            if (size != _lastTableSize || _seats.Count != _lastPlayerCount)
            {
                ForceLayout();
            }
        }

        /// <summary>
        /// Provide all seats (local included) and the local player id.
        /// Call this when player count changes or seating order changes.
        /// </summary>
        public void SetPlayers(List<PlayerSeat> seats) //string localPlayerId
        {
            _seats.Clear();
            if (seats != null)
            {
                _seats.AddRange(seats);
            }
            //_localPlayerId = localPlayerId;

            ForceLayout();
        }

        /// <summary>
        /// Call when something changes that affects layout (roster/resolution/UI scale).
        /// </summary>
        public void ForceLayout()
        {
            if (tableArea == null || playerContainer == null) return;

            _lastTableSize = tableArea.rect.size;
            _lastPlayerCount = _seats.Count;

            if (_seats.Count == 0)
                return;

            // Split local vs remote
            PlayerSeat local = null;
            List<PlayerSeat> remotes = new();

            for (int i = 0; i < _seats.Count; i++)
            {
                var s = _seats[i];
                //s.isLocal = (!string.IsNullOrEmpty(_localPlayerId) && s.playerId == _localPlayerId);
                if (s.isLocal) local = s;
                else remotes.Add(s);
            }

            // If we didn't find local by id, fall back to "first seat is local"
            if (local == null)
            {
                local = _seats[0];
                local.isLocal = true;
                remotes.Clear();
                for (int i = 1; i < _seats.Count; i++)
                {
                    _seats[i].isLocal = false;
                    remotes.Add(_seats[i]);
                }
            }

            ApplyLocalPlacement(local);
            ApplyRemotePlacement(remotes);
        }

        private void ApplyLocalPlacement(PlayerSeat local)
        {
            if (local?.board == null) return;

            // Keep everything under the container (for sorting/layers)
            local.board.SetParent(playerContainer, worldPositionStays: false);

            // Anchor/pivot to center for easier math
            local.board.anchorMin = local.board.anchorMax = new Vector2(0.5f, 0.5f);
            local.board.pivot = new Vector2(0.5f, 0.5f);

            // Place at bottom-center (inside table area)
            Vector2 size = tableArea.rect.size;
            Vector2 pos = new Vector2(
                (localAnchorNormalized.x - 0.5f) * size.x,
                (localAnchorNormalized.y - 0.5f) * size.y
            );

            local.board.anchoredPosition = pos;
            local.board.localScale = Vector3.one * localScale;

            // Ensure local appears "in front" if needed
            local.board.SetAsLastSibling();
        }

        private void ApplyRemotePlacement(List<PlayerSeat> remotes)
        {
            int remoteCount = remotes.Count;
            if (remoteCount == 0) return;

            float remoteScale = GetRemoteScaleForTotalPlayers(_seats.Count);

            // Compute ellipse settings in pixels
            Vector2 size = tableArea.rect.size;
            Vector2 radiiPx = new Vector2(size.x * ellipseRadiiNormalized.x, size.y * ellipseRadiiNormalized.y);

            Vector2 arcCenterPx = new Vector2(
                (arcCenterOffsetNormalized.x) * size.x,
                (arcCenterOffsetNormalized.y) * size.y
            );

            // Determine angle list
            // We place along arcStart -> arcEnd.
            // If centering is enabled, we place symmetrically by choosing angles around arc midpoint.
            float start = arcStartDeg;
            float end = arcEndDeg;

            // Normalize to a consistent direction (clockwise vs counterclockwise)
            // We’ll step from start to end in remoteCount steps.
            float totalSpan = ShortestArcSpanDegrees(start, end);

            // Build seating order
            if (!keepInputOrder)
            {
                // If we later want, sort by seat index, join order, etc.
                // Leaving as-is for now.
            }

            for (int i = 0; i < remoteCount; i++)
            {
                PlayerSeat seat = remotes[i];
                if (seat?.board == null) continue;

                seat.board.SetParent(playerContainer, worldPositionStays: false);
                seat.board.anchorMin = seat.board.anchorMax = new Vector2(0.5f, 0.5f);
                seat.board.pivot = new Vector2(0.5f, 0.5f);

                float t;
                if (remoteCount == 1)
                {
                    t = 0.5f;
                }
                else
                {
                    t = i / (remoteCount - 1f);
                }

                // Optionally center players on arc by shifting them toward the middle
                // This helps when remoteCount is small so they don't sit too far at the extremes.
                if (centerPlayersOnArc)
                {
                    // Blend toward middle spacing for small counts
                    float centerBias = Mathf.InverseLerp(11f, 3f, remoteCount); // more bias when fewer players
                    float centeredT = (t - 0.5f) * (1f - 0.25f * centerBias) + 0.5f;
                    t = centeredT;
                }

                float angleDeg = start + totalSpan * t;
                Vector2 pos = EllipsePoint(angleDeg, radiiPx, arcCenterPx);

                seat.board.anchoredPosition = pos;
                seat.board.localScale = Vector3.one * remoteScale;

                // Remote boards behind local (sibling order)
                seat.board.SetAsFirstSibling();
            }
        }

        private float GetRemoteScaleForTotalPlayers(int totalPlayers)
        {
            if (totalPlayers <= 6) return remoteScale_4to6;
            if (totalPlayers <= 9) return remoteScale_7to9;
            return remoteScale_10to12;
        }

        private static Vector2 EllipsePoint(float angleDeg, Vector2 radiiPx, Vector2 centerPx)
        {
            float rad = angleDeg * Mathf.Deg2Rad;
            float x = Mathf.Cos(rad) * radiiPx.x + centerPx.x;
            float y = Mathf.Sin(rad) * radiiPx.y + centerPx.y;
            return new Vector2(x, y);
        }

        /// <summary>
        /// Returns a span (could be negative) that moves from start to end "nicely" even if end is negative.
        /// Example: 210 -> -30 should span -240 or +120 depending on shortest direction.
        /// For a table arc, we typically want the long sweep across the top, so we bias toward the larger span.
        /// </summary>
        private static float ShortestArcSpanDegrees(float startDeg, float endDeg)
        {
            // Wrap both into [0, 360)
            float s = Mathf.Repeat(startDeg, 360f);
            float e = Mathf.Repeat(endDeg, 360f);

            float delta = e - s;                // could be negative
            float deltaAlt = delta > 0 ? delta - 360f : delta + 360f;

            // For table layouts, the "long" arc often looks better (more room).
            // Choose the delta with larger absolute value (longer sweep).
            return Mathf.Abs(delta) >= Mathf.Abs(deltaAlt) ? delta : deltaAlt;
        }

        public void BuildTable(IReadOnlyList<Player> players) //string localPlayerId removed unitl implemented
        {
            foreach (Transform child in playerContainer)
            {
                Destroy(child.gameObject);
            }

            List<PlayerSeat> seats = new();

            foreach (Player player in players)
            {
                GameObject boardObj = Instantiate(playerBoardPrefab, playerContainer);
                RectTransform boardRect = boardObj.GetComponent<RectTransform>();

                PlayerBoardUI boardUI = boardObj.GetComponent<PlayerBoardUI>();
                if (boardUI != null)
                {
                    boardUI.Bind(player);
                    playerBoards[player] = boardUI;
                    boardUI.OnHandCardClicked += BeginTargetSelection;
                    boardUI.OnBoardClicked += HandlePlayerBoardClicked;
                    boardUI.OnTryalCardClicked += HandleTryalCardClicked;
                }

                seats.Add(new PlayerSeat
                {
                    playerId = player.PlayerNameText,
                    board = boardRect,
                    isLocal = player.IsLocalPlayer
                });
            }

            SetPlayers(seats); //, localPlayerId
        }

        public void SetCurrentTurn(Player currentPlayer)
        {
            /*Debug.Log(
                $"[TableLayoutController] Setting current turn to: " +
                $"{currentPlayer.PlayerNameText}"
            );*/

            foreach (var pair in playerBoards)
            {
                bool isCurrentPlayer = pair.Key == currentPlayer;

                /*Debug.Log(
                    $"[TableLayoutController] Updating board: " +
                    $"{pair.Key.PlayerNameText} | isCurrentPlayer = {isCurrentPlayer}"
                );*/

                pair.Value.SetTurnIndicator(isCurrentPlayer);
            }
        }

        public void BeginTargetSelection(
            Player source,
            string prompt,
            Func<Player, bool> isValidTarget,
            Action<Player> onTargetChosen)
        {
            isSelectingTarget = true;

            selectedCard = null;
            selectedCardOwner = source;

            pendingTargetCallback = onTargetChosen;
            pendingTargetValidator = isValidTarget;

            Debug.Log($"[TableLayoutController] {prompt}");

            foreach (var pair in playerBoards)
            {
                Player player = pair.Key;
                PlayerBoardUI board = pair.Value;

                bool canTarget = isValidTarget(player);

                board.SetTargetHighlight(canTarget);
                board.SetInteractable(canTarget);
            }
        }

        public void BeginTryalSelection(Player targetPlayer, Action<int> onTryalChosen)
        {
            if (targetPlayer == null)
            {
                Debug.LogWarning("[TableLayoutController] Cannot begin Tryal selection. Target player is null.");
                return;
            }

            selectedTryalPlayer = targetPlayer;
            pendingTryalCallback = onTryalChosen;
            isSelectingTryal = true;

            Debug.Log($"[TableLayoutController] Select an unrevealed Tryal for {targetPlayer.PlayerNameText}.");

            foreach (var pair in playerBoards)
            {
                Player player = pair.Key;
                PlayerBoardUI board = pair.Value;

                bool isTargetBoard = player == targetPlayer;

                board.SetTargetHighlight(isTargetBoard);
                board.SetInteractable(false);
            }
        }

        private void BeginTargetSelection(Card card, Player owner)
        {
            selectedCard = card;
            selectedCardOwner = owner;
            isSelectingTarget = true;

            Debug.Log($"[TableLayoutController] Selected card: {card.Name} from {owner.PlayerNameText}. Pick a target.");

            if (!GameTurnManager.Instance.TryBeginPlayPhase(owner))
            {
                Debug.LogWarning("[TableLayoutController] Cannot play card right now.");
                return;
            }

            foreach (var pair in playerBoards)
            {
                Player player = pair.Key;
                PlayerBoardUI board = pair.Value;

                bool canTarget = player != owner && !player.IsEliminated;

                board.SetTargetHighlight(canTarget);
                board.SetInteractable(canTarget);
            }
        }

        private void HandlePlayerBoardClicked(PlayerBoardUI boardUI, Player target)
        {
            if (!isSelectingTarget)
                return;

            if (pendingTargetCallback != null)
            {
                if (pendingTargetValidator != null && !pendingTargetValidator(target))
                {
                    Debug.LogWarning($"[TableLayoutController] Invalid target: {target.PlayerNameText}");
                    return;
                }

                pendingTargetCallback.Invoke(target);

                pendingTargetCallback = null;
                pendingTargetValidator = null;

                ClearTargetSelection();
                return;
            }

            if (selectedCard == null || selectedCardOwner == null)
                return;

            Debug.Log($"[TableLayoutController] Playing {selectedCard.Name} from {selectedCardOwner.PlayerNameText} on {target.PlayerNameText}");

            // Capture before any ClearTargetSelection/BeginTargetSelection below wipes these fields.
            Card card = selectedCard;
            Player owner = selectedCardOwner;
            Player primary = target;

            // Two-target cards (Robbery/Scapegoat): the player now picks the RECIPIENT — never
            // themselves, never the victim. Previously the local path passed NO secondary at all, so
            // the effect silently read a stale value off the shared card asset and was rejected.
            if (card is ActionCardSO twoTarget && twoTarget.RequiresSecondTarget)
            {
                ClearTargetSelection();
                BeginTargetSelection(
                    owner,
                    $"Choose who receives the cards ({card.Name}).",
                    t => t != null && t != owner && t != primary && !t.IsEliminated,
                    recipient =>
                    {
                        if (CardEffectManager.Instance.ExecuteCardEffect(card, primary, recipient))
                            GameTurnManager.Instance.NotifyCardPlayed(owner);
                        endTurnButtonUI.Show();
                    });
                return;
            }

            if (CardEffectManager.Instance.ExecuteCardEffect(card, primary))
                GameTurnManager.Instance.NotifyCardPlayed(owner);

            endTurnButtonUI.Show();

            ClearTargetSelection();
        }

        private void ClearTargetSelection()
        {
            selectedCard = null;
            selectedCardOwner = null;
            isSelectingTarget = false;

            foreach (var pair in playerBoards)
            {
                pair.Value.SetTargetHighlight(false);
                pair.Value.SetInteractable(true);
            }
        }

        private void HandleTryalCardClicked(Player player, int tryalIndex)
        {
            if (!isSelectingTryal)
                return;

            if (player != selectedTryalPlayer)
            {
                Debug.LogWarning("[TableLayoutController] Wrong player's Tryal card clicked.");
                return;
            }

            if (tryalIndex < 0 || tryalIndex >= player.TryalCards.Count)
            {
                Debug.LogWarning("[TableLayoutController] Invalid Tryal index.");
                return;
            }

            if (player.TryalCards[tryalIndex].IsRevealed)
            {
                Debug.LogWarning("[TableLayoutController] That Tryal card is already revealed.");
                return;
            }

            Debug.Log($"[TableLayoutController] Tryal selected: {player.PlayerNameText}, Index {tryalIndex}");

            pendingTryalCallback?.Invoke(tryalIndex);

            ClearTryalSelection();
        }

        private void ClearTryalSelection()
        {
            isSelectingTryal = false;
            selectedTryalPlayer = null;
            pendingTryalCallback = null;

            foreach (var pair in playerBoards)
            {
                pair.Value.SetTargetHighlight(false);
                pair.Value.SetInteractable(true);
            }
        }
    }
}
