namespace Hanki.Core.Models;

public sealed class AppSettings
{
    public bool IsEnabled { get; set; } = true;
    public bool StartWithWindows { get; set; }
    public bool MinimizeToTray { get; set; } = true;
    public bool SpaceExpansionEnabled { get; set; } = true;
    public bool EnterExpansionEnabled { get; set; }
    public bool TabExpansionEnabled { get; set; }
    public List<string> ExcludedProcesses { get; set; } =
    [
        "cmd.exe",
        "powershell.exe",
        "WindowsTerminal.exe",
        "mstsc.exe"
    ];
    public string Theme { get; set; } = "Purple";
    public bool FirstRunCompleted { get; set; }

    public AppSettings Clone() => new()
    {
        IsEnabled = IsEnabled,
        StartWithWindows = StartWithWindows,
        MinimizeToTray = MinimizeToTray,
        SpaceExpansionEnabled = SpaceExpansionEnabled,
        EnterExpansionEnabled = EnterExpansionEnabled,
        TabExpansionEnabled = TabExpansionEnabled,
        ExcludedProcesses = [.. ExcludedProcesses],
        Theme = Theme,
        FirstRunCompleted = FirstRunCompleted
    };
}
