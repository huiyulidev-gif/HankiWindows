using System.Windows.Threading;
using Hanki.Core.Authentication;

namespace Hanki.App.Services;

/// <summary>
/// Bridges <see cref="IAuthenticationService"/> to WPF: marshals its (potentially
/// background-thread) <see cref="IAuthenticationService.StateChanged"/> event onto the UI
/// dispatcher, and owns the <see cref="CancellationTokenSource"/> for an in-flight login so the
/// "취소" button and app shutdown can cancel it. Also provides the coordinator-level guard
/// against a second concurrent login attempt.
/// </summary>
public sealed class AuthenticationUiCoordinator : IDisposable
{
    private readonly IAuthenticationService _authService;
    private readonly Dispatcher _dispatcher;
    private readonly object _gate = new();
    private CancellationTokenSource? _loginCts;
    private bool _loginInFlight;
    private bool _disposed;

    public AuthenticationUiCoordinator(IAuthenticationService authService, Dispatcher dispatcher)
    {
        _authService = authService;
        _dispatcher = dispatcher;
        _authService.StateChanged += OnAuthServiceStateChanged;
    }

    public event EventHandler? StateChanged;

    public bool IsConfigured => _authService.IsConfigured;
    public AuthenticationState State => _authService.State;
    public AuthUser? CurrentUser => _authService.CurrentUser;
    public string? LastNotice => _authService.LastNotice;

    public Task RestoreAsync(CancellationToken cancellationToken = default) =>
        _authService.RestoreSessionAsync(cancellationToken);

    public async Task<AuthResult> LoginAsync()
    {
        CancellationTokenSource cts;
        lock (_gate)
        {
            if (_loginInFlight)
                return AuthResult.Error(AuthErrorCode.AlreadyInProgress, "이미 로그인이 진행 중입니다.");
            _loginInFlight = true;
            cts = _loginCts = new CancellationTokenSource();
        }

        try
        {
            return await _authService.LoginAsync(cts.Token).ConfigureAwait(false);
        }
        finally
        {
            lock (_gate)
            {
                _loginInFlight = false;
                if (ReferenceEquals(_loginCts, cts))
                    _loginCts = null;
            }
            cts.Dispose();
        }
    }

    /// <summary>Cancels the in-flight login, if any. No-op otherwise.</summary>
    public void CancelLogin()
    {
        lock (_gate)
        {
            _loginCts?.Cancel();
        }
    }

    public Task LogoutAsync(CancellationToken cancellationToken = default) =>
        _authService.LogoutAsync(cancellationToken);

    private void OnAuthServiceStateChanged(object? sender, EventArgs e)
    {
        if (_disposed)
            return;
        try
        {
            _dispatcher.BeginInvoke(() =>
            {
                if (!_disposed)
                    StateChanged?.Invoke(this, EventArgs.Empty);
            });
        }
        catch (Exception exception) when (
            exception is TaskCanceledException or InvalidOperationException or ObjectDisposedException)
        {
            // Dispatcher is shutting down; nothing left to notify.
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _authService.StateChanged -= OnAuthServiceStateChanged;
        CancelLogin();
    }
}
