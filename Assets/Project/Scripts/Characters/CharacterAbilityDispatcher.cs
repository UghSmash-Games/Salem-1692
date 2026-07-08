using System.Collections;
using System.Collections.Generic;
using Salem.Cards;
using Salem.Data;
using Salem.Players;
using UnityEngine;

namespace Salem.Characters
{
    /// <summary>
    /// Central dispatcher for Town Hall character abilities that hang off
    /// <see cref="PlayerService.OnPlayerEliminated"/>. Routes by
    /// <see cref="Player.GetEffectiveTownHallName"/> so Martha Corey's inheritance is automatic.
    ///
    /// GROUP A (this commit) owns the SYNCHRONOUS work: Martha Corey's copied charge/limit re-resolve
    /// and the Cotton-Mather evidence revert (relocated out of <see cref="PlayerService.Eliminate"/>).
    /// GROUP B adds the ASYNC, networked John Proctor card draft via the queue below (John-first
    /// alternating pick). Other characters' scattered name-checks stay put for now and migrate later.
    ///
    /// Self-bootstraps (no scene wiring needed) so it is active for headless/incremental testing.
    /// </summary>
    public class CharacterAbilityDispatcher : MonoBehaviour
    {
        public static CharacterAbilityDispatcher Instance { get; private set; }

        // Ability registry keyed by identity. Abilities that react to eliminations implement
        // IOnPlayerEliminated; the draft coroutine invokes each on every elimination. (John's ability
        // triggers on OTHERS' deaths and finds its own drafters, so it is not keyed by the dead
        // player's identity — future holder-triggered abilities can key by GetEffectiveTownHallName.)
        private readonly Dictionary<TownhallName, ICharacterAbility> _abilities = new()
        {
            { TownhallName.JohnProctor, new JohnProctorAbility() },
        };

        // Async draft serialization. Re-entrant eliminations — the matchmaker cascade calls
        // EliminateNow() recursively inside PlayerService.Eliminate — enqueue here and drain one at a
        // time so two drafts never interleave on the network.
        private readonly Queue<(Player dead, EliminationCause cause)> _draftQueue = new();
        private bool _draftRunning;

        // Read-only debug accessors (Phase 5 verification) — confirm the queue drains cleanly and is
        // never left in a stuck _draftRunning state after an edge case (e.g. the orphaned-hand path).
        public bool IsDraftRunning => _draftRunning;
        public int QueuedDraftCount => _draftQueue.Count;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            var go = new GameObject(nameof(CharacterAbilityDispatcher));
            DontDestroyOnLoad(go);
            go.AddComponent<CharacterAbilityDispatcher>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        private void OnEnable()
        {
            PlayerService.OnPlayerEliminated += HandleEliminated;
        }

        private void OnDisable()
        {
            PlayerService.OnPlayerEliminated -= HandleEliminated;
            if (Instance == this) Instance = null;
        }

        private void HandleEliminated(Player dead, EliminationCause cause)
        {
            // STEP 1 (Group A, synchronous): Martha Corey copy re-resolve + Cotton-Mather revert.
            // Runs for every elimination (including re-entrant matchmaker-cascade ones), exactly as the
            // old inline loop in PlayerService.Eliminate did — just centralised here and now also
            // re-resolving copied CHARGES/LIMITS, not only the accusation count.
            ReResolveMarthas(dead);

            // STEP 2: John Proctor draft. Enqueue + drain via a coroutine so re-entrant eliminations
            // (matchmaker cascade) serialise instead of interleaving on the network. The queue is
            // drained after Eliminate returns; card ownership doesn't affect win conditions, so the
            // async draft never needs to block EvaluateEndGame.
            _draftQueue.Enqueue((dead, cause));
            if (!_draftRunning) StartCoroutine(ProcessDraftQueue());
        }

        private IEnumerator ProcessDraftQueue()
        {
            _draftRunning = true;
            while (_draftQueue.Count > 0)
            {
                var (dead, cause) = _draftQueue.Dequeue();
                foreach (var ability in _abilities.Values)
                    if (ability is IOnPlayerEliminated onElim)
                        yield return onElim.OnPlayerEliminated(dead, cause);
            }
            _draftRunning = false;
        }

        /// <summary>
        /// For every living Martha Corey: recompute her accusation total (evidence reverts 1→3 when
        /// Cotton Mather dies), re-resolve her copied charge/limit if her effective source changed
        /// (a neighbour died), then re-check her threshold. <see cref="Player.GetEffectiveTownHallName"/>
        /// already drops the just-eliminated player (IsEliminated set + townhallCard cleared in
        /// OnElimination), so a Martha now resolves to her next living neighbour.
        /// </summary>
        private void ReResolveMarthas(Player dead)
        {
            foreach (var m in PlayerService.GetAlivePlayers())
            {
                if (m == null || m.townhallCard == null ||
                    m.townhallCard.CardName != TownhallName.MarthaCorey) continue;

                m.RecomputeStatusFromStatusCards(); // revert the count first (no reveal yet)
                m.ReResolveMarthaCopy();            // charge/limit re-resolve on source change
                m.ApplyAccusation(0);               // recompute (idempotent) + threshold check (may reveal)
            }
        }
    }
}
