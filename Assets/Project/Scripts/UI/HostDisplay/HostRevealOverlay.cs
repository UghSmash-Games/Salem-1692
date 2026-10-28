using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Salem.Networking; // PUBLIC DTOs + host-facing public send-events ONLY.

namespace Salem.UI.HostDisplay
{
    /// <summary>
    /// The synchronized dramatic beat (stage 7d): a countdown, then the tryal cards turning.
    ///
    /// 🔴 TIMING IS DERIVED FROM `revealAt`, NEVER FROM MESSAGE RECEIPT.
    /// `phase_resolve` carries a shared wall-clock instant ~3s in the future. Every screen —
    /// this host, every mirror, every phone — schedules against that SAME absolute moment, so
    /// they flip in unison regardless of latency. Animating on receipt would desynchronise the
    /// rooms, which is the entire failure this mechanism exists to prevent. `remaining` is
    /// recomputed every frame from the timestamp; nothing caches a delay.
    ///
    /// ⏱ REALTIME THROUGHOUT (`DateTimeOffset.UtcNow`, `Time.unscaledTime`). A night kill can END
    /// THE GAME, and pauseOnGameEnd sets Time.timeScale to 0 — a scaled wait would strand the
    /// overlay mid-reveal on the single most dramatic beat in the game. GamePhaseManager's own
    /// reveal coroutine uses WaitForSecondsRealtime for exactly this reason.
    ///
    /// MIRRORS THE WEB CLIENT: webclient/src/components/RevealOverlay.tsx uses the same states and
    /// the same linger (`lingerSeconds` here == REVEALED_LINGER_MS there — keep them equal). The
    /// Phase-7 checkpoint requires host and mirror to animate together, so changes here need
    /// matching changes there.
    ///
    /// THE BEAT IS A SEQUENCE: each confession is announced with the card it turned, then the
    /// outcome last. The whole sequence is fitted INSIDE the shared linger (see BuildSteps), so the
    /// two screens still enter and leave together — the mirror simply holds one static outcome for
    /// the same window while the host steps through the detail.
    ///
    /// PRIVACY: nothing new crosses the wire. Revealed tryal labels are public by definition (a
    /// reveal IS a public event), `elimination_result` is already broadcast, and this class cannot
    /// see the `acting` flag at all.
    /// ⛔ `savedBy` is a LABEL ("constable" / "confession"), never a playerId — naming the saver
    /// would publish the CONSTABLE'S SECRET IDENTITY on a broadcast channel. Never "improve" it
    /// into a name lookup.
    /// </summary>
    public class HostRevealOverlay : MonoBehaviour
    {
        private enum State { Idle, Counting, Revealed }

        [Header("Root")]
        [Tooltip("Faded in/out. Keep this GameObject ACTIVE — Update drives the schedule.")]
        [SerializeField] private CanvasGroup group;
        [Tooltip("Visuals, switched off while idle.")]
        [SerializeField] private GameObject content;

        [Header("Counting")]
        [SerializeField] private GameObject countingRoot;
        [SerializeField] private TMP_Text secondsText;

        [Header("Revealed")]
        [SerializeField] private GameObject revealedRoot;
        [SerializeField] private TMP_Text headlineText;
        [SerializeField] private Transform cardRow;
        [SerializeField] private HostRevealCard cardPrefab;
        [SerializeField] private HostCardSpriteRegistry sprites;
        [Tooltip("A night kill flips EVERY tryal the victim holds, so a beat can turn several cards.")]
        [SerializeField] private int maxCards = 5;
        [SerializeField] private float cardStaggerSeconds = 0.12f;

        [Header("Timing")]
        [Tooltip("Must match RevealOverlay.tsx REVEALED_LINGER_MS so host and mirror clear together.")]
        [SerializeField] private float lingerSeconds = 4f;
        [SerializeField] private float fadeSeconds = 0.35f;

        private readonly List<HostRevealCard> cards = new();
        private readonly List<(string targetId, string label)> pending = new();
        private readonly List<(string targetId, string label)> confessions = new();
        private string gavelTargetId;
        private readonly List<(string headline, List<string> cardLabels)> steps = new();
        private readonly Dictionary<string, string> nameById = new();

        private State state = State.Idle;
        private long revealAtMs;
        private float targetAlpha;
        private int cardsShown;
        private bool populated;
        private int stepIndex;
        private float stepDwell;
        private float stepStartedUnscaled;
        private EliminationResultMsg elimination;

        private void Awake()
        {
            if (group == null) group = GetComponent<CanvasGroup>();
            if (group != null)
            {
                group.alpha = 0f;
                group.interactable = false;
                group.blocksRaycasts = false;
            }
            if (content != null) content.SetActive(false);
        }

        private void OnEnable()
        {
            NetworkManager.OnPhaseResolveSent += HandlePhaseResolve;
            NetworkManager.OnEliminationResultSent += HandleElimination;
            NetworkManager.OnGameEventSent += HandleGameEvent;
        }

