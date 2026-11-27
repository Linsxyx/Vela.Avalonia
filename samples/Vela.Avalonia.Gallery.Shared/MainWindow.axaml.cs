using Avalonia;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Vela.Avalonia.Controls;

namespace Vela.Avalonia.Gallery;

public partial class MainWindow : VelaWindow
{
    public MainWindow()
    {
        InitializeComponent();
        MainViewHost.BindTrayIcon(AppTrayIcon);
    }

    private void OnTrayShowWindowClick(object? sender, EventArgs e)
    {
        AppTrayIcon.HideFlyout();
        AppTrayIcon.RestoreWindow();
        MainViewHost.LogTrayEvent("Restored main window from tray.");
    }

    private void OnTrayToggleThemeClick(object? sender, EventArgs e)
    {
        if (Application.Current is null)
            return;

        var isDark = Application.Current.ActualThemeVariant == ThemeVariant.Dark;
        Application.Current.RequestedThemeVariant = isDark ? ThemeVariant.Light : ThemeVariant.Dark;
        MainViewHost.LogTrayEvent($"Switched theme to {(isDark ? "Light" : "Dark")} via tray.");
    }

    private void OnTrayToggleSyncClick(object? sender, EventArgs e)
    {
        TraySyncMenuItem.IsChecked = !TraySyncMenuItem.IsChecked;
        MainViewHost.LogTrayEvent($"Background Sync {(TraySyncMenuItem.IsChecked ? "enabled" : "paused")} via tray.");
    }

    private void OnTraySendToastClick(object? sender, EventArgs e)
    {
        AppTrayIcon.RestoreWindow();
        MainViewHost.ShowTrayDemoToast();
    }

    private void OnTrayQuitClick(object? sender, EventArgs e)
    {
        CloseBypassingTray();
    }

    private void OnTrayPopoverOpenClick(object? sender, RoutedEventArgs e) => OnTrayShowWindowClick(sender, EventArgs.Empty);

    private void OnTrayPopoverQuitClick(object? sender, RoutedEventArgs e) => OnTrayQuitClick(sender, EventArgs.Empty);
}
