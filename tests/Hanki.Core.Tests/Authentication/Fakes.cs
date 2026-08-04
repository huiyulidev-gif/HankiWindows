using System.Net;
using System.Net.Http;
using Hanki.Core.Authentication;
using Hanki.Infrastructure.Authentication;
using Hanki.Infrastructure.Logging;

namespace Hanki.Core.Tests.Authentication;

/// <summary>In-memory session store used by service-level tests instead of hitting DPAPI/disk.</summary>
internal sealed class FakeAuthSessionStore : IAuthSessionStore
{
    public AuthSession? Saved { get; private set; }
    public int SaveCount { get; private set; }
    public int DeleteCount { get; private set; }
    public Exception? SaveException { get; set; }
    public Exception? LoadException { get; set; }
    public Exception? DeleteException { get; set; }

    public Task SaveAsync(AuthSession session, CancellationToken cancellationToken = default)
    {
        if (SaveException is not null)
            return Task.FromException(SaveException);
        Saved = session;
        SaveCount++;
        return Task.CompletedTask;
    }

    public Task<AuthSession?> LoadAsync(CancellationToken cancellationToken = default) =>
        LoadException is null
            ? Task.FromResult(Saved)
            : Task.FromException<AuthSession?>(LoadException);

    public Task DeleteAsync(CancellationToken cancellationToken = default)
    {
        if (DeleteException is not null)
            return Task.FromException(DeleteException);
        Saved = null;
        DeleteCount++;
        return Task.CompletedTask;
    }
}

/// <summary>Records every URL that would have been opened, without ever starting a real process.</summary>
internal sealed class FakeBrowserLauncher : ISystemBrowserLauncher
{
    public List<string> LaunchedUrls { get; } = [];
    public Exception? ThrowOnLaunch { get; set; }

    public void Launch(string url)
    {
        if (ThrowOnLaunch is not null)
            throw ThrowOnLaunch;
        LaunchedUrls.Add(url);
    }
}

/// <summary>Returns a scripted <see cref="OAuthCallbackListenResult"/> instead of opening a real socket.</summary>
internal sealed class FakeOAuthCallbackListener : IOAuthCallbackListener
{
    private readonly Func<string, OAuthCallbackListenResult> _resultFactory;

    public FakeOAuthCallbackListener(OAuthCallbackListenResult result) : this(_ => result)
    {
    }

    public FakeOAuthCallbackListener(Func<string, OAuthCallbackListenResult> resultFactory)
    {
        _resultFactory = resultFactory;
    }

    public string? ObservedExpectedState { get; private set; }

    public Task<OAuthCallbackListenResult> WaitForCallbackAsync(
        string expectedState,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        ObservedExpectedState = expectedState;
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_resultFactory(expectedState));
    }
}

/// <summary>Fully scriptable listener for tests that need to observe/react to the cancellation token.</summary>
internal sealed class DelegatingOAuthCallbackListener(
    Func<string, TimeSpan, CancellationToken, Task<OAuthCallbackListenResult>> handler) : IOAuthCallbackListener
{
    public Task<OAuthCallbackListenResult> WaitForCallbackAsync(
        string expectedState,
        TimeSpan timeout,
        CancellationToken cancellationToken) =>
        handler(expectedState, timeout, cancellationToken);
}

/// <summary>Captures every logged event/location string so tests can assert no sensitive value ever appears.</summary>
internal sealed class FakePrivacySafeLogger : IPrivacySafeLogger
{
    public List<string> InfoEvents { get; } = [];
    public List<(string Location, Exception Exception)> Errors { get; } = [];

    public void Error(string location, Exception exception)
    {
        Errors.Add((location, exception));
    }

    public void Info(string location)
    {
        InfoEvents.Add(location);
    }

    /// <summary>True if any logged location/message string contains the given sensitive substring.</summary>
    public bool AnyLogContains(string sensitiveValue)
    {
        if (InfoEvents.Any(entry => entry.Contains(sensitiveValue, StringComparison.Ordinal)))
            return true;
        if (Errors.Any(entry =>
                entry.Location.Contains(sensitiveValue, StringComparison.Ordinal) ||
                entry.Exception.Message.Contains(sensitiveValue, StringComparison.Ordinal) ||
                entry.Exception.ToString().Contains(sensitiveValue, StringComparison.Ordinal)))
            return true;
        return false;
    }
}

/// <summary>Scripted HTTP responses keyed by request path, so token-exchange/refresh calls never hit the network.</summary>
internal sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

    public FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        _responder = responder;
    }

    public List<HttpRequestMessage> Requests { get; } = [];
    public List<string> RequestBodies { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        Requests.Add(request);
        if (request.Content is not null)
            RequestBodies.Add(await request.Content.ReadAsStringAsync(cancellationToken));
        return _responder(request);
    }

    public static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string json) => new(statusCode)
    {
        Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
    };
}
