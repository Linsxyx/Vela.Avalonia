using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Metadata;
using Avalonia.Threading;
using Vela.Avalonia.Motion;
using Vela.Avalonia.Tokens;

namespace Vela.Avalonia.Controls;

public enum VelaDockSearchMode
{
    InlineExpand,
    ActionButton,
}

/// <summary>
/// Declarative tab destination inside <see cref="VelaDockBar"/>.
/// Supports a top icon, concise bottom label, and an optional circular count/dot badge perched on the icon's top-right corner.
/// </summary>
public class VelaDockItem : AvaloniaObject
{
    public static readonly StyledProperty<string?> HeaderProperty =
        AvaloniaProperty.Register<VelaDockItem, string?>(nameof(Header));

    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<VelaDockItem, Geometry?>(nameof(Icon));

    public static readonly StyledProperty<string?> BadgeProperty =
        AvaloniaProperty.Register<VelaDockItem, string?>(nameof(Badge));

    public static readonly StyledProperty<bool> IsEnabledProperty =
        AvaloniaProperty.Register<VelaDockItem, bool>(nameof(IsEnabled), true);

    public static readonly StyledProperty<object?> TagProperty =
        AvaloniaProperty.Register<VelaDockItem, object?>(nameof(Tag));

    public static readonly StyledProperty<string?> ToolTipProperty =
        AvaloniaProperty.Register<VelaDockItem, string?>(nameof(ToolTip));

    public string? Header { get => GetValue(HeaderProperty); set => SetValue(HeaderProperty, value); }
    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }
    public string? Badge { get => GetValue(BadgeProperty); set => SetValue(BadgeProperty, value); }
    public bool IsEnabled { get => GetValue(IsEnabledProperty); set => SetValue(IsEnabledProperty, value); }
    public object? Tag { get => GetValue(TagProperty); set => SetValue(TagProperty, value); }
    public string? ToolTip { get => GetValue(ToolTipProperty); set => SetValue(ToolTipProperty, value); }

    public event EventHandler? Click;

    internal void RaiseClick() => Click?.Invoke(this, EventArgs.Empty);
}

/// <summary>
/// iOS 26 / Arc-inspired floating pill tab dock with an optional detached morphing search orb:
/// - Rounded floating capsule supporting Solid, Frosted, and GPU LiquidGlass optical refraction materials
/// - Gliding spring active pill with BOTH click and pointer drag-to-scrub selection
/// - High-contrast corner notification badge on tab icons
/// - Detached circular Search Orb that smoothly morphs into an expanded search pill while the tab bar
///   simultaneously compacts into an active-tab return orb (and cleanly restores on Escape/close/tap)
/// </summary>
[PseudoClasses(":search-expanded", ":solid", ":frosted", ":glass-card", ":glass", ":vertical", ":no-shadow")]
public class VelaDockBar : TemplatedControl
{
    public static readonly StyledProperty<Orientation> OrientationProperty =
        AvaloniaProperty.Register<VelaDockBar, Orientation>(nameof(Orientation), Orientation.Horizontal);

    public static readonly StyledProperty<double> DockWidthProperty =
        AvaloniaProperty.Register<VelaDockBar, double>(nameof(DockWidth), 78.0);

    public static readonly StyledProperty<bool> ShowShadowProperty =
        AvaloniaProperty.Register<VelaDockBar, bool>(nameof(ShowShadow), true);

