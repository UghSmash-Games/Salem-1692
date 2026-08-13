using UnityEngine;
using Salem.Networking; // PUBLIC DTOs / host-facing public events ONLY.

// See the masking-boundary banner in HostTableView.cs: no file in this folder may reference Player /
// PlayerService / TryalCard / HandManager / StatusCards.

namespace Salem.UI.HostDisplay
{
    /// <summary>
    /// Root of the HOST (TV) display. Feeds every sub-view from the PUBLIC wire contract ONLY: it
    /// subscribes to the host-side public events (the identical payloads sent to phones/mirrors) and
    /// NEVER reads game-logic models. This is the by-construction Phase-7 masking boundary — private
    /// data has no path onto the host canvas.
    ///
    /// STAGE 7a wires the public game-state → roster. Later stages add:
    ///   7c — night/dawn overlay (from GameStateUpdateMsg.phase),
    ///   7d — reveal overlay (from NetworkManager.OnPhaseResolveSent + OnEliminationResultSent),
    ///   7e — public-reveal toast (from NetworkManager.OnPublicRevealSent).
    /// The controller already owns those subscriptions so the sub-views just get handed their DTO.
    /// </summary>
    public class HostDisplayController : MonoBehaviour
    {
        [SerializeField] private HostTableView table;    // 7b (revised: rectangular ring of seats)
        [SerializeField] private HostDeckView deck;      // 7b (center of the table)
        [SerializeField] private HostHeader header;      // 7b (room code / URL self-wire; phase fed here)
        [SerializeField] private HostEventLog eventLog;  // "What Has Passed" rail
        [SerializeField] private HostTableStats stats;   // Meeting House tallies (all derived)
        [SerializeField] private HostInEffectPanel inEffect; // persistent cards currently in play
        [SerializeField] private HostPhaseOverlay phaseOverlay; // 7c — hides the board during dawn/night
        [SerializeField] private HostRevealOverlay revealOverlay; // 7d — synchronized tryal reveal

        private void OnEnable()
        {
            NetworkStateBroadcaster.OnPublicState += HandlePublicState;
        }

        private void OnDisable()
        {
            NetworkStateBroadcaster.OnPublicState -= HandlePublicState;
        }

        private void HandlePublicState(GameStateUpdateMsg state)
        {
            if (table != null) table.Render(state);
            if (deck != null) deck.Render(state);
            if (header != null) header.Render(state);
            // The log subscribes to game_event itself; this only refreshes its id→name map so
            // entries can name the PLAYER rather than echoing a raw id.
            if (eventLog != null) eventLog.Render(state);
            if (stats != null) stats.Render(state);
            if (inEffect != null) inEffect.Render(state);
            if (phaseOverlay != null) phaseOverlay.Render(state);
            // The reveal overlay schedules off phase_resolve; this only feeds it the id→name map.
            if (revealOverlay != null) revealOverlay.Render(state);
        }
    }
}
