/*
* AUTHOR: Ron Bresett
* REFERENCES:
* NOTES:
* TODO: [Planned improvements]
* FIXME: [Known bugs or issues]
*/

using System.Collections.Generic;
using Salem.Players;
using UnityEngine;

namespace Salem.Data
{
    public enum Team
    {
        Villagers,
        Witches,
        Draw
    }

    public sealed class EndGameResult
{
    public Team WinningTeam { get; }
    public IReadOnlyList<Player> Winners { get; }
    public string Reason { get; }  // optional: "All witches eliminated", "Parity reached", etc.

    public EndGameResult(Team winningTeam, IReadOnlyList<Player> winners, string reason = "")
    {
        WinningTeam = winningTeam;
        Winners = winners;
        Reason = reason;
    }
}
}
