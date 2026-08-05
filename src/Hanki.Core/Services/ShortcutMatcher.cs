using Hanki.Core.Models;
using Hanki.Core.Diagnostics;

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

    public ShortcutItem? FindExactSuffix(
        string textBeforeCaret,
        IEnumerable<ShortcutItem> shortcuts,
        DelimiterKey delimiter)
    {
        if (string.IsNullOrEmpty(textBeforeCaret))
            return null;

        var content = RemoveDelimiter(textBeforeCaret, delimiter);
        if (content is null)
            return null;

        foreach (var shortcut in shortcuts.OrderByDescending(item => item.TriggerText.Length))
        {
            if (!content.EndsWith(shortcut.TriggerText, StringComparison.Ordinal))
                continue;

            var start = content.Length - shortcut.TriggerText.Length;
            if (start == 0 || char.IsWhiteSpace(content[start - 1]))
                return shortcut;
        }

        return null;
    }

    private static string? RemoveDelimiter(string text, DelimiterKey delimiter) => delimiter switch
    {
        DelimiterKey.Space when text.EndsWith(' ') => text[..^1],
        DelimiterKey.Tab when text.EndsWith('\t') => text[..^1],
        DelimiterKey.Enter or DelimiterKey.NumpadEnter when text.EndsWith("\r\n", StringComparison.Ordinal) =>
            text[..^2],
        DelimiterKey.Enter or DelimiterKey.NumpadEnter when text.EndsWith('\r') || text.EndsWith('\n') =>
            text[..^1],
        _ => null
    };
}
