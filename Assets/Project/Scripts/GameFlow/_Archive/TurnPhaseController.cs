/*
* AUTHOR: Ron Bresett
* REFERENCES:
* NOTES:
*   Primary Purpose: Handles sub-steps of a turn (accusation, defense, trial).
*   Responsibilities:
*        • Run logic based on sub-phase
*        • Trigger step-by-step UI or input windows
*   Access Requirements:
*        • GameTurnManager
*        • UIManager

* TODO: 
*   • Start simple; expand with Witch-only actions, Confession, etc.
* FIXME: [Known bugs or issues]
*/
using System.Collections;
using System.Linq;
using Salem.Data;
using Salem.Players;
using Salem.UI;
using UnityEngine;

namespace Salem.GameFlow
{
    public class TurnPhaseController : MonoBehaviour
    {
        [SerializeField] private float voteWindowSeconds = 10f;

        private IRng rng;

        void Awake()
        {
            rng = new XorShiftRng((ulong)System.DateTime.UtcNow.Ticks);
        }

        private IEnumerator VoteRoutine(Player accused)
        {
            // TODO: collect votes; PoC — simple bandwagon to accused
            yield return new WaitForSeconds(voteWindowSeconds);

            // Reveal one Tryal on accused
            var idx = accused.TryalCards.FindIndex(tc => !tc.IsRevealed);
            if (idx >= 0) accused.RevealTryalCard(idx);

            // End/continue handled by GameTurnManager
        }
    }
}