using System.Net.Http;
using System.Threading;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Threading;
using Hanki.App.Services;
using Hanki.App.ViewModels;
using Hanki.Infrastructure;
using Hanki.Infrastructure.Authentication;
using Hanki.Infrastructure.Data;
using Hanki.Infrastructure.Diagnostics;
using Hanki.Infrastructure.Logging;
using Hanki.Infrastructure.Windows;
using Hanki.Core.Contracts;
using Hanki.Core.Services;

namespace Hanki.App;

public partial class App : System.Windows.Application
{
    private const string MutexName = "Local\\Yulbyte.Hanki.SingleInstance";
    private const string ActivationEventName = "Local\\Yulbyte.Hanki.Activate";
    private const string DiagnosticInstanceEnvironmentVariable = "HANKI_INSTANCE_NAMESPACE";
    private SingleInstanceManager? _singleInstance;
    private PrivacySafeLogger? _logger;
    private TextExpansionService? _expansionService;
    private CompatibilityDiagnosticsService? _diagnostics;
    private TrayIconService? _tray;
    private MainViewModel? _mainViewModel;
    private AccountViewModel? _accountViewModel;
    private AuthenticationUiCoordinator? _authCoordinator;
    private HttpClient? _authHttpClient;
    private MainWindowActivationService? _windowActivation;
    private MainWindow? _mainWindow;
    private bool _isExiting;
    private int _activationPending;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var diagnosticExitDelay = TryGetDiagnosticExitDelay(e.Args);
        var (mutexName, activationEventName) = GetInstanceNames();

        try
        {
            _singleInstance = SingleInstanceManager.Start(mutexName, activationEventName);
        }
        catch
        {
            MessageBox.Show(
                "한키를 시작하지 못했습니다. 잠시 후 다시 실행해 주세요.",
                "한키",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            Shutdown();
            return;
        }

        if (_singleInstance.Role == SingleInstanceRole.SecondaryInstance)
        {
            if (!_singleInstance.ActivationSignalSent)
            {
                MessageBox.Show(
                    "한키가 실행 중이지만 창을 열지 못했습니다. 작업표시줄 알림 영역에서 한키를 열어 주세요.",
                    "한키",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            Shutdown();
            return;
        }
        _singleInstance.StartListening(OnActivationRequested);

        _logger = new PrivacySafeLogger();
        DispatcherUnhandledException += (_, args) =>
        {
            _logger.Error("App.Dispatcher", args.Exception);
            MessageBox.Show("예상하지 못한 오류가 발생했습니다. 입력 내용은 로그에 저장되지 않았습니다.",
                "한키", MessageBoxButton.OK, MessageBoxImage.Warning);
            args.Handled = true;
        };

        try
        {
            var validator = new ShortcutValidator();
            var database = new SqliteDatabase(AppPaths.DatabasePath, _logger);
            await database.InitializeAsync();
            var shortcutRepository = new SqliteShortcutRepository(database, validator);
            var settingsRepository = new SqliteSettingsRepository(database);
            var backupService = new JsonBackupService(shortcutRepository, settingsRepository, validator);
            var autoStartService = new AutoStartService();
            var hook = new GlobalKeyboardHook();
            _diagnostics = new CompatibilityDiagnosticsService();
            _expansionService = new TextExpansionService(
                shortcutRepository,
                hook,
                _logger,
                _diagnostics);

            _mainViewModel = new MainViewModel(
                shortcutRepository,
                settingsRepository,
                backupService,
                autoStartService,
                _expansionService,
                _logger);
            await _mainViewModel.InitializeAsync();

            var authConfiguration = new AuthConfigurationProvider(_logger).TryLoad();
            _authHttpClient = new HttpClient();
            var authService = new SupabaseAuthenticationService(
                authConfiguration,
                _authHttpClient,
                new SecureAuthSessionStore(_logger),
                new SystemBrowserLauncher(),
                _logger);
            _authCoordinator = new AuthenticationUiCoordinator(authService, Dispatcher);
            _accountViewModel = new AccountViewModel(_authCoordinator, _logger);

            _windowActivation = new MainWindowActivationService(CreateMainWindow);

            _tray = new TrayIconService();
            _tray.OpenRequested += (_, _) => ShowAndActivateMainWindow();
            _tray.AddRequested += (_, _) =>
            {
                ShowAndActivateMainWindow();
                _mainWindow!.OpenNewShortcut();
            };
            _tray.SettingsRequested += (_, _) =>
            {
                ShowAndActivateMainWindow();
                _mainWindow!.ShowSettings();
            };
            _tray.EnabledChangeRequested += async (_, enabled) => await _mainViewModel.SetEnabledAsync(enabled);
            _tray.ExitRequested += (_, _) => ExitApplication();
            _mainViewModel.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(MainViewModel.IsEnabled))
                    _tray.UpdateStatus(_mainViewModel.IsEnabled);
            };
            _tray.UpdateStatus(_mainViewModel.IsEnabled);

            _expansionService.ShortcutUsed += (_, _) =>
                Dispatcher.InvokeAsync(_mainViewModel.RefreshAsync);
            try
            {
                _expansionService.Start();
            }
            catch (Exception exception)
            {
                _logger.Error("Hook.Start", exception);
                _mainViewModel.StatusMessage = "전역 입력 감지를 시작하지 못했습니다. 앱을 다시 실행해 주세요.";
            }

            ShowAndActivateMainWindow();
            ProcessPendingActivation();

            // Restore any saved login in the background without delaying the main window.
            _ = RestoreAuthSessionAsync();

            if (diagnosticExitDelay is not null)
            {
                ScheduleDiagnosticExit(diagnosticExitDelay.Value);
            }
            else if (!_mainViewModel.Settings.FirstRunCompleted)
            {
                var guide = new FirstRunWindow { Owner = _mainWindow };
                guide.ShowDialog();
                _mainViewModel.Settings.FirstRunCompleted = true;
                await _mainViewModel.SaveSettingsAsync();
            }
        }
        catch (Exception exception)
        {
            _logger.Error("App.Startup", exception);
            if (exception is XamlParseException xamlException)
            {
                _logger.Info(
                    $"app.startup.xaml.line{xamlException.LineNumber}.position{xamlException.LinePosition}." +
                    $"{xamlException.InnerException?.GetType().Name ?? "noinner"}");
            }
            var errorWindow = new Window
            {
                Title = "한키 - 시작 오류",
                Width = 520,
                Height = 220,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Content = new TextBlock
                {
                    Text = "로컬 데이터베이스를 열지 못했습니다.\n\n" +
                           "앱을 종료하지 않고 오류 화면을 표시했습니다. " +
                           "다시 실행해도 문제가 계속되면 huiyuli.dev@gmail.com 으로 문의해 주세요.",
                    Margin = new Thickness(28),
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 15
                }
            };
            MainWindow = errorWindow;
            errorWindow.Show();
            ProcessPendingActivation();
        }
    }

