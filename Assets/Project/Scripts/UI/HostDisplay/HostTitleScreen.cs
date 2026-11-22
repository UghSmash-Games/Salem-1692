using Salem.Systems;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

// See the masking-boundary banner in HostTableView.cs: no file in this folder may reference the seat
// model, the seat registry, tryal cards, hands or status cards. This file reads two URL strings.

namespace Salem.UI.HostDisplay
{
    /// <summary>
    /// The front door: the game's title with START / INSTRUCTIONS / QUIT, shown once the splash
    /// finishes and before anyone sees the lobby.
    ///
    /// **Why a title screen matters here more than in most games.** Salem's lobby is a wall of
    /// instructions in disguise — a room code, two near-identical URLs, a roster and a pace control —
    /// and before this it was the FIRST thing a host saw, with no context for any of it. A group
    /// opening the game for the first time had nowhere to learn that every player needs a phone and
    /// that the second address is optional, which is exactly the confusion that gets half a room
    /// typing /display and ending up as spectators.
    ///
    /// **Built in code, self-bootstrapping — the same reasoning as HostSystemMenu and
    /// HostSplashScreen.** It is a utility screen that must not be possible to half-wire: a START
    /// button that silently lost its reference would leave the application with no way into the game
    /// at all. Nothing to assemble, nothing to forget. It uses the locked Phase-7 host palette so it
    /// does not look foreign on the TV.
    ///
    /// 🔴 **IT IS A VISUAL GATE ONLY — it does not touch the connection flow.** The relay connect and
    /// `create_room` still run on scene load, behind this screen, exactly as they did behind the
    /// splash. That is deliberate: by the time someone presses START the room code is already
    /// assigned, so the lobby appears with a real code instead of `----` while people are standing
    /// there waiting to type it. The cost is one idle room on the relay while the title is up, which
    /// is a far better trade than a party game that makes its first impression as a loading dash.
    ///
    /// ⚠️ **It never comes back.** Bootstrap runs once per play session and START destroys the
    /// object, so the RESTART / NEW GAME paths (which reload the scene) go straight to the lobby.
    /// Returning to the title there would be wrong — the people in the room have already chosen to
    /// play and would have to choose again. This is the same class of bug as the game-over panel
    /// surviving a reload; see <see cref="HostSystemMenu.HandleSceneLoaded"/>.
    ///
    /// ⚠️ It may run at any timescale, so nothing here uses a scaled wait. uGUI input is unaffected
    /// by `Time.timeScale`; coroutines would not be.
    /// </summary>
    [DefaultExecutionOrder(-60)]
    public class HostTitleScreen : MonoBehaviour
    {
        /// <summary>
        /// A FULL web URL to the rulebook, or "" to hide the button.
        ///
        /// ⚠️ Deliberately empty by default. `docs/Salem_Rulebook.pdf` is a repo document and is NOT
        /// shipped inside the build, so there is no local file to open — and a MANUAL button that
        /// opens nothing is worse than no button, because it reads as a broken game rather than a
        /// missing asset. Set this to a hosted copy (the web client's domain is the obvious home)
        /// and the button appears on its own.
        /// </summary>
        private const string ManualUrl = "";

        /// <summary>
        /// True while the title owns the screen.
        ///
        /// <see cref="HostSystemMenu"/> reads this so Escape does not stack a second "QUIT SALEM?"
        /// dialog on top of a screen that already has a QUIT button in plain sight — one keypress,
        /// one meaning, the same contract it has with <see cref="HostSplashScreen.IsPlaying"/>.
        /// </summary>
        public static bool IsShowing { get; private set; }

        // The locked Phase-7 host palette (docs/phase-7-editor-steps.md). Repeated rather than shared
        // with HostSystemMenu on purpose: each of these overlays is self-contained so that none can
        // be broken by a change made for another.
        private static readonly Color Ground = new(0.09f, 0.075f, 0.059f, 1f);
        private static readonly Color Parchment = new(0.91f, 0.863f, 0.753f, 1f);
        private static readonly Color Ember = new(0.902f, 0.698f, 0.408f, 1f);
        private static readonly Color Amber = new(0.788f, 0.541f, 0.247f, 1f);
        private static readonly Color Crimson = new(0.659f, 0.137f, 0.106f, 1f);
        private static readonly Color PanelFill = new(0.13f, 0.11f, 0.09f, 1f);

        private static readonly Color Dim = new(Parchment.r, Parchment.g, Parchment.b, 0.72f);

