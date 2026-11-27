using System.Collections.ObjectModel;
using Avalonia.Controls;
using Vela.Avalonia.Controls;
using Xunit;

namespace Vela.Avalonia.Tests;

public class VelaDataViewsTests
{
    private sealed record FileItem(string Name, List<FileItem> Children);

    private sealed record MetricRow(string Name, string Region, double Latency, int Errors);

    private sealed class DeptRow
    {
        public DeptRow(string name, string owner, double budget, bool expanded = false)
        {
            Name = name;
            Owner = owner;
            Budget = budget;
            Expanded = expanded;
        }

        public string Name { get; }
        public string Owner { get; }
        public double Budget { get; }
        public bool Expanded { get; }
        public ObservableCollection<DeptRow> Children { get; } = new();
    }

    // ─── Column Layout Engine & Comparer ─────────────────────────────────

    [Fact]
    public void ColumnLayoutEngine_ResolvesFixedAutoAndStarWithMinMaxClamping()
    {
        var specs = new[]
        {
            new VelaColumnSpec(new GridLength(100), MinWidth: 60, MaxWidth: 200),
            new VelaColumnSpec(GridLength.Auto, MinWidth: 80, MaxWidth: 160, AutoWidth: 120),
            new VelaColumnSpec(new GridLength(1, GridUnitType.Star), MinWidth: 150, MaxWidth: 300),
            new VelaColumnSpec(new GridLength(3, GridUnitType.Star), MinWidth: 100, MaxWidth: 400),
        };

        // Available = 800; fixed = 100 + 120 = 220; remaining = 580
        // 1* vs 3* => 145 vs 435; col2 clamps up to MinWidth 150, col3 clamps down to MaxWidth 400.
        var widths = VelaColumnLayoutEngine.ResolveWidths(specs, 800);

        Assert.Equal(100, widths[0], 2);
        Assert.Equal(120, widths[1], 2);
        Assert.Equal(150, widths[2], 2);
        Assert.Equal(400, widths[3], 2);
    }

    [Fact]
    public void ColumnLayoutEngine_RespectsExplicitResizedWidth()
    {
        var specs = new[]
        {
            new VelaColumnSpec(new GridLength(2, GridUnitType.Star), MinWidth: 80, MaxWidth: 240, ExplicitPixelWidth: 500),
            new VelaColumnSpec(new GridLength(1, GridUnitType.Star), MinWidth: 60, MaxWidth: double.PositiveInfinity),
        };

        var widths = VelaColumnLayoutEngine.ResolveWidths(specs, 600);
        Assert.Equal(240, widths[0], 2); // clamped to MaxWidth
        Assert.Equal(360, widths[1], 2); // remaining space goes to unlocked star column
    }

    [Fact]
    public void DataComparer_ComparesNumbersStringsAndNullsConsistently()
    {
        Assert.True(VelaDataComparer.Compare(null, "a") < 0);
        Assert.True(VelaDataComparer.Compare(2, 10.5) < 0);
        Assert.Equal(0, VelaDataComparer.Compare("Vela", "vela"));
        Assert.True(VelaDataComparer.Compare(100m, 42) > 0);
    }

    // ─── VelaTreeView ────────────────────────────────────────────────────

