using System.Diagnostics;

namespace Hanki.Infrastructure.Authentication;

/// <summary>
/// Opens a URL in the OS-default browser via the shell -- never an embedded WebView, never a
/// custom login form. This is the only supported way to show the Google/Supabase login page.
/// </summary>
public sealed class SystemBrowserLauncher : ISystemBrowserLauncher
{
    public void Launch(string url)
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        }) ?? throw new InvalidOperationException("The system browser process could not be started.");
    }
}
