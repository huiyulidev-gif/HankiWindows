namespace Hanki.Core.Services;

public sealed class BrowserSitePolicy(IEnumerable<string> excludedSites)
{
    private readonly HashSet<string> _excluded = new(
        excludedSites
            .Select(NormalizeHost)
            .Where(host => host is not null)
            .Cast<string>(),
        StringComparer.OrdinalIgnoreCase);

    public bool HasRules => _excluded.Count > 0;

    public bool IsExcluded(string? host)
    {
        var normalized = NormalizeHost(host);
        if (normalized is null)
            return false;
        return _excluded.Any(rule =>
            string.Equals(normalized, rule, StringComparison.OrdinalIgnoreCase) ||
            normalized.EndsWith("." + rule, StringComparison.OrdinalIgnoreCase));
    }

    public static string? NormalizeHost(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var candidate = value.Trim();
        if (!candidate.Contains("://", StringComparison.Ordinal))
            candidate = "https://" + candidate;
        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri) ||
            string.IsNullOrWhiteSpace(uri.Host))
        {
            return null;
        }
        return uri.IdnHost.TrimEnd('.').ToLowerInvariant();
    }
}
