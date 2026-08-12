using Hanki.Core.Diagnostics;
using Hanki.Infrastructure.Windows;

namespace Hanki.Core.Tests;

[TestClass]
public sealed class ExpansionInjectionTests
{
    [TestMethod]
    public async Task DirectInjection_AllStagesMustSucceed()
    {
        var sender = new FakeInputSender();
        var coordinator = new ExpansionInjectionCoordinator(
            sender,
            new ClipboardCompatibilityService(new FakeClipboardAdapter(), TimeSpan.Zero));

        var result = await coordinator.InjectAsync(4, "expanded", DelimiterKey.Space, useClipboard: false);

        Assert.AreEqual(ExpansionResultStatus.Success, result.Status);
        CollectionAssert.AreEqual(
            new[] { InputInjectionStage.Backspace, InputInjectionStage.Text, InputInjectionStage.Delimiter },
            sender.Calls.ToArray());
    }

    [TestMethod]
    public async Task BackspacePartialFailure_StopsLaterStages_AndIsNotSuccess()
    {
        var sender = new FakeInputSender
        {
            BackspaceResult = new(InputInjectionStage.Backspace, 8, 4, 0)
        };
        var coordinator = new ExpansionInjectionCoordinator(
            sender,
            new ClipboardCompatibilityService(new FakeClipboardAdapter(), TimeSpan.Zero));

        var result = await coordinator.InjectAsync(4, "expanded", DelimiterKey.Space, useClipboard: false);

        Assert.AreEqual(ExpansionResultStatus.PartialFailure, result.Status);
        CollectionAssert.AreEqual(new[] { InputInjectionStage.Backspace }, sender.Calls.ToArray());
        Assert.IsFalse(result.IsSuccess);
    }

    [TestMethod]
    public async Task TextFailure_StopsDelimiter_AndIsPartialBecauseDeletionAlreadySucceeded()
    {
        var sender = new FakeInputSender
        {
            TextResult = new(InputInjectionStage.Text, 16, 0, 5)
        };
        var coordinator = new ExpansionInjectionCoordinator(
            sender,
            new ClipboardCompatibilityService(new FakeClipboardAdapter(), TimeSpan.Zero));

        var result = await coordinator.InjectAsync(4, "expanded", DelimiterKey.Enter, useClipboard: false);

        Assert.AreEqual(ExpansionResultStatus.PartialFailure, result.Status);
        CollectionAssert.AreEqual(
            new[] { InputInjectionStage.Backspace, InputInjectionStage.Text },
            sender.Calls.ToArray());
        Assert.IsNull(result.Delimiter);
    }

    [TestMethod]
    public async Task DelimiterFailure_IsNotCountedAsSuccess()
    {
        var sender = new FakeInputSender
        {
            DelimiterResult = new(InputInjectionStage.Delimiter, 2, 0, 5)
        };
        var coordinator = new ExpansionInjectionCoordinator(
            sender,
            new ClipboardCompatibilityService(new FakeClipboardAdapter(), TimeSpan.Zero));

        var result = await coordinator.InjectAsync(2, "ok", DelimiterKey.Tab, useClipboard: false);

        Assert.AreEqual(ExpansionResultStatus.PartialFailure, result.Status);
        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual("injection.delimiter_failed", result.ResultCode);
    }

