using Hanki.Core.Diagnostics;
using Hanki.Core.Models;
using Hanki.Core.Services;
using Hanki.Infrastructure.Windows;

namespace Hanki.Core.Tests;

[TestClass]
public sealed class CompatibilityPolicyTests
{
    [DataTestMethod]
    [DataRow("hello ", DelimiterKey.Space)]
    [DataRow("hello\t", DelimiterKey.Tab)]
    [DataRow("hello\r", DelimiterKey.Enter)]
    [DataRow("hello\n", DelimiterKey.Enter)]
    [DataRow("hello\r\n", DelimiterKey.Enter)]
    [DataRow("hello\r\n", DelimiterKey.NumpadEnter)]
    public void Matcher_SupportsEveryDelimiterRepresentation(string text, DelimiterKey delimiter)
    {
        var matcher = new ShortcutMatcher();
        var shortcut = new ShortcutItem { TriggerText = "hello", ReplacementText = "world" };
        Assert.AreSame(shortcut, matcher.FindExactSuffix(text, [shortcut], delimiter));
    }

    [TestMethod]
    public void Matcher_RequiresWordBoundary_AndExactDelimiter()
    {
        var matcher = new ShortcutMatcher();
        var shortcut = new ShortcutItem { TriggerText = "hello", ReplacementText = "world" };
        Assert.IsNull(matcher.FindExactSuffix("xhello ", [shortcut], DelimiterKey.Space));
        Assert.IsNull(matcher.FindExactSuffix("hello\t", [shortcut], DelimiterKey.Space));
        Assert.IsNull(matcher.FindExactSuffix("hello", [shortcut], DelimiterKey.Enter));
    }

    [TestMethod]
    public void ProcessPolicy_DistinguishesUnavailableProtectedAndUserExcluded()
    {
        var policy = new ProcessExclusionPolicy(["custom.exe"]);
        Assert.AreEqual(ProcessExclusionReason.ProcessUnavailable, policy.Evaluate(null));
        Assert.AreEqual(ProcessExclusionReason.ProtectedProcess, policy.Evaluate("consent.exe"));
        Assert.AreEqual(ProcessExclusionReason.UserExcluded, policy.Evaluate("CUSTOM"));
        Assert.AreEqual(ProcessExclusionReason.None, policy.Evaluate("notepad.exe"));
    }

    [TestMethod]
    public void SitePolicy_NormalizesDomains_AndMatchesSubdomainsOnly()
    {
        var policy = new BrowserSitePolicy(["https://Example.com/path", "xn--3e0b707e"]);
        Assert.IsTrue(policy.IsExcluded("example.com"));
        Assert.IsTrue(policy.IsExcluded("sub.example.com"));
        Assert.IsFalse(policy.IsExcluded("notexample.com"));
        Assert.IsNull(BrowserSitePolicy.NormalizeHost("not a host"));
    }

    [TestMethod]
    public void IntegrityComparison_DistinguishesTargetHigher()
    {
        Assert.AreEqual(
            IntegrityComparison.TargetHigher,
            ProcessIntegrityInspector.Compare(ProcessIntegrityLevel.Medium, ProcessIntegrityLevel.High));
        Assert.AreEqual(
            IntegrityComparison.HankiHigher,
            ProcessIntegrityInspector.Compare(ProcessIntegrityLevel.High, ProcessIntegrityLevel.Medium));
        Assert.AreEqual(
            IntegrityComparison.Same,
            ProcessIntegrityInspector.Compare(ProcessIntegrityLevel.Medium, ProcessIntegrityLevel.Medium));
        Assert.AreEqual(
            IntegrityComparison.Unknown,
            ProcessIntegrityInspector.Compare(ProcessIntegrityLevel.Unknown, ProcessIntegrityLevel.Medium));
    }

    [TestMethod]
    public void AppSettings_ClipboardModeDefaultsOff_AndClonePreservesCompatibilitySettings()
    {
        var defaults = new AppSettings();
        Assert.IsFalse(defaults.ClipboardCompatibilityMode);

        var configured = new AppSettings
        {
            IsPaused = true,
            EnterExpansionEnabled = true,
            TabExpansionEnabled = true,
            ClipboardCompatibilityMode = true,
            ExcludedSites = ["example.com"]
        };
        var clone = configured.Clone();

        Assert.IsTrue(clone.IsPaused);
        Assert.IsTrue(clone.EnterExpansionEnabled);
        Assert.IsTrue(clone.TabExpansionEnabled);
        Assert.IsTrue(clone.ClipboardCompatibilityMode);
        CollectionAssert.AreEqual(new[] { "example.com" }, clone.ExcludedSites);
        Assert.AreNotSame(configured.ExcludedSites, clone.ExcludedSites);
    }

