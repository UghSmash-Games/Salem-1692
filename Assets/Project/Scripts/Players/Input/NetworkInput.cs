using System.Collections;
using System.Collections.Generic;
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

        /// <summary>
        /// Window for Abigail Williams' discard confirmation. Matches the confess window (20s) and
        /// sits well under the 60s Day idle timer, so the prompt can't outlive her turn.
        /// </summary>
        private const float AbigailConfirmSeconds = 20f;

        /// <summary>
        /// Window for picking a two-target card's sub-target (Robbery's recipient / Scapegoat's
        /// destination). Under the 60s Day idle timer so the prompt can't outlive the turn.
        /// </summary>
        private const float SubTargetSeconds = 30f;

        /// <summary>Window for Will Grigs' Alibi mode choice (Witness vs normal Alibi). Under the 60s
        /// Day idle timer so it can't outlive the turn.</summary>
        private const float GrigsModeSeconds = 20f;

        /// <summary>Window for the Curse card-choice (which blue card to strip). Under the 60s Day
        /// idle timer; no answer → the op's deterministic default resolves the curse anyway.</summary>
        private const float CurseSeconds = 30f;

        /// <summary>Window for choosing WHICH face-down tryal to flip (accusation threshold,
        /// piety-loss, conspiracy step 1). Under the 60s Day idle timer so it cannot outlive the
        /// turn; no answer → a random face-down tryal turns, because the reveal is mandatory.</summary>
        private const float TryalPickSeconds = 25f;

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
                // This player just triggered a tryal reveal on someone (crossed their accusation
                // threshold, or stripped their Piety with a Curse) and the rulebook lets the TRIGGERER
                // choose which face-down card turns. Both triggers are synchronous and only flag it;
                // it resolves HERE for the same reason as Abigail below — a beat after the card
                // resolved, still this player's turn, before they are offered another action. That
                // ordering matters more here than for Abigail: the reveal feeds win conditions, so it
                // must not land after the turn has moved on.
                if (p.PendingTryalRevealTarget != null)
                {
                    var revealTarget = p.PendingTryalRevealTarget;
                    var revealReason = p.PendingTryalRevealReason;
                    p.PendingTryalRevealTarget = null;              // consume once
                    p.PendingTryalRevealReason = null;

                    int chosenIndex = -1;
                    // abortOnTurnChange: TRUE — this one IS owned by the current turn, so a
                    // force-ended turn should cancel the wait (the reveal still resolves at random).
                    yield return RequestTryal(p, revealTarget, revealReason,
                                              TimerSettings.Scale(TryalPickSeconds), true, idx => chosenIndex = idx);

                    // -1 only when they hold no face-down tryal (already fully revealed) — nothing to
                    // flip. Otherwise RequestTryal guarantees a real index, random on no answer.
                    if (chosenIndex >= 0)
                    {
                        // AWAITED, not fire-and-forget: this is the common human path and we are
                        // already in a coroutine, so the turn holds for the shared beat instead of
                        // racing ahead of a reveal that feeds win conditions.
                        var gpm = GamePhaseManager.Instance;
                        if (gpm != null)
                            yield return gpm.RevealTryalSynchronizedRoutine(revealTarget, chosenIndex);
                        else
                            revealTarget.RevealTryalCard(chosenIndex, fromAccusation: true);
                    }
                }

                // Abigail Williams: she just placed a threshold-crossing accusation and owes a
                // "may I discard my accusations?" decision. CheckAccusations is synchronous so it
                // only flags it; we resolve it HERE — a beat after the card resolved, still her turn,
                // before she's offered another action. Resolving it inside her turn (rather than on a
                // detached coroutine) matters: a late answer could otherwise wipe accusations played
                // onto her after her turn ended.
                if (p.PendingAbigailDiscardChoice)
                {
                    p.PendingAbigailDiscardChoice = false;   // consume once
                    // Pre-initialized default: RequestConfirmation fires its callback ONLY on a real
                    // answer, so if she times out / can't be reached this stays true → clears.
                    bool clearAccusations = true;
                    var reds = p.StatusCards
                        .Where(c => c != null && c.Type == Card.CardColor.Red)
                        .Select(c => c.Name)
                        .ToArray();
                    yield return RequestConfirmation(p, "abigail_discard", reds,
                        p.currentAccusationCount, TimerSettings.Scale(AbigailConfirmSeconds), v => clearAccusations = v);

                    if (clearAccusations)
                    {
                        p.ResetAccusationCount();
                        Debug.Log($"[TownHall] Abigail Williams ({p.PlayerNameText}) chose to discard her accusations.");
                    }
                    else
                    {
                        Debug.Log($"[TownHall] Abigail Williams ({p.PlayerNameText}) chose to KEEP her accusations.");
                    }
                }

                hasAction = false;
                pending = null;

                // Draw-OR-play: only offer "draw" before anything has been played.
                // After a play, the only options are play-more or end. Tituba may rearrange
                // the deck BEFORE drawing (once/game) — offered only on the first choice and
                // only with a charge; AI seats are run by AITurnSequencer and never see this.
                bool canTituba = !hasPlayed &&
                    p.HasTownHall(Salem.Cards.TownhallName.Tituba) && p.townHallAbilityCharges > 0;
                // Samuel Parris — pick up to 2 from the discard pile INSTEAD of drawing. A turn-ENDING
                // option in the same tier as "draw" (unlike Tituba's pre-turn loop-back). Same gate.
                bool canParris = !hasPlayed &&
                    p.HasTownHall(Salem.Cards.TownhallName.SamuelParris) && p.townHallAbilityCharges > 0;
                string[] actions;
                if (hasPlayed)
                {
                    actions = new[] { "play", "end" };
                }
                else
                {
                    var choice = new List<string>();
                    if (canTituba) choice.Add("tituba");
                    if (canParris) choice.Add("parris");
                    choice.Add("draw");
                    choice.Add("play");
                    actions = choice.ToArray();
                }
                // Cards in hand that can't legally be played right now (Robbery/Scapegoat need 3+
                // alive — rulebook p13). Computed here alongside `actions`, same host-gated
                // eligibility pattern as the Tituba/Parris buttons; the phone greys them out. The
                // host ALSO refuses the play in ExecuteCardEffect — never trust the client.
                int aliveNow = PlayerService.GetAlivePlayers().Count;
                var unplayable = (p.HandManager?.Hand ?? new List<Card>())
                    .OfType<ActionCardSO>()
                    .Where(c => !Salem.Rules.TargetingPolicy.ValidatePlayable(c.Op, aliveNow, out _))
                    .Select(c => c.Name)
                    .Distinct()
                    .ToArray();

                nm.SendActionRequest(new ActionRequestMsg
                {
                    playerId = p.NetworkId,
                    actions = actions,
                    unplayableCards = unplayable,
                });
                if (VerboseLogging)
                    Debug.Log($"[NetworkInput] {p.NetworkId} prompted with [{string.Join(",", actions)}]" +
                        (unplayable.Length > 0 ? $" (unplayable: [{string.Join(",", unplayable)}])" : ""));

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
                else if (card == "tituba")
                {
                    // Tituba rearrange — runs the deck-reorder input, then loops back so she
                    // still draws/plays this same turn. Re-checked host-side (not just on the
                    // phone). Does NOT end the turn.
                    if (!hasPlayed && p.HasTownHall(Salem.Cards.TownhallName.Tituba) &&
                        p.townHallAbilityCharges > 0)
                    {
                        yield return gtm.RunTitubaRearrange(p);
                    }
                    else
                    {
                        Debug.LogWarning($"[NetworkInput] {p.NetworkId} sent 'tituba' when ineligible — ignored. Re-prompting.");
                    }
                }
                else if (card == "parris")
                {
                    // Samuel Parris discard-pick — pick up to 2 (no black cards), then the turn ENDS
                    // (RunParrisDiscardPick consumes the charge + EndTurn internally, like TryDrawFromDiscard).
                    // Unlike "tituba", this does NOT loop back — set turnOver like "draw" does.
                    if (!hasPlayed && p.HasTownHall(Salem.Cards.TownhallName.SamuelParris) &&
                        p.townHallAbilityCharges > 0)
                    {
                        yield return gtm.RunParrisDiscardPick(p);
                        turnOver = true;
                    }
                    else
                    {
                        Debug.LogWarning($"[NetworkInput] {p.NetworkId} sent 'parris' when ineligible — ignored. Re-prompting.");
                    }
                }
                else if (string.IsNullOrEmpty(card))
                {
                    Debug.LogWarning($"[NetworkInput] {p.NetworkId} sent an empty action — ignored. Re-prompting.");
                }
                else
                {
                    // Play a card. Does NOT end the turn — loop and prompt for more. A coroutine
                    // because two-target cards pause to ask the player who receives the cards.
                    bool played = false;
                    yield return PlayCardRoutine(p, msg, gtm, v => played = v);
                    if (played) hasPlayed = true;
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

        /// <summary>
        /// Play one card for a networked player. A coroutine (not a bool method) because two-target
        /// cards must PAUSE to ask the player who receives the cards.
        ///
        /// Reports played=true only if the effect actually ran. A rejected play NEVER consumes the
        /// card — that was the Robbery bug: the recipient was auto-picked at random, could equal the
        /// victim, `ExecuteCardEffect` bailed on validation, and the card was discarded anyway.
        /// </summary>
        private IEnumerator PlayCardRoutine(Player p, PlayerActionMsg msg, GameTurnManager gtm,
                                            System.Action<bool> onPlayed)
        {
            var card = p.HandManager?.Hand?.FirstOrDefault(c => c != null && c.Name == msg.card);
            if (card == null)
            {
                Debug.LogWarning($"[NetworkInput] '{msg.card}' not in {p.NetworkId} hand " +
                    $"[{string.Join(", ", p.HandManager?.Hand?.Select(c => c?.Name) ?? new string[0])}].");
                onPlayed?.Invoke(false);
                yield break;
            }

            if (!gtm.TryBeginPlayPhase(p))
            {
                Debug.LogWarning($"[NetworkInput] TryBeginPlayPhase denied for {p.NetworkId}.");
                onPlayed?.Invoke(false);
                yield break;
            }

            var target = ResolveTarget(msg.targetPlayerId);
            if (target == null && !string.IsNullOrEmpty(msg.targetPlayerId))
                Debug.LogWarning($"[NetworkInput] {p.NetworkId} target '{msg.targetPlayerId}' did not resolve to any player.");
            else if (VerboseLogging)
                Debug.Log($"[NetworkInput] target '{msg.targetPlayerId}' → {(target != null ? target.PlayerNameText : "none")}");

            // Will Grigs "may choose to use alibi cards as if they were witness cards." Target-first:
            // he already picked the target above; now ask the MODE (Witness offense vs normal Alibi).
            // No answer (timeout/decline) → CANCEL the play and keep the card (Witness is an opt-in).
            if (card is ActionCardSO alibiAc && alibiAc.Op == ActionOp.Alibi
                && p.HasTownHall(Salem.Cards.TownhallName.WillGrigs) && target != null)
            {
                bool? mode = null; // stays null if unanswered (RequestConfirmation fires only on answer)
                yield return RequestConfirmation(p, "grigs_alibi_mode",
                    System.Array.Empty<string>(), 0, TimerSettings.Scale(GrigsModeSeconds), v => mode = v);

                if (mode == null)
                {
                    Debug.Log($"[NetworkInput] {p.NetworkId} Alibi mode not chosen — not played (card kept).");
                    onPlayed?.Invoke(false);
                    yield break;
                }
                p.GrigsAlibiAsWitness = mode.Value; // true = Witness (+7), false = normal Alibi
            }

            // Two-target cards (Robbery/Scapegoat): the player CHOOSES the recipient. Eligible =
            // anyone alive who is neither the player nor the victim. The choice is passed to
            // ExecuteCardEffect by parameter — never written onto the shared card asset.
            Player secondary = null;
            if (card is ActionCardSO ac && ac.RequiresSecondTarget)
            {
                var primary = target;
                bool IsEligible(Player x) => x != null && x != p && x != primary && !x.IsEliminated;

                if (!PlayerService.All.Any(IsEligible))
                {
                    // e.g. only 2 players alive — nobody can receive. Don't play, don't consume.
                    Debug.LogWarning($"[NetworkInput] {msg.card}: no eligible recipient — not played (card kept).");
                    onPlayed?.Invoke(false);
                    yield break;
                }

                string promptCode = ac.Op == ActionOp.Robbery ? "robbery_recipient" : "scapegoat_recipient";
                yield return RequestTarget(p, promptCode, IsEligible, chosen => secondary = chosen);

                if (secondary == null)
                {
                    // Declined or timed out — do NOT play and do NOT consume the card.
                    Debug.Log($"[NetworkInput] {msg.card}: no recipient chosen — not played (card kept).");
                    onPlayed?.Invoke(false);
                    yield break;
                }
            }

            // Curse: the playing player CHOOSES which blue card to strip from the victim (the Black Cat
            // is one option, not forced first). Sub-prompt via the existing card_pick event; the chosen
            // card is threaded to _ops[Curse] on CurseChosenBlueCard. Only prompt when there is a real
            // choice (>1 blue); 0 or 1 → the op handles it (no prompt). No answer / timeout → the op's
            // deterministic default is used, so a curse on a valid target still resolves.
            if (card is ActionCardSO curseAc && curseAc.Op == ActionOp.Curse && target != null)
            {
                var pool = target.StatusCards.Where(sc => sc.Type == Card.CardColor.Blue).ToList();
                if (pool.Count > 1)
                {
                    int chosenIdx = -1;
                    yield return RequestCardPick(p, pool, 1, 1, TimerSettings.Scale(CurseSeconds), false, "curse_discard", i => chosenIdx = i);
                    if (chosenIdx >= 0 && chosenIdx < pool.Count)
                        p.CurseChosenBlueCard = pool[chosenIdx];
                }
            }

            // Consume ONLY if the effect ran (validation/2-player disable can still refuse).
            bool executed = CardEffectManager.Instance.ExecuteCardEffect(card, target, secondary);
            p.GrigsAlibiAsWitness = false; // reset the transient Grigs mode after the play (read in _ops[Alibi])
            p.CurseChosenBlueCard = null;  // reset the transient Curse choice after the play (read in _ops[Curse])
            if (executed)
            {
                // ⚠ DO NOT remove the card from hand here. ExecuteCardEffect ALREADY consumed it —
                // TakeCard for Red/Blue/Stocks (transferred to the target's status cards) and
                // RemoveCard for Green (sent to the discard). A refusal returns false BEFORE any of
                // that, so there is nothing to clean up either way.
                //
                // Removing again ATE A SECOND CARD: the deck holds many references to the SAME
                // ScriptableObject (one Accusation.asset, referenced repeatedly), so List.Remove
                // matches by reference and strips another identical card from the hand — and
                // discards it. Playing one Accusation while holding two cost the player both, and
                // the persistent card wrongly appeared on top of the discard pile.
                if (VerboseLogging)
                    Debug.Log($"[NetworkInput] {p.NetworkId} played '{card.Name}' on " +
                        $"'{(target != null ? target.PlayerNameText : "none")}'. Hand now: {p.HandManager?.Hand?.Count}.");
            }
            else
            {
                Debug.LogWarning($"[NetworkInput] '{card.Name}' was refused — card kept in hand.");
            }

            onPlayed?.Invoke(executed);
        }

        // The PUBLIC id the board uses for a player: NetworkId for humans, PublicId ("ai0"…) for AI.
        // Inverse of ResolveTarget; mirrors NetworkStateBroadcaster.PublicIdFor.
        private static string PublicIdOf(Player p)
            => p == null ? "" : (!string.IsNullOrEmpty(p.NetworkId) ? p.NetworkId : (p.PublicId ?? ""));

        // Resolve a target by the PUBLIC id the board uses: NetworkId for humans,
        // PublicId ("ai0"…) for AI. Mirrors NetworkStateBroadcaster.PublicIdFor.
        private static Player ResolveTarget(string publicId)
        {
            if (string.IsNullOrEmpty(publicId)) return null;
            return PlayerService.All.FirstOrDefault(pl =>
                pl != null && (pl.NetworkId == publicId || pl.PublicId == publicId));
        }

        /// <summary>
        /// Ask this player to pick another PLAYER — the sub-target of a two-target card (Robbery's
        /// recipient, Scapegoat's destination). The host builds the eligible list by running `isValid`
        /// over every player, sends their PUBLIC ids, and RE-VERIFIES the answer against the same
        /// predicate (never trust the client). Turn-bound like RequestDeckRearrange: resolves on the
        /// submit, the host-owned deadline, or a TurnId change.
        ///
        /// Reports NULL when nothing valid was chosen (no eligible players, decline, or timeout) — the
        /// caller must then NOT play the card and NOT consume it.
        /// </summary>
        public IEnumerator RequestTarget(Player chooser, string prompt, System.Func<Player, bool> isValid, System.Action<Player> onChosen)
        {
            var nm = NetworkManager.Instance;
            if (nm == null || string.IsNullOrEmpty(chooser.NetworkId) || isValid == null)
            {
                onChosen?.Invoke(null);
                yield break;
            }

            var eligible = PlayerService.All.Where(pl => pl != null && isValid(pl)).ToList();
            if (eligible.Count == 0)
            {
                Debug.LogWarning($"[NetworkInput] RequestTarget '{prompt}': no eligible targets.");
                onChosen?.Invoke(null);
                yield break;
            }

            Player chosen = null;
            bool answered = false;

            void Handler(TargetSubmitMsg msg)
            {
                if (answered) return;
                if (msg == null || msg.playerId != chooser.NetworkId) return;
                var pick = ResolveTarget(msg.targetPlayerId);
                // Re-verify host-side: the client may only pick from the eligible set.
                if (pick == null || !isValid(pick))
                {
                    Debug.LogWarning($"[NetworkInput] RequestTarget '{prompt}': rejected ineligible pick '{msg.targetPlayerId}'.");
                    return; // ignore and keep waiting
                }
                chosen = pick;
                answered = true;
                GameTurnManager.Instance?.ResetIdleTimer();
            }

            nm.OnTargetSubmit += Handler;

            nm.SendTargetRequest(new TargetRequestMsg
            {
                playerId = chooser.NetworkId,
                prompt = prompt,
                targets = eligible.Select(PublicIdOf).ToArray(),
                seconds = Mathf.Max(1, Mathf.RoundToInt(TimerSettings.Scale(SubTargetSeconds))),
            });

            var gtm = GameTurnManager.Instance;
            int myTurnId = gtm != null ? gtm.TurnId : 0;
            float deadline = Time.realtimeSinceStartup + TimerSettings.Scale(SubTargetSeconds);

            yield return new WaitUntil(() =>
                answered ||
                Time.realtimeSinceStartup >= deadline ||
                (gtm != null && gtm.TurnId != myTurnId));

            nm.OnTargetSubmit -= Handler;

            onChosen?.Invoke(chosen); // null on timeout → caller does NOT play/consume the card
        }

        /// <summary>
        /// "Which face-down tryal do you flip?" — the ONE implementation shared by all three reveal
        /// paths (accusation threshold, piety-loss, conspiracy step 1). Before this existed each
        /// path fell back to a random pick for networked players, silently dropping a choice the
        /// rulebook gives them.
        ///
        /// 🔴 THE CHOOSER PICKS BLIND. Only a COUNT of face-down tryals goes out, and the answer is
        /// an ORDINAL into that face-down subset — the mapping ordinal → real TryalCards index is
        /// built and kept HERE, host-side. No label and no real slot position ever crosses the wire,
        /// so this cannot become a card-identity leak by a later edit. (Real indices would matter:
        /// tryals are APPENDED on receipt, so a Conspiracy giver could otherwise pin a card they
        /// just passed to an exact slot.)
        ///
        /// NEVER RETURNS -1 WHEN A FACE-DOWN TRYAL EXISTS. The reveal is a mandatory consequence, so
        /// a timeout substitutes a random face-down tryal rather than cancelling — unlike
        /// RequestTarget, where a no-answer legitimately means "don't play the card".
        /// </summary>
        public IEnumerator RequestTryal(Player chooser, Player target, string reason,
                                        float timeoutSeconds, bool abortOnTurnChange,
                                        System.Action<int> onChosen)
        {
            if (target == null) { onChosen?.Invoke(-1); yield break; }

            // The face-down subset, in TryalCards order. Its POSITIONS never leave this method.
            var faceDown = new List<int>();
            for (int i = 0; i < target.TryalCards.Count; i++)
                if (target.TryalCards[i] != null && !target.TryalCards[i].IsRevealed) faceDown.Add(i);

            if (faceDown.Count == 0) { onChosen?.Invoke(-1); yield break; }

            var rng = chooser?.Rng ?? target.Rng;
            int RandomPick() => faceDown[rng.NextInt(0, faceDown.Count)];

            var nm = NetworkManager.Instance;
            if (nm == null || chooser == null || string.IsNullOrEmpty(chooser.NetworkId))
            {
                onChosen?.Invoke(RandomPick());
                yield break;
            }

            // A single face-down card is not a choice — don't stall the turn asking for it.
            if (faceDown.Count == 1) { onChosen?.Invoke(faceDown[0]); yield break; }

            int ordinal = -1;
            bool answered = false;

            void Handler(TryalPickSubmitMsg msg)
            {
                if (answered) return;
                if (msg == null || msg.playerId != chooser.NetworkId) return;
                // Re-validate the range host-side — the client is never trusted.
                if (msg.ordinal < 0 || msg.ordinal >= faceDown.Count)
                {
                    Debug.LogWarning($"[NetworkInput] RequestTryal '{reason}': rejected out-of-range ordinal {msg.ordinal}.");
                    return; // ignore and keep waiting
                }
                ordinal = msg.ordinal;
                answered = true;
                if (abortOnTurnChange) GameTurnManager.Instance?.ResetIdleTimer();
            }

            nm.OnTryalPickSubmit += Handler;

            nm.SendTryalPickRequest(new TryalPickRequestMsg
            {
                playerId = chooser.NetworkId,
                targetPlayerId = PublicIdOf(target),
                count = faceDown.Count,
                seconds = Mathf.Max(1, Mathf.RoundToInt(timeoutSeconds)),
                reason = reason,
            });

            var gtm = GameTurnManager.Instance;
            int myTurnId = gtm != null ? gtm.TurnId : 0;
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;

            // ⚠ The TurnId escape is OPT-IN. Conspiracy prompts run on a DETACHED coroutine after the
            // drawer's turn has already ended (a Draw-2 ends it immediately), so leaving the guard on
            // would abort them within a frame and fall back to random — silently defeating the very
            // choice this seam exists to restore.
            yield return new WaitUntil(() =>
                answered ||
                Time.realtimeSinceStartup >= deadline ||
                (abortOnTurnChange && gtm != null && gtm.TurnId != myTurnId));

            nm.OnTryalPickSubmit -= Handler;

            // No answer (timeout, or the turn was force-ended under us) → random. The flip happens
            // either way; "no response" must not let a player dodge a reveal they triggered.
            onChosen?.Invoke(answered ? faceDown[ordinal] : RandomPick());
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

            // Confess window: the "confess without revealing" button is offered ONLY to a William
            // Phipps with a charge (host-gated per-player — Town Hall identity is public, like the
            // Tituba/Parris action buttons). Routed to this one socket, never broadcast.
            bool canFakeConfess = promptType == "confess" &&
                p.HasTownHall(Salem.Cards.TownhallName.WilliamsPhipps) &&
                p.townHallAbilityCharges > 0;

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
                        canFakeConfess = canFakeConfess,
                    },
                },
            });

            yield return new WaitUntil(() => confirmed);

            nm.OnSecretPhaseSubmit -= Handler;
        }

        // Tituba's deck rearrange. Send the full deck (top→bottom) to this player's phone and
        // await a reordered permutation. Two-stage like RequestSecretPhase: record the LATEST
        // order on every submit (tentative or confirmed); resolve on a CONFIRMED submit OR the
        // host-owned timeout — then commit her latest in-progress order. The host owns the
        // deadline (realtime); the phone shows the same `seconds` as a countdown. TurnId is
        // captured so a force-ended turn unblocks the wait.
        public IEnumerator RequestDeckRearrange(Player p, IReadOnlyList<Card> deck,
                                                float timeoutSeconds, System.Action<int[]> onOrder)
        {
            var nm = NetworkManager.Instance;
            if (nm == null || string.IsNullOrEmpty(p.NetworkId))
            {
                onOrder?.Invoke(null); // nothing to await — caller keeps the current order
                yield break;
            }

            int[] latestOrder = null;
            bool confirmed = false;

            void Handler(DeckRearrangeSubmitMsg msg)
            {
                if (confirmed) return;
                if (msg == null || msg.playerId != p.NetworkId) return;
                latestOrder = msg.order;             // keep her latest in-progress arrangement
                if (msg.confirmed) confirmed = true;
            }

            nm.OnDeckRearrangeSubmit += Handler;

            nm.SendDeckRearrangeRequest(new DeckRearrangeRequestMsg
            {
                playerId = p.NetworkId,
                cards = deck.Select(c => c != null ? c.Name : "").ToArray(),
                seconds = Mathf.Max(1, Mathf.RoundToInt(timeoutSeconds)),
            });

            var gtm = GameTurnManager.Instance;
            int myTurnId = gtm != null ? gtm.TurnId : 0;
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;

            // Resolve on confirm, the host-owned deadline, or the turn being force-ended.
            yield return new WaitUntil(() =>
                confirmed ||
                Time.realtimeSinceStartup >= deadline ||
                (gtm != null && gtm.TurnId != myTurnId));

            nm.OnDeckRearrangeSubmit -= Handler;

            // Commit the latest order she sent (her in-progress work at the deadline);
            // null if she never moved anything → caller keeps the current order.
            onOrder?.Invoke(latestOrder);
        }

        public IEnumerator RequestCardPick(Player p, IReadOnlyList<Card> pool, int pickNumber,
                                           int totalPicks, float timeoutSeconds, bool allowDone, string reason,
                                           System.Action<int> onIndex)
        {
            var nm = NetworkManager.Instance;
            if (nm == null || string.IsNullOrEmpty(p.NetworkId) || pool == null || pool.Count == 0)
            {
                onIndex?.Invoke(-1); // no network / nothing to pick — caller safety-picks
                yield break;
            }

            int chosen = -1;
            bool done = false;

            void Handler(CardPickSubmitMsg msg)
            {
                if (done) return;
                if (msg == null || msg.playerId != p.NetworkId) return;
                // -1 is the explicit "Done / decline" skip sentinel (for "up to N" pickers like Parris,
                // whose request sets allowDone). Any other out-of-range index is ignored.
                if (msg.index != -1 && (msg.index < 0 || msg.index >= pool.Count)) return;
                chosen = msg.index;
                done = true;
            }

            nm.OnCardPickSubmit += Handler;

            nm.SendCardPickRequest(new CardPickRequestMsg
            {
                playerId = p.NetworkId,
                cards = pool.Select(c => c != null ? c.Name : "").ToArray(),
                pickNumber = pickNumber,
                totalPicks = totalPicks,
                seconds = Mathf.Max(1, Mathf.RoundToInt(timeoutSeconds)),
                allowDone = allowDone,
                reason = reason,
            });

            // Resolve on the player's submit or the host-owned deadline. Unlike RequestDeckRearrange,
            // the draft is NOT tied to the drafter's turn, so we do NOT cancel on TurnId change (a turn
            // advancing elsewhere must not abort an in-flight draft).
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            yield return new WaitUntil(() => done || Time.realtimeSinceStartup >= deadline);

            nm.OnCardPickSubmit -= Handler;

            onIndex?.Invoke(chosen); // -1 on timeout → caller safety-picks
        }

        // A yes/no confirmation for this player's own optional ("may") choice (Abigail's discard,
        // Will Grigs' Alibi mode). Turn-bound like RequestDeckRearrange: resolves on the answer, the
        // host-owned deadline, OR a TurnId change (a force-ended turn must not hang this).
        //
        // CONTRACT: `onConfirm` fires ONLY on a REAL answer — never on timeout / no-network. The CALLER
        // owns the default by pre-initializing its own variable:
        //   • Abigail pre-inits `clearAccusations = true`, so no-answer → stays true → clears (unchanged).
        //   • Grigs uses a `bool?` that stays null on no-answer → cancels the play (keeps the card).
        public IEnumerator RequestConfirmation(Player p, string promptType, string[] contextItems,
                                               int contextCount, float timeoutSeconds, System.Action<bool> onConfirm)
        {
            var nm = NetworkManager.Instance;
            if (nm == null || string.IsNullOrEmpty(p.NetworkId))
            {
                // No channel to ask — do NOT fire; caller keeps its pre-initialized default.
                yield break;
            }

            bool answered = false;
            bool result = false;

            void Handler(ConfirmSubmitMsg msg)
            {
                if (answered) return;
                if (msg == null || msg.playerId != p.NetworkId) return;
                result = msg.confirmed;
                answered = true;
                GameTurnManager.Instance?.ResetIdleTimer(); // player acted — refresh inactivity window
            }

            nm.OnConfirmSubmit += Handler;

            nm.SendConfirmRequest(new ConfirmRequestMsg
            {
                playerId = p.NetworkId,
                prompt = promptType,
                items = contextItems ?? new string[0],
                count = contextCount,
                seconds = Mathf.Max(1, Mathf.RoundToInt(timeoutSeconds)),
            });

            var gtm = GameTurnManager.Instance;
            int myTurnId = gtm != null ? gtm.TurnId : 0;
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;

            yield return new WaitUntil(() =>
                answered ||
                Time.realtimeSinceStartup >= deadline ||
                (gtm != null && gtm.TurnId != myTurnId));

            nm.OnConfirmSubmit -= Handler;

            if (answered) onConfirm?.Invoke(result); // fire ONLY on a real answer (see CONTRACT above)
        }
    }
}
