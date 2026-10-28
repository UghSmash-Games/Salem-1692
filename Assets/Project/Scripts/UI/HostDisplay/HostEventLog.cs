using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Salem.Networking; // PUBLIC DTOs ONLY — see the masking-boundary banner in HostTableView.cs.

namespace Salem.UI.HostDisplay
{
    /// <summary>
    /// "What Has Passed" — the public event rail on the host (TV) screen.
    ///
    /// 🔴 THIS CLASS OWNS ALL THE PROSE. The wire carries no sentences: a
    /// <see cref="GameEventMsg"/> is a closed-vocabulary <c>kind</c> plus public ids and short
    /// enumerable labels. Rendering them into English happens HERE, behind the masking boundary, so
    /// a secret cannot be smuggled into the log by any call site upstream. If you find yourself
    /// wanting to send a ready-made sentence from the game logic, that is the thing this design
    /// exists to prevent — add a `kind` instead.
    ///
    /// Player names are resolved from the PUBLIC board (<see cref="GameStateUpdateMsg.players"/>),
    /// so the log shows the PLAYER's display name, never the character name.
    ///
    /// Timestamps arrive as epoch milliseconds and are formatted in THIS machine's local time, so a
    /// mirror in another region shows its own wall clock rather than the host's.
    /// </summary>
    public class HostEventLog : MonoBehaviour
    {
        [Header("List")]
        [SerializeField] private Transform entryContainer;
        [Tooltip("Row prefab: root with a TMP_Text for the timestamp and one for the body.")]
        [SerializeField] private HostEventLogEntry entryPrefab;
        [Tooltip("Oldest entries beyond this are discarded (the design shows the last 14).")]
        [SerializeField] private int maxEntries = 14;

        [Header("Format")]
        [SerializeField] private string timeFormat = "HH:mm";

        private readonly List<HostEventLogEntry> rows = new();
        private readonly Dictionary<string, string> nameById = new();

        private void OnEnable() => NetworkManager.OnGameEventSent += Append;
        private void OnDisable() => NetworkManager.OnGameEventSent -= Append;

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

        private void Append(GameEventMsg e)
        {
            if (e == null || entryContainer == null || entryPrefab == null) return;

            string body = Describe(e);
            if (string.IsNullOrEmpty(body)) return; // unknown kind — render nothing rather than guess

            var row = Instantiate(entryPrefab, entryContainer);
            row.Set(FormatTime(e.atMs), body);
            rows.Add(row);

            while (rows.Count > Mathf.Max(1, maxEntries))
            {
                var oldest = rows[0];
                rows.RemoveAt(0);
                if (oldest != null) Destroy(oldest.gameObject);
            }
        }

        /// <summary>
        /// kind + ids → one sentence. The ONLY prose in the system.
        ///
        /// An unrecognised kind returns empty and is dropped: a log that silently omits an entry is
        /// strictly safer than one that invents text for a message it does not understand.
        /// </summary>
        private string Describe(GameEventMsg e)
        {
            string actor = NameOf(e.actorId);
            string target = NameOf(e.targetId);
            string card = e.cardName;

            switch (e.kind)
            {
                case "game_started":
                    return "The table is set. Tryal cards are dealt.";

                case "phase_changed":
                    return DescribePhase(e.value);

                case "card_played":
                    return DescribeCardPlayed(actor, card, target);

                case "tryal_revealed":
                    return string.IsNullOrEmpty(target)
                        ? null
                        : $"{target}'s Tryal card is turned: {e.value}.";

                case "double_witch_revealed":
                    return string.IsNullOrEmpty(target)
                        ? null
                        : $"{target} holds another Witch card, and survives.";

                case "gavel_placed":
                    return string.IsNullOrEmpty(target)
                        ? null
                        : $"The gavel is set before {target}.";

                case "confession_revealed":
                    return string.IsNullOrEmpty(target)
                        ? null
                        : $"{target} confesses, and is spared the night.";

                case "player_eliminated":
                    return string.IsNullOrEmpty(target)
                        ? null
                        : $"{target} is hanged.";

                case "cards_drawn":
                    // DELIBERATELY SILENT. The event exists for the AUDIO cue, not the log — a turn
                    // is either a draw or a play, so logging every draw would roughly double the
                    // volume and push more interesting entries out of the window. Cased explicitly
                    // rather than left to `default` so this reads as a decision, not an oversight.
                    return null;

                case "game_over":
                    return DescribeWinner(e.value);

                default:
                    return null;
            }
        }

        /// <summary>
        /// The red accusation cards get their own phrasing — "accuses" reads as the dramatic beat it
        /// is, where "plays Accusation on" reads like a rules footnote. Everything else falls back to
        /// the generic form, so a card added later still logs sensibly without a code change.
        /// </summary>
        private static string DescribeCardPlayed(string actor, string card, string target)
        {
            if (string.IsNullOrEmpty(card)) return null;

            bool haveBoth = !string.IsNullOrEmpty(actor) && !string.IsNullOrEmpty(target);
            if (haveBoth)
            {
                switch (card)
                {
                    case "Accusation": return $"{actor} accuses {target} of consorting with the Devil.";
                    case "Evidence":   return $"{actor} presents Evidence against {target}.";
                    case "Witness":    return $"{actor} calls a Witness against {target}.";
                    default:           return $"{actor} plays {card} on {target}.";
                }
            }

            if (!string.IsNullOrEmpty(actor)) return $"{actor} plays {card}.";
            return $"{card} is played.";
        }

        private static string DescribePhase(string phase)
        {
            if (string.IsNullOrEmpty(phase)) return null;
            switch (phase.ToLowerInvariant())
            {
                case "dawn":  return "Dawn breaks over Salem.";
                case "day":   return "The town gathers in daylight.";
                case "night": return "Night falls. Players close their eyes.";
                default:      return null;
            }
        }

        private static string DescribeWinner(string winner)
        {
            if (string.IsNullOrEmpty(winner)) return null;
            var w = winner.ToLowerInvariant();
            if (w.Contains("witch")) return "The witches prevail. Salem is lost.";
            if (w.Contains("town") || w.Contains("village")) return "The town prevails. The witches are undone.";
            return null;
        }

        private string NameOf(string playerId)
        {
            if (string.IsNullOrEmpty(playerId)) return null;
            return nameById.TryGetValue(playerId, out var n) ? n : null;
        }

        /// <summary>Epoch ms → local wall clock. Never trust a preformatted time from the wire.</summary>
        private string FormatTime(long atMs)
        {
            if (atMs <= 0) return string.Empty;
            return DateTimeOffset.FromUnixTimeMilliseconds(atMs).ToLocalTime().ToString(timeFormat);
        }
    }
}