    [Fact]
    public void VelaTreeView_FlattensExpandsAndCollapsesNodesWithAccurateDepths()
    {
        var root = new VelaTreeNode("src", isExpanded: true);
        var controls = new VelaTreeNode("Controls", isExpanded: false);
        var leaf = new VelaTreeNode("VelaTreeView.cs");
        controls.Children.Add(leaf);
        root.Children.Add(controls);

        var tree = new VelaTreeView();
        tree.Nodes.Add(root);

        Assert.Equal(2, tree.VisibleCount);
        Assert.Equal(0, tree.VisibleRows[0].Depth);
        Assert.Equal(1, tree.VisibleRows[1].Depth);
        Assert.True(tree.VisibleRows[1].HasChildren);
        Assert.False(tree.VisibleRows[1].IsExpanded);

        var expandedEvents = 0;
        var collapsedEvents = 0;
        tree.NodeExpanded += (_, _) => expandedEvents++;
        tree.NodeCollapsed += (_, _) => collapsedEvents++;

        tree.Expand(controls);
        Assert.Equal(3, tree.VisibleCount);
        Assert.Equal(2, tree.VisibleRows[2].Depth);
        Assert.Same(leaf, tree.VisibleRows[2].Item);
        Assert.Equal(1, expandedEvents);

        tree.CollapseAll();
        Assert.Equal(1, tree.VisibleCount);

        tree.ExpandAll();
        Assert.Equal(3, tree.VisibleCount);

        tree.Collapse(controls);
        Assert.Equal(2, tree.VisibleCount);
        Assert.Equal(1, collapsedEvents);
    }

    [Fact]
    public void VelaTreeView_CascadesTriStateCheckBoxesAndComputesIndeterminateAncestors()
    {
        var root = new VelaTreeNode("Root", isExpanded: true);
        var childA = new VelaTreeNode("A");
        var childB = new VelaTreeNode("B");
        root.Children.Add(childA);
        root.Children.Add(childB);

        var tree = new VelaTreeView { ShowCheckBoxes = true, CascadeCheckState = true };
        tree.Nodes.Add(root);

        // Checking root checks all descendants
        tree.SetChecked(root, true);
        Assert.Equal(true, tree.GetCheckState(root));
        Assert.Equal(true, tree.GetCheckState(childA));
        Assert.Equal(true, tree.GetCheckState(childB));
        Assert.Equal(true, root.IsChecked);

        // Unchecking one child turns parent indeterminate (null)
        tree.SetChecked(childA, false);
        Assert.Null(tree.GetCheckState(root));
        Assert.Null(root.IsChecked);
        Assert.Equal(false, tree.GetCheckState(childA));
        Assert.Equal(true, tree.GetCheckState(childB));

        // Unchecking remaining child unchecks parent
        tree.SetChecked(childB, false);
        Assert.Equal(false, tree.GetCheckState(root));
        Assert.Equal(false, root.IsChecked);
    }

    [Fact]
    public void VelaTreeView_FilterRetainsAncestorsAndRestoresExpandStateWhenCleared()
    {
        var root = new VelaTreeNode("Workspace", isExpanded: false);
        var nested = new VelaTreeNode("DeepFolder", isExpanded: false);
        var target = new VelaTreeNode("ShaderEngine.sksl");
        var sibling = new VelaTreeNode("Readme.md");
        nested.Children.Add(target);
        root.Children.Add(nested);
        root.Children.Add(sibling);

        var tree = new VelaTreeView();
        tree.Nodes.Add(root);

        Assert.Equal(1, tree.VisibleCount); // Initially collapsed

        tree.FilterText = "Shader";
        Assert.Equal(3, tree.VisibleCount);
        Assert.Equal(new[] { "Workspace", "DeepFolder", "ShaderEngine.sksl" }, tree.VisibleRows.Select(r => r.Label).ToArray());

        // Clearing filter restores original collapsed state
        tree.FilterText = string.Empty;
        Assert.Equal(1, tree.VisibleCount);
    }

    [Fact]
    public void VelaTreeView_SupportsArbitraryPocoViaChildrenAndLabelSelectors()
    {
        var data = new List<FileItem>
        {
            new("Docs", [new FileItem("Guide.md", []), new FileItem("API.md", [])]),
        };

        var tree = new VelaTreeView
        {
            ItemsSource = data,
            ChildrenSelector = o => ((FileItem)o).Children,
            LabelSelector = o => ((FileItem)o).Name,
        };

        Assert.Equal(1, tree.VisibleCount);
        Assert.Equal("Docs", tree.VisibleRows[0].Label);

        tree.ExpandAll();
        Assert.Equal(3, tree.VisibleCount);
        Assert.Equal("API.md", tree.VisibleRows[2].Label);
    }

