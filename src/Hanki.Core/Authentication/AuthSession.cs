namespace Hanki.Core.Authentication;

/// <summary>
/// A persisted Supabase Auth session. Never logged or serialized to plain text on disk --
/// <see cref="IAuthSessionStore"/> implementations must encrypt it at rest.
/// </summary>
public sealed class AuthSession
{
    public required string AccessToken { get; init; }
    public required string RefreshToken { get; init; }
    public required DateTimeOffset ExpiresAtUtc { get; init; }
    public required AuthUser User { get; init; }

    public bool IsExpired(DateTimeOffset? now = null) =>
        (now ?? DateTimeOffset.UtcNow) >= ExpiresAtUtc;

    /// <summary>True when the access token is already expired or will expire within <paramref name="buffer"/>.</summary>
    public bool IsExpiringSoon(TimeSpan buffer, DateTimeOffset? now = null) =>
        (now ?? DateTimeOffset.UtcNow) >= ExpiresAtUtc - buffer;
}
