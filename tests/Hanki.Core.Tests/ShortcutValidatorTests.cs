using Hanki.Core.Exceptions;
using Hanki.Core.Models;
using Hanki.Core.Services;

namespace Hanki.Core.Tests;

[TestClass]
public sealed class ShortcutValidatorTests
{
    private readonly ShortcutValidator _validator = new();

    [TestMethod]
    public void Trigger_IsTrimmed()
    {
        var result = _validator.NormalizeAndValidate(NewShortcut("  ;문의  ", "답변"));
        Assert.AreEqual(";문의", result.TriggerText);
    }

    [TestMethod]
    public void EmptyTrigger_IsRejected()
    {
        Assert.ThrowsException<ShortcutValidationException>(
            () => _validator.NormalizeAndValidate(NewShortcut("  ", "답변")));
    }

    [TestMethod]
    public void WhitespaceReplacement_IsRejected()
    {
        Assert.ThrowsException<ShortcutValidationException>(
            () => _validator.NormalizeAndValidate(NewShortcut(";문의", "  \r\n ")));
    }

    [TestMethod]
    public void UnicodeKoreanTrigger_IsAccepted()
    {
        var result = _validator.NormalizeAndValidate(NewShortcut(";문의", "안녕하세요"));
        Assert.AreEqual(";문의", result.TriggerText);
        Assert.AreEqual("안녕하세요", result.ReplacementText);
    }

    internal static ShortcutItem NewShortcut(string trigger, string replacement) => new()
    {
        TriggerText = trigger,
        ReplacementText = replacement
    };
}