    // ─── VelaDataGrid ────────────────────────────────────────────────────

    [Fact]
    public void VelaDataGrid_SortsCyclesDirectionsFiltersAndUpdatesLiveCollection()
    {
        var rows = new ObservableCollection<MetricRow>
        {
            new("gateway", "us-east", 14.2, 1),
            new("auth", "eu-west", 3.5, 0),
            new("indexer", "us-west", 42.8, 7),
        };

        var nameCol = new VelaDataGridTextColumn { Header = "Name", ValueSelector = o => ((MetricRow)o).Name };
        var latencyCol = new VelaDataGridTextColumn { Header = "Latency", ValueSelector = o => ((MetricRow)o).Latency };

        var grid = new VelaDataGrid { ItemsSource = rows };
        grid.Columns.Add(nameCol);
        grid.Columns.Add(latencyCol);

        Assert.Equal(3, grid.VisibleCount);

        // ToggleSort: None -> Asc -> Desc -> None
        grid.ToggleSort(latencyCol);
        Assert.Equal(VelaSortDirection.Ascending, grid.SortDirection);
        Assert.Equal(new[] { "auth", "gateway", "indexer" }, grid.VisibleRows.Select(r => ((MetricRow)r.Item).Name).ToArray());

        grid.ToggleSort(latencyCol);
        Assert.Equal(VelaSortDirection.Descending, grid.SortDirection);
        Assert.Equal(new[] { "indexer", "gateway", "auth" }, grid.VisibleRows.Select(r => ((MetricRow)r.Item).Name).ToArray());

        grid.ToggleSort(latencyCol);
        Assert.Equal(VelaSortDirection.None, grid.SortDirection);
        Assert.Equal(new[] { "gateway", "auth", "indexer" }, grid.VisibleRows.Select(r => ((MetricRow)r.Item).Name).ToArray());

        // SearchText filtering
        grid.SearchText = "gate";
        Assert.Equal(1, grid.VisibleCount);
        Assert.Equal("gateway", ((MetricRow)grid.VisibleRows[0].Item).Name);

        // Live INotifyCollectionChanged update
        grid.SearchText = string.Empty;
        rows.Add(new("billing", "ap-east", 8.0, 0));
        Assert.Equal(4, grid.VisibleCount);
    }

    [Fact]
    public void VelaDataGrid_HandlesMultiSelectionSelectAllStateAndColumnResizing()
    {
        var r1 = new MetricRow("a", "us", 1, 0);
        var r2 = new MetricRow("b", "eu", 2, 0);
        var r3 = new MetricRow("c", "ap", 3, 0);

        var col = new VelaDataGridTextColumn
        {
            Header = "Name",
            Width = new GridLength(1, GridUnitType.Star),
            MinWidth = 90,
            MaxWidth = 300,
            ValueSelector = o => ((MetricRow)o).Name,
        };

        var grid = new VelaDataGrid
        {
            ItemsSource = new[] { r1, r2, r3 },
            SelectionMode = VelaSelectionMode.Multiple,
            ShowCheckBoxes = true,
        };
        grid.Columns.Add(col);

        Assert.Equal(false, grid.GetSelectAllState());

        grid.SelectedItem = r1;
        Assert.Null(grid.GetSelectAllState()); // 1 of 3 => indeterminate

        grid.SelectAll();
        Assert.Equal(true, grid.GetSelectAllState());
        Assert.Equal(3, grid.SelectedItems.Count);

        grid.ClearSelection();
        Assert.Equal(false, grid.GetSelectAllState());
        Assert.Empty(grid.SelectedItems);

        // Resize clamping & reset
        grid.ResizeColumn(col, 40);
        Assert.Equal(90, col.ActualWidth, 2); // clamped to MinWidth

        grid.ResizeColumn(col, 999);
        Assert.Equal(300, col.ActualWidth, 2); // clamped to MaxWidth

        grid.ResetColumnWidth(col);
        Assert.True(col.ActualWidth >= 90 && col.ActualWidth <= 300);
    }

