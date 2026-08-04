using Hanki.Core.Contracts;
using Hanki.Core.Services;

namespace Hanki.Core.Tests;

[TestClass]
public sealed class MainWindowActivationServiceTests
{
    [TestMethod]
    public void HiddenWindow_IsShownAndReturnedToTaskbar()
    {
        var window = new FakeWindow { IsVisible = false, ShowInTaskbar = false };
        var service = new MainWindowActivationService(() => window);

        service.ShowAndActivateMainWindow();

        Assert.AreEqual(1, window.ShowCount);
        Assert.IsTrue(window.ShowInTaskbar);
        Assert.AreEqual(1, window.ActivateCount);
    }

    [TestMethod]
    public void MinimizedWindow_IsRestored()
    {
        var window = new FakeWindow { IsVisible = true, IsMinimized = true };
        var service = new MainWindowActivationService(() => window);

        service.ShowAndActivateMainWindow();

        Assert.AreEqual(1, window.RestoreCount);
        Assert.IsFalse(window.IsMinimized);
    }

    [TestMethod]
    public void VisibleWindow_IsActivatedWithoutShowingAgain()
    {
        var window = new FakeWindow { IsVisible = true };
        var service = new MainWindowActivationService(() => window);

        service.ShowAndActivateMainWindow();

        Assert.AreEqual(0, window.ShowCount);
        Assert.AreEqual(1, window.ActivateCount);
    }

    [TestMethod]
    public void RepeatedRequests_ReuseOneWindow()
    {
        var factoryCount = 0;
        var window = new FakeWindow();
        var service = new MainWindowActivationService(() =>
        {
            factoryCount++;
            return window;
        });

        service.ShowAndActivateMainWindow();
        service.ShowAndActivateMainWindow();
        service.ShowAndActivateMainWindow();

        Assert.AreEqual(1, factoryCount);
        Assert.AreEqual(3, window.ActivateCount);
    }

    [TestMethod]
    public void ClosedWindow_IsRecreated()
    {
        var first = new FakeWindow();
        var windows = new Queue<FakeWindow>(
        [
            first,
            new FakeWindow()
        ]);
        var service = new MainWindowActivationService(() => windows.Dequeue());

        service.ShowAndActivateMainWindow();
        first.IsClosed = true;
        service.ShowAndActivateMainWindow();

        Assert.AreEqual(0, windows.Count);
    }

    [TestMethod]
    public void FailedActivation_UsesForegroundFallback()
    {
        var window = new FakeWindow
        {
            ActivationSucceeds = false,
            ForegroundSucceeds = true
        };
        var service = new MainWindowActivationService(() => window);

        service.ShowAndActivateMainWindow();

        Assert.AreEqual(1, window.ForegroundCount);
        Assert.AreEqual(0, window.TopmostChangeCount);
    }

    [TestMethod]
    public void FailedForeground_UsesTemporaryTopmostFallback()
    {
        var window = new FakeWindow
        {
            ActivationSucceeds = false,
            ForegroundSucceeds = false
        };
        var service = new MainWindowActivationService(() => window);

        service.ShowAndActivateMainWindow();

        Assert.AreEqual(2, window.TopmostChangeCount);
        Assert.IsFalse(window.Topmost);
        Assert.AreEqual(2, window.ActivateCount);
    }

    private sealed class FakeWindow : IMainWindowActivationTarget
    {
        private bool _topmost;

        public bool IsClosed { get; set; }
        public bool IsVisible { get; set; }
        public bool IsMinimized { get; set; }
        public bool IsActive { get; private set; }
        public bool ShowInTaskbar { get; set; } = true;
        public bool ActivationSucceeds { get; set; } = true;
        public bool ForegroundSucceeds { get; set; }
        public int ShowCount { get; private set; }
        public int RestoreCount { get; private set; }
        public int ActivateCount { get; private set; }
        public int ForegroundCount { get; private set; }
        public int TopmostChangeCount { get; private set; }

        public bool Topmost
        {
            get => _topmost;
            set
            {
                _topmost = value;
                TopmostChangeCount++;
            }
        }

        public void Show()
        {
            ShowCount++;
            IsVisible = true;
        }

        public void Restore()
        {
            RestoreCount++;
            IsMinimized = false;
        }

        public bool Activate()
        {
            ActivateCount++;
            IsActive = ActivationSucceeds;
            return ActivationSucceeds;
        }

        public bool TryBringToForeground()
        {
            ForegroundCount++;
            IsActive = ForegroundSucceeds;
            return ForegroundSucceeds;
        }
    }
}
