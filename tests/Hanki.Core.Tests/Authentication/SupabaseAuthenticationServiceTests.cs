using System.Net;
using System.Net.Http;
using Hanki.Core.Authentication;
using Hanki.Infrastructure.Authentication;

namespace Hanki.Core.Tests.Authentication;

[TestClass]
public sealed class SupabaseAuthenticationServiceTests
{
    private static readonly AuthConfiguration FakeConfiguration = new(
        "https://example.test",
        "fake-anon-key",
        "http://127.0.0.1:43289/auth/callback");

    private const string ValidTokenResponseJson = """
        {
          "access_token": "fake-access-token",
          "refresh_token": "fake-refresh-token",
          "expires_in": 3600,
          "token_type": "bearer",
          "user": {
            "id": "user-123",
            "email": "test@example.test",
            "user_metadata": {
              "full_name": "테스트 사용자",
              "avatar_url": "https://example.test/avatar.png"
            }
          }
        }
        """;

    [TestMethod]
    public async Task LoginAsync_NotConfigured_ReturnsConfigMissingWithoutTouchingBrowserOrNetwork()
    {
        var browserLauncher = new FakeBrowserLauncher();
        var handler = new FakeHttpMessageHandler(_ => throw new InvalidOperationException("should not be called"));
        var service = BuildService(handler, configured: false, browserLauncher: browserLauncher);

        var result = await service.LoginAsync();

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(AuthErrorCode.ConfigMissing, result.ErrorCode);
        Assert.AreEqual("로그인 설정이 없습니다.", result.ErrorMessage);
        Assert.AreEqual(0, browserLauncher.LaunchedUrls.Count);
        Assert.IsFalse(service.IsConfigured);
    }

    [TestMethod]
    public async Task LoginAsync_SuccessfulFlow_ExchangesCodeAndSavesSession()
    {
        var handler = new FakeHttpMessageHandler(_ =>
            FakeHttpMessageHandler.JsonResponse(HttpStatusCode.OK, ValidTokenResponseJson));
        var sessionStore = new FakeAuthSessionStore();
        var browserLauncher = new FakeBrowserLauncher();
        string? observedState = null;
        var listener = new FakeOAuthCallbackListener(state =>
        {
            observedState = state;
            return OAuthCallbackListenResult.Received(
                new OAuthCallbackResult(OAuthCallbackOutcome.Success, "fake-auth-code", state, null, null));
        });
        var service = BuildService(handler, sessionStore: sessionStore, browserLauncher: browserLauncher, listenerFactory: _ => listener);

        var result = await service.LoginAsync();

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(AuthenticationState.LoggedIn, service.State);
        Assert.AreEqual("user-123", service.CurrentUser?.Id);
        Assert.AreEqual("테스트 사용자", service.CurrentUser?.Name);
        Assert.AreEqual("test@example.test", service.CurrentUser?.Email);
        Assert.AreEqual(1, sessionStore.SaveCount);
        Assert.AreEqual(1, browserLauncher.LaunchedUrls.Count);
        StringAssert.Contains(browserLauncher.LaunchedUrls[0], "provider=google");
        StringAssert.Contains(browserLauncher.LaunchedUrls[0], $"{FakeConfiguration.SupabaseUrl}/auth/v1/authorize");
        var authorizeQuery = ParseQuery(new Uri(browserLauncher.LaunchedUrls[0]).Query);
        Assert.AreEqual("google", authorizeQuery["provider"].Single());
        CollectionAssert.AreEqual(new[] { "select_account" }, authorizeQuery["prompt"]);
        Assert.IsFalse(authorizeQuery.ContainsKey("consent"));
        Assert.IsFalse(authorizeQuery.ContainsKey("login_hint"));
        var redirectUri = new Uri(authorizeQuery["redirect_to"].Single());
        Assert.AreEqual(FakeConfiguration.RedirectUri, $"{redirectUri.Scheme}://{redirectUri.Authority}{redirectUri.AbsolutePath}");
        Assert.AreEqual(observedState, ParseQuery(redirectUri.Query)["state"].Single());
        Assert.AreEqual("s256", authorizeQuery["code_challenge_method"].Single());
        StringAssert.Matches(authorizeQuery["code_challenge"].Single(), new System.Text.RegularExpressions.Regex("^[A-Za-z0-9_-]{43}$"));

        var requestBody = handler.RequestBodies.Single();
        StringAssert.Contains(requestBody, "\"auth_code\":\"fake-auth-code\"");
        Assert.AreEqual("fake-anon-key", handler.Requests[0].Headers.GetValues("apikey").Single());
    }

