using Hanki.Core.Models;

namespace Hanki.Core.Services;

public static class ShortcutOrdering
{
    public static IOrderedEnumerable<ShortcutItem> FavoritesFirst(IEnumerable<ShortcutItem> shortcuts) =>
        shortcuts.OrderByDescending(item => item.IsFavorite)
            .ThenByDescending(item => item.UpdatedAt)
            .ThenBy(item => item.TriggerText, StringComparer.Ordinal);

    public static IOrderedEnumerable<ShortcutItem> MostRecentlyUpdated(IEnumerable<ShortcutItem> shortcuts) =>
        shortcuts.OrderByDescending(item => item.UpdatedAt)
            .ThenBy(item => item.TriggerText, StringComparer.Ordinal);
}
