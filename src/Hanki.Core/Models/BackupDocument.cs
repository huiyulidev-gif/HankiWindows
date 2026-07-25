namespace Hanki.Core.Models;

public sealed class BackupDocument
{
    public int SchemaVersion { get; set; } = 1;
    public DateTimeOffset ExportedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<ShortcutItem> Shortcuts { get; set; } = [];
    public AppSettings Settings { get; set; } = new();
}

public enum ImportConflictStrategy
{
    Skip,
    Overwrite,
    Rename
}

public sealed record ImportResult(int Imported, int Skipped, int Updated, int Renamed);
