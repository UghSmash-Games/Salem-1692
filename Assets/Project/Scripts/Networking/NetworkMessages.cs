using System;

namespace Salem.Networking
{
    // ΓöÇΓöÇΓöÇ Internal (Engine.io handshake parsing) ΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇ

    [Serializable]
    public class EngineIOHandshake
    {
        public string sid;
        public int pingInterval;
        public int pingTimeout;
    }

    // ΓöÇΓöÇΓöÇ Received by Host ΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇ

    [Serializable]
    public class RoomCreatedMsg
    {
        public string code;
    }

    [Serializable]
    public class PlayerJoinedMsg
    {
        public string playerId;
        public string displayName;
    }

    [Serializable]
    public class PlayerLeftMsg
    {
        public string playerId;
    }

    [Serializable]
    public class PlayerActionMsg
    {
        public string playerId;
        public string card;
        public string targetPlayerId;
    }

    [Serializable]
    public class SecretPhaseSubmitMsg
    {
        public string playerId;
        public string selection;
    }

    [Serializable]
    public class ConfessMsg
    {
        public string playerId;
        public int tryalIndex;
    }

    // ΓöÇΓöÇΓöÇ Sent by Host (and PhaseResolveMsg echoed back) ΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇ

    // Public, per-player board entry. PUBLIC DATA ONLY — never tryals/role/hand.
    [Serializable]
    public class PublicPlayerMsg
    {
        public string playerId;
        public string displayName;
        public int accusations;
        public bool eliminated;
        public string[] statusCards; // public blue cards in front of the player
    }

    // Broadcast to all players + mirrors. Shape matches webclient
    // src/socket/types.ts GameStateUpdatePayload. Contains NO private data.
    [Serializable]
    public class GameStateUpdateMsg
    {
        public string phase;
        public string whoseTurn;       // network playerId of the active player
        public PublicPlayerMsg[] players;
        public int deckCount;
        public int discardCount;
    }

    // One tryal card as shown to its OWNER. Matches webclient TryalCardView.
    [Serializable]
    public class TryalViewMsg
    {
        public string label;  // "Witch" | "Not a Witch" | "Constable"
        public bool faceUp;   // true once revealed publicly
    }

    // Sent to ONE player only (routed by playerId at the server). Private data.
    [Serializable]
    public class PrivateStateMsg
    {
        public string playerId;
        public TryalViewMsg[] tryals;
        public string[] hand;
        public string role;   // "witch" | "townsperson" | "constable"
    }

    [Serializable]
    public class SecretPhasePromptEntry
    {
        public string playerId;
        public string prompt;
        public string[] targets;
        public bool acting;
    }

    [Serializable]
    public class SecretPhasePromptMsg
    {
        public SecretPhasePromptEntry[] prompts;
    }

    [Serializable]
    public class ActionRequestMsg
    {
        public string playerId;
        public string[] actions;
    }

    [Serializable]
    public class PhaseResolveMsg
    {
        public long revealAt;
    }

    [Serializable]
    public class EliminationResultMsg
    {
        public string playerId;
        public bool eliminated;
        /// <summary>
        /// Empty string means no one saved the player.
        /// JsonUtility cannot represent null strings ΓÇö use string.IsNullOrEmpty() to check.
        /// </summary>
        public string savedBy;
    }

    [Serializable]
    public class GameOverMsg
    {
        public string winner;
    }
}
