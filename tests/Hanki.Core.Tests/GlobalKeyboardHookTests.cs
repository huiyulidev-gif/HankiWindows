using Hanki.Infrastructure.Windows;

namespace Hanki.Core.Tests;

[TestClass]
public sealed class GlobalKeyboardHookTests
{
    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public void StartTwice_DoesNotRegisterDuplicate_AndDisposeReleasesHandle()
    {
        using var hook = new GlobalKeyboardHook();
        hook.Start();
        var firstHandle = hook.HookHandleValue;
        var firstThread = hook.ThreadId;

        hook.Start();

        Assert.IsTrue(hook.IsRegistered);
        Assert.IsTrue(hook.IsThreadAlive);
        Assert.AreNotEqual(0L, firstHandle);
        Assert.AreEqual(firstHandle, hook.HookHandleValue);
        Assert.AreEqual(firstThread, hook.ThreadId);

        hook.Dispose();
        Assert.IsFalse(hook.IsRegistered);
        Assert.IsFalse(hook.IsThreadAlive);
        Assert.AreEqual(0L, hook.HookHandleValue);
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public void Restart_ReplacesHandleWithoutCreatingTwoLiveThreads()
    {
        using var hook = new GlobalKeyboardHook();
        hook.Start();
        hook.Restart();

        Assert.IsTrue(hook.IsRegistered);
        Assert.IsTrue(hook.IsThreadAlive);
        Assert.AreNotEqual(0, hook.ThreadId);
    }
}