        private void OnDisable()
        {
            NetworkManager.OnPhaseResolveSent -= HandlePhaseResolve;
            NetworkManager.OnEliminationResultSent -= HandleElimination;
            NetworkManager.OnGameEventSent -= HandleGameEvent;
        }

        /// <summary>Refreshes the id→display-name map from the public board.</summary>
        public void Render(GameStateUpdateMsg state)
        {
            if (state?.players == null) return;

            nameById.Clear();
            foreach (var p in state.players)
            {
                if (p == null || string.IsNullOrEmpty(p.playerId)) continue;
                nameById[p.playerId] = p.displayName;
            }
        }

        // ─── Signals ───────────────────────────────────────────────

        private void HandlePhaseResolve(long revealAt)
        {
            revealAtMs = revealAt;
            elimination = null;
            pending.Clear();
            confessions.Clear();
            gavelTargetId = null;
            steps.Clear();
            cardsShown = 0;
            populated = false;
            stepIndex = 0;

            foreach (var c in cards) if (c != null) c.gameObject.SetActive(false);

            if (content != null) content.SetActive(true);
            targetAlpha = 1f;

            SetSection(counting: true);
            state = State.Counting;

            // Armed late (or the instant already passed): skip straight to the reveal, exactly as
            // useSynchronizedReveal does for a mirror that joined mid-countdown.
            if (RemainingMs() <= 0) EnterRevealed();
        }

        private void HandleElimination(EliminationResultMsg msg)
        {
            // Arrives in the same synchronous burst as the reveals; the sequence is assembled a
            // frame later, so the outcome is always known by then.
            elimination = msg;
        }

        private void HandleGameEvent(GameEventMsg e)
        {
            if (e == null || state == State.Idle) return;

            // gavel_placed carries no `value` (the recipient is the whole message), so the
            // empty-value guard below must not swallow it.
            bool needsValue = !string.Equals(e.kind, "gavel_placed", StringComparison.Ordinal);
            if (needsValue && string.IsNullOrEmpty(e.value)) return;

            // Two streams, deliberately kept apart so no card is drawn twice: confession_revealed
            // says WHO confessed and with which card; tryal_revealed supplies the victim's cards.
            if (string.Equals(e.kind, "gavel_placed", StringComparison.Ordinal))
                gavelTargetId = e.targetId;
            else if (string.Equals(e.kind, "confession_revealed", StringComparison.Ordinal))
                confessions.Add((e.targetId, e.value));
            else if (string.Equals(e.kind, "tryal_revealed", StringComparison.Ordinal))
                pending.Add((e.targetId, e.value));
        }

        // ─── Schedule ──────────────────────────────────────────────

        private void Update()
        {
            if (group != null)
            {
                float step = fadeSeconds <= 0f ? 1f : Time.unscaledDeltaTime / fadeSeconds;
                group.alpha = Mathf.MoveTowards(group.alpha, targetAlpha, step);
            }

            switch (state)
            {
                case State.Counting:
                {
                    long remaining = RemainingMs();
                    if (remaining <= 0) { EnterRevealed(); break; }
                    if (secondsText != null)
                        secondsText.text = Mathf.CeilToInt(remaining / 1000f).ToString();
                    break;
                }

                case State.Revealed:
                    // Build ONE FRAME after entering, so the whole synchronous burst
                    // (confession_revealed / tryal_revealed x N, then elimination_result) has
                    // landed and the sequence can be assembled with the outcome known.
                    if (!populated) { BuildSteps(); ShowStep(0); break; }

                    if (Time.unscaledTime - stepStartedUnscaled >= stepDwell) AdvanceStep();
                    break;

                case State.Idle:
                    if (content != null && content.activeSelf && group != null &&
                        Mathf.Approximately(group.alpha, 0f))
                    {
                        content.SetActive(false);
                    }
                    break;
            }
        }

        /// <summary>Milliseconds until the shared reveal instant. Recomputed, never cached.</summary>
        private long RemainingMs() =>
            revealAtMs - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        private void EnterRevealed()
        {
            state = State.Revealed;

            SetSection(counting: false);
        }

