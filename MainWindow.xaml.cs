using System.Windows;
using System.Windows.Input;

namespace PointBlankPanel;

public partial class MainWindow : Window
{
    private System.Windows.Forms.NotifyIcon? _trayIcon;
    private bool _closeToTray = true;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => InitializeTray();
    }

    private void InitializeTray()
    {
        _trayIcon = new System.Windows.Forms.NotifyIcon
        {
            Text = "System Service Host",
            Visible = true,
        };
        _trayIcon.DoubleClick += (_, _) => RestoreFromTray();
        _trayIcon.ContextMenuStrip = new System.Windows.Forms.ContextMenuStrip();
        _trayIcon.ContextMenuStrip.Items.Add("Abrir", null, (_, _) => RestoreFromTray());
        _trayIcon.ContextMenuStrip.Items.Add("Sair", null, (_, _) => { _closeToTray = false; Close(); });
    }

    private void RestoreFromTray()
    {
        ShowInTaskbar = true;
        WindowState = WindowState.Normal;
        Activate();
    }

    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);
        if (WindowState == WindowState.Minimized)
        {
            ShowInTaskbar = false;
            _trayIcon?.ShowBalloonTip(2000, "System Service Host", "App minimizado para a bandeja.", System.Windows.Forms.ToolTipIcon.Info);
        }
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (_closeToTray && WindowState != WindowState.Minimized)
        {
            e.Cancel = true;
            WindowState = WindowState.Minimized;
            return;
        }
        _trayIcon?.Dispose();
        base.OnClosing(e);
    }

    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            DragMove();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        _closeToTray = false;
        Close();
    }

    private void ScrollUp_Click(object sender, RoutedEventArgs e)
    {
        var offset = ItemScroller.VerticalOffset - ItemScroller.ViewportHeight * 0.7;
        if (offset < 0) offset = 0;
        ItemScroller.ScrollToVerticalOffset(offset);
    }

    private void ScrollDown_Click(object sender, RoutedEventArgs e)
    {
        var offset = ItemScroller.VerticalOffset + ItemScroller.ViewportHeight * 0.7;
        if (offset > ItemScroller.ScrollableHeight) offset = ItemScroller.ScrollableHeight;
        ItemScroller.ScrollToVerticalOffset(offset);
    }
}
