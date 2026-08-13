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
        public bool confirmed; // false = tentative pick, true = final
    }

    // One witch's live tentative pick, relayed to fellow witches only.
    [Serializable]
    public class WitchVoteMsg
    {
        public string witch;   // witch display name
        public string target;  // tentative target name; "" = not yet picked
    }

    [Serializable]
    public class ConfessMsg
    {
        public string playerId;
        public int tryalIndex;
    }

    // Tituba's deck rearrange — phone → host. order = a permutation of the original
    // deck indices (top→bottom). Two-stage like SecretPhaseSubmitMsg.
    [Serializable]
    public class DeckRearrangeSubmitMsg
    {
        public string playerId;
        public int[] order;
        public bool confirmed; // false = tentative in-progress order, true = final
    }

    // John Proctor / Martha card draft — phone → host. index = the chosen card's index
    // into the pool sent in CardPickRequestMsg. One pick per request (single-stage).
    [Serializable]
    public class CardPickSubmitMsg
    {
        public string playerId;
        public int index;
    }

    // Answer to a TargetRequestMsg — phone → host. Single-stage. The host re-validates the chosen id
    // against the same eligibility rule it used to build the list.
    [Serializable]
    public class TargetSubmitMsg
    {
        public string playerId;
        public string targetPlayerId;
    }

    // Answer to a ConfirmRequestMsg — phone → host. Single-stage: the answer IS the confirmation
    // (no tentative stage). The host owns the deadline and defaults to true if this never arrives.
    [Serializable]
    public class ConfirmSubmitMsg
    {
        public string playerId;
        public bool confirmed;
    }

    // ΓöÇΓöÇΓöÇ Sent by Host (and PhaseResolveMsg echoed back) ΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇ

    // Public, per-player board entry. PUBLIC DATA ONLY — never tryals/role/hand.
    [Serializable]
    public class PublicPlayerMsg
    {
        public string playerId;
        public string displayName;
        public int accusations;
        // Reveal threshold for the accusation-progress meter: base (7, or 8 George Burroughs) → ×2 with
        // Piety. Deliberately NOT Danforth-adjusted — Danforth's −1 depends on WHO accuses, unknowable
        // before an accusation, so the PUBLIC meter shows the base/piety threshold.
        public int accusationLimit;
        public bool eliminated;

        // PUBLIC BLUE/persistent cards in front of the player (+ Black Cat). Red accusation cards
        // live in `accusationCards` — they share ONE Player.StatusCards list internally, but are
        // split here so the UI never needs card-colour (a rules concept) to render them.
        public string[] statusCards;

        // PUBLIC red accusation cards in front of the player ("Accusation"/"Evidence"/"Witness").
        // NOT a new disclosure: these were already broadcast inside `statusCards` before the split.
        public string[] accusationCards;

        // The player's PRINTED Town Hall character (display name), or null/empty when none.
        // Town Hall identity is PUBLIC (cards dealt face-up, abilities read aloud at setup) — see
        // CLAUDE.md's masking section. Card IDENTITY ONLY: never charges, never ability eligibility
        // (that stays in the per-socket host-gated action_request / canFakeConfess pattern).
        // NOT Martha Corey's copied/effective name — that is a live derivation; the printed fact is
        // what's public, and the effective name is derivable client-side from this public board.
        // Goes empty at elimination (Player.OnElimination nulls the card); consumers should cache
        // the last non-empty value rather than reach for another source.
        public string townHall;

        // COUNT of tryal cards held (revealed + unrevealed). No identities. Tabletop-visible.
        // Do NOT add an `unrevealedCount` — it is (tryalTotal - revealedTryals.Length), and two
        // independently computed numbers invite one being filled from the wrong source later.
        public int tryalTotal;

        // Labels of the player's ALREADY-REVEALED tryals only ("Witch"/"Not a Witch"/"Constable"),
        // emitted in CANONICAL SORTED order — deliberately POSITION-FREE.
        //
        // ⚠ NEVER change this to a positional shape (fixed-length array with null/"" placeholders,
        // { label, index }, or a public mirror of the private TryalViewMsg's { label, faceUp }).
        // Player.AddTryalCardAndNotify APPENDS, so after a Conspiracy pass the last slot is always
        // the card just received from the left neighbour — who knows its identity from their own
        // private_state. Publishing slot positions would let them pin a known card to an exact
        // face-down slot and narrow the rest by elimination. A tabletop gives no such handle.
        // Built ONLY by NetworkStateBroadcaster.BuildRevealedTryalLabels.
        public string[] revealedTryals;

        // COUNT of cards in this player's hand. Hand SIZE is openly countable at a physical table;
        // hand CONTENTS are private and stay in PrivateStateMsg.hand.
        // ⚠ MUST remain an int. If this is ever widened to string[] the leak is total — the type is
        // the guard. Built ONLY by NetworkStateBroadcaster.BuildPublicHandCount.
        public int handCount;
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

        // Name of the TOP card of the discard pile, or null/empty when the pile is empty.
        // Public: the discard pile is face-up at a physical table.
        // ⚠ The TOP CARD ONLY — never the pile's contents or order. Publishing the ordered pile
        // would leak play history beyond what is visible at the table, and would hand Samuel Parris'
        // discard-draw pool to every client.
        public string topDiscard;
    }

    /// <summary>
    /// The CLOSED vocabulary of loggable events ("What Has Passed" on the host + mirrors).
    ///
    /// 🔴 THIS ENUM IS THE PRIVACY MECHANISM. The log carries no prose — only a kind plus public
    /// ids — and the RENDERER turns that into a sentence. Because there is no kind for a secret
    /// action, the log is structurally incapable of describing one: it cannot say "Alice voted for
    /// Bob" because no such kind exists, not because someone remembered to be careful.
    ///
    /// ⛔ NEVER add a kind for secret-phase content: night votes, constable saves, witch identities,
    /// black-cat placement, or a confession BEFORE it resolves. Every kind below corresponds to a
    /// state transition that is already public in <see cref="GameStateUpdateMsg"/> or an already-
    /// public one-shot event.
    ///
    /// ⛔ NEVER add a free-text field to <see cref="GameEventMsg"/>. The moment one exists, every
    /// call site becomes a place a secret can leak, and the guarantee above is gone.
    ///
    /// CONFESSION, specifically: `ConfessionRevealed` fires only at the synchronized revealAt, when
    /// the flip is public by the rulebook. There is deliberately no kind for the confess WINDOW, so
    /// the masked timing is preserved.
    ///
    /// GAVEL, specifically: `GavelPlaced` carries the RECIPIENT only, never the constable. The
    /// rulebook has the token placed in front of a player and THEN everyone opening their eyes
    /// (p11), so who was protected is public — but who protected them is not, and no kind may ever
    /// carry that.
    ///
    /// NO "accusation count changed" KIND, deliberately: `Player.AccusationCountChanged` fires from
    /// four sites and THREE of them are RESETS to zero (Abigail's discard, an Alibi removal, and the
    /// red-card discard after a tryal reveal). Logging it would spam "0/7" after every reveal and
    /// mislabel removals as additions. The accusation itself is already logged as `CardPlayed`, and
    /// the running total is on the seat.
    /// </summary>
    public enum GameEventKind
    {
        GameStarted,
        PhaseChanged,
        CardPlayed,
        TryalRevealed,
        PlayerEliminated,
        DoubleWitchRevealed,
        ConfessionRevealed,
        GavelPlaced,
        GameOver,
    }

    /// <summary>
    /// One entry in the public event log. Broadcast to all players + mirrors; PUBLIC DATA ONLY.
    /// Fields are ids and short enumerable labels — never sentences (see <see cref="GameEventKind"/>).
    /// </summary>
    [Serializable]
    public class GameEventMsg
    {
        // GameEventKind as a lowercase snake_case wire string, e.g. "tryal_revealed".
        public string kind;

        // PUBLIC player id of whoever acted, or null/empty when the event has no actor
        // (e.g. phase_changed). Who plays a card is public at a physical table.
        public string actorId;

        // PUBLIC player id the event happened TO, or null/empty.
        public string targetId;

        // PUBLIC card label, or null/empty. Same visibility class as statusCards.
        public string cardName;

        // A SHORT enumerable public detail whose meaning depends on `kind` — e.g. the tryal label
        // for tryal_revealed, the phase name for phase_changed, the winner for game_over.
        // ⚠ NOT a free-text field: it must always be a value from a known small vocabulary, never a
        // sentence. The renderer owns all prose.
        public string value;

        // Epoch milliseconds, stamped by the HOST at emit. Each client formats it in ITS OWN local
        // time — never send a preformatted "19:04", which would bake in the host's timezone and
        // break a mirror in another region. Same principle as PhaseResolveMsg.revealAt.
        public long atMs;
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
        public string role;          // "witch" | "townsperson" | "constable" (primary)
        // Independent role truths — a player can be BOTH (evil constable). The phone
        // shows both; `role` alone can't represent the combination.
        public bool isWitch;
        public bool isConstable;
        public string[] fellowWitches; // names of the OTHER witches; populated only
                                       // for witches once revealed at dawn, else empty.
        public WitchVoteMsg[] witchVotes; // live tentative tally of the OTHER witches
                                          // during a witch round; empty otherwise.
    }

    [Serializable]
    public class SecretPhasePromptEntry
    {
        public string playerId;
        public string prompt;
        public string[] targets;
        public bool acting;
        // Confess window only: true ONLY on the William Phipps holder's own entry (with a charge) —
        // his phone shows the "confess without revealing" button. Town Hall identity is PUBLIC, so
        // this is host-gated per-player (like Tituba/Parris action buttons), NOT a universal control.
        // Routed to that one socket via the per-player secret_phase_prompt unpack — same privacy class
        // as the `acting` flag; never broadcast.
        public bool canFakeConfess;
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
        // Card NAMES in this player's hand that cannot legally be played right now (Robbery/Scapegoat
        // with < 3 alive — rulebook p13). Computed host-side alongside `actions`; the phone greys them
        // out. The host ALSO refuses the play if a client sends one anyway (never trust the client).
        public string[] unplayableCards;
    }

    // Tituba's deck rearrange — host → ONE player only (private; never broadcast).
    // cards = the full deck's labels top→bottom; seconds = the rules window (60) the
    // phone shows as a countdown.
    [Serializable]
    public class DeckRearrangeRequestMsg
    {
        public string playerId;
        public string[] cards;
        public int seconds;
    }

    // John Proctor / Martha card draft — host → ONE drafter only (private card identities;
    // never broadcast). cards = the draft pool's labels; pickNumber/totalPicks are display
    // hints ("pick N of up to 3"); seconds = the pick window the phone shows as a countdown.
    [Serializable]
    public class CardPickRequestMsg
    {
        public string playerId;
        public string[] cards;
        public int pickNumber;
        public int totalPicks;
        public int seconds;
        // When true the picker may decline / stop early (an "up to N" pick, e.g. Samuel Parris) — the
        // phone shows a "Done" button that submits index -1. False for a mandatory pick (John's draft).
        public bool allowDone;
        // Machine code for WHAT this pick is, so the phone phrases it correctly: "proctor_draft" /
        // "parris_discard" (taking a card) vs "curse_discard" (discarding an opponent's blue card).
        public string reason;
    }

    // Ask ONE player to pick another PLAYER — the sub-target of a two-target card (Robbery's
    // recipient, Scapegoat's destination). `targets` are the eligible PUBLIC player ids; the host
    // computes eligibility (never self, never the victim, never eliminated) and RE-VERIFIES the
    // answer. The phone resolves ids → names from its public board, which avoids duplicate-name
    // ambiguity. Declining / timing out means the card is NOT played and NOT consumed.
    [Serializable]
    public class TargetRequestMsg
    {
        public string playerId;
        public string prompt;    // e.g. "robbery_recipient" / "scapegoat_recipient"
        public string[] targets; // eligible PUBLIC player ids
        public int seconds;      // countdown window
    }

    // A yes/no confirmation for a character's OWN optional ("may") choice — host → ONE player only.
    // Currently Abigail Williams' "you may discard all accusations in front of you". `prompt` is a
    // machine code the phone maps to copy; `items`/`count` are display context (her red card names
    // and her accusation total — values differ, Evidence 3 / Witness 7). NOT a masked secret phase:
    // Town Hall identity is PUBLIC, so a holder-only prompt leaks nothing (like the Tituba/Parris
    // action buttons); it is routed to one socket because it is that player's own decision UI.
    [Serializable]
    public class ConfirmRequestMsg
    {
        public string playerId;
        public string prompt;    // e.g. "abigail_discard"
        public string[] items;   // context card labels
        public int count;        // numeric context (accusation total)
        public int seconds;      // countdown window
    }

    [Serializable]
    public class PhaseResolveMsg
    {
        public long revealAt;
    }

    // Public card-show → all players + all mirrors (host originates). PUBLIC DATA ONLY —
    // card NAMES, never tryals/role/hand. e.g. Giles Corey showing two red cards to the table.
    [Serializable]
    public class PublicRevealMsg
    {
        public string playerId;   // actor's public id (PublicIdFor)
        public string[] cards;    // shown card labels, e.g. ["Evidence","Witness"]
        public string reason;     // trigger code, e.g. "giles_corey"
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
