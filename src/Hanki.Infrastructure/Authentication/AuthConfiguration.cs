namespace Hanki.Infrastructure.Authentication;

/// <summary>
/// Local, non-secret configuration needed to talk to the shared Yulbyte Supabase project.
/// <see cref="SupabasePublishableKey"/> is the public/anon key -- safe to ship, never the
/// service_role key. No Google client secret is ever stored here; Supabase holds that.
/// </summary>
public sealed record AuthConfiguration(string SupabaseUrl, string SupabasePublishableKey, string RedirectUri)
{
    /// <summary>Loopback host the callback listener binds to. Always 127.0.0.1 -- never a wildcard.</summary>
    public string RedirectHost { get; } = new Uri(RedirectUri).Host;

    /// <summary>Fixed local port the callback listener binds to.</summary>
    public int RedirectPort { get; } = new Uri(RedirectUri).Port;

    /// <summary>Absolute path the callback listener accepts, e.g. "/auth/callback".</summary>
    public string RedirectPath { get; } = new Uri(RedirectUri).AbsolutePath.TrimEnd('/');
}
