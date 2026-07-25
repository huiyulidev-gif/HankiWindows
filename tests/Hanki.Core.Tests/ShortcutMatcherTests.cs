using Hanki.Core.Models;
using Hanki.Core.Services;

namespace Hanki.Core.Tests;

[TestClass]
public sealed class ShortcutMatcherTests
{
    private readonly ShortcutMatcher _matcher = new();
    private readonly ShortcutItem[] _shortcuts =
    [
        ShortcutValidatorTests.NewShortcut(";문의", "안녕하세요."),
        ShortcutValidatorTests.NewShortcut(";Hello", "Hello!")
    ];

    [TestMethod]
    public void ExactSuffix_Matches()
    {
        var result = _matcher.FindExactSuffix(";문의 ", _shortcuts);
        Assert.IsNotNull(result);
        Assert.AreEqual(";문의", result.TriggerText);
    }

    [TestMethod]
    public void Match_AfterWhitespace_Matches()
    {
        var result = _matcher.FindExactSuffix("앞 문장\n;문의 ", _shortcuts);
        Assert.IsNotNull(result);
    }

    [TestMethod]
    public void PartialToken_DoesNotMatch()
    {
        var result = _matcher.FindExactSuffix("상담;문의 ", _shortcuts);
        Assert.IsNull(result);
    }

    [TestMethod]
    public void PartialSuffix_DoesNotMatch()
    {
        var result = _matcher.FindExactSuffix(";문 ", _shortcuts);
        Assert.IsNull(result);
    }

    [TestMethod]
    public void Matching_IsCaseSensitive()
    {
        Assert.IsNull(_matcher.FindExactSuffix(";hello ", _shortcuts));
        Assert.IsNotNull(_matcher.FindExactSuffix(";Hello ", _shortcuts));
    }
}
