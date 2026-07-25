using Hanki.Core.Contracts;
using Hanki.Core.Exceptions;
using Hanki.Core.Models;
using Hanki.Core.Services;
using Microsoft.Data.Sqlite;

namespace Hanki.Infrastructure.Data;

public sealed class SqliteShortcutRepository(SqliteDatabase database, ShortcutValidator validator)
    : IShortcutRepository
{
    public async Task<IReadOnlyList<ShortcutItem>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<ShortcutItem>();
        await using var connection = database.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT id, title, trigger_text, replacement_text, is_favorite,
                   usage_count, last_used_at, created_at, updated_at
            FROM shortcuts
            ORDER BY is_favorite DESC, updated_at DESC;
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            results.Add(Read(reader));
        return results;
    }

    public async Task AddAsync(ShortcutItem shortcut, CancellationToken cancellationToken = default)
    {
        var item = validator.NormalizeAndValidate(shortcut);
        await using var connection = database.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO shortcuts (
                id, title, trigger_text, replacement_text, is_favorite,
                usage_count, last_used_at, created_at, updated_at
            ) VALUES (
                $id, $title, $trigger, $replacement, $favorite,
                $usage, $lastUsed, $created, $updated
            );
            """;
        AddParameters(command, item);
        try
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (SqliteException exception) when (exception.SqliteErrorCode == 19)
        {
            throw new DuplicateTriggerException(item.TriggerText);
        }
    }

    public async Task UpdateAsync(ShortcutItem shortcut, CancellationToken cancellationToken = default)
    {
        var item = validator.NormalizeAndValidate(shortcut);
        await using var connection = database.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE shortcuts SET
                title = $title,
                trigger_text = $trigger,
                replacement_text = $replacement,
                is_favorite = $favorite,
                usage_count = $usage,
                last_used_at = $lastUsed,
                updated_at = $updated
            WHERE id = $id;
            """;
        AddParameters(command, item);
        try
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (SqliteException exception) when (exception.SqliteErrorCode == 19)
        {
            throw new DuplicateTriggerException(item.TriggerText);
        }
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        await using var connection = database.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM shortcuts WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task IncrementUsageAsync(
        string id,
        DateTimeOffset usedAt,
        CancellationToken cancellationToken = default)
    {
        await using var connection = database.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE shortcuts
            SET usage_count = usage_count + 1,
                last_used_at = $usedAt,
                updated_at = $usedAt
            WHERE id = $id;
            """;
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$usedAt", usedAt.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AddParameters(SqliteCommand command, ShortcutItem item)
    {
        command.Parameters.AddWithValue("$id", item.Id);
        command.Parameters.AddWithValue("$title", (object?)item.Title ?? DBNull.Value);
        command.Parameters.AddWithValue("$trigger", item.TriggerText);
        command.Parameters.AddWithValue("$replacement", item.ReplacementText);
        command.Parameters.AddWithValue("$favorite", item.IsFavorite ? 1 : 0);
        command.Parameters.AddWithValue("$usage", item.UsageCount);
        command.Parameters.AddWithValue("$lastUsed", (object?)item.LastUsedAt?.ToString("O") ?? DBNull.Value);
        command.Parameters.AddWithValue("$created", item.CreatedAt.ToString("O"));
        command.Parameters.AddWithValue("$updated", item.UpdatedAt.ToString("O"));
    }

    private static ShortcutItem Read(SqliteDataReader reader) => new()
    {
        Id = reader.GetString(0),
        Title = reader.IsDBNull(1) ? null : reader.GetString(1),
        TriggerText = reader.GetString(2),
        ReplacementText = reader.GetString(3),
        IsFavorite = reader.GetInt64(4) != 0,
        UsageCount = reader.GetInt64(5),
        LastUsedAt = reader.IsDBNull(6) ? null : DateTimeOffset.Parse(reader.GetString(6)),
        CreatedAt = DateTimeOffset.Parse(reader.GetString(7)),
        UpdatedAt = DateTimeOffset.Parse(reader.GetString(8))
    };
}
