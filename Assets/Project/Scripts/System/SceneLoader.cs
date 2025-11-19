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
            if (isTransitioning) yield break;
            isTransitioning = true;

            // Fade out
            if (fadeCanvasGroup)
                yield return Fade(1f);

            // Begin load (async)
            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            op.allowSceneActivation = true; // no delay gate needed
            while (!op.isDone)
                yield return null;

            // Ensure there is an EventSystem in the new scene (failsafe)
            EnsureEventSystem();

            // Fade in
            if (fadeCanvasGroup)
                yield return Fade(0f);

            isTransitioning = false;
        }

        private IEnumerator Fade(float targetAlpha)
        {
            if (fadeCanvasGroup == null) yield break;

            if (blockInputDuringFade)
                fadeCanvasGroup.blocksRaycasts = true;

            fadeCanvasGroup.gameObject.SetActive(true);

            float start = fadeCanvasGroup.alpha;
            float t = 0f;
            while (t < fadeDuration)
            {
                t += Time.unscaledDeltaTime;
                fadeCanvasGroup.alpha = Mathf.Lerp(start, targetAlpha, t / fadeDuration);
                yield return null;
            }
            fadeCanvasGroup.alpha = targetAlpha;

            if (targetAlpha <= 0f)
            {
                fadeCanvasGroup.blocksRaycasts = false;
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
    }
}
