using Hanki.Core.Models;
using Hanki.Core.Services;

namespace Hanki.Core.Tests;

[TestClass]
public sealed class OrderingAndPolicyTests
{
    [TestMethod]
    public void Favorites_AreSortedFirst()
    {
        var now = DateTimeOffset.UtcNow;
        var items = new[]
        {
            Item("new", false, now),
            Item("favorite", true, now.AddDays(-1))
        };
        Assert.AreEqual("favorite", ShortcutOrdering.FavoritesFirst(items).First().TriggerText);
    }

    [TestMethod]
    public void RecentlyUpdated_AreSortedFirst()
    {
        var now = DateTimeOffset.UtcNow;
        var items = new[]
        {
            Item("old", false, now.AddDays(-1)),
            Item("new", false, now)
        };
        Assert.AreEqual("new", ShortcutOrdering.MostRecentlyUpdated(items).First().TriggerText);
    }

    [TestMethod]
    public void ExcludedProcess_IsCaseInsensitive_AndNormalizesExtension()
    {
        var policy = new ProcessExclusionPolicy(["cmd.exe", "MyGame"]);
        Assert.IsTrue(policy.IsExcluded("CMD"));
        Assert.IsTrue(policy.IsExcluded("mygame.exe"));
        Assert.IsFalse(policy.IsExcluded("notepad.exe"));
    }

    private static ShortcutItem Item(string trigger, bool favorite, DateTimeOffset updated) => new()
    {
        TriggerText = trigger,
        ReplacementText = "value",
        IsFavorite = favorite,
        UpdatedAt = updated
    };
}
