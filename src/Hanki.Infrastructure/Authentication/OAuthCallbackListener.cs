using System.Net;
using System.Text;
using Hanki.Infrastructure.Logging;

namespace Hanki.Infrastructure.Authentication;

/// <summary>
/// Wraps <see cref="HttpListener"/> bound to a single loopback host/port/path (e.g.
/// <c>http://127.0.0.1:43289/auth/callback/</c>). Never bound to "+", "*", or any non-loopback
/// host, so it does not require a URL ACL reservation or admin rights when run as the current
/// user on a fixed non-privileged port above 1024.
/// </summary>
public sealed class OAuthCallbackListener(AuthConfiguration configuration, IPrivacySafeLogger? logger = null)
    : IOAuthCallbackListener
{
    private const string SuccessHtml =
        """
        <!doctype html>
        <html lang="ko"><head><meta charset="utf-8"><title>한키 로그인</title></head>
        <body style="font-family:'Segoe UI',sans-serif;text-align:center;padding-top:80px;color:#241E36;">
        <h2>로그인이 완료되었습니다. 이 창을 닫아도 됩니다.</h2>
        </body></html>
        """;

    private const string FailureHtml =
        """
        <!doctype html>
        <html lang="ko"><head><meta charset="utf-8"><title>한키 로그인</title></head>
        <body style="font-family:'Segoe UI',sans-serif;text-align:center;padding-top:80px;color:#241E36;">
        <h2>로그인을 완료하지 못했습니다. 한키로 돌아가 다시 시도해주세요.</h2>
        </body></html>
        """;

    public async Task<OAuthCallbackListenResult> WaitForCallbackAsync(
        string expectedState,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(expectedState);

        var prefix = $"http://{configuration.RedirectHost}:{configuration.RedirectPort}{configuration.RedirectPath}/";
        using var listener = new HttpListener();
        listener.Prefixes.Add(prefix);

        try
        {
            listener.Start();
        }
        catch (HttpListenerException exception)
        {
            logger?.Error("Auth.Listener.Start", exception);
            return OAuthCallbackListenResult.ListenerStartFailed;
        }
        catch (ObjectDisposedException exception)
        {
            logger?.Error("Auth.Listener.Start", exception);
            return OAuthCallbackListenResult.ListenerStartFailed;
        }

        using var timeoutCts = new CancellationTokenSource(timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        using var registration = linkedCts.Token.Register(() =>
        {
            try
            {
                listener.Close();
            }
            catch (ObjectDisposedException)
            {
            }
        });

        try
        {
            while (true)
            {
                HttpListenerContext context;
                try
                {
                    context = await listener.GetContextAsync().ConfigureAwait(false);
                }
                catch (Exception exception) when (
                    exception is HttpListenerException or ObjectDisposedException or InvalidOperationException)
                {
                    if (cancellationToken.IsCancellationRequested)
                        return OAuthCallbackListenResult.Cancelled;
                    if (timeoutCts.IsCancellationRequested)
                        return OAuthCallbackListenResult.TimedOut;
                    return OAuthCallbackListenResult.Cancelled;
                }

                if (!string.Equals(context.Request.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase))
                {
                    context.Response.Headers[HttpResponseHeader.Allow] = "GET";
                    await RespondAsync(context, HttpStatusCode.MethodNotAllowed, "Method Not Allowed").ConfigureAwait(false);
                    continue;
                }

                var requestUrl = context.Request.Url;
                if (requestUrl is null ||
                    !OAuthCallbackParser.IsAcceptableRequest(
                        requestUrl,
                        configuration.RedirectHost,
                        configuration.RedirectPath,
                        configuration.RedirectPort))
                {
                    await RespondAsync(context, HttpStatusCode.NotFound, "Not Found").ConfigureAwait(false);
                    continue;
                }

                var result = OAuthCallbackParser.Parse(requestUrl.Query, expectedState);
                var isExpectedCompletion = result.Outcome is
                    OAuthCallbackOutcome.Success or
                    OAuthCallbackOutcome.ProviderAccessDenied or
                    OAuthCallbackOutcome.ProviderError;
                await RespondAsync(
                    context,
                    isExpectedCompletion ? HttpStatusCode.OK : HttpStatusCode.BadRequest,
                    result.Outcome == OAuthCallbackOutcome.Success ? SuccessHtml : FailureHtml).ConfigureAwait(false);
                return OAuthCallbackListenResult.Received(result);
            }
        }
        finally
        {
            try
            {
                listener.Stop();
                listener.Close();
            }
            catch (ObjectDisposedException)
            {
            }
        }
    }

    private static async Task RespondAsync(HttpListenerContext context, HttpStatusCode statusCode, string body)
    {
        try
        {
            var buffer = Encoding.UTF8.GetBytes(body);
            context.Response.StatusCode = (int)statusCode;
            context.Response.ContentType = "text/html; charset=utf-8";
            context.Response.ContentLength64 = buffer.Length;
            await context.Response.OutputStream.WriteAsync(buffer).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is HttpListenerException or ObjectDisposedException or IOException)
        {
            // The browser may have already disconnected; the login flow does not depend on this succeeding.
        }
        finally
        {
            context.Response.Close();
        }
    }
}
