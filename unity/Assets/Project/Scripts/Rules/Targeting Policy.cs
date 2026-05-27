/*
* AUTHOR: Ron Bresett
* REFERENCES:
* NOTES:
*   Primary Purpose: Resolves logic when cards are played.
*   Responsibilities:
*        • Interpret effect type
*        • Trigger gameplay consequences
*   Access Requirements:
*        • DeckManager
*        • Player
*        • GameStateManager

* TODO: [Planned improvements]
* FIXME: [Known bugs or issues]
*/

using Salem.Cards;
using Salem.Players;

namespace Salem.Rules
{
    public static class TargetingPolicy
    {
        // If one day you add a true self-target card, just whitelist it here.
        public static bool AllowsSelf(ActionOp op) => false;

        public static bool ValidatePrimary(Player source, Player target, ActionOp op, out string reason)
        {
            if (target == null) { reason = "Target required."; return false; }
            if (source == target && !AllowsSelf(op)) { reason = "You cannot target yourself."; return false; }
            reason = null; return true;
        }

        public static bool ValidateSecondary(Player source, Player primary, Player secondary, ActionOp op, out string reason)
        {
            if (secondary == null) { reason = "Second target required."; return false; }
            if (secondary == source && !AllowsSelf(op)) { reason = "You cannot select yourself as recipient."; return false; }
            if (secondary == primary) { reason = "Recipient must be different from the victim."; return false; }
            reason = null; return true;
        }
    }
}
