namespace Hanki.Core.Services;

public sealed class ProcessExclusionPolicy(IEnumerable<string> excludedProcesses)
{
    private readonly HashSet<string> _excluded = new(
        excludedProcesses.Select(Normalize),
        StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> ProtectedProcesses = new(
        ["LogonUI.exe", "CredentialUIBroker.exe", "consent.exe", "LockApp.exe"],
        StringComparer.OrdinalIgnoreCase);

    public bool IsExcluded(string? processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
            return true;

        var normalized = Normalize(processName);
        return ProtectedProcesses.Contains(normalized) || _excluded.Contains(normalized);
    }

    public static string Normalize(string processName)
    {
        var value = Path.GetFileName(processName.Trim());
        return value.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? value : value + ".exe";
    }
}
