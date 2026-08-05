using System.Globalization;
using System.Threading.Channels;
using Hanki.Core.Contracts;
using Hanki.Core.Diagnostics;
using Hanki.Core.Models;
using Hanki.Core.Services;
using Hanki.Infrastructure.Diagnostics;
using Hanki.Infrastructure.Logging;
using Microsoft.Win32;

namespace Hanki.Infrastructure.Windows;

public sealed class TextExpansionService : IDisposable
{
    public const string CompatibilityTestTrigger = ";hankitest";
    public const string CompatibilityTestReplacement = "한키 입력 테스트 성공";
    private const string CompatibilityTestShortcutId = "23ba044b-1a60-4fd9-93d5-09caafc1c4b5";

    private readonly IShortcutRepository _repository;
    private readonly GlobalKeyboardHook _hook;
    private readonly PrivacySafeLogger _logger;
    private readonly CompatibilityDiagnosticsService _diagnostics;
    private readonly WindowsInputInspector _inspector = new();
    private readonly ProcessIntegrityInspector _integrityInspector = new();
    private readonly InputEnvironmentInspector _environmentInspector = new();
    private readonly BrowserSiteInspector _browserSiteInspector = new();
    private readonly ShortcutMatcher _matcher = new();
    private readonly ReentrancyGuard _guard = new(TimeSpan.FromMilliseconds(180));
    private readonly ExpansionInjectionCoordinator _injection;
    private readonly object _configurationLock = new();
    private readonly SemaphoreSlim _recoveryGate = new(1, 1);
    private readonly CancellationTokenSource _shutdown = new();
    private readonly Channel<HookKeyEvent> _inputEvents = Channel.CreateBounded<HookKeyEvent>(
        new BoundedChannelOptions(512)
        {
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleReader = true,
            SingleWriter = true,
            AllowSynchronousContinuations = false
        });

    private AppSettings _settings = new();
    private ShortcutItem[] _shortcuts = [];
    private TemporaryTestShortcut? _temporaryTest;
    private Task? _workerTask;
    private Task? _monitorTask;
    private TaskCompletionSource<bool>? _hookSelfTest;
    private DateTimeOffset _lastCallbackDiagnosticAtUtc;
    private DateTimeOffset? _nextRestartAtUtc;
    private int _consecutiveRestartFailures;
    private int _droppedHookEvents;
    private bool _started;
    private bool _disposed;

    public TextExpansionService(
        IShortcutRepository repository,
        GlobalKeyboardHook hook,
        PrivacySafeLogger logger,
        CompatibilityDiagnosticsService diagnostics,
        IInputSender? inputSender = null,
        ClipboardCompatibilityService? clipboardService = null)
    {
        _repository = repository;
        _hook = hook;
        _logger = logger;
        _diagnostics = diagnostics;
        _injection = new ExpansionInjectionCoordinator(
            inputSender ?? new WindowsInputSender(),
            clipboardService ?? new ClipboardCompatibilityService());
        _hook.KeyEventReceived += OnHookKeyEvent;
    }

    public event EventHandler<string>? ShortcutUsed;

    public CompatibilityDiagnosticsService Diagnostics => _diagnostics;

    public void Start()
    {
        if (_started)
            return;
        ObjectDisposedException.ThrowIf(_disposed, this);
        _started = true;
        _workerTask = Task.Run(() => ProcessInputLoopAsync(_shutdown.Token));
        _monitorTask = Task.Run(() => MonitorHookAsync(_shutdown.Token));
        SystemEvents.SessionSwitch += OnSessionSwitch;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;

        var current = _integrityInspector.InspectProcess(Environment.ProcessId);
        _diagnostics.SetHankiIntegrity(current.Integrity);
        _diagnostics.Record(CompatibilityDiagnosticsService.NewEvent(
            CompatibilityEventKind.AppStarted,
            hankiIntegrity: current.Integrity,
            noteCode: "app.compatibility_service_started"));

        if (!TryStartHook("hook.initial"))
            _ = RecoverHookAsync(resetBudget: false, "hook.initial_retry", _shutdown.Token);
    }

    public void UpdateConfiguration(AppSettings settings, IEnumerable<ShortcutItem> shortcuts)
    {
        lock (_configurationLock)
        {
            _settings = settings.Clone();
            _shortcuts = shortcuts.Select(item => item.Clone()).ToArray();
        }
    }

