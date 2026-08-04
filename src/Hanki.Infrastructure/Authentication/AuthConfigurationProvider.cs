using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text;
using Hanki.Infrastructure.Logging;

namespace Hanki.Infrastructure.Authentication;

/// <summary>
/// Loads <see cref="AuthConfiguration"/> from a local, git-ignored JSON file next to the exe
/// (default: <c>hanki.auth.config.json</c> in <see cref="AppContext.BaseDirectory"/>). Never
/// throws -- a missing or invalid file simply means "not configured", and callers must disable
/// the login UI rather than crash.
/// </summary>
public sealed class AuthConfigurationProvider
{
    public const string DefaultFileName = "hanki.auth.config.json";
    public const string RequiredRedirectUri = "http://127.0.0.1:43289/auth/callback";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly string _configFilePath;
    private readonly IPrivacySafeLogger? _logger;

    public AuthConfigurationProvider(IPrivacySafeLogger? logger = null, string? configFilePath = null)
    {
        _logger = logger;
        _configFilePath = configFilePath ?? Path.Combine(AppContext.BaseDirectory, DefaultFileName);
    }

    /// <summary>Returns null when the file is missing, unreadable, or fails validation.</summary>
    public AuthConfiguration? TryLoad()
    {
        try
        {
            if (!File.Exists(_configFilePath))
            {
                _logger?.Info("auth.config.missing");
                return null;
            }

            var json = File.ReadAllText(_configFilePath);
            var raw = JsonSerializer.Deserialize<RawAuthConfiguration>(json, JsonOptions);
            var configuration = Validate(raw);
            if (configuration is null)
            {
                _logger?.Info("auth.config.invalid");
                return null;
            }

            _logger?.Info("auth.config.loaded");
            return configuration;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or JsonException or FormatException or ArgumentException or NotSupportedException)
        {
            _logger?.Info("auth.config.invalid");
            return null;
        }
    }

    private static AuthConfiguration? Validate(RawAuthConfiguration? raw)
    {
        if (raw is null)
            return null;
        if (string.IsNullOrWhiteSpace(raw.SupabaseUrl) ||
            string.IsNullOrWhiteSpace(raw.SupabasePublishableKey) ||
            string.IsNullOrWhiteSpace(raw.RedirectUri))
        {
            return null;
        }

        if (!Uri.TryCreate(raw.SupabaseUrl.Trim(), UriKind.Absolute, out var supabaseUri) ||
            supabaseUri.Scheme != Uri.UriSchemeHttps ||
            !string.IsNullOrEmpty(supabaseUri.UserInfo) ||
            !string.IsNullOrEmpty(supabaseUri.Query) ||
            !string.IsNullOrEmpty(supabaseUri.Fragment) ||
            supabaseUri.AbsolutePath != "/")
        {
            return null;
        }

        var publishableKey = raw.SupabasePublishableKey.Trim();
        if (IsPrivilegedKey(publishableKey))
        {
            return null;
        }

        var redirectText = raw.RedirectUri.Trim();
        if (!string.Equals(redirectText, RequiredRedirectUri, StringComparison.Ordinal))
            return null;

        return new AuthConfiguration(
            supabaseUri.ToString().TrimEnd('/'),
            publishableKey,
            RequiredRedirectUri);
    }

    private static bool IsPrivilegedKey(string key)
    {
        if (key.StartsWith("sb_secret_", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(key, "service_role", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var segments = key.Split('.');
        if (segments.Length != 3)
            return false;

        try
        {
            var payload = segments[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight(payload.Length + ((4 - payload.Length % 4) % 4), '=');
            using var document = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(payload)));
            if (!document.RootElement.TryGetProperty("role", out var role) ||
                role.ValueKind != JsonValueKind.String)
            {
                return true;
            }

            var roleValue = role.GetString();
            return string.Equals(roleValue, "service_role", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(roleValue, "supabase_admin", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(roleValue, "authenticated", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception exception) when (
            exception is FormatException or JsonException or ArgumentException)
        {
            return true;
        }
    }

    private sealed class RawAuthConfiguration
    {
        [JsonPropertyName("supabaseUrl")]
        public string? SupabaseUrl { get; set; }

        [JsonPropertyName("supabasePublishableKey")]
        public string? SupabasePublishableKey { get; set; }

        [JsonPropertyName("redirectUri")]
        public string? RedirectUri { get; set; }
    }
}
