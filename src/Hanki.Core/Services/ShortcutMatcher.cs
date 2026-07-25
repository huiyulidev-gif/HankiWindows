using Hanki.Core.Models;

namespace Hanki.Core.Services;

public sealed class ShortcutMatcher
{
    public ShortcutItem? FindExactSuffix(
        string textBeforeCaret,
        IEnumerable<ShortcutItem> shortcuts,
        char terminator = ' ')
    {
        if (string.IsNullOrEmpty(textBeforeCaret) || textBeforeCaret[^1] != terminator)
            return null;

        foreach (var shortcut in shortcuts.OrderByDescending(item => item.TriggerText.Length))
        {
            var suffix = shortcut.TriggerText + terminator;
            if (!textBeforeCaret.EndsWith(suffix, StringComparison.Ordinal))
                continue;

            var start = textBeforeCaret.Length - suffix.Length;
            if (start == 0 || char.IsWhiteSpace(textBeforeCaret[start - 1]))
                return shortcut;
        }

        return null;
    }
}
