/*
* AUTHOR: Ron Bresett
* REFERENCES:
* NOTES:
* TODO: [Planned improvements]
* FIXME: [Known bugs or issues]
*/

using System;
using System.Collections.Generic;
using Salem.Cards;
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

            // Matchmaker cascade — CAPTURE the bond BEFORE OnElimination, which discards the
            // Matchmaker status card and clears MatchedPlayer on BOTH sides
            // (ClearStatusCardsAndRecompute → RecomputeStatusFromStatusCards → ClearMatch).
            // Per rulebook p13, eliminating one matchmaker owner eliminates BOTH — even if the
            // partner was saved or confessed (EliminateNow reveals the partner regardless).
            var mmPartner = player.MatchedPlayer;
            bool mmCascades = mmPartner != null &&
                              player.HasStatus("Matchmaker") &&
                              mmPartner.HasStatus("Matchmaker") &&
                              !mmPartner.IsEliminated;

            // Discard hand + status cards (or transfer to John Proctor holder)
            player.OnElimination();

            // Cascade the partner now, using the captured bond (OnElimination has since cleared
            // the live references). EliminateNow reveals all the partner's tryals → TrialService
            // → PlayerService.Eliminate(partner) — the full elimination path (guarded against
            // re-entrancy by the IsEliminated check at the top of this method).
            if (mmCascades && !mmPartner.IsEliminated)
            {
                // #7 cascade exceptions (guards on the cascade victim = mmPartner):
                //  (a) Mary Warren survives the chain — if the PARTNER is Mary, spare her. (When Mary
                //      is the initially-eliminated `player`, she is not mmPartner, so her partner
                //      still dies — "if Mary dies, her partner still dies".)
                //  (b) both-teams-lose (GENERAL, any cascade): if eliminating the partner would satisfy
                //      BOTH win conditions at once, only the intended target dies. Mary-check first (cheap).
                bool partnerIsMary = mmPartner.HasTownHall(TownhallName.MaryWarren);
                bool bothTeamsLose = !partnerIsMary && GameManager.Instance != null &&
                                     GameManager.Instance.CascadeWouldEndBothTeams(player, mmPartner);

                player.ClearMatch();
                mmPartner.ClearMatch();

                if (partnerIsMary || bothTeamsLose)
                {
                    // Spare the partner — skip the cascade elimination. The now-partnerless Matchmaker
                    // card PERSISTS in front of them (blue cards persist per rulebook); the bond is
                    // already cleared by the ClearMatch above, leaving them free to re-link if a new
                    // Matchmaker is played. Safety against a bad state is the "can't receive a 2nd
                    // matchmaker" refusal in CardEffectManager, NOT discarding this card.
                    Debug.Log($"[Matchmaker] partner {mmPartner.PlayerNameText} SPARED " +
                              $"({(partnerIsMary ? "Mary Warren immune" : "both-teams-lose")}).");
                }
                else
                {
                    Debug.Log($"[Matchmaker] {player.PlayerNameText} eliminated — matched partner " +
                              $"{mmPartner.PlayerNameText} is also eliminated.");
                    mmPartner.EliminateNow();
                }
            }

            // Cotton Mather / Martha Corey re-resolve on elimination has MOVED to
            // CharacterAbilityDispatcher.HandleEliminated (Phase 5 #6), which subscribes to
            // OnPlayerEliminated (fired below). It now handles BOTH the accusation-count revert
            // (evidence 1→3 when Cotton dies) AND the copied charge/limit re-resolve (George's +1,
            // Tituba/Parris charges) that couldn't be expressed here. Firing from the event keeps the
            // same timing — still inside Eliminate, before EvaluateEndGame — for every elimination,
            // including the matchmaker-cascade ones.

            // Re-index turn order after removal
            GameTurnManager.Instance?.OnPlayerEliminated(player);

            OnPlayerEliminated?.Invoke(player, cause);
            GameManager.Instance?.EvaluateEndGame();
        }
    }
}