using System;
using System.Collections;
using System.Collections.Generic;
using Salem.Cards;
using Salem.GameFlow;
using Salem.UI;
using UnityEngine;

namespace Salem.Players
{
    /// <summary>
    /// Input from the local host UI — the legacy single-local-human behavior,
    /// now behind IPlayerInput. This is a refactor of code that already lived in
    /// GameTurnManager.RunTurn and the TableLayoutController callbacks; it does
    /// not change behavior.
    /// </summary>
    public class LocalUIInput : IPlayerInput
    {
        private TableLayoutController _table;
        private TableLayoutController Table =>
            _table != null ? _table : (_table = UnityEngine.Object.FindFirstObjectByType<TableLayoutController>());

        public IEnumerator RunTurn(Player player)
        {
            var gtm = GameTurnManager.Instance;
            if (gtm == null) yield break;

            // Identical to the former local-human branch in GameTurnManager.RunTurn:
            // block until a card is played or End Turn is pressed (UI button
            // handlers flip WaitingForHuman to false).
            gtm.WaitingForHuman = true;
            yield return new WaitUntil(() => gtm.WaitingForHuman == false);
        }

        public IEnumerator RequestTarget(Player chooser, string prompt, Func<Player, bool> isValid, Action<Player> onChosen)
        {
            if (Table == null)
            {
                Debug.LogWarning("[LocalUIInput] No TableLayoutController; cannot request target.");
                onChosen?.Invoke(null);
                yield break;
            }

            bool done = false;
            Table.BeginTargetSelection(chooser, prompt, isValid, target =>
            {
                onChosen?.Invoke(target);
                done = true;
            });
            yield return new WaitUntil(() => done);
        }

        public IEnumerator RequestTryal(Player chooser, Player target, Action<int> onChosen)
        {
            if (Table == null)
            {
                Debug.LogWarning("[LocalUIInput] No TableLayoutController; cannot request tryal.");
                onChosen?.Invoke(-1);
                yield break;
            }

            bool done = false;
            Table.BeginTryalSelection(target, idx =>
            {
                onChosen?.Invoke(idx);
                done = true;
            });
            yield return new WaitUntil(() => done);
        }

        public IEnumerator RequestSecretPhase(Player player, string promptType, string[] targetNames,
                                              bool acting, Action<Player, string, bool> onSubmit)
        {
            // Non-acting local players have no local prompt to render (masking only
            // matters for phones). Acting local human picks via the table UI. Local
            // play is single-human, so the pick is reported as an immediate confirm
            // (no tentative stage — there are no fellow witches to relay to locally).
            if (!acting || Table == null)
            {
                onSubmit?.Invoke(player, null, true);
                yield break;
            }

            bool done = false;
            string chosenName = null;
            Table.BeginTargetSelection(
                player,
                promptType,
                target => target != null && !target.IsEliminated,
                target =>
                {
                    chosenName = target != null ? target.PlayerNameText : null;
                    done = true;
                });
            yield return new WaitUntil(() => done);
            onSubmit?.Invoke(player, chosenName, true);
        }

        // Local play has no networked rearrange UI — no-op (keep the current deck order).
        // The Tituba rearrange experience is phone-driven; building a local-host reorder UI
        // is out of scope. Returning null leaves the deck unchanged.
        public IEnumerator RequestDeckRearrange(Player chooser, IReadOnlyList<Card> deck,
                                                float timeoutSeconds, Action<int[]> onOrder)
        {
            onOrder?.Invoke(null);
            yield break;
        }

        // Local play has no card-pick UI — auto-take the first card in the pool so a local/test
        // John draft still resolves (the phone-driven pick is the real experience). Building a local
        // host card-pick UI is out of scope, same rationale as RequestDeckRearrange.
        public IEnumerator RequestCardPick(Player chooser, IReadOnlyList<Card> pool, int pickNumber,
                                           int totalPicks, float timeoutSeconds, Action<int> onIndex)
        {
            onIndex?.Invoke(pool != null && pool.Count > 0 ? 0 : -1);
            yield break;
        }
    }
}
