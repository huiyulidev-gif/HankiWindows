using System.Security.Cryptography;

namespace Hanki.Infrastructure.Authentication;

/// <summary>
/// Generates the PKCE code verifier/challenge pair and the anti-forgery state value used by the
/// loopback OAuth flow. Pure and side-effect free -- no I/O, fully unit-testable.
/// </summary>
public sealed class OAuthPkceService
{
    /// <summary>RFC 7636 requires 43-128 characters; 32 random bytes base64url-encode to 43.</summary>
    private const int VerifierByteLength = 32;

    /// <summary>At least 32 random bytes as required by the task's anti-CSRF "state" contract.</summary>
    private const int StateByteLength = 32;

    /// <summary>
    /// Generates a cryptographically random PKCE code verifier using the base64url alphabet
    /// (A-Z a-z 0-9 - _), a subset of RFC 7636's "unreserved" character set.
    /// </summary>
    public string GenerateCodeVerifier() => Base64UrlEncode(RandomNumberGenerator.GetBytes(VerifierByteLength));

    /// <summary>Computes code_challenge = BASE64URL(SHA256(code_verifier)) per RFC 7636 (S256 method).</summary>
    public string GenerateCodeChallenge(string codeVerifier)
    {
        ArgumentException.ThrowIfNullOrEmpty(codeVerifier);
        var hash = SHA256.HashData(System.Text.Encoding.ASCII.GetBytes(codeVerifier));
        return Base64UrlEncode(hash);
    }

    /// <summary>Generates a random, base64url-encoded anti-forgery state token.</summary>
    public string GenerateState() => Base64UrlEncode(RandomNumberGenerator.GetBytes(StateByteLength));

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
