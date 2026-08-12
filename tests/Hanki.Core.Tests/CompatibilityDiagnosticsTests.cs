using System.IO.Compression;
using System.Text;
using Hanki.Core.Diagnostics;
using Hanki.Infrastructure.Diagnostics;

namespace Hanki.Core.Tests;

[TestClass]
public sealed class CompatibilityDiagnosticsTests
{
    [TestMethod]
    public void RecentEvents_AreBoundedToTwoHundred_AndOrdered()
    {
        var diagnostics = new CompatibilityDiagnosticsService();
        for (var index = 0; index < 260; index++)
        {
            diagnostics.Record(CompatibilityDiagnosticsService.NewEvent(
                CompatibilityEventKind.HookCallbackReceived,
                noteCode: "hook.callback_observed"));
        }

        var snapshot = diagnostics.Capture();
        Assert.AreEqual(CompatibilityDiagnosticsService.EventCapacity, snapshot.RecentEvents.Count);
        Assert.AreEqual(61L, snapshot.RecentEvents[0].Sequence);
        Assert.AreEqual(260L, snapshot.RecentEvents[^1].Sequence);
    }

    [TestMethod]
    public void EventSanitization_KeepsOnlyProcessFileName_AndTypedShortcutId()
    {
        var diagnostics = new CompatibilityDiagnosticsService();
        var id = Guid.NewGuid();
        diagnostics.Record(CompatibilityDiagnosticsService.NewEvent(
            CompatibilityEventKind.ShortcutMatched,
            processName: @"C:\Users\private\notepad.exe",
            shortcutId: id.ToString("D"),
            noteCode: "shortcut.no_exact_suffix"));
        diagnostics.Record(CompatibilityDiagnosticsService.NewEvent(
            CompatibilityEventKind.ShortcutMatched,
            processName: @"C:\Users\private\editor.exe",
            shortcutId: ";raw-shortcut",
            noteCode: "shortcut.no_exact_suffix"));

        var events = diagnostics.Capture().RecentEvents;
        Assert.AreEqual("notepad.exe", events[0].ProcessName);
        Assert.AreEqual(id.ToString("D"), events[0].ShortcutId);
        Assert.AreEqual("editor.exe", events[1].ProcessName);
        Assert.IsNull(events[1].ShortcutId);
        Assert.IsFalse(events.Any(item => item.ProcessName?.Contains("Users", StringComparison.Ordinal) == true));
    }

    [TestMethod]
    public async Task ExportZip_ContainsJsonTextAndPrivacy_WithoutSensitivePayloads()
    {
        var diagnostics = new CompatibilityDiagnosticsService();
        diagnostics.Record(CompatibilityDiagnosticsService.NewEvent(
            CompatibilityEventKind.DelimiterDetected,
            processName: "notepad.exe",
            delimiter: DelimiterKey.Space,
            noteCode: "hook.callback_observed"));
        var directory = Path.Combine(Path.GetTempPath(), "HankiTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var zipPath = Path.Combine(directory, "diagnostics.zip");

        try
        {
            await diagnostics.ExportZipAsync(zipPath);

            using var archive = ZipFile.OpenRead(zipPath);
            CollectionAssert.AreEquivalent(
                new[]
                {
                    "hanki-compatibility-diagnostics.json",
                    "hanki-compatibility-diagnostics.txt",
                    "PRIVACY.txt"
                },
                archive.Entries.Select(item => item.FullName).ToArray());
            var combined = new StringBuilder();
            foreach (var entry in archive.Entries)
            {
                using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
                combined.Append(await reader.ReadToEndAsync());
            }

            var content = combined.ToString();
            StringAssert.Contains(content, "hanki.compatibility-diagnostics.v1");
            StringAssert.Contains(content, "notepad.exe");
            Assert.IsFalse(content.Contains("replacementText", StringComparison.OrdinalIgnoreCase));
            Assert.IsFalse(content.Contains("triggerText", StringComparison.OrdinalIgnoreCase));
            Assert.IsFalse(content.Contains("clipboardContent", StringComparison.OrdinalIgnoreCase));
            Assert.IsFalse(content.Contains("windowTitle", StringComparison.OrdinalIgnoreCase));
            Assert.IsFalse(content.Contains(Environment.UserName, StringComparison.OrdinalIgnoreCase));
            Assert.IsFalse(content.Contains(directory, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void ManualCheckStatus_IsIncludedInSnapshot()
    {
        var diagnostics = new CompatibilityDiagnosticsService();
        diagnostics.SetManualCheckStatus("notepad", ManualCheckStatus.DetectedButInjectionFailed);
        var check = diagnostics.Capture().ManualChecks.Single(item => item.TargetCode == "notepad");
        Assert.AreEqual(ManualCheckStatus.DetectedButInjectionFailed, check.Status);
    }
}
