/*
* AUTHOR: Ron Bresett
* REFERENCES:
* NOTES:
*   Primary Purpose: Resolves logic when cards are played.
*   Responsibilities:
*        • Interpret effect type
*        • Trigger gameplay consequences
*   Access Requirements:
*        • DeckManager
*        • Player
*        • GameStateManager

* TODO: [Planned improvements]
* FIXME: [Known bugs or issues]
*/

// Assets/_Project/Scripts/UI/CardTooltipUI.cs
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Salem.UI
{
    public class CardTooltipUI : MonoBehaviour
    {
        public static CardTooltipUI Instance { get; private set; }

        [Header("Refs")]
        [SerializeField] private RectTransform tooltipRoot;   // the panel
        [SerializeField] private TMP_Text text;
        [SerializeField] private CanvasGroup cg;

        [Header("Layout")]
        [SerializeField] private Vector2 screenOffset = new(16f, -16f);
        [SerializeField] private float fadeTime = 0.08f;

        bool visible;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            HideImmediate();
        }

        public void Show(string body, Vector2 screenPos)
        {
            if (string.IsNullOrWhiteSpace(body)) return;
            text.text = body;

            // keep on screen: flip pivot if near edges
            Vector2 pivot = new(0, 1);
            var canvas = GetComponentInParent<Canvas>();
            var rt = (RectTransform)transform;
            Vector2 size = tooltipRoot.sizeDelta;
            Vector2 canvasSize = (canvas.transform as RectTransform).rect.size;

            // simple bounds check using desired corner
            Vector2 desired = screenPos + screenOffset;
            if (desired.x + size.x > canvasSize.x) pivot.x = 1;     // flip to left
            if (desired.y - size.y < 0) pivot.y = 0;                // flip to bottom
            tooltipRoot.pivot = pivot;

            tooltipRoot.anchoredPosition = desired;
            StopAllCoroutines();
            visible = true;
            if (cg) StartCoroutine(FadeTo(1f)); else tooltipRoot.gameObject.SetActive(true);
        }

        public void Hide()
        {
            if (!visible) return;
            visible = false;
            StopAllCoroutines();
            if (cg) StartCoroutine(FadeTo(0f));
            else tooltipRoot.gameObject.SetActive(false);
        }

        void HideImmediate()
        {
            if (cg) { cg.alpha = 0f; }
            tooltipRoot.gameObject.SetActive(false);
            visible = false;
        }

        System.Collections.IEnumerator FadeTo(float a)
        {
            tooltipRoot.gameObject.SetActive(true);
            if (!cg)
            {
                tooltipRoot.gameObject.SetActive(a > 0f);
                yield break;
            }
            float start = cg.alpha, t = 0f;
            while (t < fadeTime)
            {
                t += Time.unscaledDeltaTime;
                cg.alpha = Mathf.Lerp(start, a, t / fadeTime);
                yield return null;
            }
            cg.alpha = a;
            if (a <= 0f) tooltipRoot.gameObject.SetActive(false);
        }
    }
}
