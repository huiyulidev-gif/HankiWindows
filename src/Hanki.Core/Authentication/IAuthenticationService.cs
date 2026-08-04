namespace Hanki.Core.Authentication;

/// <summary>
/// Orchestrates Google login via Supabase Auth (loopback OAuth + PKCE). All members are safe
/// to call from any thread; state changes are announced via <see cref="StateChanged"/> so a UI
/// layer can marshal back to its own dispatcher.
/// </summary>
public interface IAuthenticationService
{
    /// <summary>False when no valid local auth configuration was found -- login must be disabled in the UI.</summary>
    bool IsConfigured { get; }

    AuthenticationState State { get; }

    AuthUser? CurrentUser { get; }

    /// <summary>
    /// A Korean, UI-safe informational message set alongside a state transition that was not
    /// user-initiated (e.g. session-restore refresh failure). Null unless there is something to
    /// show. Cleared automatically the next time login starts.
    /// </summary>
    string? LastNotice { get; }

    event EventHandler? StateChanged;

    /// <summary>
    /// Runs the full loopback OAuth + PKCE login flow: opens the system browser, waits for the
    /// local callback, exchanges the code, and persists the resulting session. Cancellable via
    /// <paramref name="cancellationToken"/> (UI "취소" button or app shutdown).
    /// </summary>
    Task<AuthResult> LoginAsync(CancellationToken cancellationToken = default);

    Task LogoutAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads any stored session and, if the access token is expired or near-expiry, refreshes
    /// it in the background. Never throws; on failure the stored session is cleared and the
    /// service stays/returns to <see cref="AuthenticationState.LoggedOut"/>.
    /// </summary>
    Task RestoreSessionAsync(CancellationToken cancellationToken = default);
}
