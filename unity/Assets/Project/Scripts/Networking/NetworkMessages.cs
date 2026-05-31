using System;

namespace Salem.Networking
{
    // ─── Internal (Engine.io handshake parsing) ───────────────────

    [Serializable]
    public class EngineIOHandshake
    {
        public string sid;
        public int pingInterval;
        public int pingTimeout;
    }

    // ─── Received by Host ─────────────────────────────────────────

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

    // ─── Sent by Host (and PhaseResolveMsg echoed back) ───────────

    [Serializable]
    public class GameStateUpdateMsg
    {
        // Start minimal — expand as board state is defined.
        // Unity serializes whatever fields are here; server passes through without inspection.
        public string turn;
        public string phase;
    }

    [Serializable]
    public class PrivateStateMsg
    {
        public string playerId;
        public string[] tryals;
        public string[] hand;
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
        /// JsonUtility cannot represent null strings — use string.IsNullOrEmpty() to check.
        /// </summary>
        public string savedBy;
    }

    [Serializable]
    public class GameOverMsg
    {
        public string winner;
    }
}
