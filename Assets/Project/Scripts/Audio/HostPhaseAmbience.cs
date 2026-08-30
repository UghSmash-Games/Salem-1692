using System;
using UnityEngine;
using Salem.Networking;

namespace Salem.Audio
{
    /// <summary>
    /// The dawn/night atmospheric bed on the host screen — the audible half of what
    /// <see cref="Salem.UI.HostDisplay.HostPhaseOverlay"/> does visually.
    ///
    /// ⚠️ THIS IS ATMOSPHERE, NOT MASKING. The rulebook prescribes stomping/creepy music because at
    /// a physical table the witches actually open their eyes and point — there is differential
    /// MOVEMENT to cover. Here every player receives an identical prompt and taps, and the phase
    /// resolves only once every connected human has confirmed, so no player is singled out by their
    /// actions. The masking is structural (see CLAUDE.md). Do not re-frame this as a fairness
    /// invariant, and do not let anyone argue a missing clip is a masking bug — it is a silent room.
    ///
    /// AUTOMATIC BY CONSTRUCTION, which the guide requires ("no one has to remember to turn it on"):
    /// it subscribes to the public state feed itself and arms off the phase string. There is no
    /// trigger to forget and no operator step.
    ///
    /// 🔴 SAME SINGLE SOURCE OF TRUTH AS THE OVERLAY — the public GameStateUpdateMsg.phase, matched
    /// case-insensitively against the same two names. The cover and the sound therefore cannot
    /// disagree about which phase the table is in. Conspiracy is deliberately EXCLUDED here exactly
    /// as it is there: it is a public event, not a secret phase.
    ///
    /// PAIRS WITH HostAudioManager, which ducks its day bed to zero on these same two phases — so
    /// the two together read as a crossfade without sharing any state. If this component is missing
    /// or its clips are unassigned, dawn/night are simply SILENT (the day bed still ducks); nothing
    /// breaks.
    /// </summary>
    public class HostPhaseAmbience : MonoBehaviour
    {
        [Header("Output")]
        [Tooltip("Dedicated looping source. Kept separate from HostAudioManager's cue and day-bed sources.")]
        [SerializeField] private AudioSource source;

        [Header("Clips")]
        [Tooltip("Dawn: the witches meet and place the black cat.")]
        [SerializeField] private AudioClip dawnClip;
        [Tooltip("Night: the witches choose, the constable answers.")]
        [SerializeField] private AudioClip nightClip;

        [Header("Levels")]
        [SerializeField, Range(0f, 1f)] private float volume = 0.5f;
        [Tooltip("Fade in/out seconds. Long enough not to snap; short enough to be under the phase.")]
        [SerializeField] private float fadeSeconds = 2f;

        private AudioClip desiredClip;
        private float targetVolume;

        private void Awake()
        {
            if (source == null) source = GetComponent<AudioSource>();
            if (source != null)
            {
                source.loop = true;
                source.playOnAwake = false;
                source.volume = 0f;
            }
        }

        private void OnEnable() => NetworkStateBroadcaster.OnPublicState += HandlePublicState;
        private void OnDisable() => NetworkStateBroadcaster.OnPublicState -= HandlePublicState;

        private void HandlePublicState(GameStateUpdateMsg state)
        {
            string phase = state?.phase ?? "";

            if (string.Equals(phase, "dawn", StringComparison.OrdinalIgnoreCase))
                desiredClip = dawnClip;
            else if (string.Equals(phase, "night", StringComparison.OrdinalIgnoreCase))
                desiredClip = nightClip;
            else
                desiredClip = null;

            targetVolume = desiredClip != null ? volume : 0f;
        }

        private void Update()
        {
            if (source == null) return;

            // UNSCALED THROUGHOUT: a night can END THE GAME, and pauseOnGameEnd sets Time.timeScale
            // to 0 — a scaled fade would freeze the bed at whatever volume it happened to be on and
            // hold it under the win screen. Same reason HostRevealOverlay is unscaled.
            float step = fadeSeconds <= 0f ? 1f : Time.unscaledDeltaTime / fadeSeconds;
            source.volume = Mathf.MoveTowards(source.volume, targetVolume, step);

            // Swap clips only at silence. Dawn and night are never adjacent (day always separates
            // them), so this normally just starts the next bed from zero — but doing it at silence
            // means a mid-fade phase change can never produce an audible cut.
            if (source.clip != desiredClip && Mathf.Approximately(source.volume, 0f))
            {
                source.Stop();
                source.clip = desiredClip;
                if (desiredClip != null) source.Play();
            }

            if (desiredClip != null && source.clip == desiredClip && !source.isPlaying && targetVolume > 0f)
                source.Play();
        }
    }
}
