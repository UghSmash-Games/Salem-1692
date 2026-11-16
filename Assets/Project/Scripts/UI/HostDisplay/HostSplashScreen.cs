using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

// See the masking-boundary banner in HostTableView.cs: no file in this folder may reference the seat
// model, the seat registry, tryal cards, hands or status cards. This one plays a video.

namespace Salem.UI.HostDisplay
{
    /// <summary>
    /// The studio splash video, played once at launch before the lobby is visible.
    ///
    /// Self-bootstrapping and built in code, like HostSystemMenu and HostVersionStamp: it must be the
    /// first thing on screen in whatever scene starts, and a splash that quietly lost a scene
    /// reference would be noticed only by its absence — which nobody checks for.
    ///
    /// 🔴 IT MUST NEVER BLOCK THE GAME. Every failure path here — missing clip, a video module that
    /// will not prepare, a codec the machine cannot decode — tears the overlay down and lets the
    /// lobby through. A logo is not worth a black screen on someone's first launch, and this runs
    /// before anyone can reach a menu to escape it.
    ///
    /// ⚠ The game continues underneath: the relay connects and the room code is assigned while this
    /// plays, so the lobby is already live the moment the video ends. That is deliberate — the splash
    /// costs no startup time, it just covers it.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class HostSplashScreen : MonoBehaviour
    {
        /// <summary>Path under a Resources folder, without the file extension.</summary>
        private const string ClipResourcePath = "Video/splash_sequence_original_logo";

        /// <summary>Safety net: if the video never reports completion, move on anyway.</summary>
        private const float MaxSecondsOnScreen = 20f;

        /// <summary>
        /// True while the splash owns the screen.
        ///
        /// HostSystemMenu reads this so Escape does not open the quit dialog BEHIND the video —
        /// the same key skips the splash instead, and two overlays never fight over one keypress.
        /// </summary>
        public static bool IsPlaying { get; private set; }

        private VideoPlayer video;
        private RenderTexture target;
        private GameObject root;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            var go = new GameObject("HostSplashScreen");
            go.AddComponent<HostSplashScreen>();
            DontDestroyOnLoad(go);
        }

        private void Awake()
        {
            var clip = Resources.Load<VideoClip>(ClipResourcePath);
            if (clip == null)
            {
                // Not fatal, and not silent: the splash is missing from the build, which is worth
                // knowing, but the game starts regardless.
                Debug.LogWarning($"[HostSplashScreen] No VideoClip at Resources/{ClipResourcePath} — skipping.");
                Destroy(gameObject);
                return;
            }

            IsPlaying = true;
            BuildOverlay(clip);
            StartCoroutine(PlayThenDismiss());
        }

        private void Update()
        {
            // Any key or click skips. A splash nobody can get past is the thing players complain
            // about, and the host operator may have seen it a hundred times.
            if (IsPlaying && (Input.anyKeyDown || Input.GetMouseButtonDown(0))) Dismiss();
        }

        private IEnumerator PlayThenDismiss()
        {
            video.Prepare();

            // Unscaled time throughout: nothing here should depend on Time.timeScale, which other
            // systems in this project do set to 0.
            float deadline = Time.realtimeSinceStartup + MaxSecondsOnScreen;
            while (!video.isPrepared && Time.realtimeSinceStartup < deadline) yield return null;

            if (!video.isPrepared)
            {
                Debug.LogWarning("[HostSplashScreen] Video never prepared — skipping.");
                Dismiss();
                yield break;
            }

            video.Play();

            // isPlaying goes false on its own at the end of the clip. The deadline is the backstop
            // for a decoder that stalls rather than finishing.
            while (video.isPlaying && Time.realtimeSinceStartup < deadline) yield return null;

            Dismiss();
        }

        private void Dismiss()
        {
            if (!IsPlaying) return;
            IsPlaying = false;

            if (video != null) video.Stop();
            if (root != null) Destroy(root);

            // Release explicitly rather than waiting for the GC: a RenderTexture holds GPU memory,
            // and this one is 1920x1080 and never needed again.
            if (target != null)
            {
                target.Release();
                Destroy(target);
            }

            Destroy(gameObject);
        }

        // ─── Construction ─────────────────────────────────────────

        private void BuildOverlay(VideoClip clip)
        {
            root = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler));
            root.transform.SetParent(transform, false);

            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Above everything, including the system menu (32000) and version stamp (31000) — this
            // is the one overlay that is meant to hide the whole application.
            canvas.sortingOrder = 32760;

            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            // Opaque backdrop so nothing shows through letterboxing on an odd aspect ratio.
            var bg = NewStretched("Background", root.transform);
            bg.AddComponent<Image>().color = Color.black;

            target = new RenderTexture((int)clip.width, (int)clip.height, 0)
            {
                name = "SplashRT",
            };

            var surface = NewStretched("Video", root.transform);
            var raw = surface.AddComponent<RawImage>();
            raw.texture = target;
            // Preserve aspect so a 16:9 clip is never stretched on a differently-shaped display.
            var fitter = surface.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = clip.height > 0 ? (float)clip.width / clip.height : 16f / 9f;

            video = gameObject.AddComponent<VideoPlayer>();
            video.playOnAwake = false;
            video.isLooping = false;
            video.source = VideoSource.VideoClip;
            video.clip = clip;
            video.renderMode = VideoRenderMode.RenderTexture;
            video.targetTexture = target;
            video.audioOutputMode = VideoAudioOutputMode.Direct;
            video.skipOnDrop = true;
        }

        private static GameObject NewStretched(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return go;
        }
    }
}