        private const float ButtonWidth = 360f;
        private const float ButtonHeight = 96f;
        private const float ButtonGap = 28f;

        private GameObject root;
        private GameObject titlePage;
        private GameObject instructionsPage;
        private Button firstTitleButton;
        private Button backButton;

        /// <summary>
        /// Create the screen in whatever scene is running, with nobody wiring it up.
        ///
        /// 🔴 **AfterSceneLoad, not BeforeSceneLoad, and that ordering is load-bearing.** The splash
        /// bootstraps at BeforeSceneLoad and sets <see cref="HostSplashScreen.IsPlaying"/> inside its
        /// Awake — which `AddComponent` runs synchronously — so by the time any AfterSceneLoad hook
        /// runs, that flag is already correct. The relative order of two BeforeSceneLoad methods is
        /// undefined, so bootstrapping there would be a coin flip on whether this screen saw the
        /// splash at all, and losing it would show the title UNDER the video.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindFirstObjectByType<HostTitleScreen>() != null) return;

            var go = new GameObject("HostTitleScreen");
            go.AddComponent<HostTitleScreen>();
            DontDestroyOnLoad(go);
        }

        private void Awake()
        {
            BuildUi();
            ShowTitle();
            IsShowing = true;

            // Hidden until the splash is finished. The splash canvas sorts above this one, so the
            // video would cover it anyway — but an invisible screen that still takes clicks would
            // eat the tap meant to skip the splash.
            root.SetActive(!HostSplashScreen.IsPlaying);
        }

        private void OnDestroy()
        {
            // Cleared here rather than only in Begin(), so an unexpected teardown can never leave
            // HostSystemMenu permanently convinced the title is up and Escape permanently dead.
            IsShowing = false;
        }

        private void Update()
        {
            if (HostSplashScreen.IsPlaying)
            {
                if (root.activeSelf) root.SetActive(false);
                return;
            }

            if (!root.activeSelf)
            {
                root.SetActive(true);
                Focus(firstTitleButton);
            }

            // Escape backs out of the instructions. On the title itself it does nothing: QUIT is
            // already on screen, and HostSystemMenu stands down while IsShowing is true.
            if (Input.GetKeyDown(KeyCode.Escape) && instructionsPage.activeSelf) ShowTitle();
        }

        // ─── States ───────────────────────────────────────────────

        private void ShowTitle()
        {
            titlePage.SetActive(true);
            instructionsPage.SetActive(false);
            Focus(firstTitleButton);
        }

        private void ShowInstructions()
        {
            titlePage.SetActive(false);
            instructionsPage.SetActive(true);
            Focus(backButton);
        }

        /// <summary>
        /// START. Nothing to hand over to — the lobby has been live behind this screen since load,
        /// so getting out of the way IS the transition.
        /// </summary>
        private void Begin()
        {
            IsShowing = false;
            Destroy(gameObject);
        }

        private static void Focus(Button b)
        {
            if (b != null) EventSystem.current?.SetSelectedGameObject(b.gameObject);
        }

        // ─── Construction ─────────────────────────────────────────

        private void BuildUi()
        {
            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Above the board and the lobby panel, BELOW the version stamp (31000) so the build
            // number stays readable here — the most useful place in the game to be able to read it —
            // and below the system menu (32000) and the splash (32760).
            canvas.sortingOrder = 30000;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);   // the host's locked canvas size
            scaler.matchWidthOrHeight = 0.5f;

            // Buttons need an EventSystem. The host scene has one, but this must work in any scene it
            // bootstraps into — including one that never had UI input.
            if (EventSystem.current == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                DontDestroyOnLoad(es);
            }

            root = NewRect("Root", canvasGo.transform, Vector2.zero, Vector2.one, Vector2.zero);
            // FULLY opaque: this is a front door, not a pause scrim. The lobby is already rendering
            // behind it and must not show through.
            root.AddComponent<Image>().color = Ground;

