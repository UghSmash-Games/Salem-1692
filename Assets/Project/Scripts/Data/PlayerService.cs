/*
* AUTHOR:
* REFERENCES:
* NOTES:
* TODO: [Planned improvements]
* FIXME: [Known bugs or issues]
*/

using UnityEngine;
using System.Collections.Generic;
using Salem.Players;

namespace Salem.Data
{
    public static class PlayerService
    {
        private static readonly List<Player> allPlayers = new();
        public static IReadOnlyList<Player> All => allPlayers;

        public static void Register(Player player)
        {
            if (!allPlayers.Contains(player))
            {
                allPlayers.Add(player);

                // Set first non-AI as local player automatically
                if (!player.IsLocalPlayer && !(player is AIPlayer) && GetLocalPlayer() == null)
                {
                    player.IsLocalPlayer = true;
                }
                //Debug.Log($"[PlayerService] Registered player: {player.PlayerName}");
            }
        }

        public static void Clear()
        {
            allPlayers.Clear();
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
    }
}