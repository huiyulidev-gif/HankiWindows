using Hanki.Core.Models;

namespace Hanki.Core.Services;

public static class ShortcutUsage
{
    public static void Record(ShortcutItem shortcut, DateTimeOffset usedAt)
    {
        checked { shortcut.UsageCount++; }
        shortcut.LastUsedAt = usedAt;
        shortcut.UpdatedAt = usedAt;
    }
}
