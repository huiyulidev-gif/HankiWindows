using Hanki.Core.Models;
using Hanki.Core.Services;

namespace Hanki.Core.Tests;

[TestClass]
public sealed class ReentrancyAndUsageTests
{
    [TestMethod]
    public async Task Reentrancy_IsBlocked_DuringLeaseAndCooldown()
    {
        var guard = new ReentrancyGuard(TimeSpan.FromMilliseconds(25));
        Assert.IsTrue(guard.TryEnter(out var lease));
        Assert.IsFalse(guard.TryEnter(out _));
        lease!.Dispose();
        Assert.IsFalse(guard.TryEnter(out _));
        await Task.Delay(40);
        Assert.IsTrue(guard.TryEnter(out var second));
        second!.Dispose();
    }

    [TestMethod]
    public void UsageCountAndLastUsed_AreUpdated()
    {
        var shortcut = new ShortcutItem { UsageCount = 3 };
        var usedAt = DateTimeOffset.UtcNow;
        ShortcutUsage.Record(shortcut, usedAt);
        Assert.AreEqual(4, shortcut.UsageCount);
        Assert.AreEqual(usedAt, shortcut.LastUsedAt);
    }
}
