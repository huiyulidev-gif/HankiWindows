using System.Text.Json;
using Hanki.Core.Contracts;
using Hanki.Core.Models;
using Hanki.Core.Services;

namespace Hanki.Infrastructure.Data;

public sealed class JsonBackupService(
    IShortcutRepository shortcutRepository,
    ISettingsRepository settingsRepository,
    ShortcutValidator validator) : IBackupService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public async Task ExportAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var document = new BackupDocument
        {
            SchemaVersion = 1,
            ExportedAt = DateTimeOffset.UtcNow,
            Shortcuts = (await shortcutRepository.GetAllAsync(cancellationToken))
                .Select(item => item.Clone()).ToList(),
            Settings = (await settingsRepository.GetAsync(cancellationToken)).Clone()
        };
        await using var stream = File.Create(filePath);
        await JsonSerializer.SerializeAsync(stream, document, JsonOptions, cancellationToken);
    }

    public async Task<ImportResult> ImportAsync(
        string filePath,
        ImportConflictStrategy strategy,
        CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(filePath);
        var document = await JsonSerializer.DeserializeAsync<BackupDocument>(stream, JsonOptions, cancellationToken)
            ?? throw new InvalidDataException("백업 파일을 읽을 수 없습니다.");
        if (document.SchemaVersion != 1)
            throw new InvalidDataException("지원하지 않는 백업 스키마 버전입니다.");
        if (document.Shortcuts is null)
            throw new InvalidDataException("단축어 목록이 없습니다.");

        var existing = (await shortcutRepository.GetAllAsync(cancellationToken))
            .ToDictionary(item => item.TriggerText, item => item, StringComparer.Ordinal);
        var imported = 0;
        var skipped = 0;
        var updated = 0;
        var renamed = 0;

        foreach (var source in document.Shortcuts)
        {
            var item = validator.NormalizeAndValidate(source);
            if (!existing.TryGetValue(item.TriggerText, out var duplicate))
            {
                item.Id = Guid.NewGuid().ToString("D");
                await shortcutRepository.AddAsync(item, cancellationToken);
                existing[item.TriggerText] = item;
                imported++;
                continue;
            }

            if (strategy == ImportConflictStrategy.Skip)
            {
                skipped++;
                continue;
            }

            if (strategy == ImportConflictStrategy.Overwrite)
            {
                item.Id = duplicate.Id;
                item.CreatedAt = duplicate.CreatedAt;
                await shortcutRepository.UpdateAsync(item, cancellationToken);
                existing[item.TriggerText] = item;
                updated++;
                continue;
            }

            item.TriggerText = CreateUniqueTrigger(item.TriggerText, existing.Keys);
            item.Id = Guid.NewGuid().ToString("D");
            item = validator.NormalizeAndValidate(item);
            await shortcutRepository.AddAsync(item, cancellationToken);
            existing[item.TriggerText] = item;
            renamed++;
        }

        if (document.Settings is not null)
            await settingsRepository.SaveAsync(document.Settings, cancellationToken);

        return new ImportResult(imported, skipped, updated, renamed);
    }

    private static string CreateUniqueTrigger(string trigger, IEnumerable<string> existing)
    {
        var set = new HashSet<string>(existing, StringComparer.Ordinal);
        const string suffix = "-가져옴";
        var baseText = trigger.Length + suffix.Length <= 100
            ? trigger
            : trigger[..(100 - suffix.Length)];
        var candidate = baseText + suffix;
        var index = 2;
        while (set.Contains(candidate))
        {
            var numberedSuffix = $"{suffix}-{index++}";
            var prefix = trigger.Length + numberedSuffix.Length <= 100
                ? trigger
                : trigger[..(100 - numberedSuffix.Length)];
            candidate = prefix + numberedSuffix;
        }
        return candidate;
    }
}
