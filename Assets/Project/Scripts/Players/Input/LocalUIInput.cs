using System;
using System.Collections;
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
    }
}
