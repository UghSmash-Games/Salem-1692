using System.Collections;
using System.Linq;
using Salem.Cards;
using Salem.Data;
using Salem.GameFlow;
using Salem.Networking;
using UnityEngine;

namespace Salem.Players
{
    /// <summary>
    /// Input from a remote phone client. Mirrors AITurnSequencer's structure, but
    /// the card + target come from `player_action` messages instead of AI logic.
    /// It drives the SAME GameTurnManager / CardEffectManager API the local UI and
    /// AI already use — no game-logic rewrite.
    ///
    /// A Day turn is a LOOP: the player either draws two (turn ends) or plays one
    /// or more cards and then ends the turn. Per the rules a turn is draw-OR-play,
    /// never both — enforced host-side here, not just hidden on the phone.
    ///
    /// Day turns only (Phase 4a). Secret-phase / tryal prompts are 4b/4c.
    /// </summary>
    public class NetworkInput : IPlayerInput
    {
        /// <summary>
        /// Diagnostic: logs the per-action turn trace (prompt / action / target /
        /// play). Off by default — flip to true to trace a stuck turn. Warnings
        /// and errors below are always logged regardless.
        /// </summary>
        public static bool VerboseLogging = false;

        private readonly Player player;
        private PlayerActionMsg pending;
        private bool hasAction;

        public NetworkInput(Player player)
        {
            this.player = player;
        }

        public IEnumerator RunTurn(Player p)
        {
            var nm = NetworkManager.Instance;
            var gtm = GameTurnManager.Instance;
            if (nm == null || gtm == null)
            {
                Debug.LogWarning("[NetworkInput] Missing NetworkManager/GameTurnManager — ending turn.");
                gtm?.RequestEndTurn(p);
                yield break;
            }

            int myTurnId = gtm.TurnId; // capture this turn's id for cancellation detection
            nm.OnPlayerAction += HandlePlayerAction;

            bool turnOver = false;
            bool hasPlayed = false;

            while (!turnOver)
            {
                hasAction = false;
                pending = null;

                // Draw-OR-play: only offer "draw" before anything has been played.
                // After a play, the only options are play-more or end.
                var actions = hasPlayed ? new[] { "play", "end" } : new[] { "draw", "play" };
                nm.SendActionRequest(new ActionRequestMsg { playerId = p.NetworkId, actions = actions });
                if (VerboseLogging)
                    Debug.Log($"[NetworkInput] {p.NetworkId} prompted with [{string.Join(",", actions)}].");

                // Wake on the player's action OR if the turn ends externally (e.g. idle timeout).
                yield return new WaitUntil(() => hasAction || gtm.TurnId != myTurnId);

                if (gtm.TurnId != myTurnId)
                {
                    Debug.Log($"[NetworkInput] {p.NetworkId}'s turn ended externally (idle timeout) — exiting loop.");
                    break;
                }

                var msg = pending;
                string card = msg?.card ?? "";
                if (VerboseLogging)
                    Debug.Log($"[NetworkInput] {p.NetworkId} action: card='{card}' target='{msg?.targetPlayerId}'");

                if (card == "draw")
                {
                    // Host-side enforcement: a draw is only valid before any play.
                    if (hasPlayed)
                    {
                        Debug.LogWarning($"[NetworkInput] {p.NetworkId} sent 'draw' after playing — ignored (draw-or-play, not both). Re-prompting.");
                    }
                    else if (gtm.TryDrawTwoCards(p)) // draws and ends the turn internally
                    {
                        turnOver = true;
                    }
                    else
                    {
                        Debug.LogWarning($"[NetworkInput] draw rejected for {p.NetworkId} — re-prompting.");
                    }
                }
                else if (card == "end")
                {
                    if (hasPlayed)
                    {
                        gtm.RequestEndTurn(p);
                        turnOver = true;
                    }
                    else
                    {
                        Debug.LogWarning($"[NetworkInput] {p.NetworkId} sent 'end' before acting — ignored (must draw or play). Re-prompting.");
                    }
                }
                else if (string.IsNullOrEmpty(card))
                {
                    Debug.LogWarning($"[NetworkInput] {p.NetworkId} sent an empty action — ignored. Re-prompting.");
                }
                else
                {
                    // Play a card. Does NOT end the turn — loop and prompt for more.
                    if (TryPlayCard(p, msg, gtm)) hasPlayed = true;
                }
            }

            nm.OnPlayerAction -= HandlePlayerAction;
        }

        private void HandlePlayerAction(PlayerActionMsg msg)
        {
            if (hasAction) return;                          // already captured this prompt
            if (msg == null || msg.playerId != player.NetworkId) return; // not this player
            pending = msg;
            hasAction = true;
            GameTurnManager.Instance?.ResetIdleTimer();     // player acted — refresh inactivity window
        }

