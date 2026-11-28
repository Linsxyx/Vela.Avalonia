using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Vela.Avalonia.Controls;
using Vela.Avalonia.Gallery.ViewModels;

namespace Vela.Avalonia.Gallery.Views;

public partial class DataViewsPage : UserControl
{
    public DataViewsPage()
    {
        InitializeComponent();
        ConfigureDataGridColumns();
        ConfigureTreeTableColumns();
        AttachedToVisualTree += OnAttached;
    }

    private void OnAttached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (DataContext is DataViewsViewModel vm)
        {
            vm.EnsureIconsLoaded();
            ExplorerTree.Refresh();
            ModulesTreeTable.IconSelector = item => item is ModuleHierarchyRow row ? vm.ResolveModuleIcon(row) : null;
            ModulesTreeTable.Refresh();
            SyncStatuses();
        }
    }

    private void ConfigureDataGridColumns()
    {
        ClusterGrid.Columns.Add(new VelaDataGridTextColumn
        {
            Header = "Service",
            Width = new GridLength(2.2, GridUnitType.Star),
            MinWidth = 150,
            FontWeight = FontWeight.Medium,
            ValueSelector = o => ((ServiceClusterRow)o).Service,
        });

        ClusterGrid.Columns.Add(new VelaDataGridTextColumn
        {
            Header = "Region",
            Width = new GridLength(1.2, GridUnitType.Star),
            MinWidth = 110,
            IsMuted = true,
            IsMonospace = true,
            ValueSelector = o => ((ServiceClusterRow)o).Region,
        });

        ClusterGrid.Columns.Add(new VelaDataGridBadgeColumn
        {
            Header = "Status",
            Width = new GridLength(116),
            MinWidth = 96,
            ShowDot = true,
            IsPill = true,
            ValueSelector = o => ((ServiceClusterRow)o).Status,
            ToneSelector = o => ((ServiceClusterRow)o).StatusTone,
        });

        ClusterGrid.Columns.Add(new VelaDataGridTextColumn
        {
            Header = "P95 Latency",
            Width = new GridLength(110),
            MinWidth = 90,
            HorizontalAlignment = HorizontalAlignment.Right,
            IsMonospace = true,
            StringFormat = "{0:0.0} ms",
            ValueSelector = o => ((ServiceClusterRow)o).LatencyMs,
        });

        ClusterGrid.Columns.Add(new VelaDataGridTextColumn
        {
            Header = "Memory",
            Width = new GridLength(104),
            MinWidth = 86,
            HorizontalAlignment = HorizontalAlignment.Right,
            IsMonospace = true,
            IsMuted = true,
            StringFormat = "{0:0} MB",
            ValueSelector = o => ((ServiceClusterRow)o).MemoryMb,
        });

        ClusterGrid.Columns.Add(new VelaDataGridTextColumn
        {
            Header = "Owner",
            Width = new GridLength(1.3, GridUnitType.Star),
            MinWidth = 110,
            ValueSelector = o => ((ServiceClusterRow)o).Owner,
        });

        ClusterGrid.Columns.Add(new VelaDataGridCheckBoxColumn
        {
            Header = "Auto",
            Width = new GridLength(68),
            MinWidth = 56,
            ValueSelector = o => ((ServiceClusterRow)o).IsAutoScale,
            ValueSetter = (o, v) => ((ServiceClusterRow)o).IsAutoScale = v == true,
        });
    }

    private void ConfigureTreeTableColumns()
    {
        ModulesTreeTable.ChildrenSelector = item => item is ModuleHierarchyRow row ? row.Children : null;
        ModulesTreeTable.InitialExpandedSelector = item => item is ModuleHierarchyRow { IsExpanded: true };

        ModulesTreeTable.Columns.Add(new VelaDataGridTextColumn
        {
            Header = "Module",
            Width = new GridLength(2.4, GridUnitType.Star),
            MinWidth = 200,
            FontWeight = FontWeight.Medium,
            ValueSelector = o => ((ModuleHierarchyRow)o).Name,
        });

        ModulesTreeTable.Columns.Add(new VelaDataGridTextColumn
        {
            Header = "Kind",
            Width = new GridLength(108),
            MinWidth = 84,
            IsMuted = true,
            ValueSelector = o => ((ModuleHierarchyRow)o).Kind,
        });

        ModulesTreeTable.Columns.Add(new VelaDataGridBadgeColumn
        {
            Header = "Status",
            Width = new GridLength(104),
            MinWidth = 88,
            ShowDot = true,
            IsPill = true,
            ValueSelector = o => ((ModuleHierarchyRow)o).Status,
            ToneSelector = o => ((ModuleHierarchyRow)o).StatusTone,
        });

        ModulesTreeTable.Columns.Add(new VelaDataGridTextColumn
        {
            Header = "Size",
            Width = new GridLength(104),
            MinWidth = 84,
            HorizontalAlignment = HorizontalAlignment.Right,
            IsMonospace = true,
            StringFormat = "{0:0.0} KB",
            ValueSelector = o => ((ModuleHierarchyRow)o).SizeKb,
        });

        ModulesTreeTable.Columns.Add(new VelaDataGridTextColumn
        {
            Header = "Coverage",
            Width = new GridLength(102),
            MinWidth = 82,
            HorizontalAlignment = HorizontalAlignment.Right,
            IsMonospace = true,
            StringFormat = "{0:0.0}%",
            ValueSelector = o => ((ModuleHierarchyRow)o).Coverage,
        });

        ModulesTreeTable.Columns.Add(new VelaDataGridTextColumn
        {
            Header = "Owner",
            Width = new GridLength(1.2, GridUnitType.Star),
            MinWidth = 100,
            IsMuted = true,
            ValueSelector = o => ((ModuleHierarchyRow)o).Owner,
        });
    }

    private void SyncStatuses()
    {
        if (DataContext is not DataViewsViewModel vm)
            return;

        vm.UpdateTreeStatus(ExplorerTree.SelectedItem, ExplorerTree.CheckedItems.Count, ExplorerTree.VisibleCount);

        var sortText = ClusterGrid.SortColumn is not null
            ? $"{ClusterGrid.SortColumn.Header} ({ClusterGrid.SortDirection})"
            : null;
        vm.UpdateGridSummary(ClusterGrid.VisibleCount, ClusterGrid.SelectedItems.Count, sortText);

        vm.UpdateTreeTableSummary(
            ModulesTreeTable.VisibleCount,
            ModulesTreeTable.SelectedItems.Count,
            ModulesTreeTable.CheckedItems.Count);

        vm.UpdateListBoxStatus(
            WorkspaceListBox.SelectedItem,
            ChannelsListBox.SelectedItems?.Count ?? 0);
    }

    private void OnListBoxStateChanged(object? sender, SelectionChangedEventArgs e) => SyncStatuses();

    private void OnListBoxItemInvoked(object? sender, VelaDataItemEventArgs e)
    {
        if (DataContext is DataViewsViewModel vm)
        {
            var label = (e.Item as VelaListBoxItem)?.Content?.ToString() ?? e.Item.ToString() ?? string.Empty;
            vm.NotifyItemInvoked("VelaListBox", label);
        }
    }

    private void OnTreeExpandAll(object? sender, RoutedEventArgs e)
    {
        ExplorerTree.ExpandAll();
        SyncStatuses();
    }

    private void OnTreeCollapseAll(object? sender, RoutedEventArgs e)
    {
        ExplorerTree.CollapseAll();
        SyncStatuses();
    }

    private void OnTreeStateChanged(object? sender, EventArgs e) => SyncStatuses();

    private void OnTreeItemInvoked(object? sender, VelaDataItemEventArgs e)
    {
        if (DataContext is DataViewsViewModel vm)
            vm.NotifyItemInvoked("VelaTreeView", e.Item.ToString() ?? string.Empty);
    }

    private void OnGridStateChanged(object? sender, EventArgs e) => SyncStatuses();
    private void OnGridSortChanged(object? sender, VelaSortChangedEventArgs e) => SyncStatuses();

    private void OnGridClearSort(object? sender, RoutedEventArgs e)
    {
        ClusterGrid.ClearSort();
        SyncStatuses();
    }

    private void OnGridRowDoubleTapped(object? sender, VelaDataItemEventArgs e)
    {
        if (DataContext is DataViewsViewModel vm && e.Item is ServiceClusterRow row)
            vm.NotifyItemInvoked("VelaDataGrid", $"{row.Service} ({row.Region})");
    }

    private void OnTreeTableExpandAll(object? sender, RoutedEventArgs e)
    {
        ModulesTreeTable.ExpandAll();
        SyncStatuses();
    }

    private void OnTreeTableCollapseAll(object? sender, RoutedEventArgs e)
    {
        ModulesTreeTable.CollapseAll();
        SyncStatuses();
    }

    private void OnTreeTableStateChanged(object? sender, EventArgs e) => SyncStatuses();
    private void OnTreeTableSortChanged(object? sender, VelaSortChangedEventArgs e) => SyncStatuses();

    private void OnTreeTableClearSort(object? sender, RoutedEventArgs e)
    {
        ModulesTreeTable.ClearSort();
        SyncStatuses();
    }

    private void OnTreeTableRowDoubleTapped(object? sender, VelaDataItemEventArgs e)
    {
        if (DataContext is DataViewsViewModel vm && e.Item is ModuleHierarchyRow row)
            vm.NotifyItemInvoked("VelaTreeTable", $"{row.Name} · {row.Kind}");
    }

    private void OnKanbanCardMoved(object? sender, VelaDragMovedEventArgs e)
    {
        if (ReferenceEquals(sender, e.TargetList) &&
            DataContext is DataViewsViewModel vm &&
            e.Item is KanbanCardItem card)
        {
            var targetColumn = e.TargetList.Tag as string ?? "Board";
            global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                vm.NotifyKanbanMoved(card, targetColumn, e.TargetIndex));
        }
    }

    private void OnKanbanCardClicked(object? sender, VelaDragItemClickedEventArgs e)
    {
        if (sender is VelaDragList list &&
            DataContext is DataViewsViewModel vm &&
            e.Item is KanbanCardItem card)
        {
            vm.NotifyKanbanClicked(card, list.Tag as string ?? "Board");
        }
    }
}
