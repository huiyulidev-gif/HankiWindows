using System.Windows.Automation;

namespace Hanki.Infrastructure.Windows;

public enum BrowserSiteInspectionStatus
{
    NotBrowser,
    Available,
    AddressUnavailable,
    AccessDenied,
    InformationUnavailable
}

public sealed record BrowserSiteInspection(BrowserSiteInspectionStatus Status, string? Host);

public sealed class BrowserSiteInspector
{
    private static readonly HashSet<string> BrowserProcesses = new(
        ["chrome.exe", "msedge.exe", "firefox.exe", "brave.exe", "opera.exe", "vivaldi.exe"],
        StringComparer.OrdinalIgnoreCase);

    public BrowserSiteInspection Inspect(string? processName)
    {
        if (string.IsNullOrWhiteSpace(processName) || !BrowserProcesses.Contains(processName))
            return new(BrowserSiteInspectionStatus.NotBrowser, null);

        try
        {
            var window = GetForegroundWindow();
            if (window == IntPtr.Zero)
                return new(BrowserSiteInspectionStatus.AddressUnavailable, null);
            var root = AutomationElement.FromHandle(window);
            if (root is null)
                return new(BrowserSiteInspectionStatus.AddressUnavailable, null);

            var edits = root.FindAll(
                TreeScope.Descendants,
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit));
            foreach (AutomationElement element in edits)
            {
                var id = element.Current.AutomationId ?? string.Empty;
                var name = element.Current.Name ?? string.Empty;
                if (!LooksLikeAddressBar(id, name))
                    continue;
                if (!element.TryGetCurrentPattern(ValuePattern.Pattern, out var rawPattern) ||
                    rawPattern is not ValuePattern pattern)
                {
                    continue;
                }

                var value = pattern.Current.Value;
                if (!TryGetHost(value, out var host))
                    continue;
                return new(BrowserSiteInspectionStatus.Available, host);
            }

            return new(BrowserSiteInspectionStatus.AddressUnavailable, null);
        }
        catch (UnauthorizedAccessException)
        {
            return new(BrowserSiteInspectionStatus.AccessDenied, null);
        }
        catch (Exception exception) when (
            exception is ElementNotAvailableException or InvalidOperationException)
        {
            return new(BrowserSiteInspectionStatus.InformationUnavailable, null);
        }
    }

    private static bool LooksLikeAddressBar(string automationId, string name)
    {
        var id = automationId.ToLowerInvariant();
        if (id.Contains("address", StringComparison.Ordinal) ||
            id.Contains("omnibox", StringComparison.Ordinal) ||
            id.Contains("urlbar", StringComparison.Ordinal))
        {
            return true;
        }

        return name.Contains("address", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("주소", StringComparison.Ordinal) ||
               name.Contains("검색", StringComparison.Ordinal);
    }

    private static bool TryGetHost(string value, out string? host)
    {
        host = null;
        var candidate = value.Trim();
        if (!candidate.Contains("://", StringComparison.Ordinal))
            candidate = "https://" + candidate;
        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri) ||
            string.IsNullOrWhiteSpace(uri.Host))
        {
            return false;
        }
        host = uri.IdnHost;
        return true;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
}
