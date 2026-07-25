using System.Windows.Automation;
using System.Windows.Automation.Text;

namespace Hanki.Infrastructure.Windows;

internal sealed class WindowsInputInspector
{
    public bool TryCapture(int maxCharacters, out InputContext? context)
    {
        context = null;
        try
        {
            var element = AutomationElement.FocusedElement;
            if (element is null ||
                element.Current.IsPassword ||
                !element.Current.IsEnabled ||
                !element.Current.IsKeyboardFocusable)
                return false;

            var controlType = element.Current.ControlType;
            if (controlType != ControlType.Edit && controlType != ControlType.Document)
                return false;

            if (!element.TryGetCurrentPattern(TextPattern.Pattern, out var rawPattern) ||
                rawPattern is not TextPattern pattern)
                return false;

            var selection = pattern.GetSelection();
            if (selection.Length != 1)
                return false;

            var caret = selection[0];
            if (caret.CompareEndpoints(
                    TextPatternRangeEndpoint.Start,
                    caret,
                    TextPatternRangeEndpoint.End) != 0)
                return false;

            var precedingRange = caret.Clone();
            precedingRange.MoveEndpointByUnit(
                TextPatternRangeEndpoint.Start,
                TextUnit.Character,
                -Math.Clamp(maxCharacters, 1, 1024));
            var precedingText = precedingRange.GetText(-1);
            context = new InputContext(element, caret.Clone(), precedingText);
            return true;
        }
        catch (ElementNotAvailableException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}

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