    public static readonly StyledProperty<int> SelectedIndexProperty =
        AvaloniaProperty.Register<VelaDockBar, int>(
            nameof(SelectedIndex),
            0,
            defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<VelaDockItem?> SelectedItemProperty =
        AvaloniaProperty.Register<VelaDockBar, VelaDockItem?>(
            nameof(SelectedItem),
            null,
            defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<VelaSurfaceMaterial> MaterialProperty =
        AvaloniaProperty.Register<VelaDockBar, VelaSurfaceMaterial>(nameof(Material), VelaSurfaceMaterial.Solid);

    public static readonly StyledProperty<bool> ShowSearchProperty =
        AvaloniaProperty.Register<VelaDockBar, bool>(nameof(ShowSearch), true);

    public static readonly StyledProperty<VelaDockSearchMode> SearchModeProperty =
        AvaloniaProperty.Register<VelaDockBar, VelaDockSearchMode>(nameof(SearchMode), VelaDockSearchMode.InlineExpand);

    public static readonly StyledProperty<bool> IsSearchExpandedProperty =
        AvaloniaProperty.Register<VelaDockBar, bool>(
            nameof(IsSearchExpanded),
            false,
            defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<string?> SearchTextProperty =
        AvaloniaProperty.Register<VelaDockBar, string?>(
            nameof(SearchText),
            null,
            defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<string> SearchPlaceholderProperty =
        AvaloniaProperty.Register<VelaDockBar, string>(nameof(SearchPlaceholder), "Search...");

    public static readonly StyledProperty<double> DockHeightProperty =
        AvaloniaProperty.Register<VelaDockBar, double>(nameof(DockHeight), 62.0);

    public static readonly StyledProperty<double> ExpandedSearchWidthProperty =
        AvaloniaProperty.Register<VelaDockBar, double>(nameof(ExpandedSearchWidth), 276.0);

    public Orientation Orientation { get => GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }
    public double DockWidth { get => GetValue(DockWidthProperty); set => SetValue(DockWidthProperty, value); }
    public bool ShowShadow { get => GetValue(ShowShadowProperty); set => SetValue(ShowShadowProperty, value); }
    public int SelectedIndex { get => GetValue(SelectedIndexProperty); set => SetValue(SelectedIndexProperty, value); }
    public VelaDockItem? SelectedItem { get => GetValue(SelectedItemProperty); set => SetValue(SelectedItemProperty, value); }
    public VelaSurfaceMaterial Material { get => GetValue(MaterialProperty); set => SetValue(MaterialProperty, value); }
    public bool ShowSearch { get => GetValue(ShowSearchProperty); set => SetValue(ShowSearchProperty, value); }
    public VelaDockSearchMode SearchMode { get => GetValue(SearchModeProperty); set => SetValue(SearchModeProperty, value); }
    public bool IsSearchExpanded { get => GetValue(IsSearchExpandedProperty); set => SetValue(IsSearchExpandedProperty, value); }
    public string? SearchText { get => GetValue(SearchTextProperty); set => SetValue(SearchTextProperty, value); }
    public string SearchPlaceholder { get => GetValue(SearchPlaceholderProperty); set => SetValue(SearchPlaceholderProperty, value); }
    public double DockHeight { get => GetValue(DockHeightProperty); set => SetValue(DockHeightProperty, value); }
    public double ExpandedSearchWidth { get => GetValue(ExpandedSearchWidthProperty); set => SetValue(ExpandedSearchWidthProperty, value); }

    [Content]
    public ObservableCollection<VelaDockItem> Items { get; } = new();

    public event EventHandler<int>? SelectionChanged;
    public event EventHandler? SearchClick;
    public event EventHandler<string>? SearchSubmitted;

    protected override Type StyleKeyOverride => typeof(VelaDockBar);

    private sealed class TabVisual
    {
        public required VelaDockItem Item;
        public required Border Root;
        public required PathIcon Icon;
        public required TextBlock Label;
        public required Border BadgeBorder;
        public required TextBlock BadgeText;
        public IDisposable? IconFgBinding;
        public IDisposable? LabelFgBinding;
        public string? CurrentFgKey;

        public void DisposeBindings()
        {
            IconFgBinding?.Dispose();
            IconFgBinding = null;
            LabelFgBinding?.Dispose();
            LabelFgBinding = null;
            CurrentFgKey = null;
        }
    }

    private readonly List<TabVisual> _tabs = new();
    private readonly SpringChannel _pillX = new(0.0, VelaMotionTokens.Springs.Morph);
    private readonly SpringChannel _pillW = new(78.0, VelaMotionTokens.Springs.Morph);
    private readonly SpringChannel _pillScale = new(1.0, VelaMotionTokens.Springs.Snappy);
    private readonly SpringChannel _searchProgress = new(0.0, VelaMotionTokens.Springs.Morph);
    private readonly SpringRunner _runner;

    private StackPanel? _rootStack;
    private Border? _tabsShell;
    private Border? _tabsBackdrop;
    private VelaLiquidGlassLens? _tabsLens;
    private Grid? _tabsTrack;
    private StackPanel? _tabsHost;
    private Border? _indicator;
    private TranslateTransform? _indTranslate;
    private ScaleTransform? _indScale;
    private Border? _compactReturnButton;
    private PathIcon? _compactReturnIcon;

    private Border? _searchShell;
    private Border? _searchBackdrop;
    private VelaLiquidGlassLens? _searchLens;
    private Grid? _searchCollapsedHit;
    private Grid? _searchExpandedRow;
    private TextBox? _searchBox;
    private Button? _searchCloseButton;

    private bool _hasPositioned;
    private int _positionedIndex = -1;
    private double _fullTabsShellWidth;
    private bool _scrubbing;
    private bool _syncingSelection;

    private bool IsVertical => Orientation == Orientation.Vertical;

    static VelaDockBar()
    {
        OrientationProperty.Changed.AddClassHandler<VelaDockBar>((d, _) => d.OnOrientationChanged());
        DockWidthProperty.Changed.AddClassHandler<VelaDockBar>((d, _) => d.OnOrientationChanged());
        ShowShadowProperty.Changed.AddClassHandler<VelaDockBar>((d, _) => d.PseudoClasses.Set(":no-shadow", !d.ShowShadow));
        SelectedIndexProperty.Changed.AddClassHandler<VelaDockBar>((d, _) => d.OnSelectedIndexChanged());
        SelectedItemProperty.Changed.AddClassHandler<VelaDockBar>((d, _) => d.OnSelectedItemChanged());
        MaterialProperty.Changed.AddClassHandler<VelaDockBar>((d, _) => d.ApplyMaterial());
        IsSearchExpandedProperty.Changed.AddClassHandler<VelaDockBar>((d, _) => d.OnSearchExpandedChanged());
        DockHeightProperty.Changed.AddClassHandler<VelaDockBar>((d, _) => d.ApplySearchMorph());
    }

    public VelaDockBar()
    {
        _runner = new SpringRunner(this, OnTick);
        Items.CollectionChanged += OnItemsChanged;
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        DetachTemplateHandlers();

        _rootStack = e.NameScope.Find<StackPanel>("PART_RootStack");
        _tabsShell = e.NameScope.Find<Border>("PART_TabsShell");
        _tabsBackdrop = e.NameScope.Find<Border>("PART_TabsBackdrop");
        _tabsLens = e.NameScope.Find<VelaLiquidGlassLens>("PART_TabsLens");
        _tabsTrack = e.NameScope.Find<Grid>("PART_TabsTrack");
        _tabsHost = e.NameScope.Find<StackPanel>("PART_TabsHost");
        _indicator = e.NameScope.Find<Border>("PART_Indicator");
        _compactReturnButton = e.NameScope.Find<Border>("PART_CompactReturn");
        _compactReturnIcon = e.NameScope.Find<PathIcon>("PART_CompactReturnIcon");

        _searchShell = e.NameScope.Find<Border>("PART_SearchShell");
        _searchBackdrop = e.NameScope.Find<Border>("PART_SearchBackdrop");
        _searchLens = e.NameScope.Find<VelaLiquidGlassLens>("PART_SearchLens");
        _searchCollapsedHit = e.NameScope.Find<Grid>("PART_SearchCollapsedHit");
        _searchExpandedRow = e.NameScope.Find<Grid>("PART_SearchExpandedRow");
        _searchBox = e.NameScope.Find<TextBox>("PART_SearchBox");
        _searchCloseButton = e.NameScope.Find<Button>("PART_SearchClose");

        if (_indicator is not null)
        {
            _indTranslate = new TranslateTransform();
            _indScale = new ScaleTransform(1, 1);
            _indicator.RenderTransformOrigin = RelativePoint.Center;
            _indicator.RenderTransform = new TransformGroup { Children = { _indScale, _indTranslate } };
        }

        if (_tabsTrack is not null)
        {
            _tabsTrack.LayoutUpdated += OnTrackLayoutUpdated;
            _tabsTrack.PointerPressed += OnTrackPointerPressed;
            _tabsTrack.PointerMoved += OnTrackPointerMoved;
            _tabsTrack.PointerReleased += OnTrackPointerReleased;
            _tabsTrack.PointerCaptureLost += OnTrackPointerCaptureLost;
        }

        if (_compactReturnButton is not null)
            _compactReturnButton.PointerPressed += OnCompactReturnPressed;

        if (_searchCollapsedHit is not null)
            _searchCollapsedHit.PointerPressed += OnSearchOrbPressed;

        if (_searchCloseButton is not null)
            _searchCloseButton.Click += OnSearchCloseClick;

        if (_searchBox is not null)
            _searchBox.KeyDown += OnSearchBoxKeyDown;

        _hasPositioned = false;
        _positionedIndex = -1;
        _searchProgress.Jump(IsSearchExpanded ? 1.0 : 0.0);

        PseudoClasses.Set(":vertical", IsVertical);
        PseudoClasses.Set(":no-shadow", !ShowShadow);
        ApplyMaterial();
        RebuildTabs();
        SyncSelectionFromIndex();
        SyncCompactReturnIcon();
        ApplySearchMorph();

        Dispatcher.UIThread.Post(() =>
        {
            MeasureFullTabsWidth();
            MoveIndicator(animate: false);
            ApplySearchMorph();
        }, DispatcherPriority.Loaded);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        SyncItemSubscriptions();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _runner.Stop();
        ClearItemSubscriptions();
        DetachTemplateHandlers();
    }

    private void DetachTemplateHandlers()
    {
        if (_tabsTrack is not null)
        {
            _tabsTrack.LayoutUpdated -= OnTrackLayoutUpdated;
            _tabsTrack.PointerPressed -= OnTrackPointerPressed;
            _tabsTrack.PointerMoved -= OnTrackPointerMoved;
            _tabsTrack.PointerReleased -= OnTrackPointerReleased;
            _tabsTrack.PointerCaptureLost -= OnTrackPointerCaptureLost;
        }
        if (_compactReturnButton is not null) _compactReturnButton.PointerPressed -= OnCompactReturnPressed;
        if (_searchCollapsedHit is not null) _searchCollapsedHit.PointerPressed -= OnSearchOrbPressed;
        if (_searchCloseButton is not null) _searchCloseButton.Click -= OnSearchCloseClick;
        if (_searchBox is not null) _searchBox.KeyDown -= OnSearchBoxKeyDown;
    }

    private readonly HashSet<VelaDockItem> _subscribedItems = new();

    private void SyncItemSubscriptions()
    {
        foreach (var item in _subscribedItems)
            item.PropertyChanged -= OnDockItemPropertyChanged;
        _subscribedItems.Clear();

        foreach (var item in Items)
        {
            if (_subscribedItems.Add(item))
                item.PropertyChanged += OnDockItemPropertyChanged;
        }
    }

    private void ClearItemSubscriptions()
    {
        foreach (var item in _subscribedItems)
            item.PropertyChanged -= OnDockItemPropertyChanged;
        _subscribedItems.Clear();
    }

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        SyncItemSubscriptions();
        RebuildTabs();
    }

    private void OnDockItemPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        SyncTabItemTexts();
        SyncCompactReturnIcon();
    }

    private void OnOrientationChanged()
    {
        PseudoClasses.Set(":vertical", IsVertical);
        _hasPositioned = false;
        _positionedIndex = -1;
        RebuildTabs();
        ApplySearchMorph();
    }

    private void RebuildTabs()
    {
        if (_tabsHost is null)
            return;

        if (_rootStack is not null)
            _rootStack.Orientation = Orientation;
        _tabsHost.Orientation = Orientation;
        _tabsHost.HorizontalAlignment = IsVertical ? HorizontalAlignment.Center : HorizontalAlignment.Left;
        _tabsHost.VerticalAlignment = IsVertical ? VerticalAlignment.Top : VerticalAlignment.Center;

        foreach (var oldTab in _tabs)
            oldTab.DisposeBindings();
        _tabsHost.Children.Clear();
        _tabs.Clear();

        foreach (var item in Items)
        {
            item.PropertyChanged -= OnDockItemPropertyChanged;
            item.PropertyChanged += OnDockItemPropertyChanged;

            var icon = new PathIcon
            {
                Width = 19,
                Height = 19,
                Data = item.Icon,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };

            var badgeText = new TextBlock
            {
                Text = item.Badge,
                FontSize = 10,
                FontWeight = FontWeight.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                [!TextBlock.ForegroundProperty] = VelaResourceBinding.Create("VelaBackgroundBrush"),
            };

            var badgeBorder = new Border
            {
                MinWidth = 17,
                Height = 17,
                Padding = new Thickness(4, 0),
                CornerRadius = new CornerRadius(999),
                BorderThickness = new Thickness(1.5),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, -4, -10, 0),
                IsVisible = !string.IsNullOrEmpty(item.Badge),
                [!Border.BackgroundProperty] = VelaResourceBinding.Create("VelaForegroundBrush"),
                [!Border.BorderBrushProperty] = VelaResourceBinding.Create("VelaSurfaceBrush"),
                Child = badgeText,
            };

            var iconLayer = new Grid
            {
                Width = 22,
                Height = 20,
                HorizontalAlignment = HorizontalAlignment.Center,
                Children = { icon, badgeBorder },
            };

            var label = new TextBlock
            {
                Text = item.Header,
                FontSize = 11.5,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
            };

            var stack = new StackPanel
            {
                Spacing = 3,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Children = { iconLayer, label },
            };

            var root = IsVertical
                ? new Border
                {
                    Width = Math.Max(48, DockWidth - 12),
                    MinHeight = 54,
                    Padding = new Thickness(6, 7),
                    CornerRadius = new CornerRadius(999),
                    Background = Brushes.Transparent,
                    Cursor = item.IsEnabled ? new Cursor(StandardCursorType.Hand) : Cursor.Default,
                    Opacity = item.IsEnabled ? 1.0 : 0.4,
                    Child = stack,
                }
                : new Border
                {
                    MinWidth = 78,
                    Height = Math.Max(42, DockHeight - 10),
                    Padding = new Thickness(12, 0),
                    CornerRadius = new CornerRadius(999),
                    Background = Brushes.Transparent,
                    Cursor = item.IsEnabled ? new Cursor(StandardCursorType.Hand) : Cursor.Default,
                    Opacity = item.IsEnabled ? 1.0 : 0.4,
                    Child = stack,
                };

            if (!string.IsNullOrEmpty(item.ToolTip))
                global::Avalonia.Controls.ToolTip.SetTip(root, item.ToolTip);

            _tabs.Add(new TabVisual
            {
                Item = item,
                Root = root,
                Icon = icon,
                Label = label,
                BadgeBorder = badgeBorder,
                BadgeText = badgeText,
            });
            _tabsHost.Children.Add(root);
        }

        UpdateTabVisualStates();
        SyncCompactReturnIcon();

        Dispatcher.UIThread.Post(() =>
        {
            MeasureFullTabsWidth();
            MoveIndicator(animate: false);
            ApplySearchMorph();
        }, DispatcherPriority.Loaded);
    }

    private void SyncTabItemTexts()
    {
        foreach (var t in _tabs)
        {
            t.Icon.Data = t.Item.Icon;
            t.Label.Text = t.Item.Header;
            t.BadgeText.Text = t.Item.Badge;
            t.BadgeBorder.IsVisible = !string.IsNullOrEmpty(t.Item.Badge);
            t.Root.Opacity = t.Item.IsEnabled ? 1.0 : 0.4;
        }
    }

    private void MeasureFullTabsWidth()
    {
        if (_tabsHost is null) return;
        _tabsHost.Measure(new Size(double.PositiveInfinity, DockHeight));
        var natural = _tabsHost.DesiredSize.Width + 10.0; // + 5px padding each side
        if (natural > DockHeight)
            _fullTabsShellWidth = natural;
    }

    private void OnSelectedIndexChanged()
    {
        if (_syncingSelection) return;
        SyncSelectionFromIndex();
    }

    private void OnSelectedItemChanged()
    {
        if (_syncingSelection) return;
        if (SelectedItem is null) return;
        var idx = Items.IndexOf(SelectedItem);
        if (idx >= 0 && idx != SelectedIndex)
        {
            _syncingSelection = true;
            try { SetCurrentValue(SelectedIndexProperty, idx); }
            finally { _syncingSelection = false; }
        }
        UpdateTabVisualStates();
        SyncCompactReturnIcon();
        MoveIndicator(animate: _hasPositioned);
    }

    private void SyncSelectionFromIndex()
    {
        if (Items.Count == 0) return;
        var idx = Math.Clamp(SelectedIndex, 0, Items.Count - 1);
        var item = Items[idx];
        _syncingSelection = true;
        try
        {
            if (SelectedIndex != idx) SetCurrentValue(SelectedIndexProperty, idx);
            if (!ReferenceEquals(SelectedItem, item)) SetCurrentValue(SelectedItemProperty, item);
        }
        finally
        {
            _syncingSelection = false;
        }

        UpdateTabVisualStates();
        SyncCompactReturnIcon();
        MoveIndicator(animate: _hasPositioned);
    }

    private void SelectIndex(int idx, bool fireClick)
    {
        if (idx < 0 || idx >= Items.Count || !Items[idx].IsEnabled)
            return;

        var changed = idx != SelectedIndex;
        if (changed)
        {
            SelectedIndex = idx;
            SelectionChanged?.Invoke(this, idx);
        }

        if (fireClick)
            Items[idx].RaiseClick();
    }

    private void UpdateTabVisualStates()
    {
        for (var i = 0; i < _tabs.Count; i++)
        {
            var tab = _tabs[i];
            var isSel = i == SelectedIndex;
            var fgKey = isSel ? "VelaForegroundBrush" : "VelaTextSecondaryBrush";
            if (!string.Equals(tab.CurrentFgKey, fgKey, StringComparison.Ordinal))
            {
                tab.DisposeBindings();
                tab.CurrentFgKey = fgKey;
                tab.IconFgBinding = VelaResourceBinding.Bind(tab.Icon, PathIcon.ForegroundProperty, fgKey);
                tab.LabelFgBinding = VelaResourceBinding.Bind(tab.Label, TextBlock.ForegroundProperty, fgKey);
            }
            tab.Label.FontWeight = isSel ? FontWeight.SemiBold : FontWeight.Medium;
        }
    }

    private void SyncCompactReturnIcon()
    {
        if (_compactReturnIcon is null || Items.Count == 0) return;
        var idx = Math.Clamp(SelectedIndex, 0, Items.Count - 1);
        _compactReturnIcon.Data = Items[idx].Icon;
    }

    // ─── Click + iOS 26 Pointer Drag-to-Scrub on Tabs ────────────────────

    private void OnTrackPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (IsSearchExpanded || _tabsTrack is null) return;
        if (!e.GetCurrentPoint(_tabsTrack).Properties.IsLeftButtonPressed) return;

        _scrubbing = true;
        e.Pointer.Capture(_tabsTrack);

        var idx = HitTestTab(e.GetPosition(_tabsTrack));
        if (idx >= 0)
            SelectIndex(idx, fireClick: false);

        if (!VelaMotionOptions.ReducedMotion)
        {
            _pillScale.SetTarget(1.045, VelaMotionTokens.Springs.Snappy);
            _runner.Wake();
        }
        e.Handled = true;
    }

