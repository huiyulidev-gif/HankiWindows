namespace Hanki.Core.Diagnostics;

public enum CompatibilityEventKind
{
    AppStarted,
    HookRegistered,
    HookRegistrationFailed,
    HookCallbackReceived,
    HookHealthCheckFailed,
    HookRestartScheduled,
    HookRestarted,
    HookStopped,
    HookSelfTestStarted,
    HookSelfTestSucceeded,
    HookSelfTestTimedOut,
    ForegroundProcessChanged,
    ImeStateChanged,
    KeyboardLayoutChanged,
    DelimiterDetected,
    ShortcutCandidateDetected,
    ShortcutMatched,
    ShortcutNotMatched,
    ExpansionBlocked,
    ExpansionInjectionStarted,
    BackspaceInjectionResult,
    ExpansionTextInjectionResult,
    DelimiterInjectionResult,
    ClipboardPasteStarted,
    ClipboardPasteResult,
    ClipboardRestoreSkipped,
    ClipboardRestoreFailed,
    ExpansionCompleted,
    ExpansionPartiallyFailed,
    ExpansionFailed,
    DiagnosticReportExported
}

public enum DiagnosticSeverity
{
    Information,
    Warning,
    Error
}

public enum DelimiterKey
{
    Space,
    Enter,
    NumpadEnter,
    Tab
}

public enum ExpansionBlockReason
{
    None,
    FeatureDisabled,
    Paused,
    DelimiterDisabled,
    NoShortcuts,
    ExcludedApplication,
    ExcludedSite,
    SensitiveField,
    ProtectedTarget,
    SecureDesktop,
    HigherIntegrityTarget,
    InputContextUnavailable,
    ReadOnlyField,
    UnsupportedControl,
    ImeCompositionActive,
    ReentrantRequest,
    HookUnavailable,
    InformationUnavailable
}

public enum ProcessIntegrityLevel
{
    Unknown,
    Untrusted,
    Low,
    Medium,
    MediumPlus,
    High,
    System,
    Protected
}

public enum IntegrityComparison
{
    Unknown,
    Same,
    HankiHigher,
    TargetHigher
}

public enum InputContextStatus
{
    Available,
    NoFocusedElement,
    SensitiveField,
    Disabled,
    ReadOnly,
    NotKeyboardFocusable,
    UnsupportedControl,
    TextPatternUnavailable,
    SelectionUnavailable,
    CaretUnavailable,
    AccessDenied,
    ElementUnavailable,
    InformationUnavailable
}

public enum ExpansionResultStatus
{
    None,
    Blocked,
    NoMatch,
    Success,
    PartialFailure,
    Failed
}

public enum ManualCheckStatus
{
    NotTested,
    Success,
    DetectedButInjectionFailed,
    NoResponse,
    NotAvailable
}

public sealed record CompatibilityEvent(
    long Sequence,
    DateTimeOffset OccurredAtUtc,
    CompatibilityEventKind Kind,
    DiagnosticSeverity Severity = DiagnosticSeverity.Information,
    string? ProcessName = null,
    ProcessIntegrityLevel? HankiIntegrity = null,
    ProcessIntegrityLevel? TargetIntegrity = null,
    IntegrityComparison? IntegrityComparison = null,
    string? KeyboardLayout = null,
    bool? ImeOpen = null,
    DelimiterKey? Delimiter = null,
    string? ShortcutId = null,
    int? RequestedInputs = null,
    int? SentInputs = null,
    ExpansionBlockReason? BlockReason = null,
    InputContextStatus? InputContextStatus = null,
    string? ErrorCode = null,
    string? NoteCode = null);

public sealed record HookDiagnosticState(
    bool IsRegistered,
    bool IsThreadAlive,
    long HandleValue,
    int ThreadId,
    DateTimeOffset? RegisteredAtUtc,
    DateTimeOffset? LastCallbackAtUtc,
    DateTimeOffset? LastDelimiterAtUtc,
    int ConsecutiveRestartFailures,
    DateTimeOffset? NextRestartAtUtc);

public sealed record TargetDiagnosticState(
    string? ProcessName,
    ProcessIntegrityLevel Integrity,
    IntegrityComparison IntegrityComparison,
    bool IsProtected,
    bool IsExcludedApplication,
    bool IsExcludedSite,
    bool IsSensitiveField,
    InputContextStatus InputContextStatus,
    ExpansionBlockReason BlockReason,
    string ReasonCode);

public sealed record InputEnvironmentState(
    string KeyboardLayout,
    bool? ImeOpen,
    bool? ImeComposing,
    bool CapsLock,
    bool NumLock,
    int WindowsSessionId,
    DelimiterKey? SelectedDelimiter);

public sealed record ProcessingDiagnosticState(
    ExpansionResultStatus Status,
    DateTimeOffset? LastCandidateAtUtc,
    DateTimeOffset? LastMatchAtUtc,
    DateTimeOffset? LastSuccessAtUtc,
    DateTimeOffset? LastFailureAtUtc,
    int BackspaceRequested,
    int BackspaceSent,
    int TextRequested,
    int TextSent,
    int DelimiterRequested,
    int DelimiterSent,
    ExpansionBlockReason BlockReason,
    string ResultCode);

public sealed record ManualCompatibilityCheck(
    string TargetCode,
    string DisplayName,
    ManualCheckStatus Status);

public sealed record CompatibilityDiagnosticSnapshot(
    string AppVersion,
    string WindowsVersion,
    string ProcessArchitecture,
    ProcessIntegrityLevel HankiIntegrity,
    DateTimeOffset CapturedAtUtc,
    HookDiagnosticState Hook,
    TargetDiagnosticState Target,
    InputEnvironmentState InputEnvironment,
    ProcessingDiagnosticState Processing,
    IReadOnlyList<ManualCompatibilityCheck> ManualChecks,
    IReadOnlyList<CompatibilityEvent> RecentEvents);
