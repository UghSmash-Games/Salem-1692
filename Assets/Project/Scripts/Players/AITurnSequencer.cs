/*
* AUTHOR: Ron Bresett
* REFERENCES:
* NOTES:
* TODO: [Planned improvements]
* FIXME: [Known bugs or issues]
*/

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Salem.Cards;
using Salem.Data;
using Salem.Deck;
using Salem.GameFlow;
using UnityEngine;

namespace Salem.Players
{
    public static class AITurnSequencer
    {
        public static IEnumerator ExecuteTurn(Player driver, DeckManager deckManager, float thinkDelay, bool forceEndTurnOnHuman)
        {
            if (driver == null)
            {
                yield break;
            }

            var turnManager = GameTurnManager.Instance;
            if (turnManager == null)
            {
                yield break;
            }

            // Reading the board. See AIPacing for why this is realtime and why the jitter is
            // multiplicative (the autopilot harness passes 0 and must stay instant).
            yield return AIPacing.Think(thinkDelay);

            var cards = driver.HandManager?.GetCards();
            if (cards == null || cards.Count == 0)
            {
                yield return DrawFallback(driver, deckManager, turnManager);
                yield break;
            }

            var actions = cards.OfType<ActionCardSO>().ToList();
            if (actions.Count == 0)
            {
                yield return DrawFallback(driver, deckManager, turnManager);
                yield break;
            }

            var chosen = actions[RNGService.Rng.NextInt(0, actions.Count)];
            if (chosen == null)
            {
                turnManager.RequestEndTurn(driver);
                yield break;
            }

            if (!turnManager.TryBeginPlayPhase(driver))
            {
                turnManager.RequestEndTurn(driver);
                yield break;
            }

            Player primary = null;
            if (chosen.RequiresTarget || (chosen is ActionCardSO actionCard && actionCard.NeedsTarget))
            {
                // Targeted by what the card DOES. Alibi, Asylum and Piety purely help whoever
                // receives them, so a random pick regularly handed them to the opponent the AI was
                // trying to beat — and a witch could just as easily accuse her own partner. See
                // AIKnowledge for what the AI is allowed to know while deciding this.
                // Scapegoat joins the gift cards here: its primary target LOSES every accusation
                // in front of them (they move to the recipient), so aiming it at an enemy was
                // doing that enemy a favour.
                bool helpsPrimary = AIKnowledge.IsBeneficialToTarget(chosen.Op)
                                    || chosen.Op == ActionOp.Scapegoat;
                primary = helpsPrimary
                    ? AIKnowledge.PickBeneficialTarget(driver)
                    : AIKnowledge.PickHarmfulTarget(driver);

                if (primary == null)
                {
                    turnManager.RequestEndTurn(driver);
                    yield break;
                }
            }

            // Two-target cards, and the two are NOT symmetric — which half helps and which half
            // hurts depends on the card:
            //   Robbery   t.TransferEntireHandTo(u)  — t is robbed, u receives the hand.
            //   Scapegoat t.TransferAllStatusesTo(u) — t is unburdened, u inherits the accusations.
            // So Robbery’s PRIMARY is the victim and its recipient is a beneficiary, while Scapegoat
            // is the other way round. Picking "harmful" for both roles — which is what this did —
            // meant the AI handed a stolen hand to an opponent, and shifted accusations off one
            // opponent onto another. Same class of self-sabotage as the beneficial-card bug.
            Player secondary = null;
            if (chosen is ActionCardSO action && action.RequiresSecondTarget)
            {
                bool recipientGains = chosen.Op == ActionOp.Robbery;   // Scapegoat: recipient suffers
                secondary = recipientGains
                    ? AIKnowledge.PickBeneficialTarget(driver)
                    : AIKnowledge.PickHarmfulTarget(driver);

                // The pick does not exclude the primary, so retry until the two differ; if they
                // still collide, bail WITHOUT playing — the card is kept, never eaten.
                int guard = 0;
                while (secondary == primary && guard++ < 4)
                {
                    secondary = recipientGains
                        ? AIKnowledge.PickBeneficialTarget(driver)
                        : AIKnowledge.PickHarmfulTarget(driver);
                }

                if (secondary == null || secondary == primary)
                {
                    turnManager.RequestEndTurn(driver);
                    yield break;
                }
            }

            if (CardEffectManager.Instance == null)
            {
                Debug.LogError("[AITurnSequencer] CardEffectManager missing; cannot execute card.");
                turnManager.RequestEndTurn(driver);
                yield break;
            }

            // Will Grigs (AI): "may choose to use alibi cards as if they were witness cards." The AI
            // has no prompt, so it takes the offensive conversion — the ability's headline use and the
            // impactful play. Human Grigs is asked via NetworkInput; local play leaves this false
            // (normal defensive Alibi). Flag is read by CardEffectManager._ops[Alibi].
            if (chosen is ActionCardSO grigsAc && grigsAc.Op == ActionOp.Alibi
                && driver.HasTownHall(Salem.Cards.TownhallName.WillGrigs))
            {
                driver.GrigsAlibiAsWitness = true;
            }

            // ExecuteCardEffect OWNS consumption: TakeCard for Red/Blue/Stocks (transferred to the
            // target's status cards) and RemoveCard for Green (sent to the discard). A rejected play
            // (e.g. the 2-player Robbery/Scapegoat disable) returns false BEFORE consuming, so the
            // card is never eaten either way.
            //
            // ⚠ Do NOT add a RemoveCard here. The deck holds many references to the SAME
            // ScriptableObject, so a second List.Remove strips ANOTHER identical card from the hand
            // and discards it — playing one Accusation while holding two cost the AI both.
            // Decided — now a shorter beat before it lands, so the turn reads as two moments
            // rather than one dead pause followed by an instant play.
            yield return AIPacing.Beat(thinkDelay);

            CardEffectManager.Instance.ExecuteCardEffect(chosen, primary, secondary);

            driver.GrigsAlibiAsWitness = false; // reset the transient mode after the play

            if (forceEndTurnOnHuman && driver.IsHuman)
            {
                turnManager.RequestEndTurn(driver);
            }
        }

        private static IEnumerator DrawFallback(Player driver, DeckManager deckManager, GameTurnManager turnManager)
        {
            yield return null;

            if (turnManager != null && turnManager.TryDrawTwoCards(driver))
            {
                yield break;
            }

            if (deckManager == null)
            {
                deckManager = Object.FindFirstObjectByType<DeckManager>();
            }

            if (deckManager != null)
            {
                deckManager.DrawMultipleCards(driver.HandManager, 2);
            }

            turnManager?.RequestEndTurn(driver);
        }
    }
}