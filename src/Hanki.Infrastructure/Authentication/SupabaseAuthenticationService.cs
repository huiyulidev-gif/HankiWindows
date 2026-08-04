using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Hanki.Core.Authentication;
using Hanki.Infrastructure.Logging;

namespace Hanki.Infrastructure.Authentication;

/// <summary>
/// Orchestrates Google login via Supabase Auth: PKCE generation, system-browser launch, the
/// loopback callback listener, code-for-session exchange, refresh, and persistence. No Google
/// client secret and no Supabase service_role key are ever referenced here -- only the public
/// project URL and publishable/anon key, matching what the Yulbyte website uses.
/// </summary>
public sealed class SupabaseAuthenticationService : IAuthenticationService
{
    private static readonly TimeSpan LoginTimeout = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan RefreshBuffer = TimeSpan.FromSeconds(60);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly AuthConfiguration? _configuration;
    private readonly HttpClient _httpClient;
    private readonly IAuthSessionStore _sessionStore;
    private readonly ISystemBrowserLauncher _browserLauncher;
    private readonly IPrivacySafeLogger? _logger;
    private readonly Func<AuthConfiguration, IOAuthCallbackListener> _listenerFactory;
    private readonly OAuthPkceService _pkce;

    private int _loginInProgress;
    private volatile AuthUser? _currentUser;
    private volatile string? _lastNotice;

    public SupabaseAuthenticationService(
        AuthConfiguration? configuration,
        HttpClient httpClient,
        IAuthSessionStore sessionStore,
        ISystemBrowserLauncher browserLauncher,
        IPrivacySafeLogger? logger = null,
        Func<AuthConfiguration, IOAuthCallbackListener>? listenerFactory = null,
        OAuthPkceService? pkceService = null)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(sessionStore);
        ArgumentNullException.ThrowIfNull(browserLauncher);