    // ─── VelaTreeTable ───────────────────────────────────────────────────

    [Fact]
    public void VelaTreeTable_PerformsLevelPreservingHierarchicalSort()
    {
        var eng = new DeptRow("Engineering", "Ada", 500, expanded: true);
        eng.Children.Add(new DeptRow("UI", "Emma", 120));
        eng.Children.Add(new DeptRow("Compiler", "Alan", 300));
        eng.Children.Add(new DeptRow("QA", "Grace", 80));

        var design = new DeptRow("Design", "Sofia", 200, expanded: true);
        design.Children.Add(new DeptRow("Tokens", "Kenji", 90));
        design.Children.Add(new DeptRow("Motion", "Liam", 110));

        var budgetCol = new VelaDataGridTextColumn { Header = "Budget", ValueSelector = o => ((DeptRow)o).Budget };
        var nameCol = new VelaDataGridTextColumn { Header = "Name", ValueSelector = o => ((DeptRow)o).Name };

        var table = new VelaTreeTable
        {
            ItemsSource = new[] { eng, design },
            ChildrenSelector = o => ((DeptRow)o).Children,
            InitialExpandedSelector = o => ((DeptRow)o).Expanded,
        };
        table.Columns.Add(nameCol);
        table.Columns.Add(budgetCol);

        // Sort ascending by Budget:
        // Root level: Design (200) before Engineering (500)
        // Design children: Tokens (90) before Motion (110)
        // Engineering children: QA (80), UI (120), Compiler (300)
        table.SortBy(budgetCol, VelaSortDirection.Ascending);

        var names = table.VisibleRows.Select(r => ((DeptRow)r.Item).Name).ToArray();
        var depths = table.VisibleRows.Select(r => r.Depth).ToArray();

        Assert.Equal(new[] { "Design", "Tokens", "Motion", "Engineering", "QA", "UI", "Compiler" }, names);
        Assert.Equal(new[] { 0, 1, 1, 0, 1, 1, 1 }, depths);
    }

    [Fact]
    public void VelaTreeTable_FiltersAcrossColumnsAndCascadesTriStateCheckBoxes()
    {
        var root = new DeptRow("Platform", "Marcus", 400, expanded: false);
        var childA = new DeptRow("Networking", "Kenji", 180);
        var childB = new DeptRow("Storage", "Nadia", 220);
        root.Children.Add(childA);
        root.Children.Add(childB);

        var table = new VelaTreeTable
        {
            ItemsSource = new[] { root },
            ChildrenSelector = o => ((DeptRow)o).Children,
            ShowCheckBoxes = true,
            CascadeCheckState = true,
        };
        table.Columns.Add(new VelaDataGridTextColumn { Header = "Name", ValueSelector = o => ((DeptRow)o).Name });
        table.Columns.Add(new VelaDataGridTextColumn { Header = "Owner", ValueSelector = o => ((DeptRow)o).Owner });

        // Search by secondary column ("Nadia") retains and auto-expands parent "Platform"
        table.SearchText = "Nadia";
        Assert.Equal(2, table.VisibleCount);
        Assert.Equal(new[] { "Platform", "Storage" }, table.VisibleRows.Select(r => ((DeptRow)r.Item).Name).ToArray());

        table.SearchText = string.Empty;
        table.ExpandAll();
        Assert.Equal(3, table.VisibleCount);

        // Tri-state cascading check + header state
        Assert.Equal(false, table.GetHeaderCheckState());

        table.SetChecked(childA, true);
        Assert.Null(table.GetCheckState(root));
        Assert.Null(table.GetHeaderCheckState());

        table.SetAllChecked(true);
        Assert.Equal(true, table.GetCheckState(root));
        Assert.Equal(true, table.GetHeaderCheckState());

        table.SetAllChecked(false);
        Assert.Equal(false, table.GetHeaderCheckState());
    }
}
