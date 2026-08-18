using System.Collections.Generic;
using UnityEngine;
using Salem.Networking;

namespace Salem.Audio
{
    /// <summary>
    /// Host-screen sound: one cue per PUBLIC game event, plus the day ambience bed.
    ///
    /// 🔴 DRIVEN ONLY BY THE PUBLIC EVENT VOCABULARY — never by game models. This is the same
    /// discipline the event log follows, and for a stronger reason: **an audio cue is a broadcast**.
    /// A sound wired to a model event that only fires in secret circumstances — a constable save
    /// landing, a witch vote resolving — is audible to the whole room and becomes a side-channel
    /// around the masking model. Because `GameEventKind` is a CLOSED set with no kind for a secret
    /// action, a cue physically cannot be conditioned on one.
    /// ⛔ Do NOT add an AudioSource call inside GamePhaseManager's secret rounds, and do not
    /// subscribe this class to Player / PlayerService / NightResolver.
    ///
    /// Note this is NOT masking audio in the rulebook's sense. The physical game prescribes stomping
    /// because witches open their eyes and point; here every player is prompted identically and taps,
    /// so there is no differential movement to cover (see CLAUDE.md). Dawn/night ambience is for
    /// DRAMA — see HostPhaseAmbience.
    ///
    /// Clips are all optional: an unassigned cue is silently skipped, so the system is safe to wire
    /// incrementally as audio is sourced.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class HostAudioManager : MonoBehaviour
    {
        /// <summary>One cue: the public event kind that fires it, and what to play.</summary>
        [System.Serializable]
        public class Cue
        {
            [Tooltip("Wire name of the GameEventKind, e.g. \"tryal_revealed\".")]
            public string kind;
            [Tooltip("Optional: only fire when GameEventMsg.value matches (e.g. \"Night\", \"witches\").")]
            public string valueFilter;
            [Tooltip("Optional: only fire when GameEventMsg.cardName matches (e.g. \"Accusation\").")]
            public string cardNameFilter;
            public AudioClip clip;
            /// ⚠ The `= 1f` does NOT apply to rows added by typing a new Size in the Inspector —
            /// Unity zero-fills array elements and ignores C# field initializers there. A row left
            /// at 0 is silent, which reads as "the audio system is broken" rather than "this row is
            /// muted". Check this column first if a cue never sounds.
            [Range(0f, 1f)] public float volume = 1f;
        }

        [Header("Output")]
        [Tooltip("One-shot cues play here. Ambience uses its own source below.")]
        [SerializeField] private AudioSource cueSource;
        [SerializeField, Range(0f, 1f)] private float masterVolume = 1f;

        [Header("Cues (public events only)")]
        [Tooltip("Matched top-to-bottom; the FIRST match wins, so put filtered entries above " +
                 "their unfiltered fallback.")]
        [SerializeField] private List<Cue> cues = new();

        [Header("Ambience")]
        [Tooltip("Looping bed for normal play. Faded out while HostPhaseAmbience covers dawn/night.")]
        [SerializeField] private AudioSource ambienceSource;
        [SerializeField] private AudioClip dayAmbience;
        [SerializeField] private float ambienceFadeSeconds = 1.5f;
        [SerializeField, Range(0f, 1f)] private float ambienceVolume = 0.35f;

        private string lastPhase;
        private float ambienceTarget;

        private void Awake()
        {
            if (cueSource == null) cueSource = GetComponent<AudioSource>();
            if (cueSource != null) cueSource.playOnAwake = false;

            if (ambienceSource != null)
            {
                ambienceSource.loop = true;
                ambienceSource.playOnAwake = false;
                ambienceSource.volume = 0f;
            }
        }

        private void OnEnable()
        {
            NetworkManager.OnGameEventSent += HandleGameEvent;
            NetworkStateBroadcaster.OnPublicState += HandlePublicState;
        }

        private void OnDisable()
        {
            NetworkManager.OnGameEventSent -= HandleGameEvent;
            NetworkStateBroadcaster.OnPublicState -= HandlePublicState;
        }

        // ─── Cues ──────────────────────────────────────────────────

        private void HandleGameEvent(GameEventMsg e)
        {
            if (e == null || cueSource == null) return;

            var cue = Match(e);
            if (cue?.clip == null) return;

            cueSource.PlayOneShot(cue.clip, Mathf.Clamp01(cue.volume * masterVolume));
        }

        /// <summary>
        /// First match wins. The filters are what let one kind drive several cues — `card_played`
        /// becomes an accusation sting or a generic card sound depending on `cardName`, and
        /// `game_over` splits into the two win cues on `value` — without the audio layer needing to
        /// know any game rules.
        /// </summary>
        private Cue Match(GameEventMsg e)
        {
            foreach (var c in cues)
            {
                if (c == null || string.IsNullOrEmpty(c.kind)) continue;
                if (!string.Equals(c.kind, e.kind, System.StringComparison.OrdinalIgnoreCase)) continue;

                if (!string.IsNullOrEmpty(c.valueFilter) &&
                    !string.Equals(c.valueFilter, e.value, System.StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!string.IsNullOrEmpty(c.cardNameFilter) &&
                    !string.Equals(c.cardNameFilter, e.cardName, System.StringComparison.OrdinalIgnoreCase))
                    continue;

                return c;
            }
            return null;
        }

        // ─── Ambience ──────────────────────────────────────────────

        /// <summary>
        /// The day bed follows the PUBLIC phase string — the same single source of truth as
        /// HostPhaseOverlay, so the audio and the visual cover can never disagree about which phase
        /// the table is in.
        /// </summary>
        private void HandlePublicState(GameStateUpdateMsg state)
        {
            string phase = state?.phase ?? "";
            if (string.Equals(phase, lastPhase, System.StringComparison.OrdinalIgnoreCase)) return;
            lastPhase = phase;

            bool secret = phase.Equals("dawn", System.StringComparison.OrdinalIgnoreCase)
                       || phase.Equals("night", System.StringComparison.OrdinalIgnoreCase);

            // Duck the day bed during dawn/night; HostPhaseAmbience owns that stretch.
            ambienceTarget = secret ? 0f : ambienceVolume * masterVolume;

            if (ambienceSource != null && dayAmbience != null && !secret)
            {
                if (ambienceSource.clip != dayAmbience) ambienceSource.clip = dayAmbience;
                if (!ambienceSource.isPlaying) ambienceSource.Play();
            }
        }

        private void Update()
        {
            if (ambienceSource == null) return;

            // UNSCALED: pauseOnGameEnd sets Time.timeScale to 0, and audio must keep behaving
            // through the win screen rather than freezing mid-fade.
            float step = ambienceFadeSeconds <= 0f ? 1f : Time.unscaledDeltaTime / ambienceFadeSeconds;
            ambienceSource.volume = Mathf.MoveTowards(ambienceSource.volume, ambienceTarget, step);

            if (Mathf.Approximately(ambienceSource.volume, 0f) && ambienceSource.isPlaying && ambienceTarget <= 0f)
                ambienceSource.Pause();
        }
    }
}
