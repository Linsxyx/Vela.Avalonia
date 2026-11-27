using Vela.Avalonia.Controls;
using Vela.Avalonia.Gallery.Services;
using Vela.Avalonia.Gallery.ViewModels;

namespace Vela.Avalonia.Gallery;

public partial class MainWindow : VelaWindow
{
    public MainWindow()
    {
        InitializeComponent();
    }

    public MainWindow(GalleryShellService shell, MainViewModel viewModel) : this()
    {
        DataContext = viewModel;
        TrayPopoverRoot.DataContext = viewModel;
        shell.AttachQuitAction(CloseBypassingTray);
        MainViewHost.Initialize(shell, viewModel, AppTrayIcon);
    }
}
