using Hanki.Core.Exceptions;
using Hanki.Core.Models;
using Hanki.Core.Services;
using Hanki.Infrastructure.Data;
using Hanki.Infrastructure.Logging;
using Microsoft.Data.Sqlite;

namespace Hanki.Core.Tests;

[TestClass]
public sealed class SqliteRepositoryTests
{
    [TestMethod]
    public async Task DuplicateTrigger_IsRejected_AndComparisonIsCaseSensitive()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        var lower = ShortcutValidatorTests.NewShortcut(";hello", "one");
        var same = ShortcutValidatorTests.NewShortcut(";hello", "two");
        var upper = ShortcutValidatorTests.NewShortcut(";Hello", "three");
        await fixture.Shortcuts.AddAsync(lower);
        await Assert.ThrowsExceptionAsync<DuplicateTriggerException>(
            () => fixture.Shortcuts.AddAsync(same));
        await fixture.Shortcuts.AddAsync(upper);
    }

    [TestMethod]
    public async Task UsageIncrement_IsPersisted()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        var shortcut = ShortcutValidatorTests.NewShortcut(";count", "value");
        await fixture.Shortcuts.AddAsync(shortcut);
        await fixture.Shortcuts.IncrementUsageAsync(shortcut.Id, DateTimeOffset.UtcNow);
        var stored = (await fixture.Shortcuts.GetAllAsync()).Single(item => item.Id == shortcut.Id);
        Assert.AreEqual(1, stored.UsageCount);
        Assert.IsNotNull(stored.LastUsedAt);
    }

    [TestMethod]
    public async Task Settings_AreSavedAndLoaded()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        var settings = new AppSettings
        {
            IsEnabled = false,
            StartWithWindows = true,
            ExcludedProcesses = ["example.exe"]
        };
        await fixture.Settings.SaveAsync(settings);
        var loaded = await fixture.Settings.GetAsync();
        Assert.IsFalse(loaded.IsEnabled);
        Assert.IsTrue(loaded.StartWithWindows);
        CollectionAssert.AreEqual(new[] { "example.exe" }, loaded.ExcludedProcesses);
    }

    internal sealed class DatabaseFixture : IAsyncDisposable
    {
        private DatabaseFixture(
            string directory,
            SqliteShortcutRepository shortcuts,
            SqliteSettingsRepository settings)
        {
            Directory = directory;
            Shortcuts = shortcuts;
            Settings = settings;
        }

        public string Directory { get; }
        public SqliteShortcutRepository Shortcuts { get; }
        public SqliteSettingsRepository Settings { get; }

        public static async Task<DatabaseFixture> CreateAsync()
        {
            var directory = Path.Combine(Path.GetTempPath(), "HankiTests", Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(directory);
            var database = new SqliteDatabase(Path.Combine(directory, "test.db"), new PrivacySafeLogger());
            await database.InitializeAsync();
            return new DatabaseFixture(
                directory,
                new SqliteShortcutRepository(database, new ShortcutValidator()),
                new SqliteSettingsRepository(database));
        }

        public ValueTask DisposeAsync()
        {
            SqliteConnection.ClearAllPools();
            if (System.IO.Directory.Exists(Directory))
                System.IO.Directory.Delete(Directory, recursive: true);
            return ValueTask.CompletedTask;
        }
    }
}
