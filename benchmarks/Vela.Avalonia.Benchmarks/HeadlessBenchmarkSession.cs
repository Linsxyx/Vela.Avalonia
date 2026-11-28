using Avalonia;
using Avalonia.Headless;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Vela.Avalonia.Tokens;

namespace Vela.Avalonia.Benchmarks;

public sealed class BenchmarkApp : Application
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

public static class BenchmarkAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder
        .Configure<BenchmarkApp>()
        .UseSkia()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions
        {
            UseHeadlessDrawing = false,
        });
}

/// <summary>
/// Wraps a long-lived <see cref="HeadlessUnitTestSession"/> for BenchmarkDotNet UI benchmarks.
/// </summary>
public sealed class HeadlessBenchmarkSession : IDisposable
{
    private readonly HeadlessUnitTestSession _session;

    public HeadlessBenchmarkSession()
    {
        _session = HeadlessUnitTestSession.StartNew(typeof(BenchmarkAppBuilder));
    }

    public void Dispatch(Action action) =>
        _session.Dispatch(action, CancellationToken.None).GetAwaiter().GetResult();

    public T Dispatch<T>(Func<T> func) =>
        _session.Dispatch(func, CancellationToken.None).GetAwaiter().GetResult();

    public void Dispose() => _session.Dispose();
}
