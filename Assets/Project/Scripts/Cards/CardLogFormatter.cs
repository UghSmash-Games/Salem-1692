/*
* AUTHOR:
* REFERENCES:
* NOTES:
* TODO: [Planned improvements]
* FIXME: [Known bugs or issues]
*/

using Salem.Cards;
using Salem.Players;

public static class CardLogFormatter
{
    public static string Format(Player source, Card card, Player target)
    {
        var src = source?.PlayerNameText ?? "Unknown";
        var tgt = target?.PlayerNameText ?? "no target";
        if (string.IsNullOrEmpty(card.LogMessage))
            return $"{src} played {card.Name} on {tgt}";
        return card.LogMessage.Replace("{source}", src)
                              .Replace("{card}", card.Name)
                              .Replace("{target}", tgt);
    }
}