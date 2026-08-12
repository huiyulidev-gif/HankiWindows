using System.Reflection;

namespace Hanki.Core.Tests;

[TestClass]
public sealed class VersionMetadataTests
{
    [TestMethod]
    public void CoreAndInfrastructure_UseReleaseVersion()
    {
        Assert.AreEqual("0.2.1", GetInformationalVersion(typeof(Hanki.Core.Models.ShortcutItem).Assembly));
        Assert.AreEqual("0.2.1", GetInformationalVersion(typeof(Hanki.Infrastructure.AppPaths).Assembly));
        Assert.AreEqual(new Version(0, 2, 1, 0), typeof(Hanki.Core.Models.ShortcutItem).Assembly.GetName().Version);
        Assert.AreEqual(new Version(0, 2, 1, 0), typeof(Hanki.Infrastructure.AppPaths).Assembly.GetName().Version);
    }

    private static string? GetInformationalVersion(Assembly assembly) =>
        assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
}
