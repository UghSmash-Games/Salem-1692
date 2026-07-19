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

            if (thinkDelay > 0f)
            {
                yield return new WaitForSeconds(thinkDelay);
            }

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
                primary = AITargetingHelper.SelectRandomTarget(driver);
                if (primary == null)
                {
                    turnManager.RequestEndTurn(driver);
                    yield break;
                }
            }

            // Two-target cards (Robbery/Scapegoat). SelectRandomTarget excludes self but NOT the
            // primary, so retry until the recipient differs; if it still collides, bail WITHOUT
            // playing (the card is kept, never eaten). The recipient is passed to ExecuteCardEffect
            // by parameter — it is NOT written onto action.target, which is a shared project asset.
            Player secondary = null;
            if (chosen is ActionCardSO action && action.RequiresSecondTarget)
            {
                secondary = AITargetingHelper.SelectRandomTarget(driver);
                int guard = 0;
                while (secondary == primary && guard++ < 4)
                {
                    secondary = AITargetingHelper.SelectRandomTarget(driver);
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

            // Only consume the card if the effect actually ran (e.g. the 2-player Robbery/Scapegoat
            // disable rejects it) — a rejected play must never eat the card.
            if (CardEffectManager.Instance.ExecuteCardEffect(chosen, primary, secondary))
                driver.HandManager?.RemoveCard(chosen);

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