using System.Globalization;
using System.Text.Json;
using Hanki.Core.Contracts;
using Hanki.Core.Models;

namespace Hanki.Infrastructure.Data;

public sealed class SqliteSettingsRepository(SqliteDatabase database) : ISettingsRepository
{
    public async Task<AppSettings> GetAsync(CancellationToken cancellationToken = default)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        await using var connection = database.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT key, value FROM settings;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            values[reader.GetString(0)] = reader.GetString(1);

        var defaults = new AppSettings();
        return new AppSettings
        {
            IsEnabled = ReadBool(values, "is_enabled", defaults.IsEnabled),
            StartWithWindows = ReadBool(values, "start_with_windows", defaults.StartWithWindows),
            MinimizeToTray = ReadBool(values, "minimize_to_tray", defaults.MinimizeToTray),
            SpaceExpansionEnabled = ReadBool(values, "space_enabled", defaults.SpaceExpansionEnabled),
            EnterExpansionEnabled = ReadBool(values, "enter_enabled", defaults.EnterExpansionEnabled),
            TabExpansionEnabled = ReadBool(values, "tab_enabled", defaults.TabExpansionEnabled),
            ExcludedProcesses = ReadList(values, "excluded_processes", defaults.ExcludedProcesses),
            Theme = values.GetValueOrDefault("theme", defaults.Theme),
            FirstRunCompleted = ReadBool(values, "first_run_completed", defaults.FirstRunCompleted)
        };
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        var values = new Dictionary<string, string>
        {
            ["is_enabled"] = settings.IsEnabled.ToString(CultureInfo.InvariantCulture),
            ["start_with_windows"] = settings.StartWithWindows.ToString(CultureInfo.InvariantCulture),
            ["minimize_to_tray"] = settings.MinimizeToTray.ToString(CultureInfo.InvariantCulture),
            ["space_enabled"] = settings.SpaceExpansionEnabled.ToString(CultureInfo.InvariantCulture),
            ["enter_enabled"] = settings.EnterExpansionEnabled.ToString(CultureInfo.InvariantCulture),
            ["tab_enabled"] = settings.TabExpansionEnabled.ToString(CultureInfo.InvariantCulture),
            ["excluded_processes"] = JsonSerializer.Serialize(settings.ExcludedProcesses),
            ["theme"] = settings.Theme,
            ["first_run_completed"] = settings.FirstRunCompleted.ToString(CultureInfo.InvariantCulture)
        };

        await using var connection = database.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        foreach (var pair in values)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = (Microsoft.Data.Sqlite.SqliteTransaction)transaction;
            command.CommandText =
                """
                INSERT INTO settings(key, value) VALUES($key, $value)
                ON CONFLICT(key) DO UPDATE SET value = excluded.value;
                """;
            command.Parameters.AddWithValue("$key", pair.Key);
            command.Parameters.AddWithValue("$value", pair.Value);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
    }

    private static bool ReadBool(
        IReadOnlyDictionary<string, string> values,
        string key,
        bool defaultValue) =>
        values.TryGetValue(key, out var raw) && bool.TryParse(raw, out var value) ? value : defaultValue;

    private static List<string> ReadList(
        IReadOnlyDictionary<string, string> values,
        string key,
        List<string> defaultValue)
    {
        if (!values.TryGetValue(key, out var raw))
            return [.. defaultValue];
        try
        {
            return JsonSerializer.Deserialize<List<string>>(raw) ?? [.. defaultValue];
        }
        catch (JsonException)
        {
            return [.. defaultValue];
        }
    }
}