        /// <summary>
        /// Assembles the beat into an ordered sequence: every confession first, the outcome last.
        ///
        /// 🔴 PARITY: the sequence is fitted INSIDE `lingerSeconds` — dwell is `linger / stepCount`
        /// — so the host lands on its final step exactly as the mirror clears, however many players
        /// confessed. Do NOT make it additive: a variable-length beat would drift out of step with
        /// RevealOverlay.tsx, which shows one static outcome for the same fixed window.
        ///
        /// Cards are attributed BY OWNER, from two separate streams. Confessor steps take their
        /// card from `confession_revealed`; only the victim's `tryal_revealed` cards reach the final
        /// step. Mixing them would render a confessor's card under "ALICE IS HANGED" as if it were
        /// hers, and drawing both streams would show the same card twice.
        /// </summary>
        private void BuildSteps()
        {
            populated = true;
            steps.Clear();

            // Gavel first — chronologically the constable places the token BEFORE the confess
            // window opens. Skipped when it duplicates the outcome: if the gavel recipient IS the
            // targeted player, the final step already reads "SAVED BY THE CONSTABLE".
            bool gavelIsTheSave = elimination != null &&
                                  string.Equals(gavelTargetId, elimination.playerId, StringComparison.Ordinal);
            if (!string.IsNullOrEmpty(gavelTargetId) && !gavelIsTheSave)
                steps.Add(($"THE GAVEL RESTS WITH {NameOf(gavelTargetId)}", new List<string>()));

            foreach (var (targetId, label) in confessions)
                steps.Add(($"{NameOf(targetId)} CONFESSES", new List<string> { label }));

            if (elimination == null)
            {
                // No elimination_result. Either a beat that killed no one (a confession-only
                // night) or a NON-NIGHT reveal — conspiracy step 1 turns exactly one card and
                // sends no outcome. Show whatever actually turned rather than implying a death:
                // "THE DEED IS DONE" over a conspiracy flip reads as an elimination that never
                // happened. Falls back to it only when nothing turned at all.
                // ⚠ SAME TWO-STREAM RULE AS ABOVE: a confessor's flip fires BOTH confession_revealed
                // and the generic tryal_revealed, so an unfiltered pass would show that card a
                // second time under a bare "TRYAL IS TURNED". Only cards belonging to nobody who
                // confessed this beat are unattributed and need a step of their own.
                int unattributed = 0;
                foreach (var (targetId, label) in pending)
                {
                    if (confessions.Exists(c => string.Equals(c.targetId, targetId, StringComparison.Ordinal)))
                        continue;
                    steps.Add(($"{NameOf(targetId)}'S TRYAL IS TURNED", new List<string> { label }));
                    unattributed++;
                }

                if (unattributed == 0 && confessions.Count == 0)
                    steps.Add(("THE DEED IS DONE", new List<string>()));
            }
            else if (elimination.eliminated)
            {
                // ApplyNightKill turns EVERY tryal the victim holds, so this step can show several.
                var victimCards = new List<string>();
                foreach (var (targetId, label) in pending)
                {
                    if (string.Equals(targetId, elimination.playerId, StringComparison.Ordinal))
                        victimCards.Add(label);
                }
                steps.Add(($"{NameOf(elimination.playerId)} IS HANGED", victimCards));
            }
            else
            {
                // Saved: targeted but alive, so their tryals never turned — text only.
                steps.Add(($"{NameOf(elimination.playerId)} WAS {SavedByPhrase(elimination.savedBy)}",
                           new List<string>()));
            }

            stepDwell = lingerSeconds / Mathf.Max(1, steps.Count);
        }

        private void ShowStep(int index)
        {
            stepIndex = index;
            stepStartedUnscaled = Time.unscaledTime;

            if (index < 0 || index >= steps.Count) return;
            var (headline, labels) = steps[index];

            if (headlineText != null) headlineText.text = headline;

            foreach (var c in cards) if (c != null) c.gameObject.SetActive(false);
            cardsShown = 0;
            foreach (var label in labels) AddCard(label);
        }

        private void AdvanceStep()
        {
            if (stepIndex + 1 < steps.Count) { ShowStep(stepIndex + 1); return; }

            targetAlpha = 0f;
            state = State.Idle;
        }

        private void SetSection(bool counting)
        {
            if (countingRoot != null) countingRoot.SetActive(counting);
            if (revealedRoot != null) revealedRoot.SetActive(!counting);
        }

        // ─── Rendering ─────────────────────────────────────────────

        /// <summary>
        /// ⛔ `savedBy` is a LABEL, not a playerId — mapping it to copy here is deliberate. Resolving
        /// it as a player would name the CONSTABLE, whose identity is secret.
        /// </summary>
        private static string SavedByPhrase(string savedBy)
        {
            switch (savedBy)
            {
                case "constable":  return "SAVED BY THE CONSTABLE";
                case "confession": return "SAVED BY CONFESSION";
                default:           return "SPARED";
            }
        }

        private void AddCard(string label)
        {
            if (cardRow == null || cardPrefab == null) return;
            if (cardsShown >= Mathf.Max(1, maxCards)) return;

            while (cards.Count <= cardsShown) cards.Add(Instantiate(cardPrefab, cardRow));

            var card = cards[cardsShown];
            card.gameObject.SetActive(true);
            card.ShowBack(sprites != null ? sprites.Back : null);
            card.Play(sprites != null ? sprites.Get(label) : null,
                      cardsShown * Mathf.Max(0f, cardStaggerSeconds));

            cardsShown++;
        }

        private string NameOf(string playerId)
        {
            if (string.IsNullOrEmpty(playerId)) return "SOMEONE";
            return nameById.TryGetValue(playerId, out var n) && !string.IsNullOrEmpty(n)
                ? n
                : playerId;
        }
    }
}
