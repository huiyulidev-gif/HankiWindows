using Hanki.Infrastructure.Windows;

namespace Hanki.Core.Tests;

[TestClass]
public sealed class SingleInstanceManagerTests
{
    [TestMethod]
    public void FirstStart_IsPrimary_AndSecondStartIsSecondary()
    {
        var names = UniqueNames();
        using var primary = SingleInstanceManager.Start(names.Mutex, names.Activation);

        var secondaryResult = Task.Run(() =>
        {
            using var secondary = SingleInstanceManager.Start(names.Mutex, names.Activation);
            return (secondary.Role, secondary.ActivationSignalSent);
        }).GetAwaiter().GetResult();

        Assert.AreEqual(SingleInstanceRole.PrimaryInstance, primary.Role);
        Assert.AreEqual(SingleInstanceRole.SecondaryInstance, secondaryResult.Role);
        Assert.IsTrue(secondaryResult.ActivationSignalSent);
    }

    [TestMethod]
    public void SecondaryStart_SendsOneActivationRequest()
    {
        var names = UniqueNames();
        using var primary = SingleInstanceManager.Start(names.Mutex, names.Activation);
        using var activated = new ManualResetEventSlim(initialState: false);
        var activationCount = 0;
        primary.StartListening(() =>
        {
            Interlocked.Increment(ref activationCount);
            activated.Set();
        });

        var signalSent = Task.Run(() =>
        {
            using var secondary = SingleInstanceManager.Start(names.Mutex, names.Activation);
            return secondary.ActivationSignalSent;
        }).GetAwaiter().GetResult();

        Assert.IsTrue(signalSent);
        Assert.IsTrue(activated.Wait(TimeSpan.FromSeconds(2)));
        Assert.AreEqual(1, Volatile.Read(ref activationCount));
    }

    [TestMethod]
    public void RapidSecondaryStarts_KeepOnePrimary()
    {
        var names = UniqueNames();
        using var primary = SingleInstanceManager.Start(names.Mutex, names.Activation);
        primary.StartListening(() => { });

        var results = Enumerable.Range(0, 5)
            .Select(_ => Task.Run(() =>
            {
                using var instance = SingleInstanceManager.Start(names.Mutex, names.Activation);
                return (instance.Role, instance.ActivationSignalSent);
            }))
            .ToArray();
        Task.WaitAll(results);

        Assert.AreEqual(SingleInstanceRole.PrimaryInstance, primary.Role);
        Assert.IsTrue(results.All(task => task.Result.Role == SingleInstanceRole.SecondaryInstance));
        Assert.IsTrue(results.All(task => task.Result.ActivationSignalSent));
    }

    [TestMethod]
    public void Dispose_ReleasesMutexForNextPrimary()
    {
        var names = UniqueNames();
        using (var first = SingleInstanceManager.Start(names.Mutex, names.Activation))
            Assert.AreEqual(SingleInstanceRole.PrimaryInstance, first.Role);

        using var next = SingleInstanceManager.Start(names.Mutex, names.Activation);
        Assert.AreEqual(SingleInstanceRole.PrimaryInstance, next.Role);
    }

    [TestMethod]
    public void SignalAfterPrimaryDispose_ReturnsFalseWithoutException()
    {
        var names = UniqueNames();
        using (var primary = SingleInstanceManager.Start(names.Mutex, names.Activation))
            primary.StartListening(() => { });

        var result = SingleInstanceManager.TrySignalExisting(
            names.Activation,
            TimeSpan.FromMilliseconds(80));

        Assert.IsFalse(result);
    }

    private static (string Mutex, string Activation) UniqueNames()
    {
        var suffix = Guid.NewGuid().ToString("N");
        return ($"Local\\Yulbyte.Hanki.Tests.{suffix}.Mutex",
            $"Local\\Yulbyte.Hanki.Tests.{suffix}.Activate");
    }
}
