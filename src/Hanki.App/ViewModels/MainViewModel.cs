using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Data;
using Hanki.Core.Contracts;
using Hanki.Core.Diagnostics;
using Hanki.Core.Models;
using Hanki.Core.Services;
using Hanki.Infrastructure.Logging;
using Hanki.Infrastructure.Diagnostics;
using Hanki.Infrastructure.Windows;

namespace Hanki.App.ViewModels;

public sealed class MainViewModel : ObservableObject, IDisposable
{
    private readonly IShortcutRepository _shortcutRepository;
    private readonly ISettingsRepository _settingsRepository;
    private readonly IBackupService _backupService;
    private readonly AutoStartService _autoStartService;
    private readonly TextExpansionService _expansionService;
    private readonly PrivacySafeLogger _logger;
    private readonly CompatibilityDiagnosticsService _diagnostics;
    private readonly SynchronizationContext? _uiContext;
    private string _searchText = string.Empty;
    private bool _favoritesOnly;
    private string _statusMessage = "준비 중…";
    private bool _isBusy;
    private CompatibilityDiagnosticSnapshot _diagnosticSnapshot;
    private string _hookSelfTestMessage = "아직 테스트하지 않았습니다.";
    private DelimiterKey _diagnosticTestDelimiter = DelimiterKey.Space;
    private bool _isCompatibilityBusy;
    private bool _disposed;

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
        _diagnostics = expansionService.Diagnostics;
        _uiContext = SynchronizationContext.Current;
        _diagnosticSnapshot = _diagnostics.Capture();
        _diagnostics.Changed += OnDiagnosticsChanged;

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
        foreach (var check in _diagnosticSnapshot.ManualChecks)
        {
            ManualCompatibilityChecks.Add(new ManualCheckItemViewModel(
                check,
                status => _diagnostics.SetManualCheckStatus(check.TargetCode, status)));
        }
        RefreshDiagnosticPresentation();
    }

    public event EventHandler<ShortcutItem?>? EditRequested;
    public event EventHandler<ShortcutItem>? DeleteRequested;

    public ObservableCollection<ShortcutRowViewModel> Shortcuts { get; } = [];
    public ICollectionView ShortcutsView { get; }
    public RelayCommand AddCommand { get; }
    public RelayCommand EditCommand { get; }
    public RelayCommand DeleteCommand { get; }
    public AsyncRelayCommand FavoriteCommand { get; }
    public ObservableCollection<ManualCheckItemViewModel> ManualCompatibilityChecks { get; } = [];
    public IReadOnlyList<DelimiterKey> DiagnosticDelimiterOptions { get; } =
        [DelimiterKey.Space, DelimiterKey.Enter, DelimiterKey.NumpadEnter, DelimiterKey.Tab];
    public IReadOnlyList<ManualCheckStatus> ManualCheckStatusOptions { get; } =
        [
            ManualCheckStatus.NotTested,
            ManualCheckStatus.Success,
            ManualCheckStatus.DetectedButInjectionFailed,
            ManualCheckStatus.NoResponse,
            ManualCheckStatus.NotAvailable
        ];
    public ObservableCollection<string> RecentDiagnosticEvents { get; } = [];

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

    public string ExcludedSitesText
    {
        get => string.Join(Environment.NewLine, Settings.ExcludedSites);
        set
        {
            Settings.ExcludedSites = value.Split(
                    ['\r', '\n'],
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(BrowserSitePolicy.NormalizeHost)
                .Where(host => host is not null)
                .Cast<string>()
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

    public bool IsCompatibilityBusy
    {
        get => _isCompatibilityBusy;
        private set => SetProperty(ref _isCompatibilityBusy, value);
    }

    public DelimiterKey DiagnosticTestDelimiter
    {
        get => _diagnosticTestDelimiter;
        set => SetProperty(ref _diagnosticTestDelimiter, value);
    }

    public string HookSelfTestMessage
    {
        get => _hookSelfTestMessage;
        private set => SetProperty(ref _hookSelfTestMessage, value);
    }

    public string DiagnosticAppStatus { get; private set; } = string.Empty;
    public string DiagnosticHookStatus { get; private set; } = string.Empty;
    public string DiagnosticHookTimes { get; private set; } = string.Empty;
    public string DiagnosticTargetStatus { get; private set; } = string.Empty;
    public string DiagnosticEnvironmentStatus { get; private set; } = string.Empty;
    public string DiagnosticProcessingStatus { get; private set; } = string.Empty;
    public string DiagnosticGuidance { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        Settings = await _settingsRepository.GetAsync();
        OnPropertyChanged(nameof(Settings));
        OnPropertyChanged(nameof(IsEnabled));
        OnPropertyChanged(nameof(ExcludedProcessesText));
        OnPropertyChanged(nameof(ExcludedSitesText));
        await RefreshAsync();
        StatusMessage = Settings.IsEnabled ? "단축어 변환이 켜져 있습니다." : "단축어 변환이 꺼져 있습니다.";
    }

    public void RefreshCompatibilityDiagnostics()
    {
        _diagnosticSnapshot = _expansionService.RefreshDiagnostics(DiagnosticTestDelimiter);
        RefreshDiagnosticPresentation();
    }

    public async Task RunHookSelfTestAsync()
    {
        if (IsCompatibilityBusy)
            return;
        IsCompatibilityBusy = true;
        HookSelfTestMessage = "8초 안에 아무 키나 한 번 눌러 주세요. 누른 키 값은 저장하지 않습니다.";
        try
        {
            var success = await _expansionService.RunHookSelfTestAsync(TimeSpan.FromSeconds(8));
            HookSelfTestMessage = success
                ? "정상: 한키가 키 입력 이벤트를 감지했습니다."
                : "실패: 한키가 키 입력을 감지하지 못했습니다. 후크 상태와 보안 정책을 확인해 주세요.";
            RefreshCompatibilityDiagnostics();
        }
        finally
        {
            IsCompatibilityBusy = false;
        }
    }

    public void BeginInternalExpansionTest()
    {
        _expansionService.BeginInternalExpansionTest(DiagnosticTestDelimiter);
        HookSelfTestMessage =
            $"아래 입력창에 {TextExpansionService.CompatibilityTestTrigger} 를 입력한 뒤 " +
            DelimiterDisplayName(DiagnosticTestDelimiter) +
            " 키를 누르세요. 임시 단축어는 2분 뒤 또는 한 번의 시도 후 사라집니다.";
        RefreshCompatibilityDiagnostics();
    }

    public async Task RestartHookAsync()
    {
        if (IsCompatibilityBusy)
            return;
        IsCompatibilityBusy = true;
        try
        {
            await _expansionService.RestartHookAsync();
            HookSelfTestMessage = _diagnostics.Capture().Hook.IsRegistered
                ? "키보드 후크를 안전하게 다시 등록했습니다."
                : "후크 재등록에 실패했습니다. 진단 이벤트를 내보내 확인해 주세요.";
            RefreshCompatibilityDiagnostics();
        }
        finally
        {
            IsCompatibilityBusy = false;
        }
    }

    public async Task ExportCompatibilityDiagnosticsAsync(string zipPath)
    {
        if (IsCompatibilityBusy)
            return;
        IsCompatibilityBusy = true;
        try
        {
            await _diagnostics.ExportZipAsync(zipPath);
            StatusMessage = "개인정보를 제외한 호환성 진단 ZIP을 저장했습니다.";
            RefreshCompatibilityDiagnostics();
        }
        finally
        {
            IsCompatibilityBusy = false;
        }
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
            OnPropertyChanged(nameof(ExcludedSitesText));
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

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _diagnostics.Changed -= OnDiagnosticsChanged;
        GC.SuppressFinalize(this);
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

    private void OnDiagnosticsChanged(object? sender, EventArgs eventArgs)
    {
        if (_disposed)
            return;
        if (_uiContext is null)
        {
            _diagnosticSnapshot = _diagnostics.Capture();
            return;
        }
        _uiContext.Post(_ =>
        {
            if (_disposed)
                return;
            _diagnosticSnapshot = _diagnostics.Capture();
            RefreshDiagnosticPresentation();
        }, null);
    }

    private void RefreshDiagnosticPresentation()
    {
        var snapshot = _diagnosticSnapshot;
        DiagnosticAppStatus =
            $"한키 {snapshot.AppVersion} · {snapshot.ProcessArchitecture} · " +
            $"무결성 {IntegrityDisplayName(snapshot.HankiIntegrity)}\n{snapshot.WindowsVersion}";
        DiagnosticHookStatus = snapshot.Hook.IsRegistered && snapshot.Hook.IsThreadAlive
            ? $"정상 · 핸들 있음 · 전용 스레드 {snapshot.Hook.ThreadId}"
            : snapshot.Hook.ConsecutiveRestartFailures >= 3
                ? "중지 · 자동 재시도 한도(3회)에 도달했습니다."
                : "비정상 · 등록 또는 스레드 상태를 확인할 수 없습니다.";
        DiagnosticHookTimes =
            $"등록: {FormatTimestamp(snapshot.Hook.RegisteredAtUtc)}\n" +
            $"마지막 키 감지: {FormatTimestamp(snapshot.Hook.LastCallbackAtUtc)} · " +
            $"마지막 종결자: {FormatTimestamp(snapshot.Hook.LastDelimiterAtUtc)}";
        DiagnosticTargetStatus =
            $"프로그램: {snapshot.Target.ProcessName ?? "확인 불가"}\n" +
            $"대상 무결성: {IntegrityDisplayName(snapshot.Target.Integrity)} · " +
            $"권한 비교: {IntegrityComparisonDisplayName(snapshot.Target.IntegrityComparison)}\n" +
            $"입력 문맥: {InputContextDisplayName(snapshot.Target.InputContextStatus)} · " +
            $"판정: {BlockReasonDisplayName(snapshot.Target.BlockReason)}";
        DiagnosticEnvironmentStatus =
            $"키보드 레이아웃: {snapshot.InputEnvironment.KeyboardLayout} · " +
            $"IME: {BoolDisplayName(snapshot.InputEnvironment.ImeOpen)} · " +
            $"조합 중: {BoolDisplayName(snapshot.InputEnvironment.ImeComposing)}\n" +
            $"Caps Lock: {OnOff(snapshot.InputEnvironment.CapsLock)} · " +
            $"Num Lock: {OnOff(snapshot.InputEnvironment.NumLock)} · " +
            $"세션: {snapshot.InputEnvironment.WindowsSessionId} · " +
            $"선택 종결자: {snapshot.InputEnvironment.SelectedDelimiter?.ToString() ?? "없음"}";
        DiagnosticProcessingStatus =
            $"결과: {ProcessingStatusDisplayName(snapshot.Processing.Status)}\n" +
            $"후보: {FormatTimestamp(snapshot.Processing.LastCandidateAtUtc)} · " +
            $"일치: {FormatTimestamp(snapshot.Processing.LastMatchAtUtc)}\n" +
            $"삭제 {snapshot.Processing.BackspaceSent}/{snapshot.Processing.BackspaceRequested} · " +
            $"변환문 {snapshot.Processing.TextSent}/{snapshot.Processing.TextRequested} · " +
            $"종결자 {snapshot.Processing.DelimiterSent}/{snapshot.Processing.DelimiterRequested}\n" +
            $"최근 성공: {FormatTimestamp(snapshot.Processing.LastSuccessAtUtc)} · " +
            $"최근 실패: {FormatTimestamp(snapshot.Processing.LastFailureAtUtc)}";
        DiagnosticGuidance = CreateGuidance(snapshot);

        OnPropertyChanged(nameof(DiagnosticAppStatus));
        OnPropertyChanged(nameof(DiagnosticHookStatus));
        OnPropertyChanged(nameof(DiagnosticHookTimes));
        OnPropertyChanged(nameof(DiagnosticTargetStatus));
        OnPropertyChanged(nameof(DiagnosticEnvironmentStatus));
        OnPropertyChanged(nameof(DiagnosticProcessingStatus));
        OnPropertyChanged(nameof(DiagnosticGuidance));

        foreach (var item in ManualCompatibilityChecks)
        {
            var latest = snapshot.ManualChecks.FirstOrDefault(check => check.TargetCode == item.TargetCode);
            if (latest is not null)
                item.SetStatusWithoutNotification(latest.Status);
        }

        RecentDiagnosticEvents.Clear();
        foreach (var item in snapshot.RecentEvents.TakeLast(20).Reverse())
        {
            RecentDiagnosticEvents.Add(
                $"{item.OccurredAtUtc.ToLocalTime():HH:mm:ss} · {item.Kind} · " +
                $"{item.BlockReason?.ToString() ?? item.NoteCode ?? "ok"}");
        }
    }

    private static string CreateGuidance(CompatibilityDiagnosticSnapshot snapshot)
    {
        if (!snapshot.Hook.IsRegistered || !snapshot.Hook.IsThreadAlive)
            return "한키가 전역 입력을 감지하지 못하고 있습니다. 먼저 ‘후크 다시 등록’을 누르고 자가 테스트를 실행하세요. 보안 프로그램이 차단했을 가능성은 진단 보고서와 현장 정책으로 추가 확인해야 합니다.";
        return snapshot.Target.BlockReason switch
        {
            ExpansionBlockReason.HigherIntegrityTarget =>
                "대상 프로그램이 한키보다 높은 권한으로 실행 중입니다. Windows UIPI 정책으로 입력이 차단될 수 있습니다. 필요한 경우에만 현재 세션 관리자 재시작을 사용하세요.",
            ExpansionBlockReason.SensitiveField =>
                "개인정보 보호를 위해 비밀번호 입력창에서는 한키가 동작하지 않습니다.",
            ExpansionBlockReason.ExcludedApplication =>
                "현재 프로그램이 설정의 제외 프로그램 목록에 있습니다.",
            ExpansionBlockReason.ExcludedSite =>
                "현재 브라우저 사이트가 제외 사이트 목록에 있습니다. 주소 전체는 저장하지 않았습니다.",
            ExpansionBlockReason.ProtectedTarget or ExpansionBlockReason.SecureDesktop =>
                "Windows 보호 화면 또는 보호 프로세스에서는 한키가 동작하지 않습니다.",
            ExpansionBlockReason.UnsupportedControl or ExpansionBlockReason.InputContextUnavailable =>
                "키 입력은 감지했지만 이 입력창에서 안전하게 단축어 문맥을 읽지 못했습니다. 선택형 클립보드 모드는 문맥 검사 자체를 우회하지 않습니다.",
            _ when snapshot.Processing.Status == ExpansionResultStatus.PartialFailure =>
                "단축어 삭제 뒤 변환문 또는 종결자 입력이 일부 실패했습니다. Ctrl+Z로 복구할 수 있는지 확인하고 진단 ZIP을 저장하세요.",
            _ when snapshot.Processing.Status == ExpansionResultStatus.Success =>
                "마지막 변환이 모든 단계에서 성공했습니다.",
            _ => "후크 자가 테스트와 내부 테스트를 차례로 실행하면 감지 실패와 대상 앱 호환성 문제를 구분할 수 있습니다."
        };
    }

    private static string FormatTimestamp(DateTimeOffset? value) =>
        value is null ? "기록 없음" : value.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");

    private static string DelimiterDisplayName(DelimiterKey value) => value switch
    {
        DelimiterKey.Space => "Space",
        DelimiterKey.Enter => "Enter",
        DelimiterKey.NumpadEnter => "숫자패드 Enter",
        DelimiterKey.Tab => "Tab",
        _ => value.ToString()
    };

    private static string IntegrityDisplayName(ProcessIntegrityLevel value) => value switch
    {
        ProcessIntegrityLevel.Low => "낮음",
        ProcessIntegrityLevel.Medium => "일반",
        ProcessIntegrityLevel.MediumPlus => "일반+",
        ProcessIntegrityLevel.High => "관리자",
        ProcessIntegrityLevel.System => "시스템",
        ProcessIntegrityLevel.Protected => "보호됨",
        ProcessIntegrityLevel.Untrusted => "신뢰되지 않음",
        _ => "확인 불가"
    };

    private static string IntegrityComparisonDisplayName(IntegrityComparison value) => value switch
    {
        IntegrityComparison.Same => "동일",
        IntegrityComparison.HankiHigher => "한키가 높음",
        IntegrityComparison.TargetHigher => "대상이 높음",
        _ => "확인 불가"
    };

    private static string InputContextDisplayName(InputContextStatus value) => value switch
    {
        InputContextStatus.Available => "입력 가능",
        InputContextStatus.SensitiveField => "민감 입력창",
        InputContextStatus.ReadOnly => "읽기 전용",
        InputContextStatus.UnsupportedControl => "지원하지 않는 컨트롤",
        InputContextStatus.AccessDenied => "접근 거부",
        InputContextStatus.TextPatternUnavailable => "텍스트 문맥 미지원",
        _ => "정보 확인 불가"
    };

    private static string BlockReasonDisplayName(ExpansionBlockReason value) => value switch
    {
        ExpansionBlockReason.None => "차단 없음",
        ExpansionBlockReason.FeatureDisabled => "기능 꺼짐",
        ExpansionBlockReason.Paused => "일시 정지",
        ExpansionBlockReason.DelimiterDisabled => "종결자 꺼짐",
        ExpansionBlockReason.ExcludedApplication => "제외 프로그램",
        ExpansionBlockReason.ExcludedSite => "제외 사이트",
        ExpansionBlockReason.SensitiveField => "민감 입력창",
        ExpansionBlockReason.HigherIntegrityTarget => "대상 권한이 더 높음",
        ExpansionBlockReason.ProtectedTarget => "보호 대상",
        ExpansionBlockReason.SecureDesktop => "Windows 보안 화면",
        ExpansionBlockReason.ImeCompositionActive => "IME 조합 중",
        ExpansionBlockReason.ReadOnlyField => "읽기 전용",
        ExpansionBlockReason.UnsupportedControl => "지원하지 않는 입력창",
        _ => "정보 확인 불가"
    };

    private static string ProcessingStatusDisplayName(ExpansionResultStatus value) => value switch
    {
        ExpansionResultStatus.Success => "성공",
        ExpansionResultStatus.PartialFailure => "부분 실패",
        ExpansionResultStatus.Failed => "실패",
        ExpansionResultStatus.Blocked => "차단됨",
        ExpansionResultStatus.NoMatch => "일치 단축어 없음",
        _ => "처리 기록 없음"
    };

    private static string BoolDisplayName(bool? value) => value switch
    {
        true => "켜짐",
        false => "꺼짐",
        null => "확인 불가"
    };

    private static string OnOff(bool value) => value ? "켜짐" : "꺼짐";

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

public sealed class ManualCheckItemViewModel : ObservableObject
{
    private readonly Action<ManualCheckStatus> _statusChanged;
    private ManualCheckStatus _status;

    public ManualCheckItemViewModel(
        ManualCompatibilityCheck check,
        Action<ManualCheckStatus> statusChanged)
    {
        TargetCode = check.TargetCode;
        DisplayName = check.DisplayName;
        _status = check.Status;
        _statusChanged = statusChanged;
    }

    public string TargetCode { get; }
    public string DisplayName { get; }

    public ManualCheckStatus Status
    {
        get => _status;
        set
        {
            if (!SetProperty(ref _status, value))
                return;
            _statusChanged(value);
        }
    }

    public void SetStatusWithoutNotification(ManualCheckStatus status)
    {
        if (_status == status)
            return;
        _status = status;
        OnPropertyChanged(nameof(Status));
    }
}
