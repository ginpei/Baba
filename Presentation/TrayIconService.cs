using System.Drawing;
using Forms = System.Windows.Forms;

namespace Baba.Presentation;

internal sealed class TrayIconService : IDisposable
{
    private readonly Forms.ContextMenuStrip _contextMenu;
    private readonly Forms.NotifyIcon _trayIcon;

    public TrayIconService(
        Action showMascot,
        Action hideMascot,
        Action openDataDirectory,
        Action exitApplication)
    {
        _contextMenu = new Forms.ContextMenuStrip();
        _contextMenu.Items.Add("Show", null, (_, _) => showMascot());
        _contextMenu.Items.Add("Hide", null, (_, _) => hideMascot());
        _contextMenu.Items.Add("Open Data Folder", null, (_, _) => openDataDirectory());
        _contextMenu.Items.Add(new Forms.ToolStripSeparator());
        _contextMenu.Items.Add("Exit", null, (_, _) => exitApplication());

        _trayIcon = new Forms.NotifyIcon
        {
            ContextMenuStrip = _contextMenu,
            Icon = SystemIcons.Application,
            Text = "Baba",
            Visible = true,
        };
        _trayIcon.DoubleClick += (_, _) => showMascot();
    }

    public void ShowContextMenuAtCursor() => _contextMenu.Show(Forms.Cursor.Position);

    public void Dispose()
    {
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        _contextMenu.Dispose();
    }
}
