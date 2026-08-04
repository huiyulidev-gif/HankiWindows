using System.Net;
using System.Net.Http;
using Hanki.Infrastructure.Authentication;

namespace Hanki.Core.Tests.Authentication;

[TestClass]
[DoNotParallelize]
public sealed class OAuthCallbackListenerTests
{
    private const string ExpectedState = "listener-test-state";
    private static readonly AuthConfiguration Configuration = new(
        "https://example.test",
        "fake-anon-key",
        AuthConfigurationProvider.RequiredRedirectUri);

    [TestMethod]
    public async Task WaitForCallbackAsync_ValidGet_ReturnsCodeAndReleasesPort()
    {
        var first = new OAuthCallbackListener(Configuration);
        var firstWait = first.WaitForCallbackAsync(ExpectedState, TimeSpan.FromSeconds(3), CancellationToken.None);

        using var client = new HttpClient();
        using var response = await client.GetAsync(
            $"{Configuration.RedirectUri}?code=auth-code&state={ExpectedState}");
        var firstResult = await firstWait;

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual(OAuthCallbackOutcome.Success, firstResult.Callback?.Outcome);
        Assert.AreEqual("auth-code", firstResult.Callback?.Code);

        var second = new OAuthCallbackListener(Configuration);
        var secondWait = second.WaitForCallbackAsync(ExpectedState, TimeSpan.FromSeconds(3), CancellationToken.None);
        using var secondResponse = await client.GetAsync(
            $"{Configuration.RedirectUri}?code=retry-code&state={ExpectedState}");
        var secondResult = await secondWait;

        Assert.AreEqual(HttpStatusCode.OK, secondResponse.StatusCode);
        Assert.AreEqual("retry-code", secondResult.Callback?.Code);
    }

    [TestMethod]
    public async Task WaitForCallbackAsync_WrongPathAndPost_AreRejectedBeforeValidGet()
    {
        var listener = new OAuthCallbackListener(Configuration);
        var wait = listener.WaitForCallbackAsync(ExpectedState, TimeSpan.FromSeconds(3), CancellationToken.None);

        using var client = new HttpClient();
        using var wrongPath = await client.GetAsync(
            $"http://127.0.0.1:43289/not-the-callback?code=wrong&state={ExpectedState}");
        using var post = await client.PostAsync(
            $"{Configuration.RedirectUri}?code=wrong&state={ExpectedState}",
            new StringContent(string.Empty));
        using var valid = await client.GetAsync(
            $"{Configuration.RedirectUri}?code=valid&state={ExpectedState}");
        var result = await wait;

        Assert.AreEqual(HttpStatusCode.NotFound, wrongPath.StatusCode);
        Assert.AreEqual(HttpStatusCode.MethodNotAllowed, post.StatusCode);
        Assert.AreEqual("GET", string.Join(",", post.Content.Headers.Allow));
        Assert.AreEqual(HttpStatusCode.OK, valid.StatusCode);
        Assert.AreEqual("valid", result.Callback?.Code);
    }

    [TestMethod]
    public async Task WaitForCallbackAsync_StateMismatch_ReturnsBadRequestAndClosesListener()
    {
        var listener = new OAuthCallbackListener(Configuration);
        var wait = listener.WaitForCallbackAsync(ExpectedState, TimeSpan.FromSeconds(3), CancellationToken.None);

        using var client = new HttpClient();
        using var response = await client.GetAsync(
            $"{Configuration.RedirectUri}?code=wrong&state=not-expected");
        var result = await wait;

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.AreEqual(OAuthCallbackOutcome.StateMismatch, result.Callback?.Outcome);
    }

