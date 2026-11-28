using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Vela.Avalonia.Controls;
using Vela.Avalonia.Gallery.Localization;
using Vela.Avalonia.Gallery.Services;

namespace Vela.Avalonia.Gallery.ViewModels;

public sealed class ServiceClusterRow
{
    public ServiceClusterRow(
        string service,
        string region,
        string status,
        VelaBadgeTone statusTone,
        double latencyMs,
        double memoryMb,
        string owner,
        bool isAutoScale)
    {
        Service = service;
        Region = region;
        Status = status;
        StatusTone = statusTone;
        LatencyMs = latencyMs;
        MemoryMb = memoryMb;
        Owner = owner;
        IsAutoScale = isAutoScale;
    }

    public string Service { get; }
    public string Region { get; }
    public string Status { get; }
    public VelaBadgeTone StatusTone { get; }
    public double LatencyMs { get; }
    public double MemoryMb { get; }
    public string Owner { get; }
    public bool IsAutoScale { get; set; }
}

public sealed class ModuleHierarchyRow
{
    public ModuleHierarchyRow(
        string name,
        string kind,
        string status,
        VelaBadgeTone statusTone,
        double sizeKb,
        double coverage,
        string owner,
        bool isFolder = false,
        bool isExpanded = false)
    {
        Name = name;
        Kind = kind;
        Status = status;
        StatusTone = statusTone;
        SizeKb = sizeKb;
        Coverage = coverage;
        Owner = owner;
        IsFolder = isFolder;
        IsExpanded = isExpanded;
    }

    public string Name { get; }
    public string Kind { get; }
    public string Status { get; }
    public VelaBadgeTone StatusTone { get; }
    public double SizeKb { get; }
    public double Coverage { get; }
    public string Owner { get; }
    public bool IsFolder { get; }
    public bool IsExpanded { get; }
    public ObservableCollection<ModuleHierarchyRow> Children { get; } = new();
}

public sealed class KanbanCardItem
{
    public KanbanCardItem(
        string id,
        string title,
        string priority,
        VelaBadgeTone priorityTone,
        string tag,
        string assignee)
    {
        Id = id;
        Title = title;
        Priority = priority;
        PriorityTone = priorityTone;
        Tag = tag;
        Assignee = assignee;
    }

    public string Id { get; }
    public string Title { get; }
    public string Priority { get; }
    public VelaBadgeTone PriorityTone { get; }
    public string Tag { get; }
    public string Assignee { get; }
}

public partial class DataViewsViewModel : ViewModelBase
{
    private readonly IGalleryShellService _shell;
    private bool _iconsApplied;

    // TreeView state
    public ObservableCollection<VelaTreeNode> ExplorerNodes { get; } = new();