    public void BeginInternalExpansionTest(DelimiterKey delimiter)
    {
        lock (_configurationLock)
        {
            _temporaryTest = new TemporaryTestShortcut(
                delimiter,
                DateTimeOffset.UtcNow.AddMinutes(2),
                new ShortcutItem
                {
                    Id = CompatibilityTestShortcutId,
                    Title = "호환성 진단 임시 테스트",
                    TriggerText = CompatibilityTestTrigger,
                    ReplacementText = CompatibilityTestReplacement,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                });
        }
        _diagnostics.Record(CompatibilityDiagnosticsService.NewEvent(
            CompatibilityEventKind.ShortcutCandidateDetected,
            delimiter: delimiter,
            shortcutId: CompatibilityTestShortcutId,
            noteCode: "internal_test.armed"));
    }

    public async Task<bool> RunHookSelfTestAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        if (!_hook.IsRegistered || !_hook.IsThreadAlive)
        {
            _diagnostics.Record(CompatibilityDiagnosticsService.NewEvent(
                CompatibilityEventKind.HookSelfTestTimedOut,
                DiagnosticSeverity.Warning,
                blockReason: ExpansionBlockReason.HookUnavailable,
                noteCode: "hook_self_test.unavailable"));
            return false;
        }

        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Interlocked.Exchange(ref _hookSelfTest, completion)?.TrySetResult(false);
        _diagnostics.Record(CompatibilityDiagnosticsService.NewEvent(
            CompatibilityEventKind.HookSelfTestStarted,
            noteCode: "hook_self_test.waiting"));

