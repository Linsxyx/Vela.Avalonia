using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Styling;
using Avalonia.Threading;
using BenchmarkDotNet.Attributes;
using Vela.Avalonia.Charts;
using Vela.Avalonia.Controls;
using Vela.Avalonia.Tokens;

namespace Vela.Avalonia.Benchmarks;

[MemoryDiagnoser]
public class ThemeAndRenderingBenchmarks
{
    private HeadlessBenchmarkSession _session = null!;
    private Window _window = null!;
    private VelaLineChart _lineChart = null!;
    private VelaBarChart _barChart = null!;
    private VelaActivityHeatmap _heatmap = null!;
    private VelaLiquidGlass _liquidGlass = null!;
    private int _accentIndex;

    private static readonly VelaAccent[] Accents =
    [
        VelaAccent.Neutral,
        VelaAccent.Blue,
        VelaAccent.Violet,
        VelaAccent.Green,
        VelaAccent.Amber,
        VelaAccent.Coral,
    ];

    [GlobalSetup]
    public void Setup()
    {
        _session = new HeadlessBenchmarkSession();
        _session.Dispatch(() =>
        {
            var points = Enumerable.Range(0, 200)
                .Select(i => 50.0 + Math.Sin(i * 0.12) * 30.0 + (i % 7))
                .ToArray();

            _lineChart = new VelaLineChart
            {
                Width = 600,
                Height = 220,
                Labels = Enumerable.Range(0, 200).Select(i => $"T{i}").ToArray(),
                Series =
                [
                    new VelaLineSeries("s1", "Latency", points),
                    new VelaLineSeries("s2", "Baseline", points.Select(v => v * 0.7).ToArray()),
                ],
            };

            _barChart = new VelaBarChart
            {
                Width = 600,
                Height = 200,
                Data = Enumerable.Range(0, 24)
                    .Select(i => new VelaBarDatum($"H{i:D2}", 40 + (i * 19) % 160))
                    .ToArray(),
            };

            _heatmap = new VelaActivityHeatmap
            {
                Width = 600,
                Height = 140,
                Weeks = 26,
                Entries = Enumerable.Range(0, 180)
                    .Select(i => new VelaHeatmapEntry(DateTime.Today.AddDays(-i), i % 8))
                    .ToArray(),
            };

            _liquidGlass = new VelaLiquidGlass
            {
                Width = 320,
                Height = 140,
                Content = new TextBlock { Text = "Liquid Glass Benchmark" },
            };

            var denseStrip = new WrapPanel();
            for (var i = 0; i < 40; i++)
            {
                denseStrip.Children.Add(new VelaButton { Content = $"Btn {i}" });
                denseStrip.Children.Add(new VelaBadge { Content = $"B{i}" });
            }

            _window = new Window
            {
                Width = 960,
                Height = 800,
                Content = new StackPanel
                {
                    Spacing = 8,
                    Children = { denseStrip, _lineChart, _barChart, _heatmap, _liquidGlass },
                },
            };
            _window.Show();
            Dispatcher.UIThread.RunJobs();
            _window.UpdateLayout();
            using var _ = _window.CaptureRenderedFrame();
        });
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _session.Dispatch(() =>
        {
            _window.Content = null;
            _window.Close();
            Dispatcher.UIThread.RunJobs();
        });
        _session.Dispose();
    }

    [Benchmark(Description = "VelaTheme: Accent switch + full Skia re-render (80+ controls)")]
    public void Theme_AccentSwitch_AndRender()
    {
        _session.Dispatch(() =>
        {
            var app = (BenchmarkApp)Application.Current!;
            app.Theme.Accent = Accents[_accentIndex++ % Accents.Length];
            Dispatcher.UIThread.RunJobs();
            _window.UpdateLayout();
            using var _ = _window.CaptureRenderedFrame();
        });
    }

    [Benchmark(Description = "VelaTheme: Light <-> Dark switch + full Skia re-render")]
    public void Theme_VariantSwitch_AndRender()
    {
        _session.Dispatch(() =>
        {
            var app = (BenchmarkApp)Application.Current!;
            app.RequestedThemeVariant = (_accentIndex++ & 1) == 0 ? ThemeVariant.Dark : ThemeVariant.Light;
            Dispatcher.UIThread.RunJobs();
            _window.UpdateLayout();
            using var _ = _window.CaptureRenderedFrame();
        });
    }

    [Benchmark(Description = "Skia Render: Charts + LiquidGlass single CaptureRenderedFrame")]
    public void Skia_ChartsAndLiquidGlass_SingleFrame()
    {
        _session.Dispatch(() =>
        {
            _lineChart.InvalidateVisual();
            _barChart.InvalidateVisual();
            _heatmap.InvalidateVisual();
            _liquidGlass.InvalidateVisual();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
            using var _ = _window.CaptureRenderedFrame();
        });
    }
}
