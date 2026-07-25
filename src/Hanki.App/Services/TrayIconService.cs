using System.Drawing;
using System.Windows.Forms;

namespace Hanki.App.Services;

public sealed class TrayIconService : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _enableItem;
    private readonly ToolStripMenuItem _disableItem;
    private bool _balloonShown;

    public TrayIconService()
    {
        _enableItem = new ToolStripMenuItem("단축어 변환 켜기", null, (_, _) =>
            EnabledChangeRequested?.Invoke(this, true));
        _disableItem = new ToolStripMenuItem("단축어 변환 끄기", null, (_, _) =>
            EnabledChangeRequested?.Invoke(this, false));

        var menu = new ContextMenuStrip();
        menu.Items.Add("한키 열기", null, (_, _) => OpenRequested?.Invoke(this, EventArgs.Empty));
        menu.Items.Add(_enableItem);
        menu.Items.Add(_disableItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("새 단축어 추가", null, (_, _) => AddRequested?.Invoke(this, EventArgs.Empty));
        menu.Items.Add("설정", null, (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("완전히 종료", null, (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty));

        _notifyIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "한키 - 준비 중",
            ContextMenuStrip = menu,
            Visible = true
        };
        _notifyIcon.DoubleClick += (_, _) => OpenRequested?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? OpenRequested;
    public event EventHandler? AddRequested;
    public event EventHandler? SettingsRequested;
    public event EventHandler<bool>? EnabledChangeRequested;
    public event EventHandler? ExitRequested;

    public void UpdateStatus(bool enabled)
    {
        _notifyIcon.Text = enabled ? "한키 - 단축어 변환 켜짐" : "한키 - 단축어 변환 꺼짐";
        _enableItem.Enabled = !enabled;
        _disableItem.Enabled = enabled;
    }

    public void ShowBalloonOnce()
    {
        if (_balloonShown)
            return;
        _balloonShown = true;
        _notifyIcon.BalloonTipTitle = "한키가 트레이에서 실행 중입니다";
        _notifyIcon.BalloonTipText = "아이콘을 두 번 클릭하면 다시 열 수 있습니다.";
        _notifyIcon.ShowBalloonTip(2500);
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}
