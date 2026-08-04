namespace Hanki.Core.Contracts;

public interface IMainWindowActivationTarget
{
    bool IsClosed { get; }
    bool IsVisible { get; }
    bool IsMinimized { get; }
    bool IsActive { get; }
    bool ShowInTaskbar { get; set; }
    bool Topmost { get; set; }

    void Show();
    void Restore();
    bool Activate();
    bool TryBringToForeground();
}
