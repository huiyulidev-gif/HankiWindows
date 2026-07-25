using System.Text;
using Hanki.Core.Exceptions;
using Hanki.Core.Models;

namespace Hanki.Core.Services;

public sealed class ShortcutValidator
{
    public ShortcutItem NormalizeAndValidate(ShortcutItem shortcut)
    {
        ArgumentNullException.ThrowIfNull(shortcut);

        var normalized = shortcut.Clone();
        normalized.TriggerText = normalized.TriggerText?.Trim() ?? string.Empty;
        normalized.Title = string.IsNullOrWhiteSpace(normalized.Title) ? null : normalized.Title.Trim();

        var errors = new List<string>();
        var triggerLength = RuneCount(normalized.TriggerText);
        var replacementLength = RuneCount(normalized.ReplacementText);
        var titleLength = RuneCount(normalized.Title ?? string.Empty);

        if (triggerLength is < 1 or > 100)
            errors.Add("단축어는 1~100자여야 합니다.");
        if (string.IsNullOrWhiteSpace(normalized.ReplacementText) || replacementLength is < 1 or > 10_000)
            errors.Add("변환 문장은 공백만 입력할 수 없으며 1~10,000자여야 합니다.");
        if (titleLength > 120)
            errors.Add("제목은 최대 120자까지 입력할 수 있습니다.");

        if (errors.Count > 0)
            throw new ShortcutValidationException(errors);

        var now = DateTimeOffset.UtcNow;
        if (string.IsNullOrWhiteSpace(normalized.Id))
            normalized.Id = Guid.NewGuid().ToString("D");
        if (normalized.CreatedAt == default)
            normalized.CreatedAt = now;
        normalized.UpdatedAt = now;
        return normalized;
    }

    private static int RuneCount(string value) => value.EnumerateRunes().Count();
}