    [TestMethod]
    public async Task WaitForCallbackAsync_MissingStateAndCode_AreRejected()
    {
        using var client = new HttpClient();

        var missingStateListener = new OAuthCallbackListener(Configuration);
        var missingStateWait = missingStateListener.WaitForCallbackAsync(
            ExpectedState,
            TimeSpan.FromSeconds(3),
            CancellationToken.None);
        using var missingStateResponse = await client.GetAsync(
            $"{Configuration.RedirectUri}?code=without-state");
        var missingStateResult = await missingStateWait;

        Assert.AreEqual(HttpStatusCode.BadRequest, missingStateResponse.StatusCode);
        Assert.AreEqual(OAuthCallbackOutcome.MissingState, missingStateResult.Callback?.Outcome);

        var missingCodeListener = new OAuthCallbackListener(Configuration);
        var missingCodeWait = missingCodeListener.WaitForCallbackAsync(
            ExpectedState,
            TimeSpan.FromSeconds(3),
            CancellationToken.None);
        using var missingCodeResponse = await client.GetAsync(
            $"{Configuration.RedirectUri}?state={ExpectedState}");
        var missingCodeResult = await missingCodeWait;

        Assert.AreEqual(HttpStatusCode.BadRequest, missingCodeResponse.StatusCode);
        Assert.AreEqual(OAuthCallbackOutcome.MissingCode, missingCodeResult.Callback?.Outcome);
    }

    [TestMethod]
    public async Task WaitForCallbackAsync_SecondCallbackCannotReplaceFirstResult()
    {
        var listener = new OAuthCallbackListener(Configuration);
        var wait = listener.WaitForCallbackAsync(ExpectedState, TimeSpan.FromSeconds(3), CancellationToken.None);
        using var client = new HttpClient();

        using var firstResponse = await client.GetAsync(
            $"{Configuration.RedirectUri}?code=first-code&state={ExpectedState}");
        var result = await wait;

        HttpResponseMessage? secondResponse = null;
        try
        {
            secondResponse = await client.GetAsync(
                $"{Configuration.RedirectUri}?code=second-code&state={ExpectedState}");
        }
        catch (HttpRequestException)
        {
            // Expected when the released loopback port no longer has a listener.
        }

        Assert.AreEqual("first-code", result.Callback?.Code);
        Assert.IsTrue(secondResponse is null || secondResponse.StatusCode != HttpStatusCode.OK);
        secondResponse?.Dispose();
    }

    [TestMethod]
    public async Task WaitForCallbackAsync_CancelThenRetry_ReleasesPort()
    {
        using var cts = new CancellationTokenSource();
        var first = new OAuthCallbackListener(Configuration);
        var cancelledWait = first.WaitForCallbackAsync(ExpectedState, TimeSpan.FromSeconds(3), cts.Token);
        cts.Cancel();

        Assert.AreEqual(OAuthListenOutcome.Cancelled, (await cancelledWait).Outcome);

        var retry = new OAuthCallbackListener(Configuration);
        var retryWait = retry.WaitForCallbackAsync(ExpectedState, TimeSpan.FromSeconds(3), CancellationToken.None);
        using var client = new HttpClient();
        using var response = await client.GetAsync(
            $"{Configuration.RedirectUri}?code=after-cancel&state={ExpectedState}");
        var result = await retryWait;

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual("after-cancel", result.Callback?.Code);
    }

    [TestMethod]
    public async Task WaitForCallbackAsync_TimeoutReturnsTimedOutAndReleasesPort()
    {
        var listener = new OAuthCallbackListener(Configuration);

        var result = await listener.WaitForCallbackAsync(
            ExpectedState,
            TimeSpan.FromMilliseconds(100),
            CancellationToken.None);

        Assert.AreEqual(OAuthListenOutcome.TimedOut, result.Outcome);

        var retry = new OAuthCallbackListener(Configuration);
        var retryWait = retry.WaitForCallbackAsync(ExpectedState, TimeSpan.FromSeconds(3), CancellationToken.None);
        using var client = new HttpClient();
        using var response = await client.GetAsync(
            $"{Configuration.RedirectUri}?code=after-timeout&state={ExpectedState}");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual("after-timeout", (await retryWait).Callback?.Code);
    }

    [TestMethod]
    public async Task WaitForCallbackAsync_WhenPortAlreadyOwned_ReturnsStartFailed()
    {
        using var owner = new HttpListener();
        owner.Prefixes.Add("http://127.0.0.1:43289/auth/callback/");
        owner.Start();

        var listener = new OAuthCallbackListener(Configuration);
        var result = await listener.WaitForCallbackAsync(
            ExpectedState,
            TimeSpan.FromSeconds(1),
            CancellationToken.None);

        Assert.AreEqual(OAuthListenOutcome.ListenerStartFailed, result.Outcome);
    }
}
