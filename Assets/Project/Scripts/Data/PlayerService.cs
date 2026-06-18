/*
* AUTHOR: Ron Bresett
* REFERENCES:
* NOTES:
* TODO: [Planned improvements]
* FIXME: [Known bugs or issues]
*/

using System;
using System.Collections.Generic;
using Salem.GameFlow;
using Salem.Players;
using UnityEngine;

namespace Salem.Data
{
    public enum EliminationCause
    {
        NightKill,
        Lynch,
        WitchTrialRevealed,
        AllTrialsRevealed,
        CardEffect,
        Disconnect,
        Other
    }

    /// <summary>
    /// Local: single local human + AI (legacy/testing). Networked: every human
    /// is a remote phone client; no player is "local". Default is Local so the
    /// existing local/AI game keeps working unchanged.
    /// </summary>
    public enum GameMode
    {
        Local,
        Networked
    }

    public static class PlayerService
    {
        private static readonly List<Player> allPlayers = new();
        public static IReadOnlyList<Player> All => allPlayers;

        /// <summary>Current input/player-creation mode. Reset to Local on Clear().</summary>
        public static GameMode Mode { get; set; } = GameMode.Local;

        // Maps network playerIds (e.g. "p0", "p1") to Player objects in networked
        // mode. Replaces the old PlayerNameText-as-id stand-in.
        private static readonly Dictionary<string, Player> byNetworkId = new();

        /// <summary>
        /// When true, player input comes from AirConsole phone controllers
        /// instead of local UI clicks. Set by AirConsoleManager on init.
        /// </summary>
        //public static bool IsAirConsoleMode { get; set; } AIRCONSOLE TEMP DISABLED 4/28/26

        public static event Action<Player, EliminationCause> OnPlayerEliminated;

        public static void Register(Player player)
        {
            if (!allPlayers.Contains(player))
            {
                allPlayers.Add(player);
                
                /*AIRCONSOLE TEMP DISABLED 4/28/26
                // In AirConsole mode, no player is "local" — input comes from phones
                if (!IsAirConsoleMode && !player.IsLocalPlayer && !(player is AIPlayer) && GetLocalPlayer() == null)
                {
                    player.IsLocalPlayer = true;
                }
                */

                // Set first non-AI as local player automatically — LOCAL mode only.
                // In networked mode every human is remote; no player is local.
                if (Mode == GameMode.Local && !player.IsLocalPlayer && !(player is AIPlayer) && GetLocalPlayer() == null)
                {
                    player.IsLocalPlayer = true;
                }
                //Debug.Log($"[PlayerService] Registered player: {player.PlayerName}");
            }
        }

        public static void Clear()
        {
            allPlayers.Clear();
            byNetworkId.Clear();
            Mode = GameMode.Local;
            //IsAirConsoleMode = false; AIRCONSOLE TEMP DISABLED 4/28/26
        }

        /// <summary>Associate a network playerId with a Player (networked mode).</summary>
        public static void RegisterNetworkId(string playerId, Player player)
        {
            if (string.IsNullOrEmpty(playerId) || player == null) return;
            byNetworkId[playerId] = player;
        }

        /// <summary>Resolve a Player from its network playerId, or null.</summary>
        public static Player GetByNetworkId(string playerId)
        {
            if (string.IsNullOrEmpty(playerId)) return null;
            return byNetworkId.TryGetValue(playerId, out var p) ? p : null;
        }

        public static Player GetLocalPlayer()
        {
            return allPlayers.Find(p => p.IsLocalPlayer);
        }

        public static List<Player> GetAlivePlayers()
        {
            return allPlayers.FindAll(p => !p.IsEliminated);
        }

        public static List<Player> GetWitches()
        {
            return allPlayers.FindAll(p => p.IsWitch);
        }

        public static List<Player> GetAliveWitches()
        {
            return allPlayers.FindAll(p => p.IsWitch && !p.IsEliminated);
        }

        public static List<Player> GetAliveVillagers()
        {
            return allPlayers.FindAll(p => !p.IsWitch && !p.IsEliminated);
        }

        public static void Eliminate(Player player, EliminationCause cause)
        {
            if (player == null || player.IsEliminated) return;

            player.IsEliminated = true;

            // Discard hand + status cards (or transfer to John Proctor holder)
            player.OnElimination();

            // Matchmaker cascade: if eliminated player has Matchmaker bond, eliminate partner too
            if (player.MatchedPlayer != null &&
                player.HasStatus("Matchmaker") &&
                player.MatchedPlayer.HasStatus("Matchmaker") &&
                !player.MatchedPlayer.IsEliminated)
            {
                var partner = player.MatchedPlayer;
                player.ClearMatch();
                partner.ClearMatch();
                partner.EliminateNow();
            }

            // Re-index turn order after removal
            GameTurnManager.Instance?.OnPlayerEliminated(player);

            OnPlayerEliminated?.Invoke(player, cause);
            GameManager.Instance?.EvaluateEndGame();
        }
    }
}