using UnityEngine;
using UnityEngine.UI;

namespace Salem.UI.HostDisplay
{
    /// <summary>
    /// One card in the synchronized reveal: starts face-down and flips to its face.
    ///
    /// The flip is a horizontal squash — scaleX 1→0, swap the sprite at the midpoint, 0→1 — which
    /// reads as a card turning without needing a 3D rotation or any animation asset.
    ///
    /// Driven by <see cref="Time.unscaledDeltaTime"/>: this plays during a reveal that may END THE
    /// GAME, and pauseOnGameEnd sets Time.timeScale to 0. A scaled animation would freeze half-turned.
    /// </summary>
    public class HostRevealCard : MonoBehaviour
    {
        [SerializeField] private Image image;
        [Tooltip("Seconds for each half of the flip (squash in, then out).")]
        [SerializeField] private float halfFlipSeconds = 0.2f;

        private Sprite face;
        private float delayRemaining;
        private float t;
        private bool playing;
        private bool swapped;

        /// <summary>Show the shared card back and hold, awaiting <see cref="Play"/>.</summary>
        public void ShowBack(Sprite back)
        {
            playing = false;
            swapped = false;
            t = 0f;
            delayRemaining = 0f;
            face = null;

            if (image != null)
            {
                image.sprite = back;
                image.enabled = back != null;
            }
            SetScaleX(1f);
        }

        /// <summary>Begin the flip after <paramref name="delaySeconds"/> (used to stagger a row).</summary>
        public void Play(Sprite faceSprite, float delaySeconds)
        {
            face = faceSprite;
            delayRemaining = Mathf.Max(0f, delaySeconds);
            t = 0f;
            swapped = false;
            playing = true;
        }

        private void Update()
        {
            if (!playing) return;

            float dt = Time.unscaledDeltaTime;

            if (delayRemaining > 0f)
            {
                delayRemaining -= dt;
                return;
            }

            t += dt;
            float half = Mathf.Max(0.01f, halfFlipSeconds);

            if (t < half)
            {
                SetScaleX(1f - (t / half));           // squash away
                return;
            }

            if (!swapped)
            {
                swapped = true;
                if (image != null)
                {
                    image.sprite = face;
                    image.enabled = face != null;
                }
            }

            float outT = Mathf.Clamp01((t - half) / half);
            SetScaleX(outT);                           // open out on the new face

            if (outT >= 1f) playing = false;
        }

        private void SetScaleX(float x)
        {
            var s = transform.localScale;
            // Guard the pinch point so the card never fully vanishes to a zero-scale artefact.
            transform.localScale = new Vector3(Mathf.Max(0.001f, x), s.y, s.z);
        }
    }
}
