using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Hanki.Core.Authentication;
using Hanki.Infrastructure.Logging;

namespace Hanki.Infrastructure.Authentication;

/// <summary>
/// Persists the auth session encrypted at rest with Windows DPAPI
/// (<see cref="ProtectedData"/>, <see cref="DataProtectionScope.CurrentUser"/>) in its own file
/// (<see cref="AppPaths.AuthSessionPath"/> by default) -- never mixed into the shortcuts SQLite
/// database or the JSON settings file. Corrupted/tampered data is treated as "no session", never
/// thrown. The file path is injectable so tests do not touch the real per-user session file.
/// </summary>
public sealed class SecureAuthSessionStore(IPrivacySafeLogger? logger = null, string? filePath = null)
    : IAuthSessionStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _filePath = filePath ?? AppPaths.AuthSessionPath;

    public async Task SaveAsync(AuthSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        var payload = new StoredSession(
            session.AccessToken,
            session.RefreshToken,
            session.ExpiresAtUtc,
            session.User.Id,
            session.User.Email,
            session.User.Name,
            session.User.AvatarUrl);

        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var json = JsonSerializer.Serialize(payload, JsonOptions);
        var plainBytes = Encoding.UTF8.GetBytes(json);
        var temporaryPath = $"{_filePath}.{Guid.NewGuid():N}.tmp";
        try
        {
            var protectedBytes = ProtectedData.Protect(plainBytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
            await File.WriteAllBytesAsync(temporaryPath, protectedBytes, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporaryPath, _filePath, overwrite: true);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plainBytes);
            TryDeleteFile(temporaryPath);
        }
    }

    public async Task<AuthSession?> LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(_filePath))
                return null;

            var protectedBytes = await File.ReadAllBytesAsync(_filePath, cancellationToken).ConfigureAwait(false);
            if (protectedBytes.Length == 0)
                return null;

            var plainBytes = ProtectedData.Unprotect(protectedBytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
            try
            {
                var json = Encoding.UTF8.GetString(plainBytes);
                var stored = JsonSerializer.Deserialize<StoredSession>(json, JsonOptions);
                if (stored is null ||
                    string.IsNullOrEmpty(stored.AccessToken) ||
                    string.IsNullOrEmpty(stored.RefreshToken) ||
                    string.IsNullOrEmpty(stored.UserId))
                {
                    TryDeleteCorruptedFile();
                    return null;
                }

                return new AuthSession
                {
                    AccessToken = stored.AccessToken,
                    RefreshToken = stored.RefreshToken,
                    ExpiresAtUtc = stored.ExpiresAtUtc,
                    User = new AuthUser(stored.UserId, stored.Email ?? string.Empty, stored.Name ?? string.Empty, stored.AvatarUrl)
                };
            }
            finally
            {
                CryptographicOperations.ZeroMemory(plainBytes);
            }
        }
        catch (Exception exception) when (
            exception is CryptographicException or JsonException or IOException or
            UnauthorizedAccessException or FormatException or ArgumentException)
        {
            // Corrupted, tampered, or foreign-user-encrypted data: treat as "no session", never crash.
            logger?.Info("auth.session.corrupted");
            TryDeleteCorruptedFile();
            return null;
        }
    }

    public Task DeleteAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (File.Exists(_filePath))
            File.Delete(_filePath);
        return Task.CompletedTask;
    }

    private void TryDeleteCorruptedFile()
    {
        try
        {
            TryDeleteFile(_filePath);
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    private sealed record StoredSession(
        string AccessToken,
        string RefreshToken,
        DateTimeOffset ExpiresAtUtc,
        string UserId,
        string? Email,
        string? Name,
        string? AvatarUrl);
}
