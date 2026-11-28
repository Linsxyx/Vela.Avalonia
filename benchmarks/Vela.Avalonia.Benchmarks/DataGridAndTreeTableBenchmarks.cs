using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using BenchmarkDotNet.Attributes;
using Vela.Avalonia.Controls;

namespace Vela.Avalonia.Benchmarks;

[MemoryDiagnoser]
public class DataGridAndTreeTableBenchmarks
{
    private sealed record RowItem(int Id, string Name, string Department, double Score);
    private sealed record TreeNodeItem(int Id, string Name, TreeNodeItem[] Children);

    private HeadlessBenchmarkSession _session = null!;
    private Window _window = null!;
    private VelaDataGrid _grid = null!;
    private VelaDataGridRowControl _recycleRow = null!;
    private VelaDataGridRowState[] _recycleStates = null!;
    private RowItem[] _rows1K = null!;
    private RowItem[] _rows10K = null!;
    private VelaTreeTable _tree = null!;
    private TreeNodeItem[] _treeRoots = null!;
    private int _toggle;

    [GlobalSetup]
    public void Setup()
    {
        _rows1K = Enumerable.Range(0, 1_000)
            .Select(i => new RowItem(i, $"User {i:D4}", i % 2 == 0 ? "Core" : "Design", (i * 13) % 100))
            .ToArray();

        _rows10K = Enumerable.Range(0, 10_000)
            .Select(i => new RowItem(i, $"User {i:D5}", i % 3 == 0 ? "Core" : "Design", (i * 17) % 100))
            .ToArray();

        _treeRoots = Enumerable.Range(0, 80)
            .Select(r => new TreeNodeItem(
                r,
                $"Folder {r}",
                Enumerable.Range(0, 15)
                    .Select(c => new TreeNodeItem(r * 100 + c, $"File {r}-{c}", []))
                    .ToArray()))
            .ToArray();

        _session = new HeadlessBenchmarkSession();
        _session.Dispatch(() =>
        {
            _grid = new VelaDataGrid { Width = 800, Height = 400 };
            _grid.Columns.Add(new VelaDataGridTextColumn { Header = "ID", ValueSelector = x => ((RowItem)x).Id.ToString() });
            _grid.Columns.Add(new VelaDataGridTextColumn { Header = "Name", ValueSelector = x => ((RowItem)x).Name });
            _grid.Columns.Add(new VelaDataGridTextColumn { Header = "Department", ValueSelector = x => ((RowItem)x).Department });
            _grid.Columns.Add(new VelaDataGridTextColumn { Header = "Score", ValueSelector = x => ((RowItem)x).Score.ToString("F0") });
            _grid.ItemsSource = _rows1K;

            _tree = new VelaTreeTable { Width = 800, Height = 400 };
            _tree.Columns.Add(new VelaDataGridTextColumn { Header = "Name", ValueSelector = x => ((TreeNodeItem)x).Name });
            _tree.Columns.Add(new VelaDataGridTextColumn { Header = "Id", ValueSelector = x => ((TreeNodeItem)x).Id.ToString() });
            _tree.ChildrenSelector = x => ((TreeNodeItem)x).Children;
            _tree.InitialExpandedSelector = _ => true;
            _tree.ItemsSource = _treeRoots;

            _window = new Window
            {
                Width = 840,
                Height = 860,
                Content = new StackPanel { Children = { _grid, _tree } },
            };
            _window.Show();
            Dispatcher.UIThread.RunJobs();
            _window.UpdateLayout();
            using var _ = _window.CaptureRenderedFrame();

            _recycleRow = new VelaDataGridRowControl();
            _recycleStates = Enumerable.Range(0, 100)
                .Select(i => new VelaDataGridRowState(_grid, _rows1K[i], i, (i & 1) == 0))
                .ToArray();
            _recycleRow.DataContext = _recycleStates[0];
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

    [Benchmark(Description = "VelaDataGridRowControl: 1,000 virtualized row recycles")]
    public void DataGridRow_Recycle_1000()
    {
        _session.Dispatch(() =>
        {
            for (var i = 0; i < 1_000; i++)
            {
                _recycleRow.DataContext = _recycleStates[i % _recycleStates.Length];
            }
        });
    }

    [Benchmark(Description = "VelaDataGrid: rebind + layout + Skia render (10,000 rows)")]
    public int DataGrid_RebindAndRender_10K()
    {
        return _session.Dispatch(() =>
        {
            _grid.ItemsSource = (_toggle++ & 1) == 0 ? _rows10K : _rows1K;
            Dispatcher.UIThread.RunJobs();
            _window.UpdateLayout();
            using var _ = _window.CaptureRenderedFrame();
            return _grid.VisibleCount;
        });
    }

    [Benchmark(Description = "VelaDataGrid: SearchText filter on 10,000 rows")]
    public int DataGrid_SearchFilter_10K()
    {
        return _session.Dispatch(() =>
        {
            _grid.ItemsSource = _rows10K;
            _grid.SearchText = (_toggle++ & 1) == 0 ? "0042" : null;
            Dispatcher.UIThread.RunJobs();
            return _grid.VisibleCount;
        });
    }

    [Benchmark(Description = "VelaTreeTable: SearchText filter on 1,280 nodes")]
    public int TreeTable_SearchFilter()
    {
        return _session.Dispatch(() =>
        {
            _tree.SearchText = (_toggle++ & 1) == 0 ? "File 12" : null;
            Dispatcher.UIThread.RunJobs();
            return _tree.VisibleCount;
        });
    }
}
