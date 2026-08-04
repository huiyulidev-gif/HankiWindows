namespace Hanki.Core.Authentication;

/// <summary>
/// Persists the current <see cref="AuthSession"/> at rest. Implementations must encrypt the
/// data (e.g. Windows DPAPI) and must never throw on corrupted/tampered data -- treat it as
/// "no session" instead.
/// </summary>
public interface IAuthSessionStore
{
    Task SaveAsync(AuthSession session, CancellationToken cancellationToken = default);

    /// <summary>Returns null when there is no session, or the stored data is missing/corrupted.</summary>
    Task<AuthSession?> LoadAsync(CancellationToken cancellationToken = default);

    Task DeleteAsync(CancellationToken cancellationToken = default);
}
