using Salem.Networking; // PUBLIC DTOs only (GameOverMsg carries a winner label and nothing else).
using Salem.Systems;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// See the masking-boundary banner in HostTableView.cs: no file in this folder may reference the seat
// model, the seat registry, tryal cards, hands or status cards. This file reads one public string.

namespace Salem.UI.HostDisplay
{
    /// <summary>
    /// The host application's only way out, and its end-of-game screen.
    ///
    /// Two states on one overlay:
    ///  • **Quit confirmation** — Escape, at any time.
    ///  • **Game over** — the winner, with NEW GAME and QUIT.
    ///
    /// 🔴 WHY THIS EXISTS AT ALL. Before this the host could only be closed with Alt+F4, and when a
    /// game ended the TV simply kept showing the final board — the result appeared only as one line
    /// in the event log. A storefront build needs a visible way to quit; a party game needs an
    /// obvious way to play again without anyone touching a keyboard shortcut.
    ///
    /// ⚠ **ESCAPE ASKS, IT DOES NOT QUIT.** A stray keypress on a host running a twelve-person game
    /// would end everyone's evening with no undo. The same reasoning is why the game-over panel never
    /// auto-dismisses: the room decides when the result stops being on screen.
    ///
    /// **Built in code, deliberately.** Every other host panel is a scene object with Inspector-wired
    /// references, which is right for the board — it is a locked visual design. This one is a utility
    /// overlay that must exist in EVERY scene and must not be possible to half-wire: a quit button
    /// that silently lost its reference is worse than no quit button, because nobody checks it again
    /// before shipping. It self-bootstraps, so there is nothing to assemble and nothing to forget. It
    /// uses the host palette so it does not look foreign on the TV.
    ///
    /// ⚠ It runs at `Time.timeScale == 0` (pauseOnGameEnd freezes the game at the end), so nothing
    /// here may use a scaled wait. uGUI input is unaffected by timeScale; coroutines would not be.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class HostSystemMenu : MonoBehaviour
    {
        // The locked Phase-7 host palette (docs/phase-7-editor-steps.md).
        private static readonly Color Ground = new(0.09f, 0.075f, 0.059f, 0.96f);
        private static readonly Color Parchment = new(0.91f, 0.863f, 0.753f, 1f);
        private static readonly Color Ember = new(0.902f, 0.698f, 0.408f, 1f);
        private static readonly Color Crimson = new(0.659f, 0.137f, 0.106f, 1f);
        private static readonly Color PanelFill = new(0.13f, 0.11f, 0.09f, 1f);

        private GameObject root;
        private TMP_Text titleText;
        private TMP_Text bodyText;
        private Button primaryButton;
        private TMP_Text primaryLabel;
        private Button secondaryButton;
        private TMP_Text secondaryLabel;

        private bool gameOverShown;

        /// <summary>
        /// Create the menu in whatever scene is running, without anyone wiring it up.
        ///
        /// ⚠ `BeforeSceneLoad` would be too early — there is no scene to parent to yet. AfterSceneLoad
        /// runs once per PLAY SESSION, not once per scene load, so the object is also marked
        /// DontDestroyOnLoad below and guards against duplicating itself on a restart.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindFirstObjectByType<HostSystemMenu>() != null) return;

            var go = new GameObject("HostSystemMenu");
            go.AddComponent<HostSystemMenu>();
            DontDestroyOnLoad(go);
        }

        private void Awake()
        {
            BuildUi();
            Hide();
        }

        private void OnEnable() => NetworkManager.OnGameOverSent += HandleGameOver;
        private void OnDisable() => NetworkManager.OnGameOverSent -= HandleGameOver;

        private void Update()
        {
            // The old input API, matching DebugManager. Player Settings has activeInputHandler = Both,
            // so this is live; if the project ever goes new-input-only this is the one line to port.
            if (!Input.GetKeyDown(KeyCode.Escape)) return;

            // Escape never dismisses the game-over panel — there is nothing to go back TO, and
            // closing it would leave a finished game on screen with no way to act on it.
            if (gameOverShown) return;

            if (root.activeSelf) Hide();
            else ShowQuitConfirm();
        }

        // ─── States ───────────────────────────────────────────────

        private void ShowQuitConfirm()
        {
            titleText.text = "QUIT SALEM?";
            bodyText.text = "The game in progress will end for everyone, and every phone will return " +
                            "to the join screen.";

            primaryLabel.text = "KEEP PLAYING";
            SetButton(primaryButton, Ember, Hide);

            secondaryLabel.text = "QUIT";
            SetButton(secondaryButton, Crimson, AppLifecycle.Quit);

            secondaryButton.gameObject.SetActive(true);
            root.SetActive(true);
            EventSystem.current?.SetSelectedGameObject(primaryButton.gameObject);
        }

