using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Hanki.Core.Diagnostics;

namespace Hanki.Infrastructure.Diagnostics;

public sealed class CompatibilityDiagnosticsService
{
    public const int EventCapacity = 200;
    private const string ReportSchema = "hanki.compatibility-diagnostics.v1";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly object _sync = new();
    private readonly Queue<CompatibilityEvent> _events = new(EventCapacity);
    private readonly Dictionary<string, ManualCompatibilityCheck> _manualChecks =
        CreateDefaultManualChecks().ToDictionary(item => item.TargetCode, StringComparer.Ordinal);

    private long _sequence;
    private HookDiagnosticState _hook = new(false, false, 0, 0, null, null, null, 0, null);
    private TargetDiagnosticState _target = new(
        null,
        ProcessIntegrityLevel.Unknown,
        IntegrityComparison.Unknown,
        false,
        false,
        false,
        false,
        InputContextStatus.InformationUnavailable,
        ExpansionBlockReason.InformationUnavailable,
        "target.not_inspected");
    private InputEnvironmentState _inputEnvironment = new(
        "unknown",
        null,
        null,
        false,
        false,
        Environment.ProcessId == 0 ? 0 : System.Diagnostics.Process.GetCurrentProcess().SessionId,
        null);
    private ProcessingDiagnosticState _processing = new(
        ExpansionResultStatus.None,
        null,
        null,
        null,
        null,
        0,
        0,
        0,
        0,
        0,
        0,
        ExpansionBlockReason.None,
        "processing.none");
    private ProcessIntegrityLevel _hankiIntegrity = ProcessIntegrityLevel.Unknown;

    public event EventHandler? Changed;

