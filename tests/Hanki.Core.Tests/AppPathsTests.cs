using Hanki.Infrastructure;

namespace Hanki.Core.Tests;

[TestClass]
[DoNotParallelize]
public sealed class AppPathsTests
{
    [TestMethod]
    public void DataDirectory_AbsoluteOverride_IsUsedForIsolatedDiagnostics()
    {
        var original = Environment.GetEnvironmentVariable(AppPaths.DataDirectoryEnvironmentVariable);
        var expected = Path.Combine(Path.GetTempPath(), $"hanki-path-test-{Guid.NewGuid():N}");
        try
        {
            Environment.SetEnvironmentVariable(AppPaths.DataDirectoryEnvironmentVariable, expected);

            Assert.AreEqual(Path.GetFullPath(expected), AppPaths.DataDirectory);
            Assert.IsTrue(AppPaths.IsDataDirectoryOverridden);
            StringAssert.StartsWith(AppPaths.DatabasePath, Path.GetFullPath(expected));
            StringAssert.StartsWith(AppPaths.AuthSessionPath, Path.GetFullPath(expected));
        }
        finally
        {
            Environment.SetEnvironmentVariable(AppPaths.DataDirectoryEnvironmentVariable, original);
        }
    }

    [TestMethod]
    public void DataDirectory_RelativeOverride_IsIgnored()
    {
        var original = Environment.GetEnvironmentVariable(AppPaths.DataDirectoryEnvironmentVariable);
        try
        {
            Environment.SetEnvironmentVariable(AppPaths.DataDirectoryEnvironmentVariable, "relative-path");

            Assert.AreNotEqual("relative-path", AppPaths.DataDirectory);
            Assert.IsFalse(AppPaths.IsDataDirectoryOverridden);
        }
        finally
        {
            Environment.SetEnvironmentVariable(AppPaths.DataDirectoryEnvironmentVariable, original);
        }
    }
}
