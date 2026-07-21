using System;
using System.Collections;
using System.Collections.Generic;
using Salem.Cards;

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

        /// <summary>
        /// Tituba's deck rearrange: show the chooser the full deck (top→bottom) and await a
        /// reordered permutation of the original indices. `timeoutSeconds` is the card's rules
        /// window (60) — the host owns the deadline. Two-stage like RequestSecretPhase: the
        /// phone sends tentative orders as it moves cards; the coroutine resolves on the
        /// player's confirm OR the timeout, then invokes `onOrder` once with the latest order
        /// (her in-progress arrangement), or null if nothing was submitted (caller keeps the
        /// current order). Private channel — the deck list is sent only to this player.
        /// </summary>
        IEnumerator RequestDeckRearrange(Player chooser, IReadOnlyList<Card> deck,
                                         float timeoutSeconds, Action<int[]> onOrder);

        /// <summary>
        /// John Proctor / Martha draft: show the chooser a pool of cards (an eliminated player's hand)
        /// and await ONE pick — the index into `pool` of the card they take. Called repeatedly by the
        /// draft coroutine, alternating between drafters (John first), up to 3 each. `pickNumber`
        /// (1-based) and `totalPicks` (3) are display hints ("pick N of up to 3"). NOT a masked secret
        /// phase — the draft's existence is public; only the card identities are private, so the pool
        /// is routed to this one socket (like RequestDeckRearrange). Resolves on the player's submit or
        /// the timeout; reports the chosen index, or -1 if nothing was submitted (caller safety-picks).
        /// `reason` is a machine code the phone maps to copy ("proctor_draft"/"parris_discard" = take;
        /// "curse_discard" = discard an opponent's blue card).
        /// </summary>
        IEnumerator RequestCardPick(Player chooser, IReadOnlyList<Card> pool, int pickNumber,
                                    int totalPicks, float timeoutSeconds, bool allowDone, string reason,
                                    Action<int> onIndex);

        /// <summary>
        /// A yes/no confirmation for this player's OWN optional ("may") ability choice — currently
        /// Abigail Williams' "you may discard all accusations in front of you". `promptType` is a
        /// machine code the phone maps to copy (e.g. "abigail_discard"); `contextItems` /
        /// `contextCount` are display context (her red card names + her accusation total, which
        /// differ because Evidence=3 / Witness=7). NOT a masked secret phase — Town Hall identity is
        /// PUBLIC, so a holder-only prompt leaks nothing (same class as the Tituba/Parris action
        /// buttons); it is routed to one socket because it is that player's own decision UI.
        /// Resolves on the player's answer, the host-owned timeout, or the turn ending.
        ///
        /// CONTRACT: `onConfirm` fires ONLY on a REAL answer — never on timeout / no channel. The
        /// CALLER owns the default by pre-initializing its own variable. This lets each ability pick
        /// its own no-answer behaviour: Abigail pre-inits `true` (take the beneficial discard), while
        /// Will Grigs uses a `bool?` that stays null so an unanswered prompt CANCELS the play.
        /// </summary>
        IEnumerator RequestConfirmation(Player chooser, string promptType, string[] contextItems,
                                        int contextCount, float timeoutSeconds, Action<bool> onConfirm);
    }
}
