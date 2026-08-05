using Hanki.Core.Diagnostics;

namespace Hanki.Infrastructure.Windows;

public sealed record ExpansionInjectionResult(
    InputSendResult Backspace,
    InputSendResult? Text,
    InputSendResult? Delimiter,
    ClipboardPasteResult? Clipboard,
    ExpansionResultStatus Status,
    string ResultCode)
{
    public bool IsSuccess => Status == ExpansionResultStatus.Success;
}

public sealed class ExpansionInjectionCoordinator(
    IInputSender inputSender,
    ClipboardCompatibilityService clipboardService)
{
    public async Task<ExpansionInjectionResult> InjectAsync(
        int deleteKeyPressCount,
        string replacementText,
        DelimiterKey delimiter,
        bool useClipboard,
        CancellationToken cancellationToken = default)
    {
        var deletion = inputSender.SendBackspaces(deleteKeyPressCount);
        if (!deletion.IsSuccess)
        {
            return new ExpansionInjectionResult(
                deletion,
                null,
                null,
                null,
                deletion.SentInputs > 0 ? ExpansionResultStatus.PartialFailure : ExpansionResultStatus.Failed,
                "injection.backspace_failed");
        }

        InputSendResult? text = null;
        ClipboardPasteResult? clipboard = null;
        if (useClipboard)
        {
            clipboard = await clipboardService.PasteAsync(replacementText, inputSender, cancellationToken);
            if (!clipboard.IsSuccess)
            {
                return new ExpansionInjectionResult(
                    deletion,
                    null,
                    null,
                    clipboard,
                    ExpansionResultStatus.PartialFailure,
                    clipboard.ResultCode);
            }
        }
        else
        {
            text = inputSender.SendUnicodeText(replacementText);
            if (!text.IsSuccess)
            {
                return new ExpansionInjectionResult(
                    deletion,
                    text,
                    null,
                    null,
                    ExpansionResultStatus.PartialFailure,
                    "injection.text_failed");
            }
        }

        var delimiterResult = inputSender.SendDelimiter(delimiter);
        if (!delimiterResult.IsSuccess)
        {
            return new ExpansionInjectionResult(
                deletion,
                text,
                delimiterResult,
                clipboard,
                ExpansionResultStatus.PartialFailure,
                "injection.delimiter_failed");
        }

        return new ExpansionInjectionResult(
            deletion,
            text,
            delimiterResult,
            clipboard,
            ExpansionResultStatus.Success,
            useClipboard ? "injection.clipboard_success" : "injection.direct_success");
    }
}
