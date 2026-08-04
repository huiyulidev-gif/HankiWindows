namespace Hanki.Core.Authentication;

/// <summary>
/// Stable, Korean-UI-safe classification of authentication failures. Never carries the
/// underlying exception, URL, or token -- only <see cref="AuthResult.ErrorMessage"/> (already
/// mapped to an approved Korean string) should reach the UI.
/// </summary>
public enum AuthErrorCode
{
    None,
    ConfigMissing,
    AlreadyInProgress,
    Network,
    Timeout,
    StateMismatch,
    MalformedCallback,
    ProviderError,
    InvalidResponse,
    BrowserLaunchFailed,
    Storage,
    SessionExpired,
    Unknown
}
