using System.Windows.Automation;
using System.Windows.Automation.Text;
using Hanki.Core.Diagnostics;

namespace Hanki.Infrastructure.Windows;

internal sealed class WindowsInputInspector
{
    public InputInspectionResult Inspect(int maxCharacters)
    {
        try
        {
            var element = AutomationElement.FocusedElement;
            if (element is null)
                return new(InputContextStatus.NoFocusedElement, null);
            if (element.Current.IsPassword)
                return new(InputContextStatus.SensitiveField, null);
            if (!element.Current.IsEnabled)
                return new(InputContextStatus.Disabled, null);
            if (!element.Current.IsKeyboardFocusable)
                return new(InputContextStatus.NotKeyboardFocusable, null);

            if (element.TryGetCurrentPattern(ValuePattern.Pattern, out var rawValue) &&
                rawValue is ValuePattern valuePattern &&
                valuePattern.Current.IsReadOnly)
            {
                return new(InputContextStatus.ReadOnly, null);
            }

            var controlType = element.Current.ControlType;
            if (controlType != ControlType.Edit && controlType != ControlType.Document)
                return new(InputContextStatus.UnsupportedControl, null);

            if (!element.TryGetCurrentPattern(TextPattern.Pattern, out var rawPattern) ||
                rawPattern is not TextPattern pattern)
            {
                return new(InputContextStatus.TextPatternUnavailable, null);
            }

            var selection = pattern.GetSelection();
            if (selection.Length != 1)
                return new(InputContextStatus.SelectionUnavailable, null);

            var caret = selection[0];
            if (caret.CompareEndpoints(
                    TextPatternRangeEndpoint.Start,
                    caret,
                    TextPatternRangeEndpoint.End) != 0)
            {
                return new(InputContextStatus.CaretUnavailable, null);
            }

            var precedingRange = caret.Clone();
            precedingRange.MoveEndpointByUnit(
                TextPatternRangeEndpoint.Start,
                TextUnit.Character,
                -Math.Clamp(maxCharacters, 1, 1024));
            var precedingText = precedingRange.GetText(-1);
            return new(
                InputContextStatus.Available,
                new InputContext(element, caret.Clone(), precedingText));
        }
        catch (ElementNotAvailableException)
        {
            return new(InputContextStatus.ElementUnavailable, null);
        }
        catch (UnauthorizedAccessException)
        {
            return new(InputContextStatus.AccessDenied, null);
        }
        catch (InvalidOperationException)
        {
            return new(InputContextStatus.InformationUnavailable, null);
        }
    }

    public bool TryCapture(int maxCharacters, out InputContext? context)
    {
        var result = Inspect(maxCharacters);
        context = result.Context;
        return result.Status == InputContextStatus.Available && context is not null;
    }
}

internal sealed record InputInspectionResult(InputContextStatus Status, InputContext? Context);

internal sealed class InputContext(
    AutomationElement element,
    TextPatternRange caret,
    string textBeforeCaret)
{
    public string TextBeforeCaret { get; } = textBeforeCaret;

    public bool TrySelectPreviousCharacters(int count)
    {
        try
        {
            if (!element.Current.HasKeyboardFocus)
                return false;
            var range = caret.Clone();
            var moved = range.MoveEndpointByUnit(
                TextPatternRangeEndpoint.Start,
                TextUnit.Character,
                -count);
            if (Math.Abs(moved) != count)
                return false;
            range.Select();
            return true;
        }
        catch (Exception exception) when (
            exception is ElementNotAvailableException or InvalidOperationException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
