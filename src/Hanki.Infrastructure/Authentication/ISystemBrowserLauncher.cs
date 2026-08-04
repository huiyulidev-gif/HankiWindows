namespace Hanki.Infrastructure.Authentication;

/// <summary>Abstraction over launching a URL in the user's default system browser, for testability.</summary>
public interface ISystemBrowserLauncher
{
    /// <summary>Throws if the browser process could not be started. Never blocks on the browser exiting.</summary>
    void Launch(string url);
}
