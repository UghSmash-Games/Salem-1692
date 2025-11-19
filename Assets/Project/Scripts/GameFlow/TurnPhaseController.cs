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
using UnityEngine;
using Salem.Data;
using Salem.Players;
using Salem.UI;

namespace Salem.GameFlow
{
    public class TurnPhaseController : MonoBehaviour
    {
        [SerializeField] private float voteWindowSeconds = 10f;
        [SerializeField] private TargetPickerUI targetPicker;

        private IRng rng;

        void Awake()
        {
            rng = new XorShiftRng((ulong)System.DateTime.UtcNow.Ticks);
        }

        public void BeginAccusationPhase(Player accuser)
        {
            // UI: pick someone (not self)
            if (accuser.IsLocalPlayer)
            {
                targetPicker.Open(accuser, false, (target, _) => StartCoroutine(VoteRoutine(target)));
            }
            else
            {
                var others = PlayerService.GetAlivePlayers().Where(p => p != accuser).ToList();
                var target = others[rng.NextInt(0, others.Count)];
                StartCoroutine(VoteRoutine(target));
            }
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