    [TestMethod]
    public async Task LoginAsync_GoogleAuthorizeUrl_UsesAccountPickerWithoutConsentOrHint()
    {
        var browserLauncher = new FakeBrowserLauncher();
        var handler = new FakeHttpMessageHandler(_ => throw new InvalidOperationException("should not be called"));
        var listener = new FakeOAuthCallbackListener(OAuthCallbackListenResult.TimedOut);
        var service = BuildService(handler, browserLauncher: browserLauncher, listenerFactory: _ => listener);

        var result = await service.LoginAsync();

        Assert.AreEqual(AuthErrorCode.Timeout, result.ErrorCode);
        var query = ParseQuery(new Uri(browserLauncher.LaunchedUrls.Single()).Query);
        CollectionAssert.AreEqual(new[] { "select_account" }, query["prompt"]);
        Assert.IsFalse(query.ContainsKey("consent"));
        Assert.IsFalse(query.ContainsKey("login_hint"));
        Assert.AreEqual(1, query["prompt"].Count);
    }

    [TestMethod]
    public async Task LoginAsync_SecondConcurrentAttempt_IsBlockedAndDoesNotOpenSecondBrowser()
    {
        var gate = new TaskCompletionSource();
        var listener = new DelegatingOAuthCallbackListener(async (state, _, _) =>
        {
            await gate.Task;
            return OAuthCallbackListenResult.Received(
                new OAuthCallbackResult(OAuthCallbackOutcome.Success, "fake-auth-code", state, null, null));
        });
        var handler = new FakeHttpMessageHandler(_ =>
            FakeHttpMessageHandler.JsonResponse(HttpStatusCode.OK, ValidTokenResponseJson));
        var browserLauncher = new FakeBrowserLauncher();
        var service = BuildService(handler, browserLauncher: browserLauncher, listenerFactory: _ => listener);

        var firstLoginTask = service.LoginAsync();
        await WaitUntil(() => browserLauncher.LaunchedUrls.Count > 0);

        var secondResult = await service.LoginAsync();

        Assert.AreEqual(AuthErrorCode.AlreadyInProgress, secondResult.ErrorCode);
        Assert.AreEqual(1, browserLauncher.LaunchedUrls.Count);

        gate.SetResult();
        var firstResult = await firstLoginTask;
        Assert.IsTrue(firstResult.IsSuccess);
    }

    [TestMethod]
    public async Task LoginAsync_BrowserLaunchThrows_ReturnsBrowserLaunchFailed()
    {
        var browserLauncher = new FakeBrowserLauncher { ThrowOnLaunch = new InvalidOperationException("no shell registered") };
        var listener = new FakeOAuthCallbackListener(_ => OAuthCallbackListenResult.Cancelled);
        var handler = new FakeHttpMessageHandler(_ => throw new InvalidOperationException("should not be called"));
        var service = BuildService(handler, browserLauncher: browserLauncher, listenerFactory: _ => listener);

        var result = await service.LoginAsync();

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(AuthErrorCode.BrowserLaunchFailed, result.ErrorCode);
        Assert.AreEqual(AuthenticationState.LoggedOut, service.State);
    }

