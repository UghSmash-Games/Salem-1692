using System;
using TMPro;
using UnityEngine;
using Salem.Networking; // PUBLIC DTOs ONLY — see the masking-boundary banner in HostTableView.cs.

namespace Salem.UI.HostDisplay
{
    /// <summary>
    /// Full-screen atmospheric cover shown while a SECRET phase runs (dawn / night), per the Phase-7
    /// checkpoint: "Night/dawn overlay covers the full screen with no player data visible during
    /// secret phases."
    ///
    /// It serves two purposes. Atmosphere is the obvious one. The other is that with everyone's eyes
    /// closed, an uncovered board is a reference an opening eye could read — the overlay removes
    /// that temptation entirely.
    ///
    /// 🔴 MASKING RULES — this is a secret-phase surface, so two things are forbidden:
    ///
    /// 1. **NEVER display progress.** No "3 of 5 have confirmed", no per-player ticks, no spinner
    ///    that resolves as submissions arrive. Phase 4c specifically closed a timing leak by making
    ///    the phase resolve only when EVERY connected human has confirmed — surfacing partial
    ///    progress here would hand that information straight back and let an observer exclude the
    ///    tardiest players from being witches.
    /// 2. **NEVER reduce the cover to partial opacity.** The board underneath is public data, but
    ///    the checkpoint requires it hidden; a translucent overlay defeats the point.
    ///
    /// Driven solely by the public <see cref="GameStateUpdateMsg.phase"/> string. It has no access
    /// to who is acting — the host display cannot see the `acting` flag at all — so it is incapable
    /// of leaking it even by accident.
    /// </summary>
    public class HostPhaseOverlay : MonoBehaviour
    {
        [Serializable]
        public struct PhaseCopy
        {
            [Tooltip("Wire phase name, lowercase (GamePhase.ToString().ToLowerInvariant()).")]
            public string phase;
            public string title;
            [TextArea(1, 2)]
            public string subtitle;
        }

        [Header("Visuals")]
        [Tooltip("Root CanvasGroup faded in/out. Keep this GameObject ACTIVE — Update drives the fade.")]
        [SerializeField] private CanvasGroup group;
        [Tooltip("The visuals, switched off once fully faded out so they cost nothing during Day.")]
        [SerializeField] private GameObject content;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text subtitleText;

        [Header("Phases that hide the board")]
        [Tooltip("Only genuinely SECRET phases belong here. Conspiracy is public (the card is drawn " +
                 "and the tryal flips openly), so it must NOT be listed.")]
        [SerializeField]
        private PhaseCopy[] phases =
        {
            new PhaseCopy { phase = "dawn",  title = "DAWN",  subtitle = "The witches stir." },
            new PhaseCopy { phase = "night", title = "NIGHT", subtitle = "Players close their eyes." },
        };

        [Header("Fade")]
        [SerializeField] private float fadeSeconds = 0.6f;

        private float targetAlpha;

        private void Awake()
        {
            if (group == null) group = GetComponent<CanvasGroup>();
            if (group != null)
            {
                group.alpha = 0f;
                // Display-only screen, but make doubly sure the cover can never intercept input.
                group.interactable = false;
                group.blocksRaycasts = false;
            }
            if (content != null) content.SetActive(false);
        }

        public void Render(GameStateUpdateMsg state)
        {
            if (!TryGetCopy(state?.phase, out var copy))
            {
                targetAlpha = 0f;
                return;
            }

            if (titleText != null) titleText.text = copy.title;
            if (subtitleText != null) subtitleText.text = copy.subtitle;

            targetAlpha = 1f;
            if (content != null) content.SetActive(true);
        }

        private void Update()
        {
            if (group == null) return;

            // UNSCALED: pauseOnGameEnd sets Time.timeScale to 0, and a frozen fade would strand the
            // board under a half-drawn cover.
            float step = fadeSeconds <= 0f
                ? 1f
                : Time.unscaledDeltaTime / fadeSeconds;

            group.alpha = Mathf.MoveTowards(group.alpha, targetAlpha, step);

            bool hidden = Mathf.Approximately(group.alpha, 0f);
            if (content != null && hidden && content.activeSelf) content.SetActive(false);
        }

        private bool TryGetCopy(string phase, out PhaseCopy copy)
        {
            copy = default;
            if (string.IsNullOrEmpty(phase) || phases == null) return false;

            foreach (var p in phases)
            {
                if (string.Equals(p.phase, phase, StringComparison.OrdinalIgnoreCase))
                {
                    copy = p;
                    return true;
                }
            }
            return false;
        }
    }
}