    [ObservableProperty]
    public partial string TreeFilter { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool TreeShowGuides { get; set; } = true;

    [ObservableProperty]
    public partial bool TreeShowCheckBoxes { get; set; }

    [ObservableProperty]
    public partial string TreeStatusText { get; set; } = string.Empty;

    // DataGrid state
    public ObservableCollection<ServiceClusterRow> ClusterRows { get; } = new();

    [ObservableProperty]
    public partial string GridSearchText { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GridDensity))]
    public partial int GridDensityIndex { get; set; } = 1;

    public VelaDataDensity GridDensity => GridDensityIndex switch
    {
        0 => VelaDataDensity.Compact,
        2 => VelaDataDensity.Comfortable,
        _ => VelaDataDensity.Standard,
    };

    [ObservableProperty]
    public partial bool GridShowCheckBoxes { get; set; } = true;

    [ObservableProperty]
    public partial bool GridAlternatingRows { get; set; }

    [ObservableProperty]
    public partial bool GridVerticalLines { get; set; }

    [ObservableProperty]
    public partial string GridSummaryText { get; set; } = string.Empty;

    // TreeTable state
    public ObservableCollection<ModuleHierarchyRow> ModuleRoots { get; } = new();

    [ObservableProperty]
    public partial string TreeTableSearchText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool TreeTableShowGuides { get; set; } = true;

    [ObservableProperty]
    public partial bool TreeTableShowCheckBoxes { get; set; }

    [ObservableProperty]
    public partial string TreeTableSummaryText { get; set; } = string.Empty;

    // ListBox state
    public ObservableCollection<string> ReleaseChannels { get; } =
    [
        "Stable (v0.1.0)",
        "Beta (v0.2.0-rc.1)",
        "Nightly (main)",
        "WebAssembly Preview",
        "Native AOT macOS arm64",
        "Native AOT Windows x64",
    ];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ListBoxVariant))]
    public partial int ListBoxVariantIndex { get; set; } = 0;

    public VelaListBoxVariant ListBoxVariant => ListBoxVariantIndex switch
    {
        1 => VelaListBoxVariant.Inset,
        2 => VelaListBoxVariant.Plain,
        _ => VelaListBoxVariant.Card,
    };

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ListBoxDensity))]
    public partial int ListBoxDensityIndex { get; set; } = 1;

    public VelaDataDensity ListBoxDensity => ListBoxDensityIndex switch
    {
        0 => VelaDataDensity.Compact,
        2 => VelaDataDensity.Comfortable,
        _ => VelaDataDensity.Standard,
    };

    [ObservableProperty]
    public partial bool ListBoxShowIndicator { get; set; } = true;

    [ObservableProperty]
    public partial bool ListBoxShowCheckmarks { get; set; }

    [ObservableProperty]
    public partial bool ListBoxShowDividers { get; set; }

    [ObservableProperty]
    public partial string ListBoxStatusText { get; set; } = string.Empty;

    // Kanban (VelaDragList) state
    public ObservableCollection<KanbanCardItem> KanbanBacklog { get; } = new();
    public ObservableCollection<KanbanCardItem> KanbanInProgress { get; } = new();
    public ObservableCollection<KanbanCardItem> KanbanDone { get; } = new();

    [ObservableProperty]
    public partial string KanbanStatusText { get; set; } = string.Empty;

    public DataViewsViewModel(IGalleryShellService shell)
    {
        _shell = shell;
        BuildExplorerTree();
        BuildClusterRows();
        BuildModuleHierarchy();
        BuildKanbanCards();
        UpdateDefaultStatuses();
        Loc.Instance.LanguageChanged += _ => UpdateDefaultStatuses();
    }

    public void EnsureIconsLoaded()
    {
        if (_iconsApplied)
            return;

        var folder = _shell.FindIcon("VelaIconFolder");
        var file = _shell.FindIcon("VelaIconFile");
        var sparkles = _shell.FindIcon("VelaIconSparkles");
        var chart = _shell.FindIcon("VelaIconChart");
        var layers = _shell.FindIcon("VelaIconLayers");

        if (folder is null)
            return;

        _iconsApplied = true;
        foreach (var root in ExplorerNodes)
            ApplyIconsRecursive(root, folder, file, sparkles, chart, layers);
    }

    public Geometry? ResolveModuleIcon(ModuleHierarchyRow row)
        => row.IsFolder ? _shell.FindIcon("VelaIconFolder") : _shell.FindIcon("VelaIconFile");

    public void UpdateTreeStatus(object? selected, int checkedCount, int visibleCount)
    {
        var label = (selected as VelaTreeNode)?.Header ?? selected?.ToString() ?? Loc.Instance.Pick("None", "无");
        TreeStatusText = Loc.Instance.IsChinese
            ? $"当前选中: {label} · 已勾选 {checkedCount} 项 · 可见 {visibleCount} 节点"
            : $"Selected: {label} · {checkedCount} checked · {visibleCount} visible";
    }

    public void UpdateGridSummary(int visibleCount, int selectedCount, string? sortSummary)
    {
        var sortPart = string.IsNullOrEmpty(sortSummary) ? string.Empty : $" · {sortSummary}";
        GridSummaryText = Loc.Instance.IsChinese
            ? $"共 {visibleCount} 条服务 · 已选 {selectedCount} 条{sortPart}"
            : $"{visibleCount} services · {selectedCount} selected{sortPart}";
    }

    public void UpdateTreeTableSummary(int visibleCount, int selectedCount, int checkedCount)
    {
        TreeTableSummaryText = Loc.Instance.IsChinese
            ? $"展开可见 {visibleCount} 行 · 已选 {selectedCount} 行 · 已勾选 {checkedCount} 项"
            : $"{visibleCount} visible rows · {selectedCount} selected · {checkedCount} checked";
    }

    public void UpdateListBoxStatus(object? selectedPrimary, int multiSelectedCount)
    {
        var label = (selectedPrimary as VelaListBoxItem)?.Content?.ToString()
                    ?? selectedPrimary?.ToString()
                    ?? Loc.Instance.Pick("None", "无");
        ListBoxStatusText = Loc.Instance.IsChinese
            ? $"当前选中: {label} · 多选渠道已选 {multiSelectedCount} 项（双击或按 Enter 触发）"
            : $"Selected: {label} · {multiSelectedCount} channels selected (double-click or Enter to invoke)";
    }

    public void NotifyItemInvoked(string source, string label)
    {
        _shell.ShowToast(
            source,
            Loc.Instance.IsChinese ? $"已打开 {label}" : $"Opened {label}",
            VelaToastTone.Info);
    }

    private void UpdateDefaultStatuses()
    {
        if (string.IsNullOrEmpty(TreeStatusText))
            UpdateTreeStatus(null, 0, 12);
        if (string.IsNullOrEmpty(GridSummaryText))
            UpdateGridSummary(ClusterRows.Count, 0, null);
        if (string.IsNullOrEmpty(TreeTableSummaryText))
            UpdateTreeTableSummary(10, 0, 0);
        if (string.IsNullOrEmpty(ListBoxStatusText))
            UpdateListBoxStatus("Vela Studio", 0);
    }

    private void BuildExplorerTree()
    {
        var src = new VelaTreeNode("src", isExpanded: true) { Badge = "2 pkgs", BadgeTone = VelaBadgeTone.Accent };

        var core = new VelaTreeNode("Vela.Avalonia", isExpanded: true) { Description = "Core UI library" };
        var controls = new VelaTreeNode("Controls", isExpanded: true) { Badge = "58", BadgeTone = VelaBadgeTone.Info };
        controls.Children.Add(new VelaTreeNode("VelaTreeView.cs") { Description = "Virtualized tree", Badge = "NEW", BadgeTone = VelaBadgeTone.Success });
        controls.Children.Add(new VelaTreeNode("VelaDataGrid.cs") { Description = "Sortable grid", Badge = "NEW", BadgeTone = VelaBadgeTone.Success });
        controls.Children.Add(new VelaTreeNode("VelaTreeTable.cs") { Description = "Tree + Columns", Badge = "NEW", BadgeTone = VelaBadgeTone.Success });
        controls.Children.Add(new VelaTreeNode("VelaNavigationView.cs") { Description = "App rail shell" });
        controls.Children.Add(new VelaTreeNode("VelaLiquidGlass.cs") { Description = "SkSL refraction", Tag = "shader" });

        var motion = new VelaTreeNode("Motion", isExpanded: false) { Description = "Analytical springs" };
        motion.Children.Add(new VelaTreeNode("SpringSolver.cs") { Description = "Exact 2nd-order ODE" });
        motion.Children.Add(new VelaTreeNode("SpringRunner.cs") { Description = "0% idle frame ticker" });
        motion.Children.Add(new VelaTreeNode("VelaMotionTokens.cs") { Description = "Curated presets" });

        var tokens = new VelaTreeNode("Tokens", isExpanded: false) { Description = "OKLCH palettes" };
        tokens.Children.Add(new VelaTreeNode("VelaColorTokens.axaml") { Description = "Light & Dark" });
        tokens.Children.Add(new VelaTreeNode("VelaAccentPalettes.g.cs") { Description = "8 accents" });

        core.Children.Add(controls);
        core.Children.Add(motion);
        core.Children.Add(tokens);

        var charts = new VelaTreeNode("Vela.Avalonia.Charts", isExpanded: false) { Badge = "6", BadgeTone = VelaBadgeTone.Neutral, Tag = "chart" };
        charts.Children.Add(new VelaTreeNode("VelaLineChart.cs") { Tag = "chart" });
        charts.Children.Add(new VelaTreeNode("VelaBarChart.cs") { Tag = "chart" });
        charts.Children.Add(new VelaTreeNode("VelaDonutChart.cs") { Tag = "chart" });

        src.Children.Add(core);
        src.Children.Add(charts);

        var tests = new VelaTreeNode("tests", isExpanded: false) { Badge = "xUnit", BadgeTone = VelaBadgeTone.Neutral };
        tests.Children.Add(new VelaTreeNode("SpringSolverTests.cs") { Description = "Physics invariants" });
        tests.Children.Add(new VelaTreeNode("VelaDataViewsTests.cs") { Description = "Tree & Grid logic", Badge = "NEW", BadgeTone = VelaBadgeTone.Success });

        ExplorerNodes.Add(src);
        ExplorerNodes.Add(tests);
    }

    private static void ApplyIconsRecursive(
        VelaTreeNode node,
        Geometry? folder,
        Geometry? file,
        Geometry? sparkles,
        Geometry? chart,
        Geometry? layers)
    {
        if (node.Children.Count > 0)
        {
            node.Icon = node.Tag as string == "chart" ? chart : folder;
        }
        else
        {
            node.Icon = (node.Tag as string) switch
            {
                "shader" => sparkles,
                "chart" => chart,
                "layer" => layers,
                _ => file,
            };
        }

        foreach (var child in node.Children)
            ApplyIconsRecursive(child, folder, file, sparkles, chart, layers);
    }

    private void BuildClusterRows()
    {
        ClusterRows.Add(new("vela-gateway-edge", "ap-northeast-1", "Healthy", VelaBadgeTone.Success, 4.2, 312, "Ada Lovelace", true));
        ClusterRows.Add(new("shader-compiler-pool", "us-west-2", "Healthy", VelaBadgeTone.Success, 11.8, 1280, "Kenji Sato", true));
        ClusterRows.Add(new("token-sync-worker", "eu-central-1", "Healthy", VelaBadgeTone.Success, 6.5, 240, "Emma Collins", true));
        ClusterRows.Add(new("telemetry-ingest", "us-east-1", "Degraded", VelaBadgeTone.Warning, 48.3, 2140, "Marcus Chen", true));
        ClusterRows.Add(new("wasm-gallery-cdn", "global-edge", "Healthy", VelaBadgeTone.Success, 2.9, 128, "Sofia Lind", false));
        ClusterRows.Add(new("aot-symbol-indexer", "eu-west-1", "Syncing", VelaBadgeTone.Info, 19.4, 864, "Alan Turing", true));
        ClusterRows.Add(new("billing-ledger-api", "us-east-1", "Healthy", VelaBadgeTone.Success, 8.7, 512, "Grace Hopper", true));
        ClusterRows.Add(new("gpu-snapshot-farm", "ap-southeast-1", "Outage", VelaBadgeTone.Danger, 184.0, 4096, "Linus Torvalds", false));
        ClusterRows.Add(new("search-ranker-v2", "us-west-2", "Healthy", VelaBadgeTone.Success, 9.1, 740, "Nadia Omar", true));
        ClusterRows.Add(new("release-signer", "eu-central-1", "Healthy", VelaBadgeTone.Success, 5.4, 196, "Liam Park", false));
        ClusterRows.Add(new("realtime-collab-hub", "ap-northeast-1", "Degraded", VelaBadgeTone.Warning, 37.6, 1520, "Marcus Chen", true));
        ClusterRows.Add(new("design-lint-bot", "us-east-1", "Healthy", VelaBadgeTone.Success, 7.3, 288, "Emma Collins", true));
    }

    private void BuildModuleHierarchy()
    {
        var core = new ModuleHierarchyRow("Vela.Avalonia", "Assembly", "Ready", VelaBadgeTone.Success, 412.6, 94.2, "Core Team", isFolder: true, isExpanded: true);

        var dataViews = new ModuleHierarchyRow("Data Views", "Namespace", "Ready", VelaBadgeTone.Success, 118.4, 96.5, "Ada Lovelace", isFolder: true, isExpanded: true);
        dataViews.Children.Add(new ModuleHierarchyRow("VelaTreeView", "Control", "Ready", VelaBadgeTone.Success, 36.2, 97.1, "Ada Lovelace"));
        dataViews.Children.Add(new ModuleHierarchyRow("VelaDataGrid", "Control", "Ready", VelaBadgeTone.Success, 42.8, 96.4, "Marcus Chen"));
        dataViews.Children.Add(new ModuleHierarchyRow("VelaTreeTable", "Control", "Ready", VelaBadgeTone.Success, 39.4, 96.0, "Emma Collins"));

        var motion = new ModuleHierarchyRow("Motion Engine", "Namespace", "Ready", VelaBadgeTone.Success, 48.2, 99.1, "Kenji Sato", isFolder: true, isExpanded: false);
        motion.Children.Add(new ModuleHierarchyRow("SpringSolver", "Physics", "Ready", VelaBadgeTone.Success, 18.5, 100.0, "Kenji Sato"));
        motion.Children.Add(new ModuleHierarchyRow("SpringRunner", "Ticker", "Ready", VelaBadgeTone.Success, 16.4, 98.4, "Kenji Sato"));
        motion.Children.Add(new ModuleHierarchyRow("VelaMotionTokens", "Tokens", "Ready", VelaBadgeTone.Info, 13.3, 98.8, "Emma Collins"));

        var shaders = new ModuleHierarchyRow("GPU Shaders", "Namespace", "Beta", VelaBadgeTone.Warning, 84.0, 88.0, "Sofia Lind", isFolder: true, isExpanded: false);
        shaders.Children.Add(new ModuleHierarchyRow("VelaLiquidGlass", "SkSL", "Ready", VelaBadgeTone.Success, 46.8, 91.2, "Sofia Lind"));
        shaders.Children.Add(new ModuleHierarchyRow("VelaShaderBackground", "SkSL", "Beta", VelaBadgeTone.Warning, 37.2, 84.8, "Sofia Lind"));

        core.Children.Add(dataViews);
        core.Children.Add(motion);
        core.Children.Add(shaders);

        var charts = new ModuleHierarchyRow("Vela.Avalonia.Charts", "Assembly", "Ready", VelaBadgeTone.Success, 164.0, 91.6, "Visualization", isFolder: true, isExpanded: true);
        charts.Children.Add(new ModuleHierarchyRow("VelaLineChart", "Chart", "Ready", VelaBadgeTone.Success, 34.5, 93.0, "Grace Hopper"));
        charts.Children.Add(new ModuleHierarchyRow("VelaBarChart", "Chart", "Ready", VelaBadgeTone.Success, 31.2, 92.4, "Grace Hopper"));
        charts.Children.Add(new ModuleHierarchyRow("VelaDonutChart", "Chart", "Ready", VelaBadgeTone.Success, 29.8, 90.5, "Nadia Omar"));
        charts.Children.Add(new ModuleHierarchyRow("VelaActivityHeatmap", "Chart", "Review", VelaBadgeTone.Info, 27.6, 89.2, "Nadia Omar"));

        ModuleRoots.Add(core);
        ModuleRoots.Add(charts);
    }

    public void NotifyKanbanMoved(KanbanCardItem card, string targetColumn, int targetIndex)
    {
        KanbanStatusText = Loc.Instance.IsChinese
            ? $"已移动 {card.Id} 到「{targetColumn}」第 {targetIndex + 1} 位 · 共 {KanbanBacklog.Count}/{KanbanInProgress.Count}/{KanbanDone.Count} 张卡片"
            : $"Moved {card.Id} to {targetColumn} #{targetIndex + 1} · Counts {KanbanBacklog.Count}/{KanbanInProgress.Count}/{KanbanDone.Count}";
    }

    public void NotifyKanbanClicked(KanbanCardItem card, string column)
    {
        KanbanStatusText = Loc.Instance.IsChinese
            ? $"已选中 {card.Id}「{card.Title}」（{column} · {card.Assignee}）"
            : $"Selected {card.Id} \"{card.Title}\" ({column} · {card.Assignee})";
        _shell.ShowToast($"{card.Id} · {card.Title}", $"{column} · {card.Priority} · {card.Assignee}");
    }

    private void BuildKanbanCards()
    {
        KanbanBacklog.Add(new("VEL-104", "Design spring tilt curve for card drag", "High", VelaBadgeTone.Danger, "Motion", "AL"));
        KanbanBacklog.Add(new("VEL-109", "Keyboard Alt+Arrow card reordering", "Medium", VelaBadgeTone.Warning, "A11y", "KS"));
        KanbanBacklog.Add(new("VEL-112", "Add swimlane grouping header preset", "Low", VelaBadgeTone.Neutral, "Layout", "EC"));

        KanbanInProgress.Add(new("VEL-101", "OverlayLayer floating ghost & FLIP shift", "Urgent", VelaBadgeTone.Danger, "Core", "MC"));
        KanbanInProgress.Add(new("VEL-106", "Auto-scroll near ScrollViewer edges", "High", VelaBadgeTone.Info, "UX", "SL"));

        KanbanDone.Add(new("VEL-098", "Coalesced TopLevel SpringRunner pump", "Medium", VelaBadgeTone.Success, "Perf", "GH"));
        KanbanDone.Add(new("VEL-099", "AOT-safe compiled bindings audit", "Low", VelaBadgeTone.Success, "Build", "LT"));

        KanbanStatusText = Loc.Instance.IsChinese
            ? "拖拽卡片可在列内排序或跨列移动，按 Esc 取消，点击卡片查看详情。"
            : "Drag cards to reorder or move across columns. Press Esc to cancel, or click a card.";
    }
}