    [TestMethod]
    public async Task LoginAsync_BrowserLaunchThrows_WaitsForListenerCancellation()
    {
        var cancellationObserved = false;
        var listener = new DelegatingOAuthCallbackListener((_, _, cancellationToken) =>
        {
            var completion = new TaskCompletionSource<OAuthCallbackListenResult>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            cancellationToken.Register(() =>
            {
                cancellationObserved = true;
                completion.TrySetResult(OAuthCallbackListenResult.Cancelled);
            });
            return completion.Task;
        });
        var browser = new FakeBrowserLauncher { ThrowOnLaunch = new InvalidOperationException("test failure") };
        var handler = new FakeHttpMessageHandler(_ => throw new InvalidOperationException("should not be called"));
        var service = BuildService(handler, browserLauncher: browser, listenerFactory: _ => listener);

        var result = await service.LoginAsync();

        Assert.AreEqual(AuthErrorCode.BrowserLaunchFailed, result.ErrorCode);
        Assert.IsTrue(cancellationObserved);
    }

    [TestMethod]
    public async Task LoginAsync_ListenerTimesOut_ReturnsTimeoutMessage()
    {
        var listener = new FakeOAuthCallbackListener(_ => OAuthCallbackListenResult.TimedOut);
        var handler = new FakeHttpMessageHandler(_ => throw new InvalidOperationException("should not be called"));
        var service = BuildService(handler, listenerFactory: _ => listener);

        var result = await service.LoginAsync();

        Assert.AreEqual(AuthErrorCode.Timeout, result.ErrorCode);
        Assert.AreEqual("로그인 시간이 초과되었습니다. 다시 시도해주세요.", result.ErrorMessage);
    }

    [TestMethod]
    public async Task LoginAsync_ListenerReportsCancelled_ReturnsCancelledWithoutExchangingCode()
    {
        var listener = new FakeOAuthCallbackListener(_ => OAuthCallbackListenResult.Cancelled);
        var handler = new FakeHttpMessageHandler(_ => throw new InvalidOperationException("should not be called"));
        var service = BuildService(handler, listenerFactory: _ => listener);

        var result = await service.LoginAsync();

        Assert.IsTrue(result.IsCancelled);
        Assert.AreEqual("로그인이 취소되었습니다.", result.ErrorMessage);
        Assert.AreEqual(0, handler.Requests.Count);
    }

    [TestMethod]
    public async Task LoginAsync_ExternalCancellation_ReturnsCancelledWithoutExchangingCode()
    {
        var tcs = new TaskCompletionSource<OAuthCallbackListenResult>();
        var listener = new DelegatingOAuthCallbackListener((_, _, ct) =>
        {
            ct.Register(() => tcs.TrySetResult(OAuthCallbackListenResult.Cancelled));
            return tcs.Task;
        });
        var handler = new FakeHttpMessageHandler(_ => throw new InvalidOperationException("should not be called"));
        var sessionStore = new FakeAuthSessionStore();
        var service = BuildService(handler, sessionStore: sessionStore, listenerFactory: _ => listener);

        using var cts = new CancellationTokenSource();
        var loginTask = service.LoginAsync(cts.Token);
        await Task.Delay(30);
        cts.Cancel();
        var result = await loginTask;

        Assert.IsTrue(result.IsCancelled);
        Assert.AreEqual(0, handler.Requests.Count);
        Assert.AreEqual(0, sessionStore.SaveCount);
    }

    [TestMethod]
    public async Task LoginAsync_ProviderAccessDenied_IsTreatedAsUserCancelled()
    {
        var listener = new FakeOAuthCallbackListener(state =>
            OAuthCallbackListenResult.Received(new OAuthCallbackResult(
                OAuthCallbackOutcome.ProviderAccessDenied, null, state, "access_denied", "user denied")));
        var handler = new FakeHttpMessageHandler(_ => throw new InvalidOperationException("should not be called"));
        var service = BuildService(handler, listenerFactory: _ => listener);

        var result = await service.LoginAsync();

        Assert.IsTrue(result.IsCancelled);
        Assert.AreEqual(0, handler.Requests.Count);
    }