    [TestMethod]
    public async Task ClipboardMode_RestoresOriginalClipboard()
    {
        var clipboard = new FakeClipboardAdapter { Current = "original" };
        var sender = new FakeInputSender();
        var service = new ClipboardCompatibilityService(clipboard, TimeSpan.Zero);

        var result = await service.PasteAsync("temporary", sender);

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual("original", clipboard.Current);
        Assert.AreEqual(ClipboardRestoreStatus.Restored, result.RestoreStatus);
        Assert.IsFalse(clipboard.ObservedValues.Any(value => value.Contains("raw-trigger", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task ClipboardMode_DoesNotOverwriteConcurrentUserChange()
    {
        var clipboard = new FakeClipboardAdapter { Current = "original" };
        var sender = new FakeInputSender
        {
            OnPaste = () => clipboard.SimulateExternalChange("user-new-value")
        };
        var service = new ClipboardCompatibilityService(clipboard, TimeSpan.Zero);

        var result = await service.PasteAsync("temporary", sender);

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual("user-new-value", clipboard.Current);
        Assert.AreEqual(ClipboardRestoreStatus.SkippedBecauseClipboardChanged, result.RestoreStatus);
    }

    [TestMethod]
    public async Task ClipboardRestoreFailure_IsReportedAndStopsDelimiter()
    {
        var clipboard = new FakeClipboardAdapter { Current = "original", ThrowOnRestore = true };
        var sender = new FakeInputSender();
        var coordinator = new ExpansionInjectionCoordinator(
            sender,
            new ClipboardCompatibilityService(clipboard, TimeSpan.Zero));

        var result = await coordinator.InjectAsync(
            2,
            "temporary",
            DelimiterKey.Space,
            useClipboard: true);

        Assert.AreEqual(ExpansionResultStatus.PartialFailure, result.Status);
        Assert.AreEqual(ClipboardRestoreStatus.Failed, result.Clipboard?.RestoreStatus);
        Assert.IsNull(result.Delimiter);
        CollectionAssert.DoesNotContain(sender.Calls, InputInjectionStage.Delimiter);
    }

    [TestMethod]
    public void WindowsInputSender_ZeroLengthOperationsAreSuccessfulNoOps()
    {
        var sender = new WindowsInputSender();
        var backspace = sender.SendBackspaces(0);
        var text = sender.SendUnicodeText(string.Empty);
        Assert.IsTrue(backspace.IsSuccess);
        Assert.IsTrue(text.IsSuccess);
        Assert.AreEqual(0, backspace.RequestedInputs);
        Assert.AreEqual(0, text.SentInputs);
    }

    private sealed class FakeInputSender : IInputSender
    {
        public List<InputInjectionStage> Calls { get; } = [];
        public InputSendResult? BackspaceResult { get; init; }
        public InputSendResult? TextResult { get; init; }
        public InputSendResult? DelimiterResult { get; init; }
        public InputSendResult? PasteResult { get; init; }
        public Action? OnPaste { get; init; }

        public InputSendResult SendBackspaces(int keyPressCount)
        {
            Calls.Add(InputInjectionStage.Backspace);
            return BackspaceResult ?? new(InputInjectionStage.Backspace, keyPressCount * 2, keyPressCount * 2, 0);
        }

        public InputSendResult SendUnicodeText(string text)
        {
            Calls.Add(InputInjectionStage.Text);
            return TextResult ?? new(InputInjectionStage.Text, text.Length * 2, text.Length * 2, 0);
        }

        public InputSendResult SendDelimiter(DelimiterKey delimiter)
        {
            Calls.Add(InputInjectionStage.Delimiter);
            return DelimiterResult ?? new(InputInjectionStage.Delimiter, 2, 2, 0);
        }

        public InputSendResult SendPasteShortcut()
        {
            Calls.Add(InputInjectionStage.PasteShortcut);
            OnPaste?.Invoke();
            return PasteResult ?? new(InputInjectionStage.PasteShortcut, 4, 4, 0);
        }
    }

    private sealed class FakeClipboardAdapter : IClipboardAdapter
    {
        private uint _sequence = 1;
        public object? Current { get; set; }
        public List<string> ObservedValues { get; } = [];
        public bool ThrowOnRestore { get; init; }

        public uint GetSequenceNumber() => _sequence;

        public object? Capture() => Current;

        public void SetText(string text)
        {
            Current = text;
            ObservedValues.Add(text);
            _sequence++;
        }

        public void Restore(object? snapshot)
        {
            if (ThrowOnRestore)
                throw new InvalidOperationException("simulated clipboard restore failure");
            Current = snapshot;
            _sequence++;
        }

        public void SimulateExternalChange(object? value)
        {
            Current = value;
            _sequence++;
        }
    }
}
