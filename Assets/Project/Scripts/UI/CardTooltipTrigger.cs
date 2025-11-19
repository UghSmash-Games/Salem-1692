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

using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using Salem.UI;
using Salem.Cards;

namespace Salem.UI
{
    public class CardTooltipTrigger : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler, IBeginDragHandler, IDragHandler
    {
        [Header("Config")]
        [SerializeField] private float hoverDelay = 0.20f;
        [SerializeField] private float longPressTime = 0.45f;   // touch hold
        [SerializeField] private float cancelMove = 10f;        // px before cancelling long-press

        private GameCardUI cardUI;
        private Card card;
        private Coroutine hoverCo, pressCo;
        private bool tooltipOpen;
        private Vector2 pressPos;

        void Awake()
        {
            cardUI = GetComponent<GameCardUI>();
        }

        void OnEnable()
        {
            card = cardUI ? cardUI.Card : null;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (eventData.pointerPress != null) return; // ignore while pressing
            // desktop hover: small delay
            hoverCo = StartCoroutine(HoverAfterDelay(eventData));
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (hoverCo != null) StopCoroutine(hoverCo);
            HideTip();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            pressPos = eventData.position;
            if (pressCo != null) StopCoroutine(pressCo);
            pressCo = StartCoroutine(LongPress(eventData));
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (pressCo != null) StopCoroutine(pressCo);
            // if we showed due to long press, hide on release
            if (tooltipOpen) HideTip();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (pressCo != null) StopCoroutine(pressCo);
            HideTip();
        }

        public void OnDrag(PointerEventData eventData)
        {
            if ((eventData.position - pressPos).sqrMagnitude > cancelMove * cancelMove)
            {
                if (pressCo != null) StopCoroutine(pressCo);
                HideTip();
            }
        }

        IEnumerator HoverAfterDelay(PointerEventData e)
        {
            yield return new WaitForSecondsRealtime(hoverDelay);
            ShowTip(e.position);
        }

        IEnumerator LongPress(PointerEventData e)
        {
            float t = 0f;
            while (t < longPressTime)
            {
                t += Time.unscaledDeltaTime;
                // cancel if moved too far
                if ((e.position - pressPos).sqrMagnitude > cancelMove * cancelMove) yield break;
                yield return null;
            }
            ShowTip(e.position);
        }

        void ShowTip(Vector2 screenPos)
        {
            if (card == null || CardTooltipUI.Instance == null) return;
            string body = ExtractRulesText(card);
            if (string.IsNullOrWhiteSpace(body)) return;
            CardTooltipUI.Instance.Show(body, screenPos);
            tooltipOpen = true;
        }

        void HideTip()
        {
            if (!tooltipOpen || CardTooltipUI.Instance == null) return;
            CardTooltipUI.Instance.Hide();
            tooltipOpen = false;
        }

        static string ExtractRulesText(Card c)
        {
            if (c is ActionCardSO ac && !string.IsNullOrWhiteSpace(ac.RulesText))
                return ac.RulesText;
            return c != null ? c.Name : "";
        }
    }
}