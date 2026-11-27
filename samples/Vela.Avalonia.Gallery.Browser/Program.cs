using System.Runtime.Versioning;
using Avalonia;
using Avalonia.Browser;
using Avalonia.Media;

[assembly: SupportedOSPlatform("browser")]

namespace Vela.Avalonia.Gallery.Browser;

internal sealed partial class Program
{
    private static Task Main(string[] args) => BuildAvaloniaApp()
        .StartBrowserAppAsync("out", new BrowserPlatformOptions
        {
            RenderingMode =
            [
                BrowserRenderingMode.WebGL2,
                BrowserRenderingMode.Software2D,
            ],
        });

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .WithInterFont()
            .With(new FontManagerOptions
            {
                DefaultFamilyName = "fonts:Inter#Inter",
                // The shared views use the short Inter name; WASM cannot resolve system fonts.
                FontFamilyMappings = new Dictionary<string, FontFamily>
                {
                    ["Inter"] = new FontFamily("fonts:Inter#Inter"),
                },
                FontFallbacks =
                [
                    new FontFallback
                    {
                        FontFamily = new FontFamily(
                            "avares://Vela.Avalonia.Gallery.Browser/Assets/Fonts#Noto Sans SC"),
                    },
                ],
            });
}
