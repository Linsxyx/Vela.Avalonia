using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Vela.Avalonia.Charts;
using Vela.Avalonia.Controls;
using Vela.Avalonia.HeadlessTests.Infrastructure;
using Vela.Avalonia.Motion;
using Vela.Avalonia.Tokens;
using Xunit;

namespace Vela.Avalonia.HeadlessTests;

/// <summary>
/// Steady-state memory growth tests, virtualization bounds verification, and deterministic
/// allocation budgets running on the real Avalonia Headless + Skia pipeline.
/// </summary>
public class MemoryStabilityAndPerformanceTests
{
    private sealed record RowItem(int Id, string Name, string Department, double Score);
    private sealed record TreeNodeItem(int Id, string Name, TreeNodeItem[] Children);

    [AvaloniaFact]
    public void CompositePage_RepeatedMountRenderUnmount_HasBoundedSteadyStateHeapGrowth()
    {
        var window = new Window { Width = 960, Height = 640 };
        window.Show();
        HeadlessLeakVerifier.FlushLayoutAndRender(window);

        try
        {
            var heapDelta = HeadlessLeakVerifier.MeasureSteadyStateHeapDelta(
                cycle: () =>
                {
                    window.Content = BuildCompositeDashboard();
                    HeadlessLeakVerifier.FlushLayoutAndRender(window, renderTicks: 1);
                    window.Content = null;
                    HeadlessLeakVerifier.FlushLayoutAndRender(window, renderTicks: 1);
                },
                warmupCycles: 4,
                measuredCycles: 25);

            // 25 full mount/render/unmount cycles of a dense composite dashboard should not
            // accumulate unbounded managed memory (allow modest GC/cache noise <= 4 MB).
            const long maxAllowedGrowthBytes = 4L * 1024 * 1024;
            Assert.True(
                heapDelta <= maxAllowedGrowthBytes,
                $"Steady-state heap grew by {heapDelta / 1024.0:F1} KB across 25 mount/unmount cycles (limit: {maxAllowedGrowthBytes / 1024.0:F0} KB).");
        }
        finally
        {
            window.Content = null;
            window.Close();
            Dispatcher.UIThread.RunJobs();
        }
    }

    [AvaloniaFact]
    public void VelaTheme_RepeatedAccentAndVariantSwitching_HasBoundedHeapGrowthAndAllocation()
    {
        var app = HeadlessLeakVerifier.CurrentApp;
        var originalVariant = app.RequestedThemeVariant;
        var originalAccent = app.Theme.Accent;

        var window = new Window
        {
            Width = 960,
            Height = 640,
            Content = BuildCompositeDashboard(),
        };
        window.Show();
        HeadlessLeakVerifier.FlushLayoutAndRender(window);

        VelaAccent[] accents =
        [
            VelaAccent.Neutral,
            VelaAccent.Blue,
            VelaAccent.Violet,
            VelaAccent.Green,
            VelaAccent.Amber,
            VelaAccent.Coral,
        ];

        try
        {
            var idx = 0;
            var heapDelta = HeadlessLeakVerifier.MeasureSteadyStateHeapDelta(
                cycle: () =>
                {
                    app.Theme.Accent = accents[idx % accents.Length];
                    app.RequestedThemeVariant = (idx & 1) == 0 ? ThemeVariant.Dark : ThemeVariant.Light;
                    idx++;
                    HeadlessLeakVerifier.FlushLayoutAndRender(window, renderTicks: 1);
                },
                warmupCycles: 6,
                measuredCycles: 30);

            const long maxAllowedGrowthBytes = 3L * 1024 * 1024;
            Assert.True(
                heapDelta <= maxAllowedGrowthBytes,
                $"Theme/Accent switching grew managed heap by {heapDelta / 1024.0:F1} KB across 30 switches (limit: {maxAllowedGrowthBytes / 1024.0:F0} KB).");
        }
        finally
        {
            app.RequestedThemeVariant = originalVariant;
            app.Theme.Accent = originalAccent;
            window.Content = null;
            window.Close();
            Dispatcher.UIThread.RunJobs();
        }
    }

