using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Hanki.Infrastructure.Windows;

internal static class ForegroundProcess
{
    public static string? GetName()
    {
        var window = GetForegroundWindow();
        if (window == IntPtr.Zero)
            return null;
        _ = GetWindowThreadProcessId(window, out var processId);
        if (processId == 0)
            return null;
        try
        {
            using var process = Process.GetProcessById((int)processId);
            return process.ProcessName + ".exe";
        }
        catch
        {
            return null;
        }
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
}
