using System.Threading;
using System.Windows;
using Hanki.App.Services;
using Hanki.App.ViewModels;
using Hanki.Infrastructure;
using Hanki.Infrastructure.Data;
using Hanki.Infrastructure.Logging;
using Hanki.Infrastructure.Windows;
using Hanki.Core.Services;

namespace Hanki.App;

public partial class App : System.Windows.Application
{
    private const string MutexName = "Local\\Yulbyte.Hanki.0.1";
    private Mutex? _singleInstanceMutex;
    private PrivacySafeLogger? _logger;
    private TextExpansionService? _expansionService;
    private TrayIconService? _tray;
    private MainWindow? _mainWindow;
    private bool _isExiting;
    private bool _ownsMutex;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstanceMutex = new Mutex(initiallyOwned: true, MutexName, out var isFirstInstance);
        _ownsMutex = isFirstInstance;
        if (!isFirstInstance)
        {
            MessageBox.Show("한키가 이미 실행 중입니다. 시스템 트레이를 확인해 주세요.",
                "한키", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

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
            _expansionService = new TextExpansionService(shortcutRepository, hook, _logger);

            var viewModel = new MainViewModel(
                shortcutRepository,
                settingsRepository,
                backupService,
                autoStartService,
                _expansionService,
                _logger);
            await viewModel.InitializeAsync();

            _mainWindow = new MainWindow(viewModel);
            MainWindow = _mainWindow;
            _mainWindow.Closing += OnMainWindowClosing;

            _tray = new TrayIconService();
            _tray.OpenRequested += (_, _) => ShowMainWindow();
            _tray.AddRequested += (_, _) =>
            {
                ShowMainWindow();
                _mainWindow.OpenNewShortcut();
            };
            _tray.SettingsRequested += (_, _) =>
            {
                ShowMainWindow();
                _mainWindow.ShowSettings();
            };
            _tray.EnabledChangeRequested += async (_, enabled) => await viewModel.SetEnabledAsync(enabled);
            _tray.ExitRequested += (_, _) => ExitApplication();
            viewModel.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(MainViewModel.IsEnabled))
                    _tray.UpdateStatus(viewModel.IsEnabled);
            };
            _tray.UpdateStatus(viewModel.IsEnabled);

            _expansionService.ShortcutUsed += (_, _) =>
                Dispatcher.InvokeAsync(viewModel.RefreshAsync);
            try
            {
                _expansionService.Start();
            }
            catch (Exception exception)
            {
                _logger.Error("Hook.Start", exception);
                viewModel.StatusMessage = "전역 입력 감지를 시작하지 못했습니다. 앱을 다시 실행해 주세요.";
            }

            _mainWindow.Show();
            if (!viewModel.Settings.FirstRunCompleted)
            {
                var guide = new FirstRunWindow { Owner = _mainWindow };
                guide.ShowDialog();
                viewModel.Settings.FirstRunCompleted = true;
                await viewModel.SaveSettingsAsync();
            }
        }
        catch (Exception exception)
        {
            _logger.Error("App.Startup", exception);
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
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        _expansionService?.Dispose();
        if (_ownsMutex)
            _singleInstanceMutex?.ReleaseMutex();
        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }

    private void OnMainWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_isExiting || _mainWindow is null)
            return;
        if (_mainWindow.ViewModel.Settings.MinimizeToTray)
        {
            e.Cancel = true;
            _mainWindow.Hide();
            _tray?.ShowBalloonOnce();
        }
        else
        {
            _isExiting = true;
        }
    }

    private void ShowMainWindow()
    {
        if (_mainWindow is null)
            return;
        _mainWindow.Show();
        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
    }

    private void ExitApplication()
    {
        _isExiting = true;
        _mainWindow?.Close();
        Shutdown();
    }
}
