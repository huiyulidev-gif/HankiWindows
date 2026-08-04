using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Hanki.Core.Contracts;

namespace Hanki.App.Services;

public sealed class WpfWindowActivationTarget : IMainWindowActivationTarget
{
    private readonly Window _window;

    public WpfWindowActivationTarget(Window window)
    {
        _window = window;
        _window.Closed += (_, _) => IsClosed = true;
    }

    public bool IsClosed { get; private set; }
    public bool IsVisible => _window.IsVisible;
    public bool IsMinimized => _window.WindowState == WindowState.Minimized;
    public bool IsActive => _window.IsActive;

    public bool ShowInTaskbar
    {
        get => _window.ShowInTaskbar;
        set => _window.ShowInTaskbar = value;
    }

    public bool Topmost
    {
        get => _window.Topmost;
        set => _window.Topmost = value;
    }

    public void Show() => _window.Show();
    public void Restore() => _window.WindowState = WindowState.Normal;
    public bool Activate() => _window.Activate();

    public bool TryBringToForeground()
    {
        var handle = new WindowInteropHelper(_window).Handle;
        return handle != IntPtr.Zero && SetForegroundWindow(handle);
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);
}