        try
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            linked.CancelAfter(timeout);
            using var registration = linked.Token.Register(() => completion.TrySetResult(false));
            var success = await completion.Task;
            _diagnostics.Record(CompatibilityDiagnosticsService.NewEvent(
                success
                    ? CompatibilityEventKind.HookSelfTestSucceeded
                    : CompatibilityEventKind.HookSelfTestTimedOut,
                success ? DiagnosticSeverity.Information : DiagnosticSeverity.Warning,
                noteCode: success ? "hook_self_test.success" : "hook_self_test.timeout"));
            return success;
        }
        finally
        {
            Interlocked.CompareExchange(ref _hookSelfTest, null, completion);
        }
    }

    public Task RestartHookAsync(CancellationToken cancellationToken = default) =>
        RecoverHookAsync(resetBudget: true, "hook.user_requested_restart", cancellationToken);

    public CompatibilityDiagnosticSnapshot RefreshDiagnostics(DelimiterKey? delimiter = null)
    {
        var input = _environmentInspector.Capture(delimiter);
        _diagnostics.UpdateInputEnvironment(input);
        var integrity = _integrityInspector.InspectForeground();
        _diagnostics.SetHankiIntegrity(integrity.Hanki.Integrity);
        _diagnostics.UpdateTarget(new TargetDiagnosticState(
            integrity.Target.ProcessName,
            integrity.Target.Integrity,
            integrity.Comparison,
            integrity.Target.IsProtected,
            false,
            false,
            false,
            InputContextStatus.InformationUnavailable,
            integrity.IsSecureDesktop
                ? ExpansionBlockReason.SecureDesktop
                : ExpansionBlockReason.None,
            integrity.IsSecureDesktop ? "target.secure_desktop" : "target.refreshed"));
        UpdateHookState();
        return _diagnostics.Capture();
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        SystemEvents.SessionSwitch -= OnSessionSwitch;
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        _shutdown.Cancel();
        _inputEvents.Writer.TryComplete();
        Interlocked.Exchange(ref _hookSelfTest, null)?.TrySetResult(false);
        _hook.KeyEventReceived -= OnHookKeyEvent;
        _hook.Dispose();
        try
        {
            Task.WaitAll(
                [_workerTask ?? Task.CompletedTask, _monitorTask ?? Task.CompletedTask],
                TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
        }
        _shutdown.Dispose();
        _recoveryGate.Dispose();
        _diagnostics.Record(CompatibilityDiagnosticsService.NewEvent(
            CompatibilityEventKind.HookStopped,
            noteCode: "hook.app_disposed"));
        GC.SuppressFinalize(this);
    }

    private void OnHookKeyEvent(object? sender, HookKeyEvent keyEvent)
    {
        if (keyEvent.IsHankiInjected)
            return;
        if (!_inputEvents.Writer.TryWrite(keyEvent))
            Interlocked.Increment(ref _droppedHookEvents);
    }

    private async Task ProcessInputLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var keyEvent in _inputEvents.Reader.ReadAllAsync(cancellationToken))
            {
                if (keyEvent.OccurredAtUtc - _lastCallbackDiagnosticAtUtc >= TimeSpan.FromSeconds(2))
                {
                    _lastCallbackDiagnosticAtUtc = keyEvent.OccurredAtUtc;
                    _diagnostics.Record(CompatibilityDiagnosticsService.NewEvent(
                        CompatibilityEventKind.HookCallbackReceived,
                        noteCode: "hook.callback_observed"));
                }

                if (!keyEvent.IsKeyDown)
                    Interlocked.Exchange(ref _hookSelfTest, null)?.TrySetResult(true);

                if (!HookEventPolicy.TryGetDelimiter(keyEvent, out var delimiter))
                    continue;

                _diagnostics.Record(CompatibilityDiagnosticsService.NewEvent(
                    CompatibilityEventKind.DelimiterDetected,
                    delimiter: delimiter));
                _diagnostics.UpdateInputEnvironment(_environmentInspector.Capture(delimiter));

                await Task.Delay(TimeSpan.FromMilliseconds(65), cancellationToken);
                await TryExpandAsync(delimiter, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _logger.Error("Expansion.InputWorker", exception);
            _diagnostics.Record(CompatibilityDiagnosticsService.NewEvent(
                CompatibilityEventKind.ExpansionFailed,
                DiagnosticSeverity.Error,
                errorCode: exception.GetType().Name,
                noteCode: "input_worker.failed"));
        }
    }

    private async Task TryExpandAsync(DelimiterKey delimiter, CancellationToken cancellationToken)
    {
        AppSettings settings;
        ShortcutItem[] shortcuts;
        TemporaryTestShortcut? temporaryTest;
        lock (_configurationLock)
        {
            settings = _settings.Clone();
            temporaryTest = _temporaryTest;
            if (temporaryTest is not null && temporaryTest.ExpiresAtUtc <= DateTimeOffset.UtcNow)
            {
                _temporaryTest = null;
                temporaryTest = null;
            }
            shortcuts = _shortcuts;
            if (temporaryTest is not null && temporaryTest.Delimiter == delimiter)
                shortcuts = [.. shortcuts, temporaryTest.Shortcut.Clone()];
        }

        var initialBlock = ExpansionPolicy.GetInitialBlockReason(settings, shortcuts.Length, delimiter);
        if (initialBlock != ExpansionBlockReason.None)
        {
            RecordBlock(initialBlock, delimiter, null, InputContextStatus.InformationUnavailable);
            return;
        }

        if (!_guard.TryEnter(out var lease))
        {
            RecordBlock(
                ExpansionBlockReason.ReentrantRequest,
                delimiter,
                null,
                InputContextStatus.InformationUnavailable);
            return;
        }

        using (lease)
        {
            try
            {
                var integrity = _integrityInspector.InspectForeground();
                var processName = integrity.Target.ProcessName;
                var processDecision = new ProcessExclusionPolicy(settings.ExcludedProcesses).Evaluate(processName);

                if (integrity.IsSecureDesktop)
                {
                    UpdateTargetAndBlock(
                        integrity,
                        ExpansionBlockReason.SecureDesktop,
                        InputContextStatus.AccessDenied,
                        processDecision,
                        false,
                        "target.secure_desktop",
                        delimiter);
                    return;
                }

                if (integrity.Target.IsProtected ||
                    integrity.Target.Status == ProcessInspectionStatus.ProtectedOrSystemProcess ||
                    processDecision == ProcessExclusionReason.ProtectedProcess)
                {
                    UpdateTargetAndBlock(
                        integrity,
                        ExpansionBlockReason.ProtectedTarget,
                        InputContextStatus.AccessDenied,
                        processDecision,
                        false,
                        "target.protected",
                        delimiter);
                    return;
                }

                if (processDecision == ProcessExclusionReason.UserExcluded)
                {
                    UpdateTargetAndBlock(
                        integrity,
                        ExpansionBlockReason.ExcludedApplication,
                        InputContextStatus.InformationUnavailable,
                        processDecision,
                        false,
                        "target.excluded_application",
                        delimiter);
                    return;
                }

                if (processDecision == ProcessExclusionReason.ProcessUnavailable)
                {
                    UpdateTargetAndBlock(
                        integrity,
                        ExpansionBlockReason.InformationUnavailable,
                        InputContextStatus.InformationUnavailable,
                        processDecision,
                        false,
                        "target.process_unavailable",
                        delimiter);
                    return;
                }

                if (integrity.Comparison == IntegrityComparison.TargetHigher)
                {
                    UpdateTargetAndBlock(
                        integrity,
                        ExpansionBlockReason.HigherIntegrityTarget,
                        InputContextStatus.AccessDenied,
                        processDecision,
                        false,
                        "target.higher_integrity",
                        delimiter);
                    return;
                }

                var siteExcluded = false;
                var sitePolicy = new BrowserSitePolicy(settings.ExcludedSites);
                if (sitePolicy.HasRules)
                {
                    var site = _browserSiteInspector.Inspect(processName);
                    siteExcluded = site.Status == BrowserSiteInspectionStatus.Available &&
                                   sitePolicy.IsExcluded(site.Host);
                    if (siteExcluded)
                    {
                        UpdateTargetAndBlock(
                            integrity,
                            ExpansionBlockReason.ExcludedSite,
                            InputContextStatus.InformationUnavailable,
                            processDecision,
                            true,
                            "target.excluded_site",
                            delimiter);
                        return;
                    }
                }

                var environment = _environmentInspector.Capture(delimiter);
                _diagnostics.UpdateInputEnvironment(environment);
                if (environment.ImeComposing == true)
                {
                    UpdateTargetAndBlock(
                        integrity,
                        ExpansionBlockReason.ImeCompositionActive,
                        InputContextStatus.CaretUnavailable,
                        processDecision,
                        siteExcluded,
                        "input.ime_composition_active",
                        delimiter);
                    return;
                }

                var longestTrigger = shortcuts.Max(item => item.TriggerText.EnumerateRunes().Count());
                var inspection = _inspector.Inspect(longestTrigger + 4);
                var inspectionBlock = MapInputContextBlock(inspection.Status);
                if (inspection.Status != InputContextStatus.Available || inspection.Context is null)
                {
                    UpdateTargetAndBlock(
                        integrity,
                        inspectionBlock,
                        inspection.Status,
                        processDecision,
                        siteExcluded,
                        $"input_context.{inspection.Status.ToString().ToLowerInvariant()}",
                        delimiter);
                    return;
                }

                _diagnostics.UpdateTarget(new TargetDiagnosticState(
                    processName,
                    integrity.Target.Integrity,
                    integrity.Comparison,
                    false,
                    false,
                    siteExcluded,
                    false,
                    inspection.Status,
                    ExpansionBlockReason.None,
                    "target.input_available"));
                _diagnostics.Record(CompatibilityDiagnosticsService.NewEvent(
                    CompatibilityEventKind.ShortcutCandidateDetected,
                    processName: processName,
                    delimiter: delimiter,
                    inputContextStatus: inspection.Status,
                    hankiIntegrity: integrity.Hanki.Integrity,
                    targetIntegrity: integrity.Target.Integrity,
                    integrityComparison: integrity.Comparison,
                    keyboardLayout: environment.KeyboardLayout,
                    imeOpen: environment.ImeOpen));

                var match = _matcher.FindExactSuffix(
                    inspection.Context.TextBeforeCaret,
                    shortcuts,
                    delimiter);
                if (match is null)
                {
                    _diagnostics.Record(CompatibilityDiagnosticsService.NewEvent(
                        CompatibilityEventKind.ShortcutNotMatched,
                        processName: processName,
                        delimiter: delimiter,
                        noteCode: "shortcut.no_exact_suffix"));
                    return;
                }

                _diagnostics.Record(CompatibilityDiagnosticsService.NewEvent(
                    CompatibilityEventKind.ShortcutMatched,
                    processName: processName,
                    delimiter: delimiter,
                    shortcutId: match.Id));
                _diagnostics.Record(CompatibilityDiagnosticsService.NewEvent(
                    CompatibilityEventKind.ExpansionInjectionStarted,
                    processName: processName,
                    delimiter: delimiter,
                    shortcutId: match.Id,
                    noteCode: settings.ClipboardCompatibilityMode
                        ? "injection.clipboard_mode"
                        : "injection.direct_mode"));

                var deleteKeyPresses = StringInfo.ParseCombiningCharacters(match.TriggerText).Length + 1;
                var result = await _injection.InjectAsync(
                    deleteKeyPresses,
                    match.ReplacementText,
                    delimiter,
                    settings.ClipboardCompatibilityMode,
                    cancellationToken);
                RecordInjectionResults(result, processName, delimiter, match.Id);

                if (!result.IsSuccess)
                {
                    if (temporaryTest?.Shortcut.Id == match.Id)
                        ClearTemporaryTest();
                    return;
                }

                var usedAt = DateTimeOffset.UtcNow;
                if (match.Id != CompatibilityTestShortcutId)
                {
                    await _repository.IncrementUsageAsync(match.Id, usedAt, cancellationToken);
                    ShortcutUsed?.Invoke(this, match.Id);
                }
                else
                {
                    _diagnostics.SetManualCheckStatus("internal", ManualCheckStatus.Success);
                    ClearTemporaryTest();
                }

                _diagnostics.Record(CompatibilityDiagnosticsService.NewEvent(
                    CompatibilityEventKind.ExpansionCompleted,
                    processName: processName,
                    delimiter: delimiter,
                    shortcutId: match.Id,
                    noteCode: "expansion.success"));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                _logger.Error("Expansion.Process", exception);
                _diagnostics.Record(CompatibilityDiagnosticsService.NewEvent(
                    CompatibilityEventKind.ExpansionFailed,
                    DiagnosticSeverity.Error,
                    delimiter: delimiter,
                    errorCode: exception.GetType().Name,
                    noteCode: "expansion.unhandled_failure"));
            }
        }
    }

    private void RecordInjectionResults(
        ExpansionInjectionResult result,
        string? processName,
        DelimiterKey delimiter,
        string shortcutId)
    {
        RecordInputStage(
            CompatibilityEventKind.BackspaceInjectionResult,
            result.Backspace,
            processName,
            delimiter,
            shortcutId);
        if (result.Text is not null)
        {
            RecordInputStage(
                CompatibilityEventKind.ExpansionTextInjectionResult,
                result.Text,
                processName,
                delimiter,
                shortcutId);
        }
        if (result.Clipboard is not null)
        {
            _diagnostics.Record(CompatibilityDiagnosticsService.NewEvent(
                CompatibilityEventKind.ClipboardPasteResult,
                result.Clipboard.IsSuccess ? DiagnosticSeverity.Information : DiagnosticSeverity.Warning,
                processName,
                delimiter,
                requestedInputs: result.Clipboard.PasteInput.RequestedInputs,
                sentInputs: result.Clipboard.PasteInput.SentInputs,
                errorCode: ErrorCode(result.Clipboard.PasteInput),
                noteCode: result.Clipboard.ResultCode,
                shortcutId: shortcutId));
            if (result.Clipboard.RestoreStatus == ClipboardRestoreStatus.SkippedBecauseClipboardChanged)
            {
                _diagnostics.Record(CompatibilityDiagnosticsService.NewEvent(
                    CompatibilityEventKind.ClipboardRestoreSkipped,
                    processName: processName,
                    delimiter: delimiter,
                    noteCode: "clipboard.user_change_preserved",
                    shortcutId: shortcutId));
            }
            else if (result.Clipboard.RestoreStatus == ClipboardRestoreStatus.Failed)
            {
                _diagnostics.Record(CompatibilityDiagnosticsService.NewEvent(
                    CompatibilityEventKind.ClipboardRestoreFailed,
                    DiagnosticSeverity.Warning,
                    processName,
                    delimiter,
                    noteCode: "clipboard.restore_failed",
                    shortcutId: shortcutId));
            }
        }
        if (result.Delimiter is not null)
        {
            RecordInputStage(
                CompatibilityEventKind.DelimiterInjectionResult,
                result.Delimiter,
                processName,
                delimiter,
                shortcutId);
        }

        var textRequested = result.Text?.RequestedInputs ?? result.Clipboard?.PasteInput.RequestedInputs ?? 0;
        var textSent = result.Text?.SentInputs ?? result.Clipboard?.PasteInput.SentInputs ?? 0;
        _diagnostics.UpdateProcessing(new ProcessingDiagnosticState(
            result.Status,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            result.IsSuccess ? DateTimeOffset.UtcNow : null,
            result.IsSuccess ? null : DateTimeOffset.UtcNow,
            result.Backspace.RequestedInputs,
            result.Backspace.SentInputs,
            textRequested,
            textSent,
            result.Delimiter?.RequestedInputs ?? 0,
            result.Delimiter?.SentInputs ?? 0,
            ExpansionBlockReason.None,
            result.ResultCode));

        if (!result.IsSuccess)
        {
            _diagnostics.Record(CompatibilityDiagnosticsService.NewEvent(
                result.Status == ExpansionResultStatus.PartialFailure
                    ? CompatibilityEventKind.ExpansionPartiallyFailed
                    : CompatibilityEventKind.ExpansionFailed,
                DiagnosticSeverity.Warning,
                processName,
                delimiter,
                noteCode: result.ResultCode,
                shortcutId: shortcutId));
        }
    }

    private void RecordInputStage(
        CompatibilityEventKind kind,
        InputSendResult result,
        string? processName,
        DelimiterKey delimiter,
        string shortcutId)
    {
        _diagnostics.Record(CompatibilityDiagnosticsService.NewEvent(
            kind,
            result.IsSuccess ? DiagnosticSeverity.Information : DiagnosticSeverity.Warning,
            processName,
            delimiter,
            requestedInputs: result.RequestedInputs,
            sentInputs: result.SentInputs,
            errorCode: ErrorCode(result),
            noteCode: result.IsSuccess ? "sendinput.success" : "sendinput.failed",
            shortcutId: shortcutId));
    }

    private static string? ErrorCode(InputSendResult result) =>
        result.IsSuccess ? null : $"win32_{result.Win32Error}";

    private static ExpansionBlockReason MapInputContextBlock(InputContextStatus status) => status switch
    {
        InputContextStatus.SensitiveField => ExpansionBlockReason.SensitiveField,
        InputContextStatus.ReadOnly => ExpansionBlockReason.ReadOnlyField,
        InputContextStatus.UnsupportedControl or InputContextStatus.TextPatternUnavailable =>
            ExpansionBlockReason.UnsupportedControl,
        InputContextStatus.AccessDenied => ExpansionBlockReason.InformationUnavailable,
        _ => ExpansionBlockReason.InputContextUnavailable
    };

    private void UpdateTargetAndBlock(
        IntegrityInspection integrity,
        ExpansionBlockReason blockReason,
        InputContextStatus inputStatus,
        ProcessExclusionReason processDecision,
        bool siteExcluded,
        string reasonCode,
        DelimiterKey delimiter)
    {
        _diagnostics.UpdateTarget(new TargetDiagnosticState(
            integrity.Target.ProcessName,
            integrity.Target.Integrity,
            integrity.Comparison,
            integrity.Target.IsProtected ||
            processDecision == ProcessExclusionReason.ProtectedProcess,
            processDecision == ProcessExclusionReason.UserExcluded,
            siteExcluded,
            inputStatus == InputContextStatus.SensitiveField,
            inputStatus,
            blockReason,
            reasonCode));
        RecordBlock(blockReason, delimiter, integrity.Target.ProcessName, inputStatus);
    }

    private void RecordBlock(
        ExpansionBlockReason reason,
        DelimiterKey delimiter,
        string? processName,
        InputContextStatus inputStatus)
    {
        _diagnostics.Record(CompatibilityDiagnosticsService.NewEvent(
            CompatibilityEventKind.ExpansionBlocked,
            DiagnosticSeverity.Information,
            processName,
            delimiter,
            reason,
            inputStatus,
            noteCode: $"blocked.{reason.ToString().ToLowerInvariant()}"));
    }

    private bool TryStartHook(string noteCode)
    {
        try
        {
            _hook.Start();
            _consecutiveRestartFailures = 0;
            _nextRestartAtUtc = null;
            UpdateHookState();
            _diagnostics.Record(CompatibilityDiagnosticsService.NewEvent(
                CompatibilityEventKind.HookRegistered,
                noteCode: noteCode));
            return true;
        }
        catch (Exception exception)
        {
            _consecutiveRestartFailures++;
            UpdateHookState();
            _logger.Error("Hook.Start", exception);
            _diagnostics.Record(CompatibilityDiagnosticsService.NewEvent(
                CompatibilityEventKind.HookRegistrationFailed,
                DiagnosticSeverity.Error,
                errorCode: exception.GetType().Name,
                noteCode: noteCode));
            return false;
        }
    }

    private async Task RecoverHookAsync(
        bool resetBudget,
        string reasonCode,
        CancellationToken cancellationToken)
    {
        if (!await _recoveryGate.WaitAsync(0, cancellationToken))
            return;
        try
        {
            if (resetBudget)
                _consecutiveRestartFailures = 0;
            while (_consecutiveRestartFailures < 3 && !cancellationToken.IsCancellationRequested)
            {
                var delay = _consecutiveRestartFailures switch
                {
                    0 => TimeSpan.Zero,
                    1 => TimeSpan.FromMilliseconds(250),
                    _ => TimeSpan.FromSeconds(1)
                };
                _nextRestartAtUtc = DateTimeOffset.UtcNow + delay;
                UpdateHookState();
                _diagnostics.Record(CompatibilityDiagnosticsService.NewEvent(
                    CompatibilityEventKind.HookRestartScheduled,
                    DiagnosticSeverity.Warning,
                    noteCode: reasonCode));
                if (delay > TimeSpan.Zero)
                    await Task.Delay(delay, cancellationToken);

                try
                {
                    _hook.Restart();
                    _consecutiveRestartFailures = 0;
                    _nextRestartAtUtc = null;
                    UpdateHookState();
                    _diagnostics.Record(CompatibilityDiagnosticsService.NewEvent(
                        CompatibilityEventKind.HookRestarted,
                        noteCode: reasonCode));
                    return;
                }
                catch (Exception exception)
                {
                    _consecutiveRestartFailures++;
                    _logger.Error("Hook.Restart", exception);
                    _diagnostics.Record(CompatibilityDiagnosticsService.NewEvent(
                        CompatibilityEventKind.HookRegistrationFailed,
                        DiagnosticSeverity.Error,
                        errorCode: exception.GetType().Name,
                        noteCode: reasonCode));
                }
            }

            _nextRestartAtUtc = null;
            UpdateHookState();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            _recoveryGate.Release();
        }
    }

    private async Task MonitorHookAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                UpdateHookState();
                var dropped = Interlocked.Exchange(ref _droppedHookEvents, 0);
                if (dropped > 0)
                {
                    _diagnostics.Record(CompatibilityDiagnosticsService.NewEvent(
                        CompatibilityEventKind.HookHealthCheckFailed,
                        DiagnosticSeverity.Warning,
                        errorCode: "hook_channel_full",
                        noteCode: "hook.events_dropped"));
                }

                if ((!_hook.IsRegistered || !_hook.IsThreadAlive) &&
                    _consecutiveRestartFailures < 3)
                {
                    _diagnostics.Record(CompatibilityDiagnosticsService.NewEvent(
                        CompatibilityEventKind.HookHealthCheckFailed,
                        DiagnosticSeverity.Warning,
                        blockReason: ExpansionBlockReason.HookUnavailable,
                        noteCode: "hook.handle_or_thread_missing"));
                    await RecoverHookAsync(
                        resetBudget: false,
                        "hook.health_recovery",
                        cancellationToken);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private void UpdateHookState()
    {
        _diagnostics.UpdateHook(new HookDiagnosticState(
            _hook.IsRegistered,
            _hook.IsThreadAlive,
            _hook.HookHandleValue,
            _hook.ThreadId,
            _hook.RegisteredAtUtc,
            _hook.LastCallbackAtUtc,
            _diagnostics.Capture().Hook.LastDelimiterAtUtc,
            _consecutiveRestartFailures,
            _nextRestartAtUtc));
    }

    private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
    {
        if (e.Reason is SessionSwitchReason.SessionUnlock or SessionSwitchReason.SessionLogon)
            _ = RecoverHookAsync(resetBudget: true, "hook.session_resume", _shutdown.Token);
    }

    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume)
            _ = RecoverHookAsync(resetBudget: true, "hook.power_resume", _shutdown.Token);
    }

    private void ClearTemporaryTest()
    {
        lock (_configurationLock)
            _temporaryTest = null;
    }

    private sealed record TemporaryTestShortcut(
        DelimiterKey Delimiter,
        DateTimeOffset ExpiresAtUtc,
        ShortcutItem Shortcut);
}
