using TMPro;
using UnityEngine;
using UnityEngine.UI;

// See the masking-boundary banner in HostTableView.cs: no file in this folder may reference the seat
// model, the seat registry, tryal cards, hands or status cards. This one reads a build constant.

namespace Salem.UI.HostDisplay
{
    /// <summary>
    /// A small version stamp in the corner of the host screen.
    ///
    /// 🔴 WHY THE HOST ESPECIALLY. Salem ships as three pieces that update on DIFFERENT schedules:
    /// the web client redeploys on every push, the relay redeploys when someone runs `fly deploy`,
    /// and THIS is a downloaded build that a player may keep running for months. So the host is the
    /// one tier whose version cannot be inferred from anything else — and the one most likely to be
    /// stale when a bug is reported.
    ///
    /// Reads <see cref="Application.version"/>, which is Player Settings > Version
    /// (`bundleVersion`), stamped from the repo's VERSION file by scripts/set-version.mjs.
    ///
    /// Built in code and self-bootstrapping, for the same reason as HostSystemMenu: it must appear
    /// on every screen the host can show — lobby, board, game over — without anyone remembering to
    /// place it, and a version label that quietly lost its scene reference is worse than none,
    /// because it is only ever read when something has already gone wrong.
    ///
    /// ⚠ Deliberately NOT part of the locked Phase-7 board design, and deliberately not mirrored:
    /// it carries no game information, so it is outside the host/mirror parity rule (the web client
    /// shows its own version on its join screens instead — the two numbers are allowed to differ,
    /// and when they do, that difference is exactly the thing worth seeing).
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class HostVersionStamp : MonoBehaviour
    {
        /// <summary>Set false to keep the stamp off a public-facing build.</summary>
        private const bool ShowOnHostScreen = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (!ShowOnHostScreen) return;
            if (FindFirstObjectByType<HostVersionStamp>() != null) return;

            var go = new GameObject("HostVersionStamp");
            go.AddComponent<HostVersionStamp>();
            DontDestroyOnLoad(go);
        }

        private void Awake()
        {
            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Above the board, below the system menu (32000) — a quit dialog must never be competing
            // with a version label for the top layer.
            canvas.sortingOrder = 31000;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            // No GraphicRaycaster on this canvas: the stamp must never swallow a click meant for the
            // board or a button underneath it.
            var textGo = new GameObject("Version", typeof(RectTransform));
            textGo.transform.SetParent(canvasGo.transform, false);

            var rt = (RectTransform)textGo.transform;
            rt.anchorMin = new Vector2(1, 0);
            rt.anchorMax = new Vector2(1, 0);
            rt.pivot = new Vector2(1, 0);
            rt.sizeDelta = new Vector2(360, 36);
            rt.anchoredPosition = new Vector2(-18, 12);

            var text = textGo.AddComponent<TextMeshProUGUI>();
            text.text = $"v{Application.version}";
            text.fontSize = 18;
            text.alignment = TextAlignmentOptions.BottomRight;
            text.raycastTarget = false;
            text.color = new Color(0.91f, 0.863f, 0.753f, 0.35f);   // host parchment, well faded
        }
    }
}
