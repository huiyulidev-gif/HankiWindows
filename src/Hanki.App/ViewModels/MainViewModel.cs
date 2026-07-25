using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Data;
using Hanki.Core.Contracts;
using Hanki.Core.Models;
using Hanki.Core.Services;
using Hanki.Infrastructure.Logging;
using Hanki.Infrastructure.Windows;

namespace Hanki.App.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly IShortcutRepository _shortcutRepository;
    private readonly ISettingsRepository _settingsRepository;
    private readonly IBackupService _backupService;
    private readonly AutoStartService _autoStartService;
    private readonly TextExpansionService _expansionService;
    private readonly PrivacySafeLogger _logger;
    private string _searchText = string.Empty;
    private bool _favoritesOnly;
    private string _statusMessage = "준비 중…";
    private bool _isBusy;

    public MainViewModel(
        IShortcutRepository shortcutRepository,
        ISettingsRepository settingsRepository,
        IBackupService backupService,
        AutoStartService autoStartService,
        TextExpansionService expansionService,
        PrivacySafeLogger logger)
    {
        _shortcutRepository = shortcutRepository;
        _settingsRepository = settingsRepository;
        _backupService = backupService;
        _autoStartService = autoStartService;
        _expansionService = expansionService;
        _logger = logger;

        ShortcutsView = CollectionViewSource.GetDefaultView(Shortcuts);
        ShortcutsView.Filter = FilterShortcut;
        AddCommand = new RelayCommand(_ => EditRequested?.Invoke(this, null));
        EditCommand = new RelayCommand(row =>
        {
            if (row is ShortcutRowViewModel item)
                EditRequested?.Invoke(this, item.Model.Clone());
        });
        DeleteCommand = new RelayCommand(row =>
        {
            if (row is ShortcutRowViewModel item)
                DeleteRequested?.Invoke(this, item.Model.Clone());
        });
        FavoriteCommand = new AsyncRelayCommand(ToggleFavoriteAsync);
    }

    public event EventHandler<ShortcutItem?>? EditRequested;
    public event EventHandler<ShortcutItem>? DeleteRequested;

    public ObservableCollection<ShortcutRowViewModel> Shortcuts { get; } = [];
    public ICollectionView ShortcutsView { get; }
    public RelayCommand AddCommand { get; }
    public RelayCommand EditCommand { get; }
    public RelayCommand DeleteCommand { get; }
    public AsyncRelayCommand FavoriteCommand { get; }

    public AppSettings Settings { get; private set; } = new();

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
                ShortcutsView.Refresh();
        }
    }

    public bool FavoritesOnly
    {
        get => _favoritesOnly;
        set
        {
            if (SetProperty(ref _favoritesOnly, value))
                ShortcutsView.Refresh();
        }
    }

    public bool IsEnabled
    {
        get => Settings.IsEnabled;
        set
        {
            if (Settings.IsEnabled == value)
                return;
            Settings.IsEnabled = value;
            OnPropertyChanged();
            _ = SaveSettingsAsync();
        }
    }

    public string ExcludedProcessesText
    {
        get => string.Join(Environment.NewLine, Settings.ExcludedProcesses);
        set
        {
            Settings.ExcludedProcesses = value.Split(
                    ['\r', '\n'],
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(ProcessExclusionPolicy.Normalize)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            OnPropertyChanged();
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public async Task InitializeAsync()
    {
        Settings = await _settingsRepository.GetAsync();
        OnPropertyChanged(nameof(Settings));
        OnPropertyChanged(nameof(IsEnabled));
        OnPropertyChanged(nameof(ExcludedProcessesText));
        await RefreshAsync();
        StatusMessage = Settings.IsEnabled ? "단축어 변환이 켜져 있습니다." : "단축어 변환이 꺼져 있습니다.";
    }

    public async Task RefreshAsync()
    {
        var items = await _shortcutRepository.GetAllAsync();
        Shortcuts.Clear();
        foreach (var item in ShortcutOrdering.FavoritesFirst(items))
            Shortcuts.Add(new ShortcutRowViewModel(item));
        _expansionService.UpdateConfiguration(Settings, items);
        ShortcutsView.Refresh();
    }

    public async Task SaveShortcutAsync(ShortcutItem shortcut, bool isNew)
    {
        await RunAsync(async () =>
        {
            if (isNew)
                await _shortcutRepository.AddAsync(shortcut);
            else
                await _shortcutRepository.UpdateAsync(shortcut);
            await RefreshAsync();
            StatusMessage = isNew ? "단축어를 추가했습니다." : "단축어를 수정했습니다.";
        }, "Shortcut.Save");
    }

    public async Task DeleteShortcutAsync(ShortcutItem shortcut)
    {
        await RunAsync(async () =>
        {
            await _shortcutRepository.DeleteAsync(shortcut.Id);
            await RefreshAsync();
            StatusMessage = "단축어를 삭제했습니다.";
        }, "Shortcut.Delete");
    }

    public async Task SetEnabledAsync(bool enabled)
    {
        Settings.IsEnabled = enabled;
        OnPropertyChanged(nameof(IsEnabled));
        await SaveSettingsAsync();
    }

    public async Task SaveSettingsAsync()
    {
        await RunAsync(async () =>
        {
            await _settingsRepository.SaveAsync(Settings);
            _autoStartService.SetEnabled(Settings.StartWithWindows, Environment.ProcessPath!);
            _expansionService.UpdateConfiguration(Settings, Shortcuts.Select(item => item.Model));
            StatusMessage = "설정을 저장했습니다.";
            OnPropertyChanged(nameof(IsEnabled));
        }, "Settings.Save");
    }

    public async Task ExportAsync(string filePath)
    {
        await RunAsync(async () =>
        {
            await _backupService.ExportAsync(filePath);
            StatusMessage = "JSON 백업을 저장했습니다.";
        }, "Backup.Export");
    }

    public async Task<ImportResult?> ImportAsync(string filePath, ImportConflictStrategy strategy)
    {
        ImportResult? result = null;
        await RunAsync(async () =>
        {
            result = await _backupService.ImportAsync(filePath, strategy);
            Settings = await _settingsRepository.GetAsync();
            OnPropertyChanged(nameof(Settings));
            OnPropertyChanged(nameof(IsEnabled));
            OnPropertyChanged(nameof(ExcludedProcessesText));
            await RefreshAsync();
            StatusMessage = "JSON 백업을 가져왔습니다.";
        }, "Backup.Import");
        return result;
    }

    public void OpenDataFolder()
    {
        Directory.CreateDirectory(Hanki.Infrastructure.AppPaths.DataDirectory);
        Process.Start(new ProcessStartInfo
        {
            FileName = Hanki.Infrastructure.AppPaths.DataDirectory,
            UseShellExecute = true
        });
    }

    private bool FilterShortcut(object item)
    {
        if (item is not ShortcutRowViewModel row)
            return false;
        if (FavoritesOnly && !row.IsFavorite)
            return false;
        if (string.IsNullOrWhiteSpace(SearchText))
            return true;
        return row.Title.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
               row.TriggerText.Contains(SearchText, StringComparison.Ordinal) ||
               row.ReplacementText.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
    }

    private async Task ToggleFavoriteAsync(object? parameter)
    {
        if (parameter is not ShortcutRowViewModel row)
            return;
        var item = row.Model.Clone();
        item.IsFavorite = !item.IsFavorite;
        await SaveShortcutAsync(item, isNew: false);
    }

    private async Task RunAsync(Func<Task> operation, string location)
    {
        if (IsBusy)
            return;
        IsBusy = true;
        try
        {
            await operation();
        }
        catch (Exception exception)
        {
            _logger.Error(location, exception);
            StatusMessage = ToFriendlyMessage(exception);
            throw;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static string ToFriendlyMessage(Exception exception) => exception switch
    {
        Hanki.Core.Exceptions.ShortcutValidationException validation => validation.Message,
        Hanki.Core.Exceptions.DuplicateTriggerException => "동일한 단축어가 이미 있습니다.",
        InvalidDataException => "올바른 한키 JSON 백업 파일이 아닙니다.",
        UnauthorizedAccessException => "파일 또는 설정에 접근할 권한이 없습니다.",
        _ => "작업을 완료하지 못했습니다. 잠시 후 다시 시도해 주세요."
    };
}