            BuildTitlePage();
            BuildInstructionsPage();
        }

        private void BuildTitlePage()
        {
            titlePage = NewRect("TitlePage", root.transform, Vector2.zero, Vector2.one, Vector2.zero);

            var title = NewText("Title", titlePage.transform, 190, FontStyles.Bold, Parchment,
                                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -300), new Vector2(-200, 220));
            title.text = "SALEM 1692";
            title.characterSpacing = 14f;

            var tagline = NewText("Tagline", titlePage.transform, 40, FontStyles.Italic, Dim,
                                  new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -470), new Vector2(-400, 70));
            tagline.text = "Someone at this table is a witch.";

            // Vertical stack, centred low so the title has the upper half to itself.
            var specs = new (string label, Color colour, UnityAction action)[]
            {
                ("START", Ember, Begin),
                ("INSTRUCTIONS", Amber, ShowInstructions),
                ("QUIT", Crimson, AppLifecycle.Quit),
            };

            float top = -660f;
            for (int i = 0; i < specs.Length; i++)
            {
                var (button, label) = NewButton($"Title{specs[i].label}", titlePage.transform,
                                                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                                                new Vector2(0, top - i * (ButtonHeight + ButtonGap)));
                label.text = specs[i].label;
                button.image.color = specs[i].colour;
                button.onClick.AddListener(specs[i].action);
                if (i == 0) firstTitleButton = button;
            }
        }

        /// <summary>
        /// Two columns on one screen, with NO scrolling.
        ///
        /// ⚠ This is read across a room off a TV, usually with no mouse in anyone's hand. A scroll
        /// view would hide the half of the page nobody can reach. The text uses TMP auto-sizing
        /// instead, so on an unusual aspect ratio it shrinks to fit rather than clipping — the
        /// failure mode of a fixed size is a sentence that silently ends mid-word.
        /// </summary>
        private void BuildInstructionsPage()
        {
            instructionsPage = NewRect("InstructionsPage", root.transform, Vector2.zero, Vector2.one, Vector2.zero);

            var heading = NewText("Heading", instructionsPage.transform, 76, FontStyles.Bold, Parchment,
                                  new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -90), new Vector2(-200, 100));
            heading.text = "HOW TO PLAY";

            // Left column: the two addresses. This is the part that actually goes wrong in a real
            // living room, so it leads.
            var left = NewPanel("LeftColumn", instructionsPage.transform,
                                new Vector2(0, 0), new Vector2(0.5f, 1),
                                new Vector4(60, -160, -20, 270));

            Section(left.transform, 0, 300,
                "THE PHONES ARE THE CONTROLLERS",
                "Every player needs their own phone or tablet. That device IS their hand: it shows " +
                "the tryal cards only they can see, and it is where they take their turn, accuse " +
                "someone, and act at night.\n\n" +
                "Everyone opens the <b>JOIN</b> address on the next screen and types the " +
                "four-letter room code.\n\n" +
                "<b>No phone, no seat.</b> This screen is a shared board, not a controller.");

            Section(left.transform, 330, 300,
                "EXTRA SCREENS ARE OPTIONAL",
                "Anyone who cannot see this screen — a second room, or the far end of a long " +
                "table — can open the <b>DISPLAY</b> address on a smart TV, tablet or computer. " +
                "It mirrors this board, so they see what the people sitting here see.\n\n" +
                "A mirror is a <b>display only</b>: never a turn, never anyone's private cards. " +
                "Safe to point at a whole room, but not a substitute for a player's own phone.");

            // Right column: the brief manual.
            var right = NewPanel("RightColumn", instructionsPage.transform,
                                 new Vector2(0.5f, 0), new Vector2(1, 1),
                                 new Vector4(20, -160, -60, 270));

            Section(right.transform, 0, 630,
                "THE GAME IN BRIEF",
                "<b>Setup.</b> Four or more players — empty seats can be filled with AI. Everyone " +
                "gets face-down <i>tryal</i> cards, some reading <i>Witch</i>, and one face-up " +
                "character whose ability the whole table can see.\n\n" +
                "<b>Your turn.</b> Draw two cards, or play cards from your hand.\n\n" +
                "<b>Accusations.</b> Red cards pile accusations on a player. At seven, one of their " +
                "tryal cards is turned over — and the accuser chooses which. Accusations then " +
                "clear. Some cards and characters shift that number.\n\n" +
                "<b>Night.</b> Every phone shows the same screen and everyone taps something, so " +
                "nobody can be spotted by what they are doing. Behind that: the witches choose a " +
                "victim, the constable may protect someone, and anyone may <i>confess</i> — " +
                "turning over one of their own tryal cards to survive the night.\n\n" +
                "<b>Winning.</b> The town wins the moment the last <i>Witch</i> card is turned face " +
                "up; the witches need not be eliminated. The witches win when no ordinary " +
                "townsfolk are left alive.");

            // Footer.
            bool haveManual = !string.IsNullOrWhiteSpace(ManualUrl);

            var (back, backLabel) = NewButton("Back", instructionsPage.transform,
                                              new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                                              new Vector2(haveManual ? -(ButtonWidth + ButtonGap) / 2f : 0f, 90f));
            backLabel.text = "BACK";
            back.image.color = Ember;
            back.onClick.AddListener(ShowTitle);
            backButton = back;

            if (haveManual)
            {
                var (manual, manualLabel) = NewButton("Manual", instructionsPage.transform,
                                                      new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                                                      new Vector2((ButtonWidth + ButtonGap) / 2f, 90f));
                manualLabel.text = "FULL MANUAL";
                manual.image.color = Amber;
                manual.onClick.AddListener(() => Application.OpenURL(ManualUrl));
            }

            var urls = NewText("Urls", instructionsPage.transform, 26, FontStyles.Normal, Dim,
                               new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 215), new Vector2(-200, 70));
            urls.text = DescribeUrls();
        }

        /// <summary>
        /// The two real addresses, when this build has one configured.
        ///
        /// Resolved from <see cref="HostLobbyPanel"/> rather than re-derived, so this page and the
        /// lobby can never print different addresses — the exact failure DeploymentConfig exists to
        /// prevent. When nothing is configured the page says nothing rather than printing a bare
        /// "/join", which reads as an address people then try to type.
        /// </summary>
        private static string DescribeUrls()
        {
            var lobby = FindFirstObjectByType<HostLobbyPanel>();
            if (lobby == null || string.IsNullOrWhiteSpace(lobby.ResolvedBaseUrl)) return "";

            return $"PLAYERS: <b>{lobby.JoinUrl}</b>      " +
                   $"OPTIONAL SECOND SCREEN: <b>{lobby.DisplayUrl}</b>";
        }

        private static void Section(Transform parent, float topOffset, float height, string head, string body)
        {
            var headText = NewText($"{head}-head", parent, 34, FontStyles.Bold, Ember,
                                   new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -topOffset - 24), new Vector2(-60, 48));
            headText.text = head;
            headText.alignment = TextAlignmentOptions.Left;

            var bodyText = NewText($"{head}-body", parent, 27, FontStyles.Normal, Dim,
                                   new Vector2(0, 1), new Vector2(1, 1),
                                   new Vector2(0, -topOffset - 60 - (height - 60) / 2f), new Vector2(-60, height - 60));
            bodyText.text = body;
            bodyText.alignment = TextAlignmentOptions.TopLeft;
            bodyText.richText = true;
            // Shrink rather than clip. See BuildInstructionsPage's note on why there is no scroll view.
            bodyText.enableAutoSizing = true;
            bodyText.fontSizeMin = 21;
            bodyText.fontSizeMax = 27;
        }

        // ─── Primitives ───────────────────────────────────────────

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

        /// <summary>
        /// A filled, outlined column stretched between its anchors. `insets` is
        /// (left, top, right, bottom) in pixels, where top/right are negative — the uGUI
        /// convention, since offsetMax grows up and right.
        ///
        /// ⚠ Takes no size: the offsets stretch the rect between its anchors and completely
        /// supersede sizeDelta, so a size argument here would be a parameter that silently did
        /// nothing — which is exactly what it was before this was fixed.
        /// </summary>
        private static GameObject NewPanel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax,
                                           Vector4 insets)
        {
            var go = NewRect(name, parent, anchorMin, anchorMax, Vector2.zero);
            var rt = (RectTransform)go.transform;
            rt.offsetMin = new Vector2(insets.x, insets.w);
            rt.offsetMax = new Vector2(insets.z, insets.y);

            go.AddComponent<Image>().color = PanelFill;
            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(Parchment.r, Parchment.g, Parchment.b, 0.18f);
            outline.effectDistance = new Vector2(2, 2);
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
            return text;
        }

        private static (Button, TMP_Text) NewButton(string name, Transform parent, Vector2 anchorMin,
                                                    Vector2 anchorMax, Vector2 position)
        {
            var go = NewRect(name, parent, anchorMin, anchorMax, new Vector2(ButtonWidth, ButtonHeight), position);
            var image = go.AddComponent<Image>();
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;

            var label = NewText("Label", go.transform, 34, FontStyles.Bold, new Color(0.06f, 0.05f, 0.04f),
                                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            return (button, label);
        }
    }
}
