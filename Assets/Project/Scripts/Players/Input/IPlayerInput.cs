using System;
using System.Collections;

namespace Salem.Players
{
    /// <summary>
    /// Abstracts WHERE a human player's decisions come from — local touch UI on
    /// the host, or the network (their phone). AI players do not implement this;
    /// GameTurnManager runs them via AITurnSequencer.
    ///
    /// Both implementations drive the SAME existing game API (GameTurnManager /
    /// CardEffectManager / TableLayoutController), so the game coroutines are
    /// identical regardless of input source.
    /// </summary>
    public interface IPlayerInput
    {
        /// <summary>
        /// Run this player's Day turn: choose draw vs play(card,target) vs end,
        /// applying the choice through the existing GameTurnManager API. Yields
        /// until the turn's action has been submitted. The core Phase 4a method.
        /// </summary>
        IEnumerator RunTurn(Player player);

        /// <summary>
        /// Ask the player to pick a target Player from the valid set. Local wraps
        /// TableLayoutController.BeginTargetSelection; network sends a prompt.
        /// (Day card-plays usually bundle the target inside player_action, so this
        /// is for sub-target / future secret-phase use.)
        /// </summary>
        IEnumerator RequestTarget(Player chooser, string prompt, Func<Player, bool> isValid, Action<Player> onChosen);

        /// <summary>
        /// Ask the player to pick a tryal index on a target player. Local wraps
        /// TableLayoutController.BeginTryalSelection.
        /// </summary>
        IEnumerator RequestTryal(Player chooser, Player target, Action<int> onChosen);

        /// <summary>
        /// Deliver a masked secret-phase prompt (dawn/night) and await this player's
        /// selection. Sent to EVERY player — acting and non-acting alike — so phones
        /// look identical; the host records only acting submissions and discards the
        /// rest. Two-stage: `onSubmit` fires on EVERY submit with `confirmed=false`
        /// (tentative) or `confirmed=true` (final). The coroutine completes only on a
        /// confirmed submit. `targetNames` are display names; the callback reports the
        /// chosen name. Network: emits secret_phase_prompt, resolves from
        /// secret_phase_submit — the same channel RunTurn uses for action_request.
        /// </summary>
        IEnumerator RequestSecretPhase(Player player, string promptType, string[] targetNames,
                                       bool acting, Action<Player, string, bool> onSubmit);
    }
}
