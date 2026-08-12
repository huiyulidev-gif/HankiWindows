using Hanki.Core.Diagnostics;
using Hanki.Core.Models;

namespace Hanki.Core.Services;

public static class ExpansionPolicy
{
    public static ExpansionBlockReason GetInitialBlockReason(
        AppSettings settings,
        int shortcutCount,
        DelimiterKey delimiter)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (!settings.IsEnabled)
            return ExpansionBlockReason.FeatureDisabled;
        if (settings.IsPaused)
            return ExpansionBlockReason.Paused;
        if (shortcutCount <= 0)
            return ExpansionBlockReason.NoShortcuts;
        var delimiterEnabled = delimiter switch
        {
            DelimiterKey.Space => settings.SpaceExpansionEnabled,
            DelimiterKey.Enter or DelimiterKey.NumpadEnter => settings.EnterExpansionEnabled,
            DelimiterKey.Tab => settings.TabExpansionEnabled,
            _ => false
        };
        return delimiterEnabled ? ExpansionBlockReason.None : ExpansionBlockReason.DelimiterDisabled;
    }
}
