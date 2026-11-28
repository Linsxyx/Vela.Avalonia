using Avalonia;
using Avalonia.Headless;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Vela.Avalonia.Tokens;

[assembly: AvaloniaTestApplication(typeof(Vela.Avalonia.HeadlessTests.HeadlessTestAppBuilder))]

namespace Vela.Avalonia.HeadlessTests;

public sealed class HeadlessTestApp : Application
{
    public VelaTheme Theme { get; } = new();

    public override void Initialize()
    {
        RequestedThemeVariant = ThemeVariant.Light;
        Styles.Add(new FluentTheme());
        Theme.BeginInit();
        Theme.Accent = VelaAccent.Violet;
        Theme.Modules = VelaThemeModules.All;
        Theme.EndInit();
        Styles.Add(Theme);
    }
}

public sealed class HeadlessTestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder
        .Configure<HeadlessTestApp>()
        .UseSkia()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions
        {
            UseHeadlessDrawing = false,
        });
}
