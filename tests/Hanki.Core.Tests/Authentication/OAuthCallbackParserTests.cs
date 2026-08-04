using Hanki.Infrastructure.Authentication;

namespace Hanki.Core.Tests.Authentication;

[TestClass]
public sealed class OAuthCallbackParserTests
{
    private const string ExpectedState = "expected-state-abc123";

    [TestMethod]
    public void Parse_ValidCallback_ParsesCodeAndState()
    {
        var result = OAuthCallbackParser.Parse($"?code=auth-code-xyz&state={ExpectedState}", ExpectedState);

        Assert.AreEqual(OAuthCallbackOutcome.Success, result.Outcome);
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual("auth-code-xyz", result.Code);
        Assert.AreEqual(ExpectedState, result.State);
    }

    [TestMethod]
    public void Parse_MissingCode_ReturnsMissingCode()
    {
        var result = OAuthCallbackParser.Parse($"?state={ExpectedState}", ExpectedState);

        Assert.AreEqual(OAuthCallbackOutcome.MissingCode, result.Outcome);
        Assert.IsFalse(result.IsSuccess);
    }

    [TestMethod]
    public void Parse_MissingState_ReturnsMissingState()
    {
        var result = OAuthCallbackParser.Parse("?code=auth-code-xyz", ExpectedState);

        Assert.AreEqual(OAuthCallbackOutcome.MissingState, result.Outcome);
    }

    [TestMethod]
    public void Parse_StateMismatch_ReturnsStateMismatch()
    {
        var result = OAuthCallbackParser.Parse("?code=auth-code-xyz&state=some-other-state", ExpectedState);

        Assert.AreEqual(OAuthCallbackOutcome.StateMismatch, result.Outcome);
    }

    [TestMethod]
    public void Parse_ProviderErrorParam_ReturnsProviderError()
    {
        var result = OAuthCallbackParser.Parse(
            $"?error=server_error&error_description=Something+broke&state={ExpectedState}",
            ExpectedState);

        Assert.AreEqual(OAuthCallbackOutcome.ProviderError, result.Outcome);
        Assert.AreEqual("server_error", result.ProviderError);
        Assert.AreEqual("Something broke", result.ProviderErrorDescription);
    }

    [TestMethod]
    public void Parse_UserCancelledShape_ReturnsProviderAccessDenied()
    {
        var result = OAuthCallbackParser.Parse(
            $"?error=access_denied&error_description=The+user+denied+the+request&state={ExpectedState}",
            ExpectedState);

        Assert.AreEqual(OAuthCallbackOutcome.ProviderAccessDenied, result.Outcome);
    }

    [TestMethod]
    public void Parse_EmptyQuery_ReturnsMissingState()
    {
        var result = OAuthCallbackParser.Parse(string.Empty, ExpectedState);

        Assert.AreEqual(OAuthCallbackOutcome.MissingState, result.Outcome);
    }

    [TestMethod]
    public void Parse_NullQuery_DoesNotThrowAndReturnsMissingState()
    {
        var result = OAuthCallbackParser.Parse(null, ExpectedState);

        Assert.AreEqual(OAuthCallbackOutcome.MissingState, result.Outcome);
    }

    [TestMethod]
    public void Parse_ProviderErrorWithWrongState_IsRejectedBeforeErrorIsTrusted()
    {
        var result = OAuthCallbackParser.Parse(
            "?error=access_denied&state=attacker-controlled",
            ExpectedState);

        Assert.AreEqual(OAuthCallbackOutcome.StateMismatch, result.Outcome);
        Assert.IsNull(result.ProviderError);
    }

    [TestMethod]
    public void Parse_ProviderErrorWithoutState_IsRejected()
    {
        var result = OAuthCallbackParser.Parse("?error=server_error", ExpectedState);

        Assert.AreEqual(OAuthCallbackOutcome.MissingState, result.Outcome);
        Assert.IsNull(result.ProviderError);
    }

    [TestMethod]
    public void Parse_MalformedPercentEncoding_DoesNotThrow()
    {
        var result = OAuthCallbackParser.Parse("?code=abc%&state=" + ExpectedState, ExpectedState);

        Assert.IsNotNull(result);
    }

    [TestMethod]
    public void IsAcceptableRequest_ExactHostAndPath_ReturnsTrue()
    {
        var url = new Uri("http://127.0.0.1:43289/auth/callback?code=abc&state=xyz");

        Assert.IsTrue(OAuthCallbackParser.IsAcceptableRequest(url, "127.0.0.1", "/auth/callback"));
    }

    [TestMethod]
    public void IsAcceptableRequest_TrailingSlash_StillMatches()
    {
        var url = new Uri("http://127.0.0.1:43289/auth/callback/?code=abc");

        Assert.IsTrue(OAuthCallbackParser.IsAcceptableRequest(url, "127.0.0.1", "/auth/callback"));
    }

    [TestMethod]
    public void IsAcceptableRequest_WrongPath_ReturnsFalse()
    {
        var url = new Uri("http://127.0.0.1:43289/some/other/path");

        Assert.IsFalse(OAuthCallbackParser.IsAcceptableRequest(url, "127.0.0.1", "/auth/callback"));
    }

    [TestMethod]
    public void IsAcceptableRequest_WrongHost_ReturnsFalse()
    {
        var url = new Uri("http://evil.example.test:43289/auth/callback");

        Assert.IsFalse(OAuthCallbackParser.IsAcceptableRequest(url, "127.0.0.1", "/auth/callback"));
    }

    [TestMethod]
    public void IsAcceptableRequest_RootPath_ReturnsFalse()
    {
        var url = new Uri("http://127.0.0.1:43289/");

        Assert.IsFalse(OAuthCallbackParser.IsAcceptableRequest(url, "127.0.0.1", "/auth/callback"));
    }

    [TestMethod]
    public void IsAcceptableRequest_HttpsScheme_ReturnsFalse()
    {
        var url = new Uri("https://127.0.0.1:43289/auth/callback");

        Assert.IsFalse(OAuthCallbackParser.IsAcceptableRequest(
            url,
            "127.0.0.1",
            "/auth/callback",
            43289));
    }

    [TestMethod]
    public void IsAcceptableRequest_WrongPort_ReturnsFalse()
    {
        var url = new Uri("http://127.0.0.1:43290/auth/callback");

        Assert.IsFalse(OAuthCallbackParser.IsAcceptableRequest(
            url,
            "127.0.0.1",
            "/auth/callback",
            43289));
    }
}
