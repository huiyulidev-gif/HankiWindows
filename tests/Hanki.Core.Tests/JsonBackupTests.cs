using Hanki.Core.Models;
using Hanki.Core.Services;
using Hanki.Infrastructure.Data;

namespace Hanki.Core.Tests;

[TestClass]
public sealed class JsonBackupTests
{
    [TestMethod]
    public async Task JsonExportAndImport_RoundTripsShortcutsAndSettings()
    {
        await using var source = await SqliteRepositoryTests.DatabaseFixture.CreateAsync();
        await using var target = await SqliteRepositoryTests.DatabaseFixture.CreateAsync();
        var sourceBackup = new JsonBackupService(source.Shortcuts, source.Settings, new ShortcutValidator());
        var targetBackup = new JsonBackupService(target.Shortcuts, target.Settings, new ShortcutValidator());
        var file = Path.Combine(source.Directory, "backup.json");

        await source.Shortcuts.AddAsync(ShortcutValidatorTests.NewShortcut(";백업", "백업 문장"));
        await source.Settings.SaveAsync(new AppSettings
        {
            IsEnabled = false,
            ExcludedProcesses = ["custom.exe"]
        });

        await sourceBackup.ExportAsync(file);
        var result = await targetBackup.ImportAsync(file, ImportConflictStrategy.Overwrite);
        var imported = await target.Shortcuts.GetAllAsync();
        var settings = await target.Settings.GetAsync();

        Assert.IsTrue(imported.Any(item => item.TriggerText == ";백업" && item.ReplacementText == "백업 문장"));
        Assert.IsTrue(result.Imported >= 1);
        Assert.IsFalse(settings.IsEnabled);
        CollectionAssert.AreEqual(new[] { "custom.exe" }, settings.ExcludedProcesses);
    }

    [TestMethod]
    public async Task RenameStrategy_CreatesUniqueTrigger()
    {
        await using var source = await SqliteRepositoryTests.DatabaseFixture.CreateAsync();
        await using var target = await SqliteRepositoryTests.DatabaseFixture.CreateAsync();
        var file = Path.Combine(source.Directory, "backup.json");
        var validator = new ShortcutValidator();
        await new JsonBackupService(source.Shortcuts, source.Settings, validator).ExportAsync(file);

        var result = await new JsonBackupService(target.Shortcuts, target.Settings, validator)
            .ImportAsync(file, ImportConflictStrategy.Rename);
        var imported = await target.Shortcuts.GetAllAsync();

        Assert.AreEqual(1, result.Renamed);
        Assert.IsTrue(imported.Any(item => item.TriggerText.StartsWith(";문의-가져옴", StringComparison.Ordinal)));
    }
}
