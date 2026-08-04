using System.Windows.Media.Imaging;
using Hanki.App.Services;
using Hanki.Core.Authentication;
using Hanki.Infrastructure.Logging;

namespace Hanki.App.ViewModels;

/// <summary>
/// Backs the "계정" tab. Never gates any existing shortcut feature -- it only reflects login
/// state and lets the user sign in/out. Talks to <see cref="AuthenticationUiCoordinator"/>,
/// never directly to <see cref="IAuthenticationService"/>.
/// </summary>
public sealed class AccountViewModel : ObservableObject, IDisposable
{
    private readonly AuthenticationUiCoordinator _coordinator;
    private readonly IPrivacySafeLogger _logger;
    private string? _errorMessage;
    private string? _noticeMessage;
    private BitmapImage? _avatarImage;
    private bool _avatarLoadFailed;
    private bool _disposed;

    public AccountViewModel(AuthenticationUiCoordinator coordinator, IPrivacySafeLogger logger)
    {
        _coordinator = coordinator;
        _logger = logger;
        _coordinator.StateChanged += OnCoordinatorStateChanged;

        LoginCommand = new AsyncRelayCommand(_ => LoginAsync());
        CancelCommand = new RelayCommand(_ => _coordinator.CancelLogin());
        LogoutCommand = new AsyncRelayCommand(_ => LogoutAsync());

        RefreshFromCoordinator();
    }

    public AsyncRelayCommand LoginCommand { get; }
    public RelayCommand CancelCommand { get; }
    public AsyncRelayCommand LogoutCommand { get; }

    public bool IsConfigured => _coordinator.IsConfigured;
    public bool IsLoggedOut => _coordinator.State == AuthenticationState.LoggedOut;
    public bool IsRestoring => _coordinator.State == AuthenticationState.Restoring;
    public bool IsLoggingIn => _coordinator.State == AuthenticationState.LoggingIn;
    public bool IsLoggedIn => _coordinator.State == AuthenticationState.LoggedIn;
    public bool CanStartLogin => IsConfigured && IsLoggedOut;
    public bool IsBusy => IsRestoring || IsLoggingIn;

    public static string ConfigMissingTooltip => "로그인 설정이 없습니다.";
    public string? LoginTooltip => CanStartLogin
        ? null
        : IsRestoring
            ? "저장된 로그인 상태를 확인하고 있습니다."
            : ConfigMissingTooltip;
    public static string LoggedOutDescriptionPrimary => "Yulbyte와 같은 Google 계정으로 로그인할 수 있습니다.";
    public static string LoggedOutDescriptionSecondary => "현재 계정 연결을 지원하며, 단축어 클라우드 동기화는 추후 제공될 예정입니다.";
    public static string InProgressMessage => "브라우저에서 로그인을 완료해주세요.";
    public static string RestoreInProgressMessage => "저장된 로그인 상태를 확인하고 있습니다.";

    public string UserName => _coordinator.CurrentUser?.Name ?? string.Empty;
    public string UserEmail => _coordinator.CurrentUser?.Email ?? string.Empty;

    public BitmapImage? AvatarImage => _avatarLoadFailed ? null : _avatarImage;
    public bool ShowDefaultAvatar => _avatarImage is null || _avatarLoadFailed;

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    public string? NoticeMessage
    {
        get => _noticeMessage;
        private set => SetProperty(ref _noticeMessage, value);
    }

    public async Task RestoreSessionAsync(CancellationToken cancellationToken = default)
    {
        await _coordinator.RestoreAsync(cancellationToken);
    }

    private async Task LoginAsync()
    {
        ErrorMessage = null;
        NoticeMessage = null;
        var result = await _coordinator.LoginAsync();
        if (!result.IsSuccess)
        {
            if (result.ErrorCode != AuthErrorCode.AlreadyInProgress)
                _logger.Info("auth.ui.login_failed");
            ErrorMessage = result.ErrorMessage;
        }
        RefreshFromCoordinator();
    }

    private async Task LogoutAsync()
    {
        await _coordinator.LogoutAsync();
        RefreshFromCoordinator();
    }

    private void OnCoordinatorStateChanged(object? sender, EventArgs e) => RefreshFromCoordinator();

    private void RefreshFromCoordinator()
    {
        if (_coordinator.State is not AuthenticationState.LoggingIn and not AuthenticationState.Restoring)
        {
            var notice = _coordinator.LastNotice;
            if (!string.IsNullOrEmpty(notice))
                NoticeMessage = notice;
        }

        UpdateAvatar();

        OnPropertyChanged(nameof(IsConfigured));
        OnPropertyChanged(nameof(IsLoggedOut));
        OnPropertyChanged(nameof(IsRestoring));
        OnPropertyChanged(nameof(IsLoggingIn));
        OnPropertyChanged(nameof(IsLoggedIn));
        OnPropertyChanged(nameof(CanStartLogin));
        OnPropertyChanged(nameof(IsBusy));
        OnPropertyChanged(nameof(LoginTooltip));
        OnPropertyChanged(nameof(UserName));
        OnPropertyChanged(nameof(UserEmail));
    }

    private void UpdateAvatar()
    {
        _avatarLoadFailed = false;
        var url = _coordinator.CurrentUser?.AvatarUrl;
        if (string.IsNullOrWhiteSpace(url) ||
            !Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps)
        {
            _avatarImage = null;
            OnPropertyChanged(nameof(AvatarImage));
            OnPropertyChanged(nameof(ShowDefaultAvatar));
            return;
        }

        try
        {
            var image = new BitmapImage();
            image.DownloadFailed += (_, _) =>
            {
                _avatarLoadFailed = true;
                OnPropertyChanged(nameof(AvatarImage));
                OnPropertyChanged(nameof(ShowDefaultAvatar));
            };
            // No CacheOption=OnLoad: keeps the download on WPF's background imaging thread
            // instead of blocking the UI thread while the avatar downloads.
            image.BeginInit();
            image.UriSource = uri;
            image.EndInit();
            _avatarImage = image;
        }
        catch (Exception exception) when (exception is NotSupportedException or ArgumentException or IOException)
        {
            _avatarImage = null;
            _avatarLoadFailed = true;
        }

        OnPropertyChanged(nameof(AvatarImage));
        OnPropertyChanged(nameof(ShowDefaultAvatar));
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _coordinator.StateChanged -= OnCoordinatorStateChanged;
    }
}
