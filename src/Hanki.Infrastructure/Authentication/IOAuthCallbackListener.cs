namespace Hanki.Infrastructure.Authentication;

/// <summary>
/// Waits for exactly one matching OAuth loopback callback request. Abstracted so
/// <c>SupabaseAuthenticationService</c> can be unit-tested with a fake implementation instead of
/// a real socket.
/// </summary>
public interface IOAuthCallbackListener
{
    /// <summary>
    /// Starts listening, waits for one request matching the configured host/path, and returns
    /// its parsed result. Any other request is rejected with a plain 404 and does not stop the
    /// wait. Always releases the port before returning, including on timeout/cancellation.
    /// </summary>
    Task<OAuthCallbackListenResult> WaitForCallbackAsync(
        string expectedState,
        TimeSpan timeout,
        CancellationToken cancellationToken);
}