    [TestMethod]
    public async Task LoginAsync_StateMismatch_ReturnsMalformedCallbackMessageWithoutExchangingCode()
    {
        var listener = new FakeOAuthCallbackListener(_ =>
            OAuthCallbackListenResult.Received(new OAuthCallbackResult(
                OAuthCallbackOutcome.StateMismatch, "fake-auth-code", "wrong-state", null, null)));
        var handler = new FakeHttpMessageHandler(_ => throw new InvalidOperationException("should not be called"));
        var service = BuildService(handler, listenerFactory: _ => listener);

        var result = await service.LoginAsync();

        Assert.AreEqual(AuthErrorCode.StateMismatch, result.ErrorCode);
        Assert.AreEqual("로그인 요청을 확인할 수 없습니다. 다시 로그인해주세요.", result.ErrorMessage);
        Assert.AreEqual(0, handler.Requests.Count);
    }

    [TestMethod]
    [DataRow(OAuthCallbackOutcome.MissingCode, AuthErrorCode.MalformedCallback)]
    [DataRow(OAuthCallbackOutcome.MissingState, AuthErrorCode.MalformedCallback)]
    [DataRow(OAuthCallbackOutcome.ProviderError, AuthErrorCode.ProviderError)]
    public async Task LoginAsync_InvalidCallback_MapsSpecificSafeError(
        OAuthCallbackOutcome callbackOutcome,
        AuthErrorCode expectedError)
    {
        var listener = new FakeOAuthCallbackListener(state =>
            OAuthCallbackListenResult.Received(new OAuthCallbackResult(
                callbackOutcome,
                null,
                state,
                callbackOutcome == OAuthCallbackOutcome.ProviderError ? "server_error" : null,
                null)));
        var handler = new FakeHttpMessageHandler(_ => throw new InvalidOperationException("should not be called"));
        var service = BuildService(handler, listenerFactory: _ => listener);

        var result = await service.LoginAsync();

        Assert.AreEqual(expectedError, result.ErrorCode);
        Assert.AreEqual("로그인 요청을 확인할 수 없습니다. 다시 로그인해주세요.", result.ErrorMessage);
        Assert.AreEqual(0, handler.Requests.Count);
    }