        /// <summary>
        /// The end of the game, from the PUBLIC wire payload — the same `winner` label every phone and
        /// mirror receives. Nothing here reads the game model, so the host-display boundary holds.
        /// </summary>
        private void HandleGameOver(GameOverMsg msg)
        {
            gameOverShown = true;

            string winner = msg != null ? msg.winner : null;
            titleText.text = winner switch
            {
                "witches" => "THE WITCHES WIN",
                "townspeople" => "THE TOWN PREVAILS",
                _ => "THE TRYAL IS ENDED",
            };
            bodyText.text = "Every tryal card is revealed on the board behind this panel.";

            primaryLabel.text = "NEW GAME";
            SetButton(primaryButton, Ember, AppLifecycle.RestartGame);

            secondaryLabel.text = "QUIT";
            SetButton(secondaryButton, Crimson, AppLifecycle.Quit);

            secondaryButton.gameObject.SetActive(true);
            root.SetActive(true);
            EventSystem.current?.SetSelectedGameObject(primaryButton.gameObject);
        }

        private void Hide()
        {
            if (root != null) root.SetActive(false);
        }

        private static void SetButton(Button button, Color colour, UnityEngine.Events.UnityAction action)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
            button.image.color = colour;
        }

        // ─── Construction ─────────────────────────────────────────

        private void BuildUi()
        {
            // A very high sorting order so this sits above the board, the night overlay and the
            // reveal overlay — all of which are legitimately full-screen.
            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32000;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);   // the host's locked canvas size
            scaler.matchWidthOrHeight = 0.5f;

            // Buttons need an EventSystem. The host scene has one, but this overlay must work in any
            // scene it bootstraps into — including one that never had UI input before.
            if (EventSystem.current == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                DontDestroyOnLoad(es);
            }

            root = NewRect("Root", canvasGo.transform, Vector2.zero, Vector2.one, Vector2.zero);
            var scrim = root.AddComponent<Image>();
            scrim.color = Ground;   // also swallows clicks aimed at the board underneath

            var panel = NewRect("Panel", root.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                new Vector2(980, 460));
            panel.AddComponent<Image>().color = PanelFill;

            var outline = panel.AddComponent<Outline>();
            outline.effectColor = new Color(Parchment.r, Parchment.g, Parchment.b, 0.25f);
            outline.effectDistance = new Vector2(2, 2);

            titleText = NewText("Title", panel.transform, 72, FontStyles.Bold, Parchment,
                                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -130), new Vector2(-120, 120));
            titleText.alignment = TextAlignmentOptions.Center;

            bodyText = NewText("Body", panel.transform, 30, FontStyles.Normal,
                               new Color(Parchment.r, Parchment.g, Parchment.b, 0.75f),
                               new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -240), new Vector2(-180, 110));
            bodyText.alignment = TextAlignmentOptions.Top;

            (primaryButton, primaryLabel) = NewButton("Primary", panel.transform, new Vector2(-180, 90));
            (secondaryButton, secondaryLabel) = NewButton("Secondary", panel.transform, new Vector2(180, 90));
        }

        private static GameObject NewRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax,
                                          Vector2 size, Vector2 position = default)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.sizeDelta = size;
            rt.anchoredPosition = position;
            return go;
        }

        private static TMP_Text NewText(string name, Transform parent, float size, FontStyles style,
                                        Color colour, Vector2 anchorMin, Vector2 anchorMax,
                                        Vector2 position, Vector2 sizeDelta)
        {
            var go = NewRect(name, parent, anchorMin, anchorMax, sizeDelta, position);
            var text = go.AddComponent<TextMeshProUGUI>();
            text.fontSize = size;
            text.fontStyle = style;
            text.color = colour;
            text.alignment = TextAlignmentOptions.Center;
            // Word wrapping is on by default; setting it explicitly would mean picking between
            // `textWrappingMode` (TMP 3.2+) and the older `enableWordWrapping`, and this is the only
            // place in the project that would depend on which one this TMP version exposes.
            return text;
        }

        private static (Button, TMP_Text) NewButton(string name, Transform parent, Vector2 position)
        {
            var go = NewRect(name, parent, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                             new Vector2(320, 96), position);
            var image = go.AddComponent<Image>();
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;

            var label = NewText("Label", go.transform, 34, FontStyles.Bold, new Color(0.06f, 0.05f, 0.04f),
                                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            label.alignment = TextAlignmentOptions.Center;

            return (button, label);
        }
    }
}
