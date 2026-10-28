using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Salem.Networking; // PUBLIC DTOs + host-facing public send-events ONLY.

// See the masking-boundary banner in HostTableView.cs: no file in this folder may reference Player /
// PlayerService / TryalCard / HandManager / StatusCards.

namespace Salem.UI.HostDisplay
{
    /// <summary>
    /// Stage 7e: a brief, non-blocking announcement for `public_reveal` — a player SHOWING cards to
    /// the whole table (currently only Giles Corey's two red cards, rulebook: "IF YOU DRAW TWO RED
    /// CARDS, SHOW THE OTHER PLAYERS").
    ///
    /// MIRRORS THE WEB CLIENT: webclient/src/components/PublicRevealToast.tsx. Same shape — composed
    /// generically from actor + card names, latest-wins, auto-dismiss — so the two surfaces read the
    /// same. `REASON_VERB` is the only extension point on both sides.
    ///
    /// ⚠️ `autoDismissSeconds` matches the web AUTO_DISMISS_MS (4000) for CONSISTENCY ONLY. Unlike
    /// HostRevealOverlay.lingerSeconds ↔ REVEALED_LINGER_MS, this is NOT a synchronization contract:
    /// there is no `revealAt` here, each screen shows the toast on receipt, and nothing downstream
    /// depends on the two clearing together. Changing one will not desynchronise a dramatic beat.
    ///
    /// NOT A DRAMATIC BEAT — informational. It must sit BELOW HostRevealOverlay and HostPhaseOverlay
    /// in sibling order (uGUI draws later siblings on top), exactly as the web toast sits at z-40
    /// under the reveal's z-50: an elimination reveal always wins, and the dawn/night cover is never
    /// painted over.
    ///
    /// PRIVACY: `public_reveal` carries card NAMES the holder is showing the table by the card's own
    /// rules — the same visibility class as `game_state_update.statusCards`. Nothing here is derived
    /// from a model. Note this does NOT feed the event log: `GameEventKind` is a CLOSED vocabulary and
    /// gains no kind for this (see NetworkMessages.cs) — the toast is the surface, deliberately.
    ///
    /// The host is NOT socket-echoed `public_reveal`, so NetworkManager.OnPublicRevealSent is the
    /// only signal that this ever happened. Local (non-networked) play emits nothing — the legacy
    /// CardLogManager line covers that path.
    /// </summary>
    public class HostPublicRevealToast : MonoBehaviour
    {
        [Header("Root")]
        [Tooltip("Faded in/out. Keep this GameObject ACTIVE — Update drives the dismiss timer.")]
        [SerializeField] private CanvasGroup group;
        [Tooltip("Visuals, switched off while idle.")]
        [SerializeField] private GameObject content;

        [Header("Copy")]
        [SerializeField] private TMP_Text bodyText;

        [Header("Card art (optional)")]
        [Tooltip("Leave empty for a text-only toast; assign to show the shown cards' faces.")]
        [SerializeField] private Transform cardRow;
        [SerializeField] private Image cardPrefab;
        [SerializeField] private HostCardSpriteRegistry sprites;
        [SerializeField] private int maxCards = 4;

        [Header("Timing")]
        [Tooltip("Cosmetic parity with the web toast's AUTO_DISMISS_MS. Not a sync contract.")]
        [SerializeField] private float autoDismissSeconds = 4f;
        [SerializeField] private float fadeSeconds = 0.25f;

        private readonly Dictionary<string, string> nameById = new();
        private readonly List<Image> cards = new();

        private float targetAlpha;
        private float dismissAtUnscaled;
        private bool showing;

        private void Awake()
        {
            if (group == null) group = GetComponent<CanvasGroup>();
            if (group != null)
            {
                group.alpha = 0f;
                group.interactable = false;
                group.blocksRaycasts = false; // display-only screen: never intercept input
            }
            if (content != null) content.SetActive(false);
        }

