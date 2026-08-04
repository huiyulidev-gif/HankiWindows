namespace Hanki.Infrastructure.Authentication;

/// <summary>
/// Pure parsing/validation logic for the OAuth loopback callback request, kept separate from
/// <see cref="OAuthCallbackListener"/> so it is unit-testable without a real socket.
/// </summary>
public static class OAuthCallbackParser
{
    /// <summary>
    /// Parses the callback query string and validates it against the expected anti-forgery
    /// state. Never throws -- malformed input simply yields a non-success outcome.
    /// </summary>
    public static OAuthCallbackResult Parse(string? query, string expectedState)
    {
        var values = ParseQuery(query);
        values.TryGetValue("code", out var code);
        values.TryGetValue("state", out var state);

        // Validate the anti-forgery state before trusting even an OAuth error response.
        if (string.IsNullOrEmpty(state))
            return new OAuthCallbackResult(OAuthCallbackOutcome.MissingState, code, null, null, null);
        if (!string.Equals(state, expectedState, StringComparison.Ordinal))
            return new OAuthCallbackResult(OAuthCallbackOutcome.StateMismatch, code, state, null, null);

        if (values.TryGetValue("error", out var error) && !string.IsNullOrEmpty(error))
        {
            var outcome = string.Equals(error, "access_denied", StringComparison.OrdinalIgnoreCase)
                ? OAuthCallbackOutcome.ProviderAccessDenied
                : OAuthCallbackOutcome.ProviderError;
            values.TryGetValue("error_description", out var description);
            return new OAuthCallbackResult(outcome, Code: null, state, error, description);
        }

        if (string.IsNullOrEmpty(code))
            return new OAuthCallbackResult(OAuthCallbackOutcome.MissingCode, null, state, null, null);

        return new OAuthCallbackResult(OAuthCallbackOutcome.Success, code, state, null, null);
    }

    /// <summary>
    /// True only for a request to the exact configured loopback host and path (case-insensitive,
    /// ignoring a trailing slash). Everything else -- including any other host header a local
    /// process might send -- must be rejected with a plain 404.
    /// </summary>
    public static bool IsAcceptableRequest(
        Uri requestUrl,
        string expectedHost,
        string expectedPath,
        int? expectedPort = null)
    {
        ArgumentNullException.ThrowIfNull(requestUrl);
        ArgumentException.ThrowIfNullOrEmpty(expectedHost);
        ArgumentException.ThrowIfNullOrEmpty(expectedPath);

        if (requestUrl.Scheme != Uri.UriSchemeHttp ||
            !string.Equals(requestUrl.Host, expectedHost, StringComparison.OrdinalIgnoreCase) ||
            (expectedPort.HasValue && requestUrl.Port != expectedPort.Value))
        {
            return false;
        }

        var actualPath = requestUrl.AbsolutePath.TrimEnd('/');
        var normalizedExpected = expectedPath.TrimEnd('/');
        return string.Equals(actualPath, normalizedExpected, StringComparison.Ordinal);
    }

    private static Dictionary<string, string> ParseQuery(string? query)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(query))
            return result;

        var trimmed = query.TrimStart('?');
        if (trimmed.Length == 0)
            return result;

        foreach (var pair in trimmed.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separatorIndex = pair.IndexOf('=');
            var rawKey = separatorIndex >= 0 ? pair[..separatorIndex] : pair;
            var rawValue = separatorIndex >= 0 ? pair[(separatorIndex + 1)..] : string.Empty;
            try
            {
                var key = Uri.UnescapeDataString(rawKey.Replace('+', ' '));
                var value = Uri.UnescapeDataString(rawValue.Replace('+', ' '));
                if (key.Length > 0)
                    result[key] = value;
            }
            catch (FormatException)
            {
                // Malformed percent-encoding in a query pair: skip it rather than throw.
            }
        }

        return result;
    }
}