    private void OnTrackPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_scrubbing || _tabsTrack is null) return;
        var idx = HitTestTab(e.GetPosition(_tabsTrack));
        if (idx >= 0 && idx != SelectedIndex)
            SelectIndex(idx, fireClick: false);
        e.Handled = true;
    }

    private void OnTrackPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_scrubbing || _tabsTrack is null) return;
        _scrubbing = false;
        e.Pointer.Capture(null);

        var idx = HitTestTab(e.GetPosition(_tabsTrack));
        if (idx >= 0)
            SelectIndex(idx, fireClick: true);

        _pillScale.SetTarget(1.0, VelaMotionTokens.Springs.Snappy);
        _runner.Wake();
        e.Handled = true;
    }

    private void OnTrackPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (!_scrubbing) return;
        _scrubbing = false;
        _pillScale.SetTarget(1.0, VelaMotionTokens.Springs.Snappy);
        _runner.Wake();
    }

    private int HitTestTab(Point localPoint)
    {
        if (_tabsTrack is null || _tabs.Count == 0) return -1;
        var best = -1;
        var bestDist = double.PositiveInfinity;
        var axisPos = IsVertical ? localPoint.Y : localPoint.X;

        for (var i = 0; i < _tabs.Count; i++)
        {
            var r = _tabs[i].Root;
            var p = r.TranslatePoint(new Point(0, 0), _tabsTrack);
            if (!p.HasValue) continue;
            var start = IsVertical ? p.Value.Y : p.Value.X;
            var end = start + (IsVertical ? r.Bounds.Height : r.Bounds.Width);
            if (axisPos >= start && axisPos <= end)
                return i;

            var center = (start + end) * 0.5;
            var dist = Math.Abs(axisPos - center);
            if (dist < bestDist)
            {
                bestDist = dist;
                best = i;
            }
        }
        return best;
    }

    // ─── Detached Search Orb Coordinated Morph ───────────────────────────

    private void OnSearchOrbPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        SearchClick?.Invoke(this, EventArgs.Empty);

        if (SearchMode == VelaDockSearchMode.InlineExpand)
            IsSearchExpanded = true;

        e.Handled = true;
    }

    private void OnCompactReturnPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        IsSearchExpanded = false;
        e.Handled = true;
    }

    private void OnSearchCloseClick(object? sender, RoutedEventArgs e)
    {
        SearchText = string.Empty;
        IsSearchExpanded = false;
    }

    private void OnSearchBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            IsSearchExpanded = false;
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            SearchSubmitted?.Invoke(this, SearchText ?? string.Empty);
            e.Handled = true;
        }
    }

    private void OnSearchExpandedChanged()
    {
        PseudoClasses.Set(":search-expanded", IsSearchExpanded);
        MeasureFullTabsWidth();

        var target = IsSearchExpanded ? 1.0 : 0.0;
        if (VelaMotionOptions.ReducedMotion)
        {
            _searchProgress.Jump(target);
            ApplySearchMorph();
        }
        else
        {
            _searchProgress.SetTarget(target, IsSearchExpanded ? VelaMotionTokens.Springs.Morph : VelaMotionTokens.Springs.Smooth);
            _runner.Wake();
        }

        if (IsSearchExpanded)
        {
            Dispatcher.UIThread.Post(() => _searchBox?.Focus(), DispatcherPriority.Input);
        }
    }

    // ─── Material & Layout Sync ──────────────────────────────────────────

    private void ApplyMaterial()
    {
        PseudoClasses.Set(":solid", Material == VelaSurfaceMaterial.Solid);
        PseudoClasses.Set(":frosted", Material == VelaSurfaceMaterial.Frosted);
        PseudoClasses.Set(":glass-card", Material == VelaSurfaceMaterial.GlassCard);
        PseudoClasses.Set(":glass", Material == VelaSurfaceMaterial.LiquidGlass);

        var blur = Material switch
        {
            VelaSurfaceMaterial.Frosted => new BlurEffect { Radius = 18 },
            VelaSurfaceMaterial.GlassCard => new BlurEffect { Radius = 32 },
            VelaSurfaceMaterial.LiquidGlass => new BlurEffect { Radius = 10 },
            _ => null,
        };

        if (_tabsBackdrop is not null)
        {
            _tabsBackdrop.BackdropEffectCache ??= new RetainedBackdropEffectCache();
            _tabsBackdrop.BackdropEffect = blur;
        }
        if (_searchBackdrop is not null)
        {
            _searchBackdrop.BackdropEffectCache ??= new RetainedBackdropEffectCache();
            _searchBackdrop.BackdropEffect = blur;
        }

        var enableLens = Material == VelaSurfaceMaterial.LiquidGlass;
        if (_tabsLens is not null) _tabsLens.IsVisible = enableLens;
        if (_searchLens is not null) _searchLens.IsVisible = enableLens;
        if (_indicator is not null && _hasPositioned)
            _indicator.Opacity = enableLens ? 0.62 : 1.0;
    }

    private void OnTrackLayoutUpdated(object? sender, EventArgs e)
    {
        if (_fullTabsShellWidth <= DockHeight && !IsSearchExpanded)
            MeasureFullTabsWidth();

        if (!_hasPositioned)
        {
            MoveIndicator(animate: false);
        }
        else if (_positionedIndex == SelectedIndex && !_pillX.IsAnimating && !_pillW.IsAnimating)
        {
            MoveIndicator(animate: false);
        }
    }

    private void MoveIndicator(bool animate)
    {
        if (_tabsTrack is null || _indicator is null)
            return;

        if (SelectedIndex < 0 || SelectedIndex >= _tabs.Count)
        {
            _indicator.Opacity = 0;
            return;
        }

        var tab = _tabs[SelectedIndex].Root;
        if (tab.Bounds.Width <= 0 || tab.Bounds.Height <= 0)
            return;

        var pos = tab.TranslatePoint(new Point(0, 0), _tabsTrack);
        if (!pos.HasValue)
            return;

        double targetPos;
        double targetLen;
        if (IsVertical)
        {
            targetPos = pos.Value.Y;
            targetLen = tab.Bounds.Height;
            _indicator.Width = tab.Bounds.Width;
        }
        else
        {
            targetPos = pos.Value.X;
            targetLen = tab.Bounds.Width;
            _indicator.Height = tab.Bounds.Height;
        }

        _indicator.Opacity = Material == VelaSurfaceMaterial.LiquidGlass ? 0.62 : 1.0;
        _positionedIndex = SelectedIndex;

        if (!animate || !_hasPositioned || VelaMotionOptions.ReducedMotion)
        {
            _hasPositioned = true;
            _pillX.Jump(targetPos);
            _pillW.Jump(targetLen);
            ApplyIndicator();
            return;
        }

        _pillX.SetTarget(targetPos, VelaMotionTokens.Springs.Morph);
        _pillW.SetTarget(targetLen, VelaMotionTokens.Springs.Morph);
        _runner.Wake();
    }

    private bool OnTick(double dt)
    {
        var active = _pillX.Step(dt);
        active |= _pillW.Step(dt);
        active |= _pillScale.Step(dt);
        active |= _searchProgress.Step(dt);

        ApplyIndicator();
        ApplySearchMorph();
        return active;
    }

    private void ApplyIndicator()
    {
        if (_indicator is null || _indTranslate is null || _indScale is null)
            return;

        if (IsVertical)
        {
            _indTranslate.X = 0;
            _indTranslate.Y = _pillX.Value;
            _indicator.Height = Math.Max(0, _pillW.Value);
        }
        else
        {
            _indTranslate.X = _pillX.Value;
            _indTranslate.Y = 0;
            _indicator.Width = Math.Max(0, _pillW.Value);
        }

        var s = Math.Clamp(_pillScale.Value, 0.94, 1.08);
        _indScale.ScaleX = s;
        _indScale.ScaleY = s;
    }

    private void ApplySearchMorph()
    {
        var p = Math.Clamp(_searchProgress.Value, 0.0, 1.0);
        var orbSize = Math.Max(48.0, DockHeight);

        if (_tabsShell is not null)
        {
            if (IsVertical)
            {
                _tabsShell.Width = Math.Max(56.0, DockWidth);
                _tabsShell.Height = double.NaN;
            }
            else
            {
                var fullW = _fullTabsShellWidth > orbSize ? _fullTabsShellWidth : Math.Max(orbSize, _tabs.Count * 78.0 + 10.0);
                _tabsShell.Width = fullW + (orbSize - fullW) * p;
                _tabsShell.Height = orbSize;
            }
        }

        if (_tabsTrack is not null)
        {
            var tabsOp = Math.Clamp(1.0 - p * 1.8, 0.0, 1.0);
            _tabsTrack.Opacity = tabsOp;
            _tabsTrack.IsHitTestVisible = p < 0.35;
        }

        if (_compactReturnButton is not null)
        {
            var retOp = Math.Clamp((p - 0.35) / 0.65, 0.0, 1.0);
            _compactReturnButton.Opacity = retOp;
            _compactReturnButton.IsVisible = retOp > 0.01;
            _compactReturnButton.IsHitTestVisible = p > 0.5;
        }

        if (_searchShell is not null)
        {
            var targetSearchW = Math.Max(orbSize + 80.0, ExpandedSearchWidth);
            _searchShell.Width = orbSize + (targetSearchW - orbSize) * p;
            _searchShell.Height = orbSize;
        }

        if (_searchCollapsedHit is not null)
        {
            var orbOp = Math.Clamp(1.0 - p * 2.2, 0.0, 1.0);
            _searchCollapsedHit.Opacity = orbOp;
            _searchCollapsedHit.IsVisible = orbOp > 0.01;
            _searchCollapsedHit.IsHitTestVisible = p < 0.35;
        }

        if (_searchExpandedRow is not null)
        {
            var fieldOp = Math.Clamp((p - 0.25) / 0.75, 0.0, 1.0);
            _searchExpandedRow.Opacity = fieldOp;
            _searchExpandedRow.IsVisible = fieldOp > 0.01;
            _searchExpandedRow.IsHitTestVisible = p > 0.4;
        }
    }
}
