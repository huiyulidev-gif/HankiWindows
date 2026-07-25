using System.Text;
using Hanki.Core.Contracts;
using Hanki.Core.Models;
using Hanki.Core.Services;
using Hanki.Infrastructure.Logging;

namespace Hanki.Infrastructure.Windows;

public sealed class TextExpansionService : IDisposable
{
    private readonly IShortcutRepository _repository;
    private readonly GlobalKeyboardHook _hook;
    private readonly PrivacySafeLogger _logger;
    private readonly WindowsInputInspector _inspector = new();
    private readonly ShortcutMatcher _matcher = new();
    private readonly ReentrancyGuard _guard = new(TimeSpan.FromMilliseconds(180));
    private readonly object _configurationLock = new();

    private AppSettings _settings = new();
    private ShortcutItem[] _shortcuts = [];
    private bool _disposed;

    public TextExpansionService(
        IShortcutRepository repository,
        GlobalKeyboardHook hook,
        PrivacySafeLogger logger)
    {
        _repository = repository;
        _hook = hook;
        _logger = logger;
        _hook.SpacePressed += OnSpacePressed;
    }

    public event EventHandler<string>? ShortcutUsed;

    public void Start() => _hook.Start();

    public void UpdateConfiguration(AppSettings settings, IEnumerable<ShortcutItem> shortcuts)
    {
        lock (_configurationLock)
        {
            _settings = settings.Clone();
            _shortcuts = shortcuts.Select(item => item.Clone()).ToArray();
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _hook.SpacePressed -= OnSpacePressed;
        _hook.Dispose();
        GC.SuppressFinalize(this);
    }

    private void OnSpacePressed(object? sender, EventArgs eventArgs)
    {
        AppSettings settings;
        ShortcutItem[] shortcuts;
        lock (_configurationLock)
        {
            settings = _settings.Clone();
            shortcuts = _shortcuts;
        }

        if (!settings.IsEnabled || !settings.SpaceExpansionEnabled || shortcuts.Length == 0)
            return;

        _ = Task.Run(async () =>
        {
            await Task.Delay(70);
            await TryExpandAsync(settings, shortcuts);
        });
    }

    private async Task TryExpandAsync(AppSettings settings, ShortcutItem[] shortcuts)
    {
        if (!_guard.TryEnter(out var lease))
            return;
        using (lease)
        {
            try
            {
                var processName = ForegroundProcess.GetName();
                if (new ProcessExclusionPolicy(settings.ExcludedProcesses).IsExcluded(processName))
                    return;

                var longestTrigger = shortcuts.Max(item => item.TriggerText.EnumerateRunes().Count());
                if (!_inspector.TryCapture(longestTrigger + 2, out var context) || context is null)
                    return;

                var match = _matcher.FindExactSuffix(context.TextBeforeCaret, shortcuts);
                if (match is null)
                    return;

                var selectCount = match.TriggerText.EnumerateRunes().Count() + 1;
                if (!context.TrySelectPreviousCharacters(selectCount))
                    return;

                UnicodeInputSender.SendText(match.ReplacementText + " ");
                var usedAt = DateTimeOffset.UtcNow;
                await _repository.IncrementUsageAsync(match.Id, usedAt);
                ShortcutUsed?.Invoke(this, match.Id);
            }
            catch (Exception exception)
            {
                _logger.Error("Expansion.Process", exception);
            }
        }
    }
}
