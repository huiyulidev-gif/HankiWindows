using Microsoft.Data.Sqlite;
using Hanki.Infrastructure.Logging;

namespace Hanki.Infrastructure.Data;

public sealed class SqliteDatabase(string databasePath, PrivacySafeLogger logger)
{
    public string DatabasePath { get; } = databasePath;

    public SqliteConnection CreateConnection()
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            Pooling = true
        };
        return new SqliteConnection(builder.ToString());
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath)!);
        try
        {
            await InitializeCoreAsync(cancellationToken);
        }
        catch (SqliteException exception)
        {
            logger.Error("Database.Initialize", exception);
            await RecoverCorruptDatabaseAsync(cancellationToken);
        }
    }

    private async Task InitializeCoreAsync(CancellationToken cancellationToken)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using (var pragma = connection.CreateCommand())
        {
            pragma.CommandText = "PRAGMA journal_mode=WAL; PRAGMA foreign_keys=ON; PRAGMA busy_timeout=3000;";
            await pragma.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var integrity = connection.CreateCommand())
        {
            integrity.CommandText = "PRAGMA quick_check;";
            var result = Convert.ToString(await integrity.ExecuteScalarAsync(cancellationToken));
            if (!string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase))
                throw new SqliteException("Database integrity check failed.", 11);
        }

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            CREATE TABLE IF NOT EXISTS shortcuts (
                id TEXT PRIMARY KEY,
                title TEXT,
                trigger_text TEXT NOT NULL UNIQUE COLLATE BINARY,
                replacement_text TEXT NOT NULL,
                is_favorite INTEGER NOT NULL DEFAULT 0,
                usage_count INTEGER NOT NULL DEFAULT 0,
                last_used_at TEXT,
                created_at TEXT NOT NULL,
                updated_at TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS settings (
                key TEXT PRIMARY KEY,
                value TEXT NOT NULL
            );
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);

        await using var seed = connection.CreateCommand();
        seed.CommandText =
            """
            INSERT INTO shortcuts (
                id, title, trigger_text, replacement_text, is_favorite,
                usage_count, last_used_at, created_at, updated_at
            )
            SELECT $id, $title, $trigger, $replacement, 1, 0, NULL, $now, $now
            WHERE NOT EXISTS (SELECT 1 FROM shortcuts);
            """;
        seed.Parameters.AddWithValue("$id", Guid.NewGuid().ToString("D"));
        seed.Parameters.AddWithValue("$title", "문의 답변");
        seed.Parameters.AddWithValue("$trigger", ";문의");
        seed.Parameters.AddWithValue("$replacement", "안녕하세요. 문의해 주셔서 감사합니다.");
        seed.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));
        await seed.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task RecoverCorruptDatabaseAsync(CancellationToken cancellationToken)
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(DatabasePath))
        {
            var recoveryPath = DatabasePath + $".corrupt-{DateTime.UtcNow:yyyyMMddHHmmss}";
            File.Move(DatabasePath, recoveryPath, overwrite: false);
        }

        foreach (var suffix in new[] { "-wal", "-shm" })
        {
            var sidecar = DatabasePath + suffix;
            if (File.Exists(sidecar))
                File.Move(sidecar, sidecar + $".corrupt-{DateTime.UtcNow:yyyyMMddHHmmss}", overwrite: false);
        }

        await InitializeCoreAsync(cancellationToken);
    }
}