    [AvaloniaFact]
    public void VelaDataGrid_TenThousandRows_RemainsVirtualizedAndBoundedInVisualTree()
    {
        var rows = Enumerable.Range(0, 10_000)
            .Select(i => new RowItem(i, $"Employee {i:D5}", i % 3 == 0 ? "Core" : "Design", (i * 17) % 100))
            .ToArray();

        var grid = new VelaDataGrid { Width = 800, Height = 400 };
        grid.Columns.Add(new VelaDataGridTextColumn { Header = "ID", ValueSelector = x => ((RowItem)x).Id.ToString() });
        grid.Columns.Add(new VelaDataGridTextColumn { Header = "Name", ValueSelector = x => ((RowItem)x).Name });
        grid.Columns.Add(new VelaDataGridTextColumn { Header = "Department", ValueSelector = x => ((RowItem)x).Department });
        grid.Columns.Add(new VelaDataGridTextColumn { Header = "Score", ValueSelector = x => ((RowItem)x).Score.ToString("F0") });
        grid.ItemsSource = rows;

        var window = new Window { Width = 840, Height = 440, Content = grid };
        try
        {
            var sw = Stopwatch.StartNew();
            window.Show();
            HeadlessLeakVerifier.FlushLayoutAndRender(window);
            sw.Stop();

            Assert.Equal(10_000, grid.VisibleCount);

            var realizedRows = grid.GetVisualDescendants().OfType<VelaDataGridRowControl>().Count();
            Assert.InRange(realizedRows, 1, 60);

            // Scroll deep into the 10,000-row dataset via inner ScrollViewer and verify row count stays bounded
            var scrollViewer = grid.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
            if (scrollViewer is not null)
            {
                scrollViewer.Offset = new global::Avalonia.Vector(0, 120_000);
                HeadlessLeakVerifier.FlushLayoutAndRender(window, renderTicks: 1);

                var realizedRowsAfterScroll = grid.GetVisualDescendants().OfType<VelaDataGridRowControl>().Count();
                Assert.InRange(realizedRowsAfterScroll, 1, 60);
            }

            // Coarse regression ceiling against O(N^2) blowups (not a tight microbenchmark threshold)
            Assert.True(
                sw.Elapsed < TimeSpan.FromSeconds(5),
                $"Initial layout + render of 10,000-row VelaDataGrid took {sw.Elapsed.TotalMilliseconds:F0} ms.");
        }
        finally
        {
            window.Content = null;
            window.Close();
            Dispatcher.UIThread.RunJobs();
        }
    }

    [AvaloniaFact]
    public void VelaTreeTable_LargeHierarchy_RemainsVirtualizedInVisualTree()
    {
        var roots = Enumerable.Range(0, 100)
            .Select(r => new TreeNodeItem(
                r,
                $"Folder {r}",
                Enumerable.Range(0, 20)
                    .Select(c => new TreeNodeItem(r * 100 + c, $"File {r}-{c}", []))
                    .ToArray()))
            .ToArray();

        var tree = new VelaTreeTable { Width = 800, Height = 400 };
        tree.Columns.Add(new VelaDataGridTextColumn { Header = "Name", ValueSelector = x => ((TreeNodeItem)x).Name });
        tree.Columns.Add(new VelaDataGridTextColumn { Header = "Id", ValueSelector = x => ((TreeNodeItem)x).Id.ToString() });
        tree.ChildrenSelector = x => ((TreeNodeItem)x).Children;
        tree.InitialExpandedSelector = _ => true;
        tree.ItemsSource = roots;

        var window = new Window { Width = 840, Height = 440, Content = tree };
        try
        {
            window.Show();
            HeadlessLeakVerifier.FlushLayoutAndRender(window);

            Assert.Equal(2_100, tree.VisibleCount);

            var realizedRows = tree.GetVisualDescendants().OfType<VelaTreeTableRowControl>().Count();
            Assert.InRange(realizedRows, 1, 60);
        }
        finally
        {
            window.Content = null;
            window.Close();
            Dispatcher.UIThread.RunJobs();
        }
    }

    [AvaloniaFact]
    public void SpringChannel_ThousandFrameStepping_IsZeroAllocation()
    {
        var spring = new SpringChannel(0.0, SpringSpec.FromVisualDuration(0.30, bounce: 0.2));
        spring.SetTarget(100.0);

        // Warmup JIT
        spring.Step(1.0 / 60.0);

        var allocated = HeadlessLeakVerifier.MeasureAllocatedBytes(() =>
        {
            for (var i = 0; i < 1_000; i++)
            {
                if (!spring.IsAnimating)
                    spring.SetTarget((i & 1) == 0 ? 100.0 : 0.0);
                spring.Step(1.0 / 60.0);
            }
        });

        Assert.Equal(0, allocated);
    }

    private static Control BuildCompositeDashboard()
    {
        var grid = new VelaDataGrid { Height = 180 };
        grid.Columns.Add(new VelaDataGridTextColumn { Header = "Name", ValueSelector = x => ((RowItem)x).Name });
        grid.Columns.Add(new VelaDataGridTextColumn { Header = "Dept", ValueSelector = x => ((RowItem)x).Department });
        grid.ItemsSource = Enumerable.Range(0, 40)
            .Select(i => new RowItem(i, $"Item {i}", "Platform", i * 2.5))
            .ToArray();

        return new StackPanel
        {
            Spacing = 12,
            Children =
            {
                new StackPanel
                {
                    Orientation = global::Avalonia.Layout.Orientation.Horizontal,
                    Spacing = 8,
                    Children =
                    {
                        new VelaButton { Content = "Primary" },
                        new VelaInput { Width = 160, Text = "Search..." },
                        new VelaSwitch { IsChecked = true },
                        new VelaBadge { Content = "Beta" },
                        new VelaAvatar { Initials = "VL" },
                    },
                },
                new VelaGlassCard
                {
                    Content = new VelaLineChart
                    {
                        Width = 420,
                        Height = 140,
                        Labels = ["D1", "D2", "D3", "D4", "D5", "D6"],
                        Series = [new VelaLineSeries("m", "Throughput", [12, 24, 18, 36, 30, 45])],
                    },
                },
                grid,
            },
        };
    }
}
