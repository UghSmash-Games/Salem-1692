using System.Collections;
using Salem.Cards;
using Salem.Data;
using Salem.Players;

namespace Salem.Characters
{
    /// <summary>
    /// Minimal Town Hall character-ability convention (Phase 5 #5/#6 foundation). Abilities are looked
    /// up by identity and dispatched via <see cref="Player.GetEffectiveTownHallName"/>, so a Martha Corey
    /// copying another character automatically routes through that character's ability object.
    ///
    /// Kept deliberately small — only the hooks #5/#6 need exist. Remaining characters (Parris, the
    /// passives) migrate onto this incrementally by implementing the relevant capability interface; the
    /// dispatcher does <c>is</c>-checks, so an ability implements only the hooks it uses.
    /// </summary>
    public interface ICharacterAbility
    {
        TownhallName Name { get; }
    }

    /// <summary>Ability reacts when any player is eliminated. May run async (networked) work — the
    /// dispatcher drives the coroutine from its serialized queue. Used by John Proctor (card draft).</summary>
    public interface IOnPlayerEliminated
    {
        IEnumerator OnPlayerEliminated(Player dead, EliminationCause cause);
    }

    /// <summary>Ability gained mid-game (e.g. Martha's effective source changes to this character).</summary>
    public interface IOnAbilityInherited
    {
        void OnAbilityInherited(Player holder);
    }

    /// <summary>Ability lost mid-game (e.g. Martha's effective source changes away from this character).</summary>
    public interface IOnAbilityLost
    {
        void OnAbilityLost(Player holder);
    }
}
