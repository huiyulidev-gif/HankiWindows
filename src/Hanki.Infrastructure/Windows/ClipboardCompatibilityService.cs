using System.Runtime.InteropServices;
using System.Windows;

namespace Hanki.Infrastructure.Windows;

public enum ClipboardRestoreStatus
{
    NotNeeded,
    Restored,
    SkippedBecauseClipboardChanged,
    Failed
}

public sealed record ClipboardPasteResult(
    bool ClipboardAccessSucceeded,
    InputSendResult PasteInput,
    ClipboardRestoreStatus RestoreStatus,
    string ResultCode)
{
    public bool IsSuccess =>
        ClipboardAccessSucceeded &&
        PasteInput.IsSuccess &&
        RestoreStatus is ClipboardRestoreStatus.Restored or ClipboardRestoreStatus.SkippedBecauseClipboardChanged;
}

public interface IClipboardAdapter
{
    uint GetSequenceNumber();
    object? Capture();
    void SetText(string text);
    void Restore(object? snapshot);
}

public sealed class WindowsClipboardAdapter : IClipboardAdapter
{
    public uint GetSequenceNumber() => GetClipboardSequenceNumber();

    public object? Capture() => Clipboard.GetDataObject();

    public void SetText(string text) => Clipboard.SetDataObject(text, copy: true);

    public void Restore(object? snapshot)
    {
        if (snapshot is IDataObject dataObject)
            Clipboard.SetDataObject(dataObject, copy: true);
        else
            Clipboard.Clear();
    }

    [DllImport("user32.dll")]
    private static extern uint GetClipboardSequenceNumber();
}

public sealed class ClipboardCompatibilityService(
    IClipboardAdapter? clipboard = null,
    TimeSpan? restoreDelay = null)
{
    private readonly IClipboardAdapter _clipboard = clipboard ?? new WindowsClipboardAdapter();
    private readonly TimeSpan _restoreDelay = restoreDelay ?? TimeSpan.FromMilliseconds(150);

    public Task<ClipboardPasteResult> PasteAsync(
        string text,
        IInputSender inputSender,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(inputSender);

        var completion = new TaskCompletionSource<ClipboardPasteResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var snapshot = _clipboard.Capture();
                _clipboard.SetText(text);
                var temporarySequence = _clipboard.GetSequenceNumber();
                var paste = inputSender.SendPasteShortcut();

                if (_restoreDelay > TimeSpan.Zero)
                    Thread.Sleep(_restoreDelay);

                ClipboardRestoreStatus restoreStatus;
                if (_clipboard.GetSequenceNumber() != temporarySequence)
                {
                    restoreStatus = ClipboardRestoreStatus.SkippedBecauseClipboardChanged;
                }
                else
                {
                    try
                    {
                        _clipboard.Restore(snapshot);
                        restoreStatus = ClipboardRestoreStatus.Restored;
                    }
                    catch (Exception exception) when (
                        exception is ExternalException or InvalidOperationException)
                    {
                        restoreStatus = ClipboardRestoreStatus.Failed;
                    }
                }

                completion.TrySetResult(new ClipboardPasteResult(
                    true,
                    paste,
                    restoreStatus,
                    restoreStatus switch
                    {
                        ClipboardRestoreStatus.Restored when paste.IsSuccess => "clipboard.paste_restored",
                        ClipboardRestoreStatus.SkippedBecauseClipboardChanged when paste.IsSuccess =>
                            "clipboard.paste_user_change_preserved",
                        ClipboardRestoreStatus.Failed => "clipboard.restore_failed",
                        _ when !paste.IsSuccess => "clipboard.paste_input_failed",
                        _ => "clipboard.unknown"
                    }));
            }
            catch (OperationCanceledException)
            {
                completion.TrySetCanceled(cancellationToken);
            }
            catch (Exception exception) when (
                exception is ExternalException or InvalidOperationException or ThreadStateException)
            {
                completion.TrySetResult(new ClipboardPasteResult(
                    false,
                    new InputSendResult(InputInjectionStage.PasteShortcut, 0, 0, 0),
                    ClipboardRestoreStatus.NotNeeded,
                    "clipboard.access_failed"));
            }
        })
        {
            IsBackground = true,
            Name = "Hanki.ClipboardCompatibility"
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }
}
