using Hanki.Core.Contracts;

namespace Hanki.Core.Services;

public sealed class MainWindowActivationService(Func<IMainWindowActivationTarget> windowFactory)
{
    private readonly object _windowGate = new();
    private IMainWindowActivationTarget? _window;

    public void ShowAndActivateMainWindow()
    {
        var window = GetOrCreateWindow();

        window.ShowInTaskbar = true;
        if (!window.IsVisible)
            window.Show();
        if (window.IsMinimized)
            window.Restore();

        if (window.Activate() || window.IsActive)
            return;
        if (window.TryBringToForeground() && window.IsActive)
            return;

        var wasTopmost = window.Topmost;
        if (!wasTopmost)
            window.Topmost = true;
        window.Activate();
        if (!wasTopmost)
            window.Topmost = false;
    }

    private IMainWindowActivationTarget GetOrCreateWindow()
    {
        lock (_windowGate)
        {
            if (_window is null || _window.IsClosed)
                _window = windowFactory();
            return _window;
        }
    }
}
