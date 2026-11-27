using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Vela.Avalonia.Gallery.Localization;
using Vela.Avalonia.Gallery.Services;
using Vela.Avalonia.Gallery.ViewModels;
using Vela.Avalonia.Tokens;

namespace Vela.Avalonia.Gallery;

public partial class App : Application
{
    public VelaTheme? Theme { get; private set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        Theme = Styles.OfType<VelaTheme>().FirstOrDefault();
        Loc.Instance.InitializeFromSystem();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var shell = new GalleryShellService();
        var mainVm = new MainViewModel(shell);

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow(shell, mainVm);
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            var mainView = new MainView();
            mainView.Initialize(shell, mainVm);
            singleViewPlatform.MainView = mainView;
        }

        base.OnFrameworkInitializationCompleted();
    }

    public void SetAccent(VelaAccent accent)
    {
        if (Theme is not null)
            Theme.Accent = accent;
    }
}
