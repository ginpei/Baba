using System.Drawing;
using Forms = System.Windows.Forms;

namespace Baba.Presentation;

internal sealed class TrayIconService : IDisposable
{
    private readonly Forms.ContextMenuStrip _contextMenu;
    private readonly Forms.NotifyIcon _trayIcon;

    public TrayIconService(
        Action resetWindowBounds,
        Action reloadSettings,
        Action openDataDirectory,
        Action exitApplication)
    {
        _contextMenu = new Forms.ContextMenuStrip();
        _contextMenu.Items.Add("Reset Position and Size", null, (_, _) => resetWindowBounds());
        _contextMenu.Items.Add(new Forms.ToolStripSeparator());
        _contextMenu.Items.Add("Reload Settings", null, (_, _) => reloadSettings());
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
    }

    public void ShowContextMenuAtCursor() => _contextMenu.Show(Forms.Cursor.Position);

    public void Dispose()
    {
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        _contextMenu.Dispose();
    }
}
