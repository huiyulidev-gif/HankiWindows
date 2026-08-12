namespace Hanki.Core.Models;

public sealed class AppSettings
{
    public bool IsEnabled { get; set; } = true;
    public bool IsPaused { get; set; }
    public bool StartWithWindows { get; set; }
    public bool MinimizeToTray { get; set; } = true;
    public bool SpaceExpansionEnabled { get; set; } = true;
    public bool EnterExpansionEnabled { get; set; }
    public bool TabExpansionEnabled { get; set; }
    public bool ClipboardCompatibilityMode { get; set; }
    public List<string> ExcludedProcesses { get; set; } =
    [
        "cmd.exe",
        "powershell.exe",
        "WindowsTerminal.exe",
        "mstsc.exe"
    ];
    public List<string> ExcludedSites { get; set; } = [];
    public string Theme { get; set; } = "Purple";
    public bool FirstRunCompleted { get; set; }

    public AppSettings Clone() => new()
    {
        IsEnabled = IsEnabled,
        IsPaused = IsPaused,
        StartWithWindows = StartWithWindows,
        MinimizeToTray = MinimizeToTray,
        SpaceExpansionEnabled = SpaceExpansionEnabled,
        EnterExpansionEnabled = EnterExpansionEnabled,
        TabExpansionEnabled = TabExpansionEnabled,
        ClipboardCompatibilityMode = ClipboardCompatibilityMode,
        ExcludedProcesses = [.. ExcludedProcesses],
        ExcludedSites = [.. ExcludedSites],
        Theme = Theme,
        FirstRunCompleted = FirstRunCompleted
    };
}