    private static TimeSpan? TryGetDiagnosticExitDelay(IEnumerable<string> arguments)
    {
        const string prefix = "--diagnostic-exit-after-ms=";
        if (!AppPaths.IsDataDirectoryOverridden)
            return null;

        var value = arguments
            .FirstOrDefault(argument => argument.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))?
            [prefix.Length..];
        // Diagnostic runs may intentionally last longer than the old 20-minute cap
        // (for example, a 45-minute handle/memory soak). Keep the upper bound finite
        // while allowing a one-hour observation window.
        return int.TryParse(value, out var milliseconds) && milliseconds is >= 250 and <= 3_600_000
            ? TimeSpan.FromMilliseconds(milliseconds)
            : null;
    }

    private static (string Mutex, string ActivationEvent) GetInstanceNames()
    {
        var diagnosticNamespace = Environment.GetEnvironmentVariable(DiagnosticInstanceEnvironmentVariable);
        if (!AppPaths.IsDataDirectoryOverridden ||
            string.IsNullOrWhiteSpace(diagnosticNamespace) ||
            diagnosticNamespace.Length > 64 ||
            diagnosticNamespace.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not '-' and not '_'))
        {
            return (MutexName, ActivationEventName);
        }

        return (
            $"Local\\Yulbyte.Hanki.{diagnosticNamespace}.SingleInstance",
            $"Local\\Yulbyte.Hanki.{diagnosticNamespace}.Activate");
    }

    private void ScheduleDiagnosticExit(TimeSpan delay)
    {
        var timer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher)
        {
            Interval = delay
        };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            ExitApplication();
        };
        timer.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _isExiting = true;
        Interlocked.Exchange(ref _activationPending, 0);
        _tray?.Dispose();
        _mainViewModel?.Dispose();
        _expansionService?.Dispose();
        _accountViewModel?.Dispose();
        _authCoordinator?.Dispose();
        _authHttpClient?.Dispose();
        _singleInstance?.Dispose();
        base.OnExit(e);
    }

    private async Task RestoreAuthSessionAsync()
    {
        if (_accountViewModel is null)
            return;
        try
        {
            await _accountViewModel.RestoreSessionAsync();
        }
        catch (Exception exception)
        {
            _logger?.Error("Auth.Restore", exception);
        }
    }

    private void OnMainWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_isExiting || _mainWindow is null)
            return;
        if (_mainWindow.ViewModel.Settings.MinimizeToTray)
        {
            e.Cancel = true;
            _mainWindow.ShowInTaskbar = false;
            _mainWindow.Hide();
            _tray?.ShowBalloonOnce();
        }
        else
        {
            _isExiting = true;
        }
    }

    private IMainWindowActivationTarget CreateMainWindow()
    {
        if (_mainViewModel is null)
            throw new InvalidOperationException("The main view model is not initialized.");
        if (_accountViewModel is null)
            throw new InvalidOperationException("The account view model is not initialized.");

        _mainWindow = new MainWindow(_mainViewModel, _accountViewModel);
        MainWindow = _mainWindow;
        _mainWindow.Closing += OnMainWindowClosing;
        return new WpfWindowActivationTarget(_mainWindow);
    }

    private void OnActivationRequested()
    {
        if (_isExiting || Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
            return;

        Interlocked.Exchange(ref _activationPending, 1);
        try
        {
            Dispatcher.BeginInvoke(ProcessPendingActivation);
        }
        catch (InvalidOperationException)
        {
        }
    }

    private void ProcessPendingActivation()
    {
        if (_isExiting)
        {
            Interlocked.Exchange(ref _activationPending, 0);
            return;
        }
        if (Volatile.Read(ref _activationPending) == 0)
            return;
        if (_windowActivation is null)
        {
            if (MainWindow is { } startupWindow)
            {
                startupWindow.ShowInTaskbar = true;
                if (!startupWindow.IsVisible)
                    startupWindow.Show();
                if (startupWindow.WindowState == WindowState.Minimized)
                    startupWindow.WindowState = WindowState.Normal;
                startupWindow.Activate();
                Interlocked.Exchange(ref _activationPending, 0);
            }
            return;
        }

        Interlocked.Exchange(ref _activationPending, 0);
        ShowAndActivateMainWindow();
    }

    private void ShowAndActivateMainWindow()
    {
        if (_isExiting || _windowActivation is null)
            return;
        _windowActivation.ShowAndActivateMainWindow();
    }

    private void ExitApplication()
    {
        _isExiting = true;
        _mainWindow?.Close();
        Shutdown();
    }
}
