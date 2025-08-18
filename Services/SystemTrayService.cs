using System.Drawing;
using System.Windows.Forms;

namespace TaskTracker.Services;

public enum TrayIconStatus
{
    Inactive,    // Red dot - outside tracking hours
    Active,      // Green dot - within tracking hours
    Lunch        // Orange dot - on lunch break
}

public interface ISystemTrayService
{
    void Initialize();
    void UpdateStatus(TrayIconStatus status);
    void ShowMainWindow();
    void Dispose();
    
    event EventHandler? MainWindowRequested;
    event EventHandler? ExitRequested;
}

public class SystemTrayService : ISystemTrayService, IDisposable
{
    private NotifyIcon? _notifyIcon;
    private bool _disposed = false;

    public event EventHandler? MainWindowRequested;
    public event EventHandler? ExitRequested;

    public void Initialize()
    {
        if (_notifyIcon != null) return;

    _notifyIcon = new NotifyIcon
        {
            Text = "TaskTracker",
            Visible = true
        };

        _notifyIcon.DoubleClick += OnNotifyIconDoubleClick;
    _notifyIcon.ContextMenuStrip = BuildContextMenu();
        
        // Set initial icon
        UpdateStatus(TrayIconStatus.Inactive);
    }

    public void UpdateStatus(TrayIconStatus status)
    {
        if (_notifyIcon == null) return;

        var icon = CreateStatusIcon(status);
        _notifyIcon.Icon = icon;
        
        _notifyIcon.Text = status switch
        {
            TrayIconStatus.Active => "TaskTracker - Active",
            TrayIconStatus.Lunch => "TaskTracker - On Lunch",
            TrayIconStatus.Inactive => "TaskTracker - Inactive",
            _ => "TaskTracker"
        };
    }

    public void ShowMainWindow()
    {
        MainWindowRequested?.Invoke(this, EventArgs.Empty);
    }

    private Icon CreateStatusIcon(TrayIconStatus status)
    {
        // Create a simple colored dot icon
        var bitmap = new Bitmap(16, 16);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.Clear(Color.Transparent);
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            var color = status switch
            {
                TrayIconStatus.Active => Color.Green,
                TrayIconStatus.Lunch => Color.Orange,
                TrayIconStatus.Inactive => Color.Red,
                _ => Color.Gray
            };

            using (var brush = new SolidBrush(color))
            {
                graphics.FillEllipse(brush, 2, 2, 12, 12);
            }

            using (var pen = new Pen(Color.Black, 1))
            {
                graphics.DrawEllipse(pen, 2, 2, 12, 12);
            }
        }

        return Icon.FromHandle(bitmap.GetHicon());
    }

    private void OnNotifyIconDoubleClick(object? sender, EventArgs e)
    {
        ShowMainWindow();
    }

    private ContextMenuStrip BuildContextMenu()
    {
        var menu = new ContextMenuStrip();
        var showItem = new ToolStripMenuItem("Show TaskTracker");
        showItem.Click += (_, __) => ShowMainWindow();
        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += (_, __) => ExitRequested?.Invoke(this, EventArgs.Empty);
        menu.Items.Add(showItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exitItem);
        return menu;
    }

    public void Dispose()
    {
        if (_disposed) return;

        _notifyIcon?.Dispose();
        _disposed = true;
    }
}
