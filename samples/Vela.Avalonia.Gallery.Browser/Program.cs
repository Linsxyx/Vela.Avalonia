using System.Runtime.Versioning;
using Avalonia;
using Avalonia.Browser;

[assembly: SupportedOSPlatform("browser")]

namespace Vela.Avalonia.Gallery.Browser;

internal sealed partial class Program
{
    private static Task Main(string[] args) => BuildAvaloniaApp()
        .WithInterFont()
        .StartBrowserAppAsync("out", new BrowserPlatformOptions
        {
            RenderingMode =
            [
                BrowserRenderingMode.WebGL2,
                BrowserRenderingMode.Software2D,
            ],
        });

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>();
}