    public void Record(CompatibilityEvent diagnosticEvent)
    {
        CompatibilityEvent normalized;
        lock (_sync)
        {
            normalized = diagnosticEvent with
            {
                Sequence = ++_sequence,
                OccurredAtUtc = diagnosticEvent.OccurredAtUtc == default
                    ? DateTimeOffset.UtcNow
                    : diagnosticEvent.OccurredAtUtc,
                ProcessName = SanitizeProcessName(diagnosticEvent.ProcessName),
                KeyboardLayout = SanitizeCode(diagnosticEvent.KeyboardLayout, 32),
                ShortcutId = SanitizeShortcutId(diagnosticEvent.ShortcutId),
                ErrorCode = SanitizeCode(diagnosticEvent.ErrorCode, 80),
                NoteCode = SanitizeCode(diagnosticEvent.NoteCode, 80)
            };

            _events.Enqueue(normalized);
            while (_events.Count > EventCapacity)
                _events.Dequeue();

            ApplyEventToState(normalized);
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void UpdateHook(HookDiagnosticState state)
    {
        lock (_sync)
            _hook = state;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void UpdateTarget(TargetDiagnosticState state)
    {
        lock (_sync)
        {
            _target = state with
            {
                ProcessName = SanitizeProcessName(state.ProcessName),
                ReasonCode = SanitizeCode(state.ReasonCode, 80) ?? "target.unknown"
            };
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void UpdateInputEnvironment(InputEnvironmentState state)
    {
        lock (_sync)
            _inputEnvironment = state with { KeyboardLayout = SanitizeCode(state.KeyboardLayout, 32) ?? "unknown" };
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void UpdateProcessing(ProcessingDiagnosticState state)
    {
        lock (_sync)
            _processing = state with { ResultCode = SanitizeCode(state.ResultCode, 80) ?? "processing.unknown" };
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void SetHankiIntegrity(ProcessIntegrityLevel integrity)
    {
        lock (_sync)
            _hankiIntegrity = integrity;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void SetManualCheckStatus(string targetCode, ManualCheckStatus status)
    {
        lock (_sync)
        {
            if (_manualChecks.TryGetValue(targetCode, out var current))
                _manualChecks[targetCode] = current with { Status = status };
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public CompatibilityDiagnosticSnapshot Capture()
    {
        lock (_sync)
        {
            return new CompatibilityDiagnosticSnapshot(
                GetAppVersion(),
                RuntimeInformation.OSDescription,
                RuntimeInformation.ProcessArchitecture.ToString(),
                _hankiIntegrity,
                DateTimeOffset.UtcNow,
                _hook,
                _target,
                _inputEnvironment,
                _processing,
                _manualChecks.Values.ToArray(),
                _events.ToArray());
        }
    }

    public async Task ExportZipAsync(string zipPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(zipPath);
        var fullPath = Path.GetFullPath(zipPath);
        var parent = Path.GetDirectoryName(fullPath)
            ?? throw new InvalidOperationException("진단 보고서 폴더를 확인할 수 없습니다.");
        Directory.CreateDirectory(parent);

        var snapshot = Capture();
        var report = new DiagnosticReport(
            ReportSchema,
            snapshot,
            new DiagnosticPrivacyStatement(
                "키 입력·단축어·변환문·클립보드·창 제목·사용자 경로는 수집하지 않습니다.",
                [
                    "UTC 시각",
                    "앱/Windows/프로세스 아키텍처",
                    "대상 실행 파일명(경로 제외)",
                    "후크/무결성/IME/키보드 레이아웃 상태",
                    "종결자 종류와 단계별 요청/성공 개수",
                    "열거형 차단/오류 사유"
                ]));

        var json = JsonSerializer.Serialize(report, JsonOptions);
        var text = CreateTextReport(snapshot);

        await using var file = new FileStream(
            fullPath,
            FileMode.Create,
            FileAccess.ReadWrite,
            FileShare.None,
            4096,
            useAsync: true);
        using (var archive = new ZipArchive(file, ZipArchiveMode.Create, leaveOpen: true))
        {
            await WriteEntryAsync(
                archive,
                "hanki-compatibility-diagnostics.json",
                json,
                cancellationToken);
            await WriteEntryAsync(
                archive,
                "hanki-compatibility-diagnostics.txt",
                text,
                cancellationToken);
            await WriteEntryAsync(
                archive,
                "PRIVACY.txt",
                "이 ZIP에는 입력한 문자, 단축어 원문, 변환문, 클립보드, 창 제목, 사용자 이름이나 전체 경로가 포함되지 않습니다.\r\n",
                cancellationToken);
        }

        Record(NewEvent(
            CompatibilityEventKind.DiagnosticReportExported,
            noteCode: "report.zip_exported"));
    }

    public static CompatibilityEvent NewEvent(
        CompatibilityEventKind kind,
        DiagnosticSeverity severity = DiagnosticSeverity.Information,
        string? processName = null,
        DelimiterKey? delimiter = null,
        ExpansionBlockReason? blockReason = null,
        InputContextStatus? inputContextStatus = null,
        int? requestedInputs = null,
        int? sentInputs = null,
        string? errorCode = null,
        string? noteCode = null,
        string? shortcutId = null,
        ProcessIntegrityLevel? hankiIntegrity = null,
        ProcessIntegrityLevel? targetIntegrity = null,
        IntegrityComparison? integrityComparison = null,
        string? keyboardLayout = null,
        bool? imeOpen = null) =>
        new(
            0,
            DateTimeOffset.UtcNow,
            kind,
            severity,
            processName,
            hankiIntegrity,
            targetIntegrity,
            integrityComparison,
            keyboardLayout,
            imeOpen,
            delimiter,
            shortcutId,
            requestedInputs,
            sentInputs,
            blockReason,
            inputContextStatus,
            errorCode,
            noteCode);

    private void ApplyEventToState(CompatibilityEvent diagnosticEvent)
    {
        switch (diagnosticEvent.Kind)
        {
            case CompatibilityEventKind.HookCallbackReceived:
                _hook = _hook with { LastCallbackAtUtc = diagnosticEvent.OccurredAtUtc };
                break;
            case CompatibilityEventKind.DelimiterDetected:
                _hook = _hook with { LastDelimiterAtUtc = diagnosticEvent.OccurredAtUtc };
                _processing = _processing with { LastCandidateAtUtc = diagnosticEvent.OccurredAtUtc };
                break;
            case CompatibilityEventKind.ShortcutMatched:
                _processing = _processing with { LastMatchAtUtc = diagnosticEvent.OccurredAtUtc };
                break;
            case CompatibilityEventKind.ExpansionCompleted:
                _processing = _processing with
                {
                    Status = ExpansionResultStatus.Success,
                    LastSuccessAtUtc = diagnosticEvent.OccurredAtUtc,
                    BlockReason = ExpansionBlockReason.None,
                    ResultCode = diagnosticEvent.NoteCode ?? "expansion.success"
                };
                break;
            case CompatibilityEventKind.ExpansionPartiallyFailed:
                _processing = _processing with
                {
                    Status = ExpansionResultStatus.PartialFailure,
                    LastFailureAtUtc = diagnosticEvent.OccurredAtUtc,
                    ResultCode = diagnosticEvent.NoteCode ?? "expansion.partial_failure"
                };
                break;
            case CompatibilityEventKind.ExpansionFailed:
                _processing = _processing with
                {
                    Status = ExpansionResultStatus.Failed,
                    LastFailureAtUtc = diagnosticEvent.OccurredAtUtc,
                    ResultCode = diagnosticEvent.NoteCode ?? "expansion.failed"
                };
                break;
            case CompatibilityEventKind.ExpansionBlocked:
                _processing = _processing with
                {
                    Status = ExpansionResultStatus.Blocked,
                    LastFailureAtUtc = diagnosticEvent.OccurredAtUtc,
                    BlockReason = diagnosticEvent.BlockReason ?? ExpansionBlockReason.InformationUnavailable,
                    ResultCode = diagnosticEvent.NoteCode ?? "expansion.blocked"
                };
                break;
            case CompatibilityEventKind.ShortcutNotMatched:
                _processing = _processing with
                {
                    Status = ExpansionResultStatus.NoMatch,
                    ResultCode = diagnosticEvent.NoteCode ?? "shortcut.not_matched"
                };
                break;
        }
    }

    private static async Task WriteEntryAsync(
        ZipArchive archive,
        string entryName,
        string content,
        CancellationToken cancellationToken)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        await using var stream = entry.Open();
        await using var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: false);
        await writer.WriteAsync(content.AsMemory(), cancellationToken);
    }

    private static string CreateTextReport(CompatibilityDiagnosticSnapshot snapshot)
    {
        var builder = new StringBuilder();
        builder.AppendLine("한키 호환성 진단 보고서");
        builder.AppendLine($"생성 시각(UTC): {snapshot.CapturedAtUtc:O}");
        builder.AppendLine($"앱 버전: {snapshot.AppVersion}");
        builder.AppendLine($"Windows: {snapshot.WindowsVersion}");
        builder.AppendLine($"프로세스 아키텍처: {snapshot.ProcessArchitecture}");
        builder.AppendLine($"한키 무결성: {snapshot.HankiIntegrity}");
        builder.AppendLine();
        builder.AppendLine("[후크]");
        builder.AppendLine($"등록: {snapshot.Hook.IsRegistered}");
        builder.AppendLine($"스레드 생존: {snapshot.Hook.IsThreadAlive}");
        builder.AppendLine($"등록 시각(UTC): {snapshot.Hook.RegisteredAtUtc:O}");
        builder.AppendLine($"마지막 키 감지(UTC): {snapshot.Hook.LastCallbackAtUtc:O}");
        builder.AppendLine($"마지막 종결자 감지(UTC): {snapshot.Hook.LastDelimiterAtUtc:O}");
        builder.AppendLine($"연속 재시작 실패: {snapshot.Hook.ConsecutiveRestartFailures}");
        builder.AppendLine();
        builder.AppendLine("[현재 대상]");
        builder.AppendLine($"프로세스 파일명: {snapshot.Target.ProcessName ?? "확인 불가"}");
        builder.AppendLine($"대상 무결성: {snapshot.Target.Integrity}");
        builder.AppendLine($"권한 비교: {snapshot.Target.IntegrityComparison}");
        builder.AppendLine($"입력 문맥: {snapshot.Target.InputContextStatus}");
        builder.AppendLine($"차단 사유: {snapshot.Target.BlockReason}");
        builder.AppendLine($"판정 코드: {snapshot.Target.ReasonCode}");
        builder.AppendLine();
        builder.AppendLine("[입력 환경]");
        builder.AppendLine($"키보드 레이아웃: {snapshot.InputEnvironment.KeyboardLayout}");
        builder.AppendLine($"IME 열림: {snapshot.InputEnvironment.ImeOpen?.ToString() ?? "확인 불가"}");
        builder.AppendLine($"IME 조합 중: {snapshot.InputEnvironment.ImeComposing?.ToString() ?? "확인 불가"}");
        builder.AppendLine($"Caps Lock: {snapshot.InputEnvironment.CapsLock}");
        builder.AppendLine($"Num Lock: {snapshot.InputEnvironment.NumLock}");
        builder.AppendLine($"Windows 세션: {snapshot.InputEnvironment.WindowsSessionId}");
        builder.AppendLine();
        builder.AppendLine("[마지막 처리]");
        builder.AppendLine($"결과: {snapshot.Processing.Status}");
        builder.AppendLine($"삭제: {snapshot.Processing.BackspaceSent}/{snapshot.Processing.BackspaceRequested}");
        builder.AppendLine($"변환문: {snapshot.Processing.TextSent}/{snapshot.Processing.TextRequested}");
        builder.AppendLine($"종결자: {snapshot.Processing.DelimiterSent}/{snapshot.Processing.DelimiterRequested}");
        builder.AppendLine($"결과 코드: {snapshot.Processing.ResultCode}");
        builder.AppendLine();
        builder.AppendLine("[수동 확인]");
        foreach (var check in snapshot.ManualChecks)
            builder.AppendLine($"{check.DisplayName}: {check.Status}");
        builder.AppendLine();
        builder.AppendLine($"[최근 이벤트 {snapshot.RecentEvents.Count}개]");
        foreach (var item in snapshot.RecentEvents)
        {
            builder.Append(item.Sequence).Append('\t')
                .Append(item.OccurredAtUtc.ToString("O")).Append('\t')
                .Append(item.Kind).Append('\t')
                .Append(item.Severity).Append('\t')
                .Append(item.ProcessName ?? "-").Append('\t')
                .Append(item.Delimiter?.ToString() ?? "-").Append('\t')
                .Append(item.BlockReason?.ToString() ?? "-").Append('\t')
                .Append(item.RequestedInputs?.ToString() ?? "-").Append('/')
                .Append(item.SentInputs?.ToString() ?? "-").Append('\t')
                .Append(item.ErrorCode ?? "-").Append('\t')
                .AppendLine(item.NoteCode ?? "-");
        }
        builder.AppendLine();
        builder.AppendLine("[개인정보]");
        builder.AppendLine("키 입력·단축어·변환문·클립보드·창 제목·사용자 이름·전체 파일 경로는 포함하지 않습니다.");
        return builder.ToString();
    }

    private static string GetAppVersion()
    {
        var assembly = Assembly.GetEntryAssembly() ?? typeof(CompatibilityDiagnosticsService).Assembly;
        return assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString()
            ?? "unknown";
    }

    private static string? SanitizeProcessName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var fileName = Path.GetFileName(value.Trim());
        if (fileName.Length > 128)
            fileName = fileName[..128];
        return new string(fileName
            .Where(character => char.IsLetterOrDigit(character) || character is '.' or '_' or '-')
            .ToArray());
    }

    private static string? SanitizeShortcutId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        return Guid.TryParse(value, out var id) ? id.ToString("D") : null;
    }

    private static string? SanitizeCode(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var sanitized = new string(value
            .Take(maxLength)
            .Where(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-')
            .ToArray());
        return sanitized.Length == 0 ? null : sanitized;
    }

    private static IEnumerable<ManualCompatibilityCheck> CreateDefaultManualChecks()
    {
        yield return new("internal", "한키 내부 테스트 입력창", ManualCheckStatus.NotTested);
        yield return new("notepad", "일반 메모장", ManualCheckStatus.NotTested);
        yield return new("notepad_elevated", "관리자 권한 메모장", ManualCheckStatus.NotTested);
        yield return new("browser_address", "브라우저 주소창", ManualCheckStatus.NotTested);
        yield return new("browser_input", "브라우저 일반 입력창", ManualCheckStatus.NotTested);
        yield return new("discord", "Discord", ManualCheckStatus.NotTested);
        yield return new("affected_app", "문제가 발생한 게임/프로그램", ManualCheckStatus.NotTested);
        yield return new("harness", "WPF Compatibility Harness", ManualCheckStatus.NotTested);
    }

    private sealed record DiagnosticPrivacyStatement(string ExcludedData, IReadOnlyList<string> IncludedData);
    private sealed record DiagnosticReport(
        string Schema,
        CompatibilityDiagnosticSnapshot Snapshot,
        DiagnosticPrivacyStatement Privacy);
}