    [TestMethod]
    public async Task LoginAsync_TokenExchangeHttpFailure_ReturnsNetworkMessage()
    {
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("""{"error":"invalid_grant"}""")
        });
        var listener = new FakeOAuthCallbackListener(state =>
            OAuthCallbackListenResult.Received(new OAuthCallbackResult(OAuthCallbackOutcome.Success, "fake-auth-code", state, null, null)));
        var service = BuildService(handler, listenerFactory: _ => listener);

        var result = await service.LoginAsync();

        Assert.AreEqual(AuthErrorCode.Network, result.ErrorCode);
        Assert.AreEqual("인터넷 연결을 확인한 뒤 다시 시도해주세요.", result.ErrorMessage);
        Assert.AreEqual(AuthenticationState.LoggedOut, service.State);
    }

    [TestMethod]
    public async Task LoginAsync_HttpClientThrows_ReturnsNetworkMessage()
    {
        var handler = new FakeHttpMessageHandler(_ => throw new HttpRequestException("connection refused"));
        var listener = new FakeOAuthCallbackListener(state =>
            OAuthCallbackListenResult.Received(new OAuthCallbackResult(OAuthCallbackOutcome.Success, "fake-auth-code", state, null, null)));
        var service = BuildService(handler, listenerFactory: _ => listener);

        var result = await service.LoginAsync();

        Assert.AreEqual(AuthErrorCode.Network, result.ErrorCode);
    }

    [TestMethod]
    public async Task LoginAsync_ResponseMissingUser_ReturnsInvalidResponseMessage()
    {
        var handler = new FakeHttpMessageHandler(_ => FakeHttpMessageHandler.JsonResponse(HttpStatusCode.OK, """
            { "access_token": "fake-access-token", "refresh_token": "fake-refresh-token", "expires_in": 3600 }
            """));
        var listener = new FakeOAuthCallbackListener(state =>
            OAuthCallbackListenResult.Received(new OAuthCallbackResult(OAuthCallbackOutcome.Success, "fake-auth-code", state, null, null)));
        var service = BuildService(handler, listenerFactory: _ => listener);

        var result = await service.LoginAsync();

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(AuthErrorCode.InvalidResponse, result.ErrorCode);
    }

    [TestMethod]
    public async Task LoginAsync_ProfileMissingNameAndAvatar_FallsBackToEmailLocalPartAndNullAvatar()
    {
        var handler = new FakeHttpMessageHandler(_ => FakeHttpMessageHandler.JsonResponse(HttpStatusCode.OK, """
            {
              "access_token": "fake-access-token",
              "refresh_token": "fake-refresh-token",
              "expires_in": 3600,
              "user": { "id": "user-456", "email": "someone@example.test", "user_metadata": {} }
            }
            """));
        var listener = new FakeOAuthCallbackListener(state =>
            OAuthCallbackListenResult.Received(new OAuthCallbackResult(OAuthCallbackOutcome.Success, "fake-auth-code", state, null, null)));
        var service = BuildService(handler, listenerFactory: _ => listener);

        var result = await service.LoginAsync();

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual("someone", result.Session!.User.Name);
        Assert.IsNull(result.Session.User.AvatarUrl);
    }

    [TestMethod]
    public async Task LoginAsync_HttpAvatarUrl_IsDiscarded()
    {
        var handler = new FakeHttpMessageHandler(_ => FakeHttpMessageHandler.JsonResponse(HttpStatusCode.OK, """
            {
              "access_token": "fake-access-token",
              "refresh_token": "fake-refresh-token",
              "expires_in": 3600,
              "user": {
                "id": "user-456",
                "email": "someone@example.test",
                "user_metadata": { "avatar_url": "http://example.test/avatar.png" }
              }
            }
            """));
        var listener = new FakeOAuthCallbackListener(state =>
            OAuthCallbackListenResult.Received(new OAuthCallbackResult(
                OAuthCallbackOutcome.Success,
                "fake-auth-code",
                state,
                null,
                null)));
        var service = BuildService(handler, listenerFactory: _ => listener);

        var result = await service.LoginAsync();

        Assert.IsTrue(result.IsSuccess);
        Assert.IsNull(result.Session!.User.AvatarUrl);
    }

    [TestMethod]
    public async Task LoginAsync_SessionSaveFails_ReturnsStorageErrorAndRemainsLoggedOut()
    {
        var store = new FakeAuthSessionStore { SaveException = new IOException("test disk failure") };
        var handler = new FakeHttpMessageHandler(_ =>
            FakeHttpMessageHandler.JsonResponse(HttpStatusCode.OK, ValidTokenResponseJson));
        var listener = new FakeOAuthCallbackListener(state =>
            OAuthCallbackListenResult.Received(new OAuthCallbackResult(
                OAuthCallbackOutcome.Success,
                "fake-auth-code",
                state,
                null,
                null)));
        var service = BuildService(handler, sessionStore: store, listenerFactory: _ => listener);

        var result = await service.LoginAsync();

        Assert.AreEqual(AuthErrorCode.Storage, result.ErrorCode);
        Assert.AreEqual(AuthenticationState.LoggedOut, service.State);
        Assert.IsNull(service.CurrentUser);
        Assert.AreEqual(1, store.DeleteCount);
    }

    [TestMethod]
    public async Task LoginAsync_NeverLogsSensitiveValues()
    {
        var logger = new FakePrivacySafeLogger();
        var handler = new FakeHttpMessageHandler(_ =>
            FakeHttpMessageHandler.JsonResponse(HttpStatusCode.OK, ValidTokenResponseJson));
        var listener = new FakeOAuthCallbackListener(state =>
            OAuthCallbackListenResult.Received(new OAuthCallbackResult(OAuthCallbackOutcome.Success, "super-secret-auth-code", state, null, null)));
        var service = BuildService(handler, logger: logger, listenerFactory: _ => listener);

        var result = await service.LoginAsync();

        Assert.IsTrue(result.IsSuccess);
        Assert.IsTrue(logger.InfoEvents.Contains("auth.login.started"));
        Assert.IsTrue(logger.InfoEvents.Contains("auth.login.succeeded"));
        Assert.IsFalse(logger.AnyLogContains("super-secret-auth-code"));
        Assert.IsFalse(logger.AnyLogContains("fake-access-token"));
        Assert.IsFalse(logger.AnyLogContains("fake-refresh-token"));
        Assert.IsFalse(logger.AnyLogContains(result.Session!.AccessToken));
        Assert.IsFalse(logger.AnyLogContains(FakeConfiguration.SupabaseUrl));
    }

    [TestMethod]
    public async Task RestoreSessionAsync_UnexpiredSession_SetsLoggedInWithoutNetworkCall()
    {
        var sessionStore = new FakeAuthSessionStore();
        await sessionStore.SaveAsync(new AuthSession
        {
            AccessToken = "fake-access-token",
            RefreshToken = "fake-refresh-token",
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(1),
            User = new AuthUser("user-123", "test@example.test", "테스트 사용자", null)
        });
        var handler = new FakeHttpMessageHandler(_ => throw new InvalidOperationException("should not be called"));
        var service = BuildService(handler, sessionStore: sessionStore);
        var observedStates = new List<AuthenticationState>();
        service.StateChanged += (_, _) => observedStates.Add(service.State);

        await service.RestoreSessionAsync();

        Assert.AreEqual(AuthenticationState.LoggedIn, service.State);
        Assert.AreEqual("user-123", service.CurrentUser?.Id);
        Assert.AreEqual(0, handler.Requests.Count);
        CollectionAssert.AreEqual(
            new[] { AuthenticationState.Restoring, AuthenticationState.LoggedIn },
            observedStates);
    }

    [TestMethod]
    public async Task RestoreSessionAsync_ExpiringSoon_RefreshesInBackground()
    {
        var sessionStore = new FakeAuthSessionStore();
        await sessionStore.SaveAsync(new AuthSession
        {
            AccessToken = "old-access-token",
            RefreshToken = "fake-refresh-token",
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(10),
            User = new AuthUser("user-123", "test@example.test", "테스트 사용자", null)
        });
        var handler = new FakeHttpMessageHandler(_ =>
            FakeHttpMessageHandler.JsonResponse(HttpStatusCode.OK, ValidTokenResponseJson));
        var service = BuildService(handler, sessionStore: sessionStore);

        await service.RestoreSessionAsync();

        Assert.AreEqual(AuthenticationState.LoggedIn, service.State);
        Assert.AreEqual("fake-access-token", sessionStore.Saved?.AccessToken);
        StringAssert.Contains(handler.RequestBodies.Single(), "refresh_token");
    }

    [TestMethod]
    public async Task RestoreSessionAsync_RefreshFails_ClearsSessionAndSetsNotice()
    {
        var sessionStore = new FakeAuthSessionStore();
        await sessionStore.SaveAsync(new AuthSession
        {
            AccessToken = "old-access-token",
            RefreshToken = "expired-refresh-token",
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(1),
            User = new AuthUser("user-123", "test@example.test", "테스트 사용자", null)
        });
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("""{"error":"invalid_grant"}""")
        });
        var service = BuildService(handler, sessionStore: sessionStore);

        await service.RestoreSessionAsync();

        Assert.AreEqual(AuthenticationState.LoggedOut, service.State);
        Assert.IsNull(service.CurrentUser);
        Assert.AreEqual(1, sessionStore.DeleteCount);
        Assert.AreEqual("저장된 로그인 정보가 만료되었습니다. 다시 로그인해주세요.", service.LastNotice);
    }

    [TestMethod]
    public async Task RestoreSessionAsync_NoStoredSession_StaysLoggedOut()
    {
        var handler = new FakeHttpMessageHandler(_ => throw new InvalidOperationException("should not be called"));
        var service = BuildService(handler);

        await service.RestoreSessionAsync();

        Assert.AreEqual(AuthenticationState.LoggedOut, service.State);
    }

    [TestMethod]
    public async Task RestoreSessionAsync_StoreReadFails_ReturnsLoggedOutWithNotice()
    {
        var store = new FakeAuthSessionStore { LoadException = new IOException("test read failure") };
        var handler = new FakeHttpMessageHandler(_ => throw new InvalidOperationException("should not be called"));
        var service = BuildService(handler, sessionStore: store);

        await service.RestoreSessionAsync();

        Assert.AreEqual(AuthenticationState.LoggedOut, service.State);
        Assert.IsNull(service.CurrentUser);
        Assert.AreEqual("저장된 로그인 정보를 읽지 못했습니다. 다시 로그인해주세요.", service.LastNotice);
    }

    [TestMethod]
    public async Task LogoutAsync_ClearsSessionAndState()
    {
        var sessionStore = new FakeAuthSessionStore();
        await sessionStore.SaveAsync(new AuthSession
        {
            AccessToken = "fake-access-token",
            RefreshToken = "fake-refresh-token",
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(1),
            User = new AuthUser("user-123", "test@example.test", "테스트 사용자", null)
        });
        var handler = new FakeHttpMessageHandler(_ => throw new InvalidOperationException("should not be called"));
        var service = BuildService(handler, sessionStore: sessionStore);
        await service.RestoreSessionAsync();
        Assert.AreEqual(AuthenticationState.LoggedIn, service.State);

        await service.LogoutAsync();

        Assert.AreEqual(AuthenticationState.LoggedOut, service.State);
        Assert.IsNull(service.CurrentUser);
        Assert.AreEqual(1, sessionStore.DeleteCount);
    }

    [TestMethod]
    public async Task LogoutAsync_StoreDeleteFails_StillLogsOutAndSetsNotice()
    {
        var store = new FakeAuthSessionStore();
        await store.SaveAsync(new AuthSession
        {
            AccessToken = "fake-access-token",
            RefreshToken = "fake-refresh-token",
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(1),
            User = new AuthUser("user-123", "test@example.test", "테스트 사용자", null)
        });
        var handler = new FakeHttpMessageHandler(_ => throw new InvalidOperationException("should not be called"));
        var service = BuildService(handler, sessionStore: store);
        await service.RestoreSessionAsync();
        store.DeleteException = new IOException("test delete failure");

        await service.LogoutAsync();

        Assert.AreEqual(AuthenticationState.LoggedOut, service.State);
        Assert.IsNull(service.CurrentUser);
        Assert.AreEqual(
            "저장된 로그인 정보를 삭제하지 못했습니다. 앱을 다시 시작한 뒤 다시 시도해주세요.",
            service.LastNotice);
    }

    [TestMethod]
    public async Task LogoutAsync_Twice_IsIdempotent()
    {
        var store = new FakeAuthSessionStore();
        var handler = new FakeHttpMessageHandler(_ => throw new InvalidOperationException("should not be called"));
        var service = BuildService(handler, sessionStore: store);

        await service.LogoutAsync();
        await service.LogoutAsync();

        Assert.AreEqual(AuthenticationState.LoggedOut, service.State);
        Assert.IsNull(service.CurrentUser);
        Assert.AreEqual(2, store.DeleteCount);
    }

    private static SupabaseAuthenticationService BuildService(
        FakeHttpMessageHandler handler,
        bool configured = true,
        FakeAuthSessionStore? sessionStore = null,
        FakeBrowserLauncher? browserLauncher = null,
        FakePrivacySafeLogger? logger = null,
        Func<AuthConfiguration, IOAuthCallbackListener>? listenerFactory = null)
    {
        return new SupabaseAuthenticationService(
            configured ? FakeConfiguration : null,
            new HttpClient(handler),
            sessionStore ?? new FakeAuthSessionStore(),
            browserLauncher ?? new FakeBrowserLauncher(),
            logger ?? new FakePrivacySafeLogger(),
            listenerFactory ?? (_ => new FakeOAuthCallbackListener(OAuthCallbackListenResult.TimedOut)));
    }

    private static Dictionary<string, List<string>> ParseQuery(string query)
    {
        var result = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            var key = Uri.UnescapeDataString(parts[0]);
            var value = parts.Length == 2 ? Uri.UnescapeDataString(parts[1]) : string.Empty;
            if (!result.TryGetValue(key, out var values))
            {
                values = [];
                result[key] = values;
            }

            values.Add(value);
        }

        return result;
    }

    private static async Task WaitUntil(Func<bool> condition, int timeoutMs = 2000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException("Condition was not met in time.");
            await Task.Delay(10);
        }
    }
}
