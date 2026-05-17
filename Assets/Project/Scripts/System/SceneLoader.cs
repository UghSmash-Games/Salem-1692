/*
* AUTHOR: Ron Bresett
* REFERENCES:
* NOTES:
* TODO: [Planned improvements]
* FIXME: [Known bugs or issues]
*/
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Salem.Systems
{
    public class SceneLoader : MonoBehaviour
    {
        public static SceneLoader Instance { get; private set; }

        [Header("Fade")]
        [SerializeField] private CanvasGroup fadeCanvasGroup;  // assign the FadeCanvas prefab instance
        [SerializeField] private float fadeDuration = 0.35f;   // seconds, unscaled

        [Header("Options")]
        [SerializeField] private bool blockInputDuringFade = true;

        private bool isTransitioning;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (fadeCanvasGroup == null)
                Debug.LogWarning("[SceneLoader] No fade CanvasGroup assigned. Fades will be skipped.");
        }

        private void Start()
        {
            ResetFadeCanvas();
        }

        // ---- Public API ----

        public void LoadScene(string sceneName)         => StartCoroutine(LoadSceneRoutine(sceneName));
        public void ReloadCurrent()                     => LoadScene(SceneManager.GetActiveScene().name);
        public void LoadNextInBuild()                   => LoadScene(GetNextSceneName());
        public void LoadMainMenu(string mainMenuName)   => LoadScene(mainMenuName);

        public void Quit()
        {
            #if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
            #else
                Application.Quit();
            #endif
        }

        public void SetFadeDuration(float seconds)      => fadeDuration = Mathf.Max(0f, seconds);

        // ---- Core ----

        private IEnumerator LoadSceneRoutine(string sceneName)
        {
            Debug.Log($"[SceneLoader] LoadSceneRoutine started: {sceneName}");

            if (isTransitioning)
            {
                Debug.LogWarning("[SceneLoader] Already transitioning.");
                yield break;
            }

            isTransitioning = true;

            // Fade out
            if (fadeCanvasGroup)
            {
                Debug.Log("[SceneLoader] Starting fade OUT.");
                yield return Fade(1f);
            }
            else
            {
                Debug.LogError("[SceneLoader] Missing fadeCanvasGroup.");
            }

            // Begin load (async)
            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            op.allowSceneActivation = true; // no delay gate needed
            while (!op.isDone)
                yield return null;

            // Ensure there is an EventSystem in the new scene (failsafe)
            EnsureEventSystem();

            // Fade in
            if (fadeCanvasGroup)
            {
                Debug.Log("[SceneLoader] Starting fade IN.");
                yield return Fade(0f);
            }

            isTransitioning = false;
        }

        private IEnumerator Fade(float targetAlpha)
        {
            Debug.Log($"[SceneLoader] Fade called. Target alpha: {targetAlpha}, Duration: {fadeDuration}");

            if (fadeCanvasGroup == null)
            {
                Debug.LogError("[SceneLoader] Fade stopped. fadeCanvasGroup is null.");
                yield break;
            } 

            fadeCanvasGroup.gameObject.SetActive(true);
            fadeCanvasGroup.blocksRaycasts = blockInputDuringFade;
            fadeCanvasGroup.interactable = blockInputDuringFade;

            float start = fadeCanvasGroup.alpha;
            float t = 0f;
            while (t < fadeDuration)
            {
                t += Time.unscaledDeltaTime;
                fadeCanvasGroup.alpha = Mathf.Lerp(start, targetAlpha, t / fadeDuration);
                yield return null;
            }
            fadeCanvasGroup.alpha = targetAlpha;

            Debug.Log($"[SceneLoader] Fade complete. Alpha: {fadeCanvasGroup.alpha}");

            if (targetAlpha <= 0f)
            {
                fadeCanvasGroup.blocksRaycasts = false;
                fadeCanvasGroup.interactable = false;
                fadeCanvasGroup.gameObject.SetActive(false);
            }
        }

        private static string GetNextSceneName()
        {
            int i = SceneManager.GetActiveScene().buildIndex;
            int next = (i + 1) % SceneManager.sceneCountInBuildSettings;
            string path = SceneUtility.GetScenePathByBuildIndex(next);
            int slash = path.LastIndexOf('/');
            int dot = path.LastIndexOf('.');
            return path.Substring(slash + 1, dot - slash - 1);
        }

        private static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var go = new GameObject("EventSystem (Auto)");
                go.AddComponent<UnityEngine.EventSystems.EventSystem>();
                go.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
                DontDestroyOnLoad(go);
            }
        }

        private void ResetFadeCanvas()
        {
            if (fadeCanvasGroup == null)
                return;

            fadeCanvasGroup.alpha = 0f;
            fadeCanvasGroup.blocksRaycasts = false;
            fadeCanvasGroup.interactable = false;
            fadeCanvasGroup.gameObject.SetActive(false);
        }
    }
}