        private void OnEnable()
        {
            NetworkManager.OnPublicRevealSent += HandlePublicReveal;
        }

        private void OnDisable()
        {
            NetworkManager.OnPublicRevealSent -= HandlePublicReveal;
        }

        /// <summary>Refreshes the id→display-name map from the public board.</summary>
        public void Render(GameStateUpdateMsg state)
        {
            if (state?.players == null) return;

            nameById.Clear();
            foreach (var p in state.players)
            {
                if (p == null || string.IsNullOrEmpty(p.playerId)) continue;
                nameById[p.playerId] = p.displayName;
            }
        }

        // ─── Signal ────────────────────────────────────────────────

        /// <summary>
        /// Latest-wins, matching the web toast: a second reveal replaces the first and restarts the
        /// timer rather than queueing. Two of these in one window would need a Giles to draw twice
        /// in ~4s; showing the newer fact beats holding a stale one on screen.
        /// </summary>
        private void HandlePublicReveal(PublicRevealMsg msg)
        {
            if (msg == null) return;

            if (bodyText != null)
                bodyText.text = $"{NameOf(msg.playerId)} {VerbFor(msg.reason)} {CardList(msg.cards)}";

            RenderCards(msg.cards);

            if (content != null) content.SetActive(true);
            targetAlpha = 1f;
            dismissAtUnscaled = Time.unscaledTime + Mathf.Max(0.5f, autoDismissSeconds);
            showing = true;
        }

        private void Update()
        {
            if (group != null)
            {
                float step = fadeSeconds <= 0f ? 1f : Time.unscaledDeltaTime / fadeSeconds;
                group.alpha = Mathf.MoveTowards(group.alpha, targetAlpha, step);
            }

            // Unscaled: a reveal can end the game, and pauseOnGameEnd sets Time.timeScale to 0 —
            // a scaled timer would strand the toast on screen for the rest of the session.
            if (showing && Time.unscaledTime >= dismissAtUnscaled)
            {
                showing = false;
                targetAlpha = 0f;
            }

            // Hide once faded out — or IMMEDIATELY if there is no CanvasGroup to fade. Requiring a
            // non-null group here meant a root missing its CanvasGroup could never satisfy this
            // condition, so the toast appeared and stayed on screen for the rest of the session.
            if (!showing && content != null && content.activeSelf &&
                (group == null || Mathf.Approximately(group.alpha, 0f)))
            {
                content.SetActive(false);
            }
        }

        // ─── Rendering ─────────────────────────────────────────────

        private void RenderCards(string[] labels)
        {
            if (cardRow == null || cardPrefab == null) return;

            foreach (var c in cards) if (c != null) c.gameObject.SetActive(false);
            if (labels == null) return;

            int shown = 0;
            foreach (var label in labels)
            {
                if (shown >= Mathf.Max(1, maxCards)) break;

                while (cards.Count <= shown) cards.Add(Instantiate(cardPrefab, cardRow));

                var img = cards[shown];
                var sprite = sprites != null ? sprites.Get(label) : null;
                img.sprite = sprite;
                img.enabled = sprite != null;
                img.gameObject.SetActive(true);
                shown++;
            }
        }

        /// <summary>
        /// reason → verb, mirroring the web toast's REASON_VERB. Reason-AGNOSTIC by design: a new
        /// `public_reveal` reason renders correctly with no change here, falling back to "shows".
        /// </summary>
        private static string VerbFor(string reason)
        {
            switch (reason)
            {
                default: return "shows";
            }
        }

        private static string CardList(string[] cards)
            => cards == null || cards.Length == 0 ? "" : string.Join(" & ", cards);

        private string NameOf(string playerId)
        {
            if (string.IsNullOrEmpty(playerId)) return "A player";
            return nameById.TryGetValue(playerId, out var n) && !string.IsNullOrEmpty(n)
                ? n
                : playerId;
        }
    }
}