    [DataTestMethod]
    [DataRow(0x20u, false, DelimiterKey.Space)]
    [DataRow(0x0Du, false, DelimiterKey.Enter)]
    [DataRow(0x0Du, true, DelimiterKey.NumpadEnter)]
    [DataRow(0x09u, false, DelimiterKey.Tab)]
    public void HookPolicy_RecognizesAllSupportedDelimiters(
        uint virtualKey,
        bool extended,
        DelimiterKey expected)
    {
        var keyEvent = KeyEvent(virtualKey, extended: extended);
        Assert.IsTrue(HookEventPolicy.TryGetDelimiter(keyEvent, out var actual));
        Assert.AreEqual(expected, actual);
    }

    [DataTestMethod]
    [DataRow(HookModifierKeys.Control)]
    [DataRow(HookModifierKeys.Alt)]
    [DataRow(HookModifierKeys.Windows)]
    [DataRow(HookModifierKeys.Control | HookModifierKeys.Shift)]
    public void HookPolicy_IgnoresControlAltAndWindowsCombinations(HookModifierKeys modifiers)
    {
        Assert.IsFalse(HookEventPolicy.TryGetDelimiter(KeyEvent(0x20, modifiers: modifiers), out _));
    }

    [TestMethod]
    public void HookPolicy_AllowsShiftModifiedDelimiter()
    {
        Assert.IsTrue(HookEventPolicy.TryGetDelimiter(
            KeyEvent(0x20, modifiers: HookModifierKeys.Shift),
            out var delimiter));
        Assert.AreEqual(DelimiterKey.Space, delimiter);
    }

    [TestMethod]
    public void HookPolicy_IgnoresHankiInjection_ButAllowsExternalAccessibilityInjection()
    {
        Assert.IsFalse(HookEventPolicy.TryGetDelimiter(
            KeyEvent(0x20, injected: true, hankiInjected: true),
            out _));
        Assert.IsTrue(HookEventPolicy.TryGetDelimiter(
            KeyEvent(0x20, injected: true, hankiInjected: false),
            out var delimiter));
        Assert.AreEqual(DelimiterKey.Space, delimiter);
    }

    [TestMethod]
    public void HookPolicy_IgnoresKeyDownAndUnknownKeys()
    {
        Assert.IsFalse(HookEventPolicy.TryGetDelimiter(KeyEvent(0x20, keyDown: true), out _));
        Assert.IsFalse(HookEventPolicy.TryGetDelimiter(KeyEvent(0x41), out _));
    }

    [TestMethod]
    public void ExpansionPolicy_DistinguishesDisabledPausedAndNoShortcuts()
    {
        Assert.AreEqual(
            ExpansionBlockReason.FeatureDisabled,
            ExpansionPolicy.GetInitialBlockReason(new AppSettings { IsEnabled = false }, 1, DelimiterKey.Space));
        Assert.AreEqual(
            ExpansionBlockReason.Paused,
            ExpansionPolicy.GetInitialBlockReason(
                new AppSettings { IsEnabled = true, IsPaused = true },
                1,
                DelimiterKey.Space));
        Assert.AreEqual(
            ExpansionBlockReason.NoShortcuts,
            ExpansionPolicy.GetInitialBlockReason(new AppSettings(), 0, DelimiterKey.Space));
    }

    [DataTestMethod]
    [DataRow(DelimiterKey.Space)]
    [DataRow(DelimiterKey.Enter)]
    [DataRow(DelimiterKey.NumpadEnter)]
    [DataRow(DelimiterKey.Tab)]
    public void ExpansionPolicy_DistinguishesEachDisabledDelimiter(DelimiterKey delimiter)
    {
        var settings = new AppSettings
        {
            SpaceExpansionEnabled = delimiter != DelimiterKey.Space,
            EnterExpansionEnabled = delimiter is not DelimiterKey.Enter and not DelimiterKey.NumpadEnter,
            TabExpansionEnabled = delimiter != DelimiterKey.Tab
        };
        Assert.AreEqual(
            ExpansionBlockReason.DelimiterDisabled,
            ExpansionPolicy.GetInitialBlockReason(settings, 1, delimiter));
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public void CurrentProcessIntegrityAndInputEnvironment_AreInspectable()
    {
        var process = new ProcessIntegrityInspector().InspectProcess(Environment.ProcessId);
        Assert.AreEqual(ProcessInspectionStatus.Available, process.Status);
        Assert.AreNotEqual(ProcessIntegrityLevel.Unknown, process.Integrity);
        StringAssert.EndsWith(process.ProcessName, ".exe");

        var input = new InputEnvironmentInspector().Capture(DelimiterKey.Space);
        Assert.AreEqual(8, input.KeyboardLayout.Length);
        Assert.IsTrue(input.KeyboardLayout.All(Uri.IsHexDigit));
        Assert.AreEqual(DelimiterKey.Space, input.SelectedDelimiter);
        Assert.IsTrue(input.WindowsSessionId >= 0);
    }

    private static HookKeyEvent KeyEvent(
        uint virtualKey,
        bool keyDown = false,
        bool extended = false,
        bool injected = false,
        bool hankiInjected = false,
        HookModifierKeys modifiers = HookModifierKeys.None) =>
        new(
            DateTimeOffset.UtcNow,
            virtualKey,
            0,
            keyDown,
            extended,
            injected,
            hankiInjected,
            modifiers);
}
