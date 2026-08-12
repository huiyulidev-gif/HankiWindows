namespace Hanki.Core.Services;

public enum ProcessExclusionReason
{
    None,
    ProcessUnavailable,
    ProtectedProcess,
    UserExcluded
}

public sealed class ProcessExclusionPolicy(IEnumerable<string> excludedProcesses)
{
    private readonly HashSet<string> _excluded = new(
        excludedProcesses.Select(Normalize),
        StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> ProtectedProcesses = new(
        ["LogonUI.exe", "CredentialUIBroker.exe", "consent.exe", "LockApp.exe"],
        StringComparer.OrdinalIgnoreCase);

    public bool IsExcluded(string? processName)
        => Evaluate(processName) != ProcessExclusionReason.None;

    public ProcessExclusionReason Evaluate(string? processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
            return ProcessExclusionReason.ProcessUnavailable;

        var normalized = Normalize(processName);
        if (ProtectedProcesses.Contains(normalized))
            return ProcessExclusionReason.ProtectedProcess;
        return _excluded.Contains(normalized)
            ? ProcessExclusionReason.UserExcluded
            : ProcessExclusionReason.None;
    }

    public static string Normalize(string processName)
    {
        var value = Path.GetFileName(processName.Trim());
        return value.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? value : value + ".exe";
    }
}
