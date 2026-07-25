namespace Hanki.Core.Models;

public sealed class ShortcutItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("D");
    public string? Title { get; set; }
    public string TriggerText { get; set; } = string.Empty;
    public string ReplacementText { get; set; } = string.Empty;
    public bool IsFavorite { get; set; }
    public long UsageCount { get; set; }
    public DateTimeOffset? LastUsedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ShortcutItem Clone() => new()
    {
        Id = Id,
        Title = Title,
        TriggerText = TriggerText,
        ReplacementText = ReplacementText,
        IsFavorite = IsFavorite,
        UsageCount = UsageCount,
        LastUsedAt = LastUsedAt,
        CreatedAt = CreatedAt,
        UpdatedAt = UpdatedAt
    };
}
