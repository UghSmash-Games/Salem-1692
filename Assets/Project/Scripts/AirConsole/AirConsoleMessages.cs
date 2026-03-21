/*
* AUTHOR: Claude Code
* REFERENCES: AirConsole Unity Plugin API
* NOTES:
*   Primary Purpose: Defines the JSON message protocol between screen and phone controllers.
*   Responsibilities:
*        • Define screen-to-controller message types (game state, hand, targets, turn)
*        • Define controller-to-screen message types (play card, select target, draw, end turn)
*   Access Requirements:
*        • AirConsoleManager
*        • AirConsoleInputHandler

* TODO: [Planned improvements]
* FIXME: [Known bugs or issues]
*/
using System;
using System.Collections.Generic;

namespace Salem.AirConsole
{
    // ──────────────────────────────────────────────
    // Controller → Screen messages
    // ──────────────────────────────────────────────

    /// <summary>
    /// Base envelope for all controller-to-screen messages.
    /// The "action" field determines which handler processes it.
    /// </summary>
    [Serializable]
    public class ControllerMessage
    {
        public string action;
    }

    [Serializable]
    public class PlayCardMessage : ControllerMessage
    {
        public int cardIndex;
        public PlayCardMessage() { action = "play_card"; }
    }

    [Serializable]
    public class SelectTargetMessage : ControllerMessage
    {
        public int targetIndex;
        public SelectTargetMessage() { action = "select_target"; }
    }

    [Serializable]
    public class DrawCardsMessage : ControllerMessage
    {
        public DrawCardsMessage() { action = "draw_cards"; }
    }

    [Serializable]
    public class EndTurnMessage : ControllerMessage
    {
        public EndTurnMessage() { action = "end_turn"; }
    }

    // ──────────────────────────────────────────────
    // Screen → Controller messages
    // ──────────────────────────────────────────────

    [Serializable]
    public class CardInfo
    {
        public int index;
        public string name;
        public string type;  // "Green", "Blue", "Red", etc.
        public bool needsTarget;
    }

    [Serializable]
    public class TargetInfo
    {
        public int index;
        public string playerName;
    }

    [Serializable]
    public class ScreenMessage
    {
        public string type;
    }

    [Serializable]
    public class HandUpdateMessage : ScreenMessage
    {
        public List<CardInfo> cards;
        public HandUpdateMessage() { type = "hand_update"; }
    }

    [Serializable]
    public class TurnNotifyMessage : ScreenMessage
    {
        public bool isYourTurn;
        public bool canDraw;
        public bool canPlay;
        public TurnNotifyMessage() { type = "turn_notify"; }
    }

    [Serializable]
    public class RequestTargetMessage : ScreenMessage
    {
        public List<TargetInfo> validTargets;
        public bool needsSecondTarget;
        public RequestTargetMessage() { type = "request_target"; }
    }

    [Serializable]
    public class EliminatedMessage : ScreenMessage
    {
        public string reason;
        public EliminatedMessage() { type = "eliminated"; }
    }

    [Serializable]
    public class GamePhaseMessage : ScreenMessage
    {
        public string phase;
        public string currentPlayerName;
        public GamePhaseMessage() { type = "game_phase"; }
    }
}
