using System.Security.Cryptography;
using System.Text;
using Hanki.Infrastructure.Authentication;

namespace Hanki.Core.Tests.Authentication;

[TestClass]
public sealed class OAuthPkceServiceTests
{
    private static readonly System.Text.RegularExpressions.Regex UnreservedBase64Url =
        new("^[A-Za-z0-9_-]+$", System.Text.RegularExpressions.RegexOptions.Compiled);

    [TestMethod]
    public void GenerateCodeVerifier_HasLengthWithinRfcRange()
    {
        var service = new OAuthPkceService();
        var verifier = service.GenerateCodeVerifier();

        Assert.IsTrue(verifier.Length is >= 43 and <= 128, $"Length was {verifier.Length}");
    }

    [TestMethod]
    public void GenerateCodeVerifier_UsesOnlyUnreservedBase64UrlCharacters()
    {
        var service = new OAuthPkceService();
        var verifier = service.GenerateCodeVerifier();

        Assert.IsTrue(UnreservedBase64Url.IsMatch(verifier), $"Verifier had disallowed characters: {verifier}");
    }

    [TestMethod]
    public void GenerateCodeVerifier_IsDifferentEachCall()
    {
        var service = new OAuthPkceService();
        var first = service.GenerateCodeVerifier();
        var second = service.GenerateCodeVerifier();

        Assert.AreNotEqual(first, second);
    }

    [TestMethod]
    public void GenerateCodeChallenge_MatchesManualSha256Base64Url()
    {
        var service = new OAuthPkceService();
        const string verifier = "fake-verifier-0123456789abcdefghijklmno";

        var challenge = service.GenerateCodeChallenge(verifier);

        var expectedHash = SHA256.HashData(Encoding.ASCII.GetBytes(verifier));
        var expected = Convert.ToBase64String(expectedHash).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        Assert.AreEqual(expected, challenge);
    }

    [TestMethod]
    public void GenerateCodeChallenge_DoesNotContainPaddingOrUnsafeCharacters()
    {
        var service = new OAuthPkceService();
        var challenge = service.GenerateCodeChallenge("fake-verifier-for-challenge-shape-test");

        Assert.IsFalse(challenge.Contains('='));
        Assert.IsFalse(challenge.Contains('+'));
        Assert.IsFalse(challenge.Contains('/'));
    }

    [TestMethod]
    public void GenerateCodeChallenge_IsDeterministicForSameVerifier()
    {
        var service = new OAuthPkceService();
        const string verifier = "same-verifier-value-used-twice-in-a-row-here";

        Assert.AreEqual(service.GenerateCodeChallenge(verifier), service.GenerateCodeChallenge(verifier));
    }

    [TestMethod]
    public void GenerateState_IsNotAllZeroAndHasSufficientLength()
    {
        var service = new OAuthPkceService();
        var state = service.GenerateState();

        Assert.IsTrue(state.Length >= 32, $"State length was {state.Length}");
        Assert.IsFalse(state.All(character => character == 'A' || character == '0'));
    }

    [TestMethod]
    public void GenerateState_ChangesEachCall()
    {
        var service = new OAuthPkceService();
        var values = Enumerable.Range(0, 20).Select(_ => service.GenerateState()).ToHashSet(StringComparer.Ordinal);

        Assert.AreEqual(20, values.Count);
    }

    [TestMethod]
    public void GenerateState_UsesOnlyUnreservedBase64UrlCharacters()
    {
        var service = new OAuthPkceService();
        var state = service.GenerateState();

        Assert.IsTrue(UnreservedBase64Url.IsMatch(state), $"State had disallowed characters: {state}");
    }
}