        private bool TryPlayCard(Player p, PlayerActionMsg msg, GameTurnManager gtm)
        {
            var card = p.HandManager?.Hand?.FirstOrDefault(c => c != null && c.Name == msg.card);
            if (card == null)
            {
                Debug.LogWarning($"[NetworkInput] '{msg.card}' not in {p.NetworkId} hand " +
                    $"[{string.Join(", ", p.HandManager?.Hand?.Select(c => c?.Name) ?? new string[0])}].");
                return false;
            }

            if (!gtm.TryBeginPlayPhase(p))
            {
                Debug.LogWarning($"[NetworkInput] TryBeginPlayPhase denied for {p.NetworkId}.");
                return false;
            }

            var target = ResolveTarget(msg.targetPlayerId);
            if (target == null && !string.IsNullOrEmpty(msg.targetPlayerId))
                Debug.LogWarning($"[NetworkInput] {p.NetworkId} target '{msg.targetPlayerId}' did not resolve to any player.");
            else if (VerboseLogging)
                Debug.Log($"[NetworkInput] target '{msg.targetPlayerId}' → {(target != null ? target.PlayerNameText : "none")}");

            // Two-target cards (Robbery/Scapegoat): the phone only sends one target.
            // 4a stopgap — pick the secondary like the AI does.
            if (card is ActionCardSO ac && ac.RequiresSecondTarget)
            {
                ac.target = AITargetingHelper.SelectRandomTarget(p);
            }

            CardEffectManager.Instance.ExecuteCardEffect(card, target);
            p.HandManager?.RemoveCard(card);
            if (VerboseLogging)
                Debug.Log($"[NetworkInput] {p.NetworkId} played '{card.Name}' on " +
                    $"'{(target != null ? target.PlayerNameText : "none")}'. Hand now: {p.HandManager?.Hand?.Count}.");
            return true;
        }

        // Resolve a target by the PUBLIC id the board uses: NetworkId for humans,
        // PublicId ("ai0"…) for AI. Mirrors NetworkStateBroadcaster.PublicIdFor.
        private static Player ResolveTarget(string publicId)
        {
            if (string.IsNullOrEmpty(publicId)) return null;
            return PlayerService.All.FirstOrDefault(pl =>
                pl != null && (pl.NetworkId == publicId || pl.PublicId == publicId));
        }

        // Day turns bundle the target inside player_action, so these aren't used in
        // 4a. Provided so the interface is complete; wired for secret phases in 4b/4c.
        public IEnumerator RequestTarget(Player chooser, string prompt, System.Func<Player, bool> isValid, System.Action<Player> onChosen)
        {
            Debug.LogWarning("[NetworkInput] RequestTarget not implemented for Phase 4a.");
            onChosen?.Invoke(null);
            yield break;
        }

        public IEnumerator RequestTryal(Player chooser, Player target, System.Action<int> onChosen)
        {
            Debug.LogWarning("[NetworkInput] RequestTryal not implemented for Phase 4a.");
            onChosen?.Invoke(-1);
            yield break;
        }

        // Secret phase (dawn/night). Symmetric with RunTurn's action flow: send this
        // player's prompt (acting flag included) and await this player's submits. Sent
        // for acting AND non-acting players so phones look identical. Two-stage: report
        // every submit (tentative or confirmed) via onSubmit; complete only when a
        // CONFIRMED submit arrives. The host decides whether to record or discard.
        public IEnumerator RequestSecretPhase(Player p, string promptType, string[] targetNames,
                                              bool acting, System.Action<Player, string, bool> onSubmit)
        {
            var nm = NetworkManager.Instance;
            if (nm == null || string.IsNullOrEmpty(p.NetworkId))
            {
                onSubmit?.Invoke(p, null, true); // nothing to await — treat as a confirm
                yield break;
            }

            bool confirmed = false;

            void Handler(SecretPhaseSubmitMsg msg)
            {
                if (confirmed) return;                          // already finalized
                if (msg == null || msg.playerId != p.NetworkId) return;
                onSubmit?.Invoke(p, msg.selection, msg.confirmed);
                if (msg.confirmed) confirmed = true;
            }

            nm.OnSecretPhaseSubmit += Handler;

            nm.SendSecretPhasePrompt(new SecretPhasePromptMsg
            {
                prompts = new[]
                {
                    new SecretPhasePromptEntry
                    {
                        playerId = p.NetworkId,
                        prompt = promptType,
                        targets = targetNames,
                        acting = acting,
                    },
                },
            });

            yield return new WaitUntil(() => confirmed);

            nm.OnSecretPhaseSubmit -= Handler;
        }
    }
}
