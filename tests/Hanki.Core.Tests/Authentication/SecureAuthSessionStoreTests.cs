using Hanki.Core.Authentication;
using Hanki.Infrastructure.Authentication;

namespace Hanki.Core.Tests.Authentication;

[TestClass]
public sealed class SecureAuthSessionStoreTests
{
    [TestMethod]
    public async Task LoadAsync_MissingFile_ReturnsNull()
    {
        var store = new SecureAuthSessionStore(filePath: TempPath());

        Assert.IsNull(await store.LoadAsync());
    }

    [TestMethod]
    public async Task SaveAndLoad_RoundTripsSessionExactly()
    {
        var path = TempPath();
        var store = new SecureAuthSessionStore(filePath: path);
        var session = NewSession();

        await store.SaveAsync(session);
        var loaded = await store.LoadAsync();

        Assert.IsNotNull(loaded);
        Assert.AreEqual(session.AccessToken, loaded!.AccessToken);
        Assert.AreEqual(session.RefreshToken, loaded.RefreshToken);
        Assert.AreEqual(session.ExpiresAtUtc, loaded.ExpiresAtUtc);
        Assert.AreEqual(session.User.Id, loaded.User.Id);
        Assert.AreEqual(session.User.Email, loaded.User.Email);
        Assert.AreEqual(session.User.Name, loaded.User.Name);
        Assert.AreEqual(session.User.AvatarUrl, loaded.User.AvatarUrl);
        TryDelete(path);
    }

    [TestMethod]
    public async Task DeleteAsync_RemovesStoredSession()
    {
        var path = TempPath();
        var store = new SecureAuthSessionStore(filePath: path);
        await store.SaveAsync(NewSession());

        await store.DeleteAsync();

        Assert.IsFalse(File.Exists(path));
        Assert.IsNull(await store.LoadAsync());
    }

    [TestMethod]
    public async Task DeleteAsync_WhenNothingStored_DoesNotThrow()
    {
        var store = new SecureAuthSessionStore(filePath: TempPath());

        await store.DeleteAsync();
    }

    [TestMethod]
    public async Task LoadAsync_CorruptedBytes_ReturnsNullWithoutThrowing()
    {
        var path = TempPath();
        await File.WriteAllBytesAsync(path, [1, 2, 3, 4, 5, 6, 7, 8]);
        var store = new SecureAuthSessionStore(filePath: path);

        var loaded = await store.LoadAsync();

        Assert.IsNull(loaded);
        TryDelete(path);
    }

    [TestMethod]
    public async Task LoadAsync_EmptyFile_ReturnsNull()
    {
        var path = TempPath();
        await File.WriteAllBytesAsync(path, []);
        var store = new SecureAuthSessionStore(filePath: path);

        Assert.IsNull(await store.LoadAsync());
        TryDelete(path);
    }

    [TestMethod]
    public async Task LoadAsync_UnrelatedDpapiProtectedGarbage_ReturnsNull()
    {
        // Valid DPAPI blob (protects arbitrary bytes) but not JSON once unprotected -- must not throw.
        var path = TempPath();
        var garbage = System.Security.Cryptography.ProtectedData.Protect(
            "not a json session"u8.ToArray(), null, System.Security.Cryptography.DataProtectionScope.CurrentUser);
        await File.WriteAllBytesAsync(path, garbage);
        var store = new SecureAuthSessionStore(filePath: path);

        Assert.IsNull(await store.LoadAsync());
        TryDelete(path);
    }

    [TestMethod]
    public async Task LoadAsync_ValidUnexpiredSession_Restores()
    {
        var path = TempPath();
        var store = new SecureAuthSessionStore(filePath: path);
        var session = NewSession(expiresInMinutes: 30);
        await store.SaveAsync(session);

        var loaded = await store.LoadAsync();

        Assert.IsNotNull(loaded);
        Assert.IsFalse(loaded!.IsExpired());
        TryDelete(path);
    }

    private static AuthSession NewSession(int expiresInMinutes = 60) => new()
    {
        AccessToken = "fake-access-token",
        RefreshToken = "fake-refresh-token",
        ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(expiresInMinutes),
        User = new AuthUser("user-id-123", "test@example.test", "테스트 사용자", "https://example.test/avatar.png")
    };

    private static string TempPath() => Path.Combine(Path.GetTempPath(), $"hanki-auth-session-test-{Guid.NewGuid():N}.bin");

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
    }
}
