namespace Hanki.Core.Exceptions;

public sealed class ShortcutValidationException : Exception
{
    public ShortcutValidationException(IEnumerable<string> errors)
        : base(string.Join(Environment.NewLine, errors))
    {
        Errors = errors.ToArray();
    }

    public IReadOnlyList<string> Errors { get; }
}

public sealed class DuplicateTriggerException(string triggerText)
    : Exception($"이미 등록된 단축어입니다: {triggerText}")
{
    public string TriggerText { get; } = triggerText;
}
