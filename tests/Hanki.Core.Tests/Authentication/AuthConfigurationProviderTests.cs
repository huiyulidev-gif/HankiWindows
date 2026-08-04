using Hanki.Infrastructure.Authentication;

namespace Hanki.Core.Tests.Authentication;

[TestClass]
public sealed class AuthConfigurationProviderTests
{
    [TestMethod]
    public void TryLoad_MissingFile_ReturnsNull()
    {
        var path = Path.Combine(Path.GetTempPath(), $"hanki-auth-test-{Guid.NewGuid():N}.json");
        var provider = new AuthConfigurationProvider(configFilePath: path);

        Assert.IsNull(provider.TryLoad());
    }

    [TestMethod]
    public void TryLoad_ValidFile_ReturnsConfiguration()
    {
        var path = WriteConfig("""
            {
              "supabaseUrl": "https://example.test",
              "supabasePublishableKey": "fake-anon-key",
              "redirectUri": "http://127.0.0.1:43289/auth/callback"
            }
            """);

        var configuration = new AuthConfigurationProvider(configFilePath: path).TryLoad();

        Assert.IsNotNull(configuration);
        Assert.AreEqual("https://example.test", configuration!.SupabaseUrl);
        Assert.AreEqual("fake-anon-key", configuration.SupabasePublishableKey);
        Assert.AreEqual("127.0.0.1", configuration.RedirectHost);
        Assert.AreEqual(43289, configuration.RedirectPort);
        Assert.AreEqual("/auth/callback", configuration.RedirectPath);
        TryDelete(path);
    }

    [TestMethod]
    public void TryLoad_InvalidJson_ReturnsNullWithoutThrowing()
    {
        var path = WriteConfig("{ not valid json ");

        var configuration = new AuthConfigurationProvider(configFilePath: path).TryLoad();

        Assert.IsNull(configuration);
        TryDelete(path);
    }

    [TestMethod]
    public void TryLoad_EmptyFields_ReturnsNull()
    {
        var path = WriteConfig("""{ "supabaseUrl": "", "supabasePublishableKey": "", "redirectUri": "" }""");

        Assert.IsNull(new AuthConfigurationProvider(configFilePath: path).TryLoad());
        TryDelete(path);
    }

    [TestMethod]
    public void TryLoad_NonHttpsSupabaseUrl_ReturnsNull()
    {
        var path = WriteConfig("""
            {
              "supabaseUrl": "http://example.test",
              "supabasePublishableKey": "fake-anon-key",
              "redirectUri": "http://127.0.0.1:43289/auth/callback"
            }
            """);

        Assert.IsNull(new AuthConfigurationProvider(configFilePath: path).TryLoad());
        TryDelete(path);
    }

    [TestMethod]
    public void TryLoad_HttpsRedirectUri_ReturnsNull()
    {
        var path = WriteConfig("""
            {
              "supabaseUrl": "https://example.test",
              "supabasePublishableKey": "fake-anon-key",
              "redirectUri": "https://127.0.0.1:43289/auth/callback"
            }
            """);

        Assert.IsNull(new AuthConfigurationProvider(configFilePath: path).TryLoad());
        TryDelete(path);
    }

    [TestMethod]
    public void TryLoad_NonLoopbackRedirectHost_ReturnsNull()
    {
        var path = WriteConfig("""
            {
              "supabaseUrl": "https://example.test",
              "supabasePublishableKey": "fake-anon-key",
              "redirectUri": "http://0.0.0.0:43289/auth/callback"
            }
            """);

        Assert.IsNull(new AuthConfigurationProvider(configFilePath: path).TryLoad());
        TryDelete(path);
    }

    [TestMethod]
    public void TryLoad_PrivilegedPort_ReturnsNull()
    {
        var path = WriteConfig("""
            {
              "supabaseUrl": "https://example.test",
              "supabasePublishableKey": "fake-anon-key",
              "redirectUri": "http://127.0.0.1:80/auth/callback"
            }
            """);

        Assert.IsNull(new AuthConfigurationProvider(configFilePath: path).TryLoad());
        TryDelete(path);
    }

    [TestMethod]
    [DataRow("http://127.0.0.1:43290/auth/callback")]
    [DataRow("http://127.0.0.1:43289/other/callback")]
    [DataRow("http://localhost:43289/auth/callback")]
    [DataRow("http://127.0.0.1:43289/auth/callback/")]
    public void TryLoad_AnyRedirectOtherThanFixedLoopbackUri_ReturnsNull(string redirectUri)
    {
        var path = WriteConfig($$"""
            {
              "supabaseUrl": "https://example.test",
              "supabasePublishableKey": "fake-anon-key",
              "redirectUri": "{{redirectUri}}"
            }
            """);

        Assert.IsNull(new AuthConfigurationProvider(configFilePath: path).TryLoad());
        TryDelete(path);
    }

    [TestMethod]
    [DataRow("sb_secret_never-ship-this")]
    [DataRow("service_role")]
    [DataRow("eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJyb2xlIjoic2VydmljZV9yb2xlIn0.fake-signature")]
    [DataRow("eyJhbGciOiJIUzI1NiJ9.eyJyb2xlIjoiYXV0aGVudGljYXRlZCJ9.fake-signature")]
    [DataRow("header.not-json.signature")]
    public void TryLoad_PrivilegedSupabaseKey_ReturnsNull(string key)
    {
        var path = WriteConfig($$"""
            {
              "supabaseUrl": "https://example.test",
              "supabasePublishableKey": "{{key}}",
              "redirectUri": "http://127.0.0.1:43289/auth/callback"
            }
            """);

        Assert.IsNull(new AuthConfigurationProvider(configFilePath: path).TryLoad());
        TryDelete(path);
    }

    [TestMethod]
    [DataRow("https://user:password@example.test")]
    [DataRow("https://example.test/project")]
    [DataRow("https://example.test?query=not-allowed")]
    [DataRow("https://example.test/#fragment")]
    public void TryLoad_NonOriginSupabaseUrl_ReturnsNull(string supabaseUrl)
    {
        var path = WriteConfig($$"""
            {
              "supabaseUrl": "{{supabaseUrl}}",
              "supabasePublishableKey": "fake-anon-key",
              "redirectUri": "http://127.0.0.1:43289/auth/callback"
            }
            """);

        Assert.IsNull(new AuthConfigurationProvider(configFilePath: path).TryLoad());
        TryDelete(path);
    }

    private static string WriteConfig(string json)
    {
        var path = Path.Combine(Path.GetTempPath(), $"hanki-auth-test-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, json);
        return path;
    }

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
