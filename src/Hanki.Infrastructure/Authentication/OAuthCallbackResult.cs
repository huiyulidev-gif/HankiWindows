namespace Hanki.Infrastructure.Authentication;

public enum OAuthCallbackOutcome
{
    Success,
    MissingCode,
    MissingState,
    StateMismatch,
    ProviderError,
    ProviderAccessDenied
}

/// <summary>Parsed, validated result of a single OAuth callback request's query string.</summary>
public sealed record OAuthCallbackResult(
    OAuthCallbackOutcome Outcome,
    string? Code,
    string? State,
    string? ProviderError,
    string? ProviderErrorDescription)
{
    public bool IsSuccess => Outcome == OAuthCallbackOutcome.Success;
}

public enum OAuthListenOutcome
{
    Received,
    TimedOut,
    Cancelled,
    ListenerStartFailed
}

/// <summary>Result of waiting for exactly one matching callback request (or timing out/being cancelled).</summary>
public sealed record OAuthCallbackListenResult(OAuthListenOutcome Outcome, OAuthCallbackResult? Callback)
{
    public static OAuthCallbackListenResult Received(OAuthCallbackResult callback) =>
        new(OAuthListenOutcome.Received, callback);

    public static readonly OAuthCallbackListenResult TimedOut = new(OAuthListenOutcome.TimedOut, null);
    public static readonly OAuthCallbackListenResult Cancelled = new(OAuthListenOutcome.Cancelled, null);
    public static readonly OAuthCallbackListenResult ListenerStartFailed = new(OAuthListenOutcome.ListenerStartFailed, null);
}