        _configuration = configuration;
        _httpClient = httpClient;
        _sessionStore = sessionStore;
        _browserLauncher = browserLauncher;
        _logger = logger;
        _listenerFactory = listenerFactory ?? (config => new OAuthCallbackListener(config, logger));
        _pkce = pkceService ?? new OAuthPkceService();
    }

    public bool IsConfigured => _configuration is not null;
    public AuthenticationState State { get; private set; } = AuthenticationState.LoggedOut;
    public AuthUser? CurrentUser => _currentUser;
    public string? LastNotice => _lastNotice;
    public event EventHandler? StateChanged;

    public async Task<AuthResult> LoginAsync(CancellationToken cancellationToken = default)
    {
        if (_configuration is null)
            return AuthResult.Error(AuthErrorCode.ConfigMissing, "로그인 설정이 없습니다.");

        if (Interlocked.CompareExchange(ref _loginInProgress, 1, 0) != 0)
            return AuthResult.Error(AuthErrorCode.AlreadyInProgress, "이미 로그인이 진행 중입니다.");

        try
        {
            _lastNotice = null;
            SetState(AuthenticationState.LoggingIn);
            _logger?.Info("auth.login.started");

            var verifier = _pkce.GenerateCodeVerifier();
            var challenge = _pkce.GenerateCodeChallenge(verifier);
            var state = _pkce.GenerateState();

            // Linked so a browser-launch failure below can stop the listener immediately instead
            // of leaving it bound to the port for the full login timeout.
            using var listenerCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var listener = _listenerFactory(_configuration);
            var listenTask = listener.WaitForCallbackAsync(state, LoginTimeout, listenerCts.Token);

            var authorizeUrl = BuildAuthorizeUrl(_configuration, challenge, state);
            try
            {
                _browserLauncher.Launch(authorizeUrl);
            }
            catch (Exception exception)
            {
                _logger?.Error("Auth.Browser.Launch", exception);
                listenerCts.Cancel();
                await ObserveListenerShutdownAsync(listenTask).ConfigureAwait(false);
                SetState(AuthenticationState.LoggedOut);
                return AuthResult.Error(AuthErrorCode.BrowserLaunchFailed, "브라우저를 열지 못했습니다. 다시 시도해주세요.");
            }

            OAuthCallbackListenResult listenResult;
            try
            {
                listenResult = await listenTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                _logger?.Info("auth.login.cancelled");
                SetState(AuthenticationState.LoggedOut);
                return AuthResult.Cancelled();
            }
            catch (Exception exception)
            {
                _logger?.Error("Auth.Listener.Wait", exception);
                SetState(AuthenticationState.LoggedOut);
                return AuthResult.Error(AuthErrorCode.Unknown, "로그인을 시작하지 못했습니다. 다시 시도해주세요.");
            }

            switch (listenResult.Outcome)
            {
                case OAuthListenOutcome.Cancelled:
                    _logger?.Info("auth.login.cancelled");
                    SetState(AuthenticationState.LoggedOut);
                    return AuthResult.Cancelled();
                case OAuthListenOutcome.TimedOut:
                    _logger?.Info("auth.login.timeout");
                    SetState(AuthenticationState.LoggedOut);
                    return AuthResult.Error(AuthErrorCode.Timeout, "로그인 시간이 초과되었습니다. 다시 시도해주세요.");
                case OAuthListenOutcome.ListenerStartFailed:
                    _logger?.Info("auth.login.listener_start_failed");
                    SetState(AuthenticationState.LoggedOut);
                    return AuthResult.Error(AuthErrorCode.Unknown, "로그인을 시작하지 못했습니다. 다시 시도해주세요.");
            }

            var callback = listenResult.Callback!;
            if (callback.Outcome == OAuthCallbackOutcome.ProviderAccessDenied)
            {
                _logger?.Info("auth.login.cancelled");
                SetState(AuthenticationState.LoggedOut);
                return AuthResult.Cancelled();
            }
            if (callback.Outcome != OAuthCallbackOutcome.Success)
            {
                _logger?.Info("auth.login.invalid_callback");
                SetState(AuthenticationState.LoggedOut);
                var errorCode = callback.Outcome switch
                {
                    OAuthCallbackOutcome.StateMismatch => AuthErrorCode.StateMismatch,
                    OAuthCallbackOutcome.ProviderError => AuthErrorCode.ProviderError,
                    _ => AuthErrorCode.MalformedCallback
                };
                return AuthResult.Error(errorCode, "로그인 요청을 확인할 수 없습니다. 다시 로그인해주세요.");
            }

            AuthSession session;
            try
            {
                session = await ExchangeCodeAsync(_configuration, callback.Code!, verifier, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger?.Info("auth.login.cancelled");
                SetState(AuthenticationState.LoggedOut);
                return AuthResult.Cancelled();
            }
            catch (AuthProtocolException protocolException)
            {
                _logger?.Info("auth.token.exchange_failed");
                SetState(AuthenticationState.LoggedOut);
                return AuthResult.Error(protocolException.ErrorCode, protocolException.Message);
            }
            catch (Exception exception)
            {
                _logger?.Error("Auth.TokenExchange", exception);
                SetState(AuthenticationState.LoggedOut);
                return AuthResult.Error(AuthErrorCode.Network, "인터넷 연결을 확인한 뒤 다시 시도해주세요.");
            }

            try
            {
                await _sessionStore.SaveAsync(session, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                await TryDeleteStoredSessionAsync().ConfigureAwait(false);
                SetState(AuthenticationState.LoggedOut);
                return AuthResult.Cancelled();
            }
            catch (Exception exception)
            {
                _logger?.Error("Auth.Session.Save", exception);
                await TryDeleteStoredSessionAsync().ConfigureAwait(false);
                _currentUser = null;
                SetState(AuthenticationState.LoggedOut);
                return AuthResult.Error(
                    AuthErrorCode.Storage,
                    "로그인 정보를 안전하게 저장하지 못했습니다. 다시 시도해주세요.");
            }

            _currentUser = session.User;
            SetState(AuthenticationState.LoggedIn);
            _logger?.Info("auth.login.succeeded");
            return AuthResult.Success(session);
        }
        finally
        {
            Volatile.Write(ref _loginInProgress, 0);
        }
    }

    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        _currentUser = null;
        _lastNotice = null;
        SetState(AuthenticationState.LoggedOut);

        try
        {
            await _sessionStore.DeleteAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            _logger?.Error("Auth.Session.Delete", exception);
            _lastNotice = "저장된 로그인 정보를 삭제하지 못했습니다. 앱을 다시 시작한 뒤 다시 시도해주세요.";
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        _logger?.Info("auth.logout");
    }

    public async Task RestoreSessionAsync(CancellationToken cancellationToken = default)
    {
        if (_configuration is null)
            return;

        _lastNotice = null;
        SetState(AuthenticationState.Restoring);

        AuthSession? session;
        try
        {
            session = await _sessionStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            SetState(AuthenticationState.LoggedOut);
            return;
        }
        catch (Exception exception)
        {
            _logger?.Error("Auth.Session.Load", exception);
            _lastNotice = "저장된 로그인 정보를 읽지 못했습니다. 다시 로그인해주세요.";
            SetState(AuthenticationState.LoggedOut);
            return;
        }

        if (session is null)
        {
            SetState(AuthenticationState.LoggedOut);
            return;
        }

        if (!session.IsExpiringSoon(RefreshBuffer))
        {
            _currentUser = session.User;
            SetState(AuthenticationState.LoggedIn);
            return;
        }

        AuthSession refreshed;
        try
        {
            refreshed = await RefreshAsync(_configuration, session.RefreshToken, session.User, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // App is shutting down mid-restore: leave the stored session untouched for next launch.
            SetState(AuthenticationState.LoggedOut);
            return;
        }
        catch (Exception exception)
        {
            if (exception is not AuthProtocolException)
                _logger?.Error("Auth.TokenRefresh", exception);
            _logger?.Info("auth.token.refresh_failed");
            await TryDeleteStoredSessionAsync().ConfigureAwait(false);
            _currentUser = null;
            _lastNotice = "저장된 로그인 정보가 만료되었습니다. 다시 로그인해주세요.";
            SetState(AuthenticationState.LoggedOut);
            return;
        }

        try
        {
            await _sessionStore.SaveAsync(refreshed, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            SetState(AuthenticationState.LoggedOut);
            return;
        }
        catch (Exception exception)
        {
            _logger?.Error("Auth.Session.SaveRefreshed", exception);
            await TryDeleteStoredSessionAsync().ConfigureAwait(false);
            _currentUser = null;
            _lastNotice = "로그인 정보를 안전하게 저장하지 못했습니다. 다시 로그인해주세요.";
            SetState(AuthenticationState.LoggedOut);
            return;
        }

        _currentUser = refreshed.User;
        SetState(AuthenticationState.LoggedIn);
        _logger?.Info("auth.token.refreshed");
    }

    private async Task ObserveListenerShutdownAsync(Task<OAuthCallbackListenResult> listenTask)
    {
        try
        {
            await listenTask.ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is OperationCanceledException or ObjectDisposedException)
        {
        }
        catch (Exception exception)
        {
            _logger?.Error("Auth.Listener.Stop", exception);
        }
    }

    private async Task TryDeleteStoredSessionAsync()
    {
        try
        {
            await _sessionStore.DeleteAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            _logger?.Error("Auth.Session.Delete", exception);
        }
    }

    private void SetState(AuthenticationState state)
    {
        State = state;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private static string BuildAuthorizeUrl(AuthConfiguration configuration, string codeChallenge, string state)
    {
        var redirectWithState = $"{configuration.RedirectUri}?state={Uri.EscapeDataString(state)}";
        var query = string.Join('&',
            "provider=google",
            $"redirect_to={Uri.EscapeDataString(redirectWithState)}",
            $"code_challenge={Uri.EscapeDataString(codeChallenge)}",
            "code_challenge_method=s256",
            $"prompt={Uri.EscapeDataString("select_account")}");
        return $"{configuration.SupabaseUrl}/auth/v1/authorize?{query}";
    }

    private async Task<AuthSession> ExchangeCodeAsync(
        AuthConfiguration configuration,
        string code,
        string verifier,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{configuration.SupabaseUrl}/auth/v1/token?grant_type=pkce");
        request.Headers.Add("apikey", configuration.SupabasePublishableKey);
        var payload = JsonSerializer.Serialize(new { auth_code = code, code_verifier = verifier });
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw new AuthProtocolException(AuthErrorCode.Network, "인터넷 연결을 확인한 뒤 다시 시도해주세요.");

        return ParseTokenResponse(body, fallbackUser: null);
    }

    private async Task<AuthSession> RefreshAsync(
        AuthConfiguration configuration,
        string refreshToken,
        AuthUser previousUser,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{configuration.SupabaseUrl}/auth/v1/token?grant_type=refresh_token");
        request.Headers.Add("apikey", configuration.SupabasePublishableKey);
        var payload = JsonSerializer.Serialize(new { refresh_token = refreshToken });
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw new AuthProtocolException(AuthErrorCode.SessionExpired, "저장된 로그인 정보가 만료되었습니다. 다시 로그인해주세요.");

        return ParseTokenResponse(body, fallbackUser: previousUser);
    }

    private static AuthSession ParseTokenResponse(string body, AuthUser? fallbackUser)
    {
        const string invalidResponseMessage = "로그인 요청을 확인할 수 없습니다. 다시 로그인해주세요.";

        TokenResponse? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<TokenResponse>(body, JsonOptions);
        }
        catch (JsonException)
        {
            throw new AuthProtocolException(AuthErrorCode.InvalidResponse, invalidResponseMessage);
        }

        if (parsed is null || string.IsNullOrEmpty(parsed.AccessToken) || string.IsNullOrEmpty(parsed.RefreshToken))
            throw new AuthProtocolException(AuthErrorCode.InvalidResponse, invalidResponseMessage);

        AuthUser user;
        if (parsed.User is not null && !string.IsNullOrEmpty(parsed.User.Id))
            user = ToAuthUser(parsed.User);
        else if (fallbackUser is not null)
            user = fallbackUser;
        else
            throw new AuthProtocolException(AuthErrorCode.InvalidResponse, invalidResponseMessage);

        var expiresIn = parsed.ExpiresIn > 0 ? parsed.ExpiresIn : 3600;
        return new AuthSession
        {
            AccessToken = parsed.AccessToken,
            RefreshToken = parsed.RefreshToken,
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(expiresIn),
            User = user
        };
    }

    private static AuthUser ToAuthUser(TokenUser raw)
    {
        var email = raw.Email?.Trim() ?? string.Empty;
        var emailLocalPart = email.Contains('@') ? email[..email.IndexOf('@')] : string.Empty;

        var name = ExtractMetadataString(raw.UserMetadata, "full_name")
            ?? ExtractMetadataString(raw.UserMetadata, "name");
        if (string.IsNullOrWhiteSpace(name))
            name = !string.IsNullOrEmpty(emailLocalPart) ? emailLocalPart : "Yulbyte 사용자";

        var avatarCandidate = ExtractMetadataString(raw.UserMetadata, "avatar_url")
            ?? ExtractMetadataString(raw.UserMetadata, "picture");
        string? avatarUrl = null;
        if (!string.IsNullOrWhiteSpace(avatarCandidate) &&
            Uri.TryCreate(avatarCandidate, UriKind.Absolute, out var parsedAvatar) &&
            parsedAvatar.Scheme == Uri.UriSchemeHttps)
        {
            avatarUrl = parsedAvatar.ToString();
        }

        return new AuthUser(raw.Id!, email, name, avatarUrl);
    }

    private static string? ExtractMetadataString(Dictionary<string, JsonElement>? metadata, string key)
    {
        if (metadata is null || !metadata.TryGetValue(key, out var element))
            return null;
        if (element.ValueKind != JsonValueKind.String)
            return null;
        var value = element.GetString();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private sealed class AuthProtocolException(AuthErrorCode errorCode, string koreanMessage) : Exception(koreanMessage)
    {
        public AuthErrorCode ErrorCode { get; } = errorCode;
    }

    private sealed class TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; set; }

        [JsonPropertyName("expires_in")]
        public long ExpiresIn { get; set; }

        [JsonPropertyName("user")]
        public TokenUser? User { get; set; }
    }

    private sealed class TokenUser
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("email")]
        public string? Email { get; set; }

        [JsonPropertyName("user_metadata")]
        public Dictionary<string, JsonElement>? UserMetadata { get; set; }
    }
}
