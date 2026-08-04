using System.Reflection;

namespace Hanki.Core.Tests;

[TestClass]
public sealed class VersionMetadataTests
{
    [TestMethod]
    public void CoreAndInfrastructure_UseRcVersion()
    {
        Assert.AreEqual("0.2.0", GetInformationalVersion(typeof(Hanki.Core.Models.ShortcutItem).Assembly));
        Assert.AreEqual("0.2.0", GetInformationalVersion(typeof(Hanki.Infrastructure.AppPaths).Assembly));
    }

    private static string? GetInformationalVersion(Assembly assembly) =>
        assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
}
