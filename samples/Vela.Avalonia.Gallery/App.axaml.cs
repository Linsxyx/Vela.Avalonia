using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Vela.Avalonia.Tokens;

namespace Vela.Avalonia.Gallery;

public partial class App : Application
{
    public VelaTheme? Theme { get; private set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        Theme = Styles.OfType<VelaTheme>().FirstOrDefault();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }

    public void SetAccent(VelaAccent accent)
    {
        if (Theme is not null)
            Theme.Accent = accent;
    }
}
