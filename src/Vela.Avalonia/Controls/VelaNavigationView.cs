using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Metadata;
using Avalonia.Threading;
using Vela.Avalonia.Motion;

namespace Vela.Avalonia.Controls;

public enum VelaNavigationDisplayMode
{
    Left,
    Top,
}

public enum VelaNavigationPaneVariant
{
    Standard,
    Floating,
    Frosted,
}

/// <summary>
/// Quiet section heading inside <see cref="VelaNavigationView"/> (e.g. "Workspace", "Pinned").
/// Smoothly crossfades into a centered hairline divider when the sidebar collapses to icon rail mode.
/// </summary>
[PseudoClasses(":compact")]
public class VelaNavSectionHeader : TemplatedControl
{
    public static readonly StyledProperty<string?> HeaderProperty =
        AvaloniaProperty.Register<VelaNavSectionHeader, string?>(nameof(Header));

    public static readonly DirectProperty<VelaNavSectionHeader, double> LabelOpacityProperty =
        AvaloniaProperty.RegisterDirect<VelaNavSectionHeader, double>(nameof(LabelOpacity), o => o.LabelOpacity);

    public static readonly DirectProperty<VelaNavSectionHeader, double> DividerOpacityProperty =
        AvaloniaProperty.RegisterDirect<VelaNavSectionHeader, double>(nameof(DividerOpacity), o => o.DividerOpacity);

    internal VelaNavigationView? Owner { get; set; }

    static VelaNavSectionHeader()
    {
        HeaderProperty.Changed.AddClassHandler<VelaNavSectionHeader>((sec, _) => sec.Owner?.UpdateTopBreadcrumb());
    }

    public string? Header { get => GetValue(HeaderProperty); set => SetValue(HeaderProperty, value); }

    private double _labelOpacity = 1.0;
    public double LabelOpacity
    {
        get => _labelOpacity;
        internal set => SetAndRaise(LabelOpacityProperty, ref _labelOpacity, value);
    }

    private double _dividerOpacity;
    public double DividerOpacity
    {
        get => _dividerOpacity;
        internal set => SetAndRaise(DividerOpacityProperty, ref _dividerOpacity, value);
    }

    protected override Type StyleKeyOverride => typeof(VelaNavSectionHeader);

    internal void SetLayoutMode(bool isCompact, bool isTop, double openProgress)
    {
        IsVisible = !isTop;
        PseudoClasses.Set(":compact", isCompact);
        var p = Math.Clamp(openProgress, 0.0, 1.0);
        LabelOpacity = p;
        DividerOpacity = (1.0 - p) * 0.75;
    }
}

/// <summary>
/// Hierarchical navigation destination or collapsible folder group inside <see cref="VelaNavigationView"/>.
/// Supports inline <see cref="PageContent"/>, lazy <see cref="PageFactory"/>, quiet right-aligned <see cref="Badge"/>,
/// and nested <see cref="Children"/> rendered with a vertical tree guide line and spring collapse animation.
/// </summary>
[PseudoClasses(":selected", ":compact", ":top", ":has-badge", ":has-icon", ":has-children", ":expanded", ":child", ":footer-item")]
public class VelaNavItem : TemplatedControl
{
    public static readonly StyledProperty<string?> HeaderProperty =
        AvaloniaProperty.Register<VelaNavItem, string?>(nameof(Header));

    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<VelaNavItem, Geometry?>(nameof(Icon));

    public static readonly StyledProperty<object?> BadgeProperty =
        AvaloniaProperty.Register<VelaNavItem, object?>(nameof(Badge));

    public static readonly StyledProperty<object?> PageContentProperty =
        AvaloniaProperty.Register<VelaNavItem, object?>(nameof(PageContent));

    public static readonly StyledProperty<Func<object?>?> PageFactoryProperty =
        AvaloniaProperty.Register<VelaNavItem, Func<object?>?>(nameof(PageFactory));

    public static readonly StyledProperty<string?> CategoryProperty =
        AvaloniaProperty.Register<VelaNavItem, string?>(nameof(Category));

    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<VelaNavItem, bool>(nameof(IsExpanded), true, defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<bool> IsSelectedProperty =
        AvaloniaProperty.Register<VelaNavItem, bool>(nameof(IsSelected), false, defaultBindingMode: BindingMode.TwoWay);

    public static readonly DirectProperty<VelaNavItem, double> LabelOpacityProperty =
        AvaloniaProperty.RegisterDirect<VelaNavItem, double>(nameof(LabelOpacity), o => o.LabelOpacity);

    public static readonly DirectProperty<VelaNavItem, bool> HasChildrenProperty =
        AvaloniaProperty.RegisterDirect<VelaNavItem, bool>(nameof(HasChildren), o => o.HasChildren);

    public static readonly DirectProperty<VelaNavItem, bool> EffectiveExpandedProperty =
        AvaloniaProperty.RegisterDirect<VelaNavItem, bool>(nameof(EffectiveExpanded), o => o.EffectiveExpanded);

    public string? Header { get => GetValue(HeaderProperty); set => SetValue(HeaderProperty, value); }
    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }
    public object? Badge { get => GetValue(BadgeProperty); set => SetValue(BadgeProperty, value); }
    public object? PageContent { get => GetValue(PageContentProperty); set => SetValue(PageContentProperty, value); }
    public Func<object?>? PageFactory { get => GetValue(PageFactoryProperty); set => SetValue(PageFactoryProperty, value); }
    public string? Category { get => GetValue(CategoryProperty); set => SetValue(CategoryProperty, value); }
    public bool IsExpanded { get => GetValue(IsExpandedProperty); set => SetValue(IsExpandedProperty, value); }
    public bool IsSelected { get => GetValue(IsSelectedProperty); set => SetValue(IsSelectedProperty, value); }

    [Content]
    public ObservableCollection<VelaNavItem> Children { get; } = new();

    public event EventHandler? Click;

    private double _labelOpacity = 1.0;
    public double LabelOpacity
    {
        get => _labelOpacity;
        internal set => SetAndRaise(LabelOpacityProperty, ref _labelOpacity, value);
    }

    private bool _hasChildren;
    public bool HasChildren
    {
        get => _hasChildren;
        private set => SetAndRaise(HasChildrenProperty, ref _hasChildren, value);
    }

    private bool _effectiveExpanded;
    public bool EffectiveExpanded
    {
        get => _effectiveExpanded;
        private set => SetAndRaise(EffectiveExpandedProperty, ref _effectiveExpanded, value);
    }

    internal VelaNavigationView? Owner { get; set; }
    internal VelaNavItem? ParentItem { get; set; }
    internal VelaNavSectionHeader? SectionHeader { get; set; }
    internal string? SectionTitle => SectionHeader?.Header;
    internal bool IsFooterItem { get; set; }
    internal bool IsNavigable => PageContent is not null || PageFactory is not null || Children.Count == 0;

    protected override Type StyleKeyOverride => typeof(VelaNavItem);

    private readonly SpringChannel _chevronAngle = new(0.0, VelaMotionTokens.Springs.Snappy);
    private readonly SpringRunner _runner;
    private Border? _rowBorder;
    private PathIcon? _chevronIcon;
    private RotateTransform? _chevronRotate;
    private StackPanel? _childrenHost;
    private bool _isCompact;
    private bool _isTop;

    static VelaNavItem()
    {
        BadgeProperty.Changed.AddClassHandler<VelaNavItem>((item, _) =>
            item.PseudoClasses.Set(":has-badge", item.Badge is not null));
        IconProperty.Changed.AddClassHandler<VelaNavItem>((item, _) =>
            item.PseudoClasses.Set(":has-icon", item.Icon is not null));
        HeaderProperty.Changed.AddClassHandler<VelaNavItem>((item, _) =>
        {
            item.SyncCompactTooltip();
            item.Owner?.UpdateTopBreadcrumb();
        });
        IsSelectedProperty.Changed.AddClassHandler<VelaNavItem>((item, _) =>
            item.PseudoClasses.Set(":selected", item.IsSelected));
        IsExpandedProperty.Changed.AddClassHandler<VelaNavItem>((item, _) => item.OnExpandedChanged());
    }

    public VelaNavItem()
    {
        _runner = new SpringRunner(this, OnTick);
        Children.CollectionChanged += OnChildrenChanged;
    }

    internal Control? GetIndicatorTarget() => _rowBorder ?? (Control)this;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (_rowBorder is not null)
            _rowBorder.PointerPressed -= OnRowPointerPressed;

        _rowBorder = e.NameScope.Find<Border>("PART_RowBorder");
        _chevronIcon = e.NameScope.Find<PathIcon>("PART_Chevron");
        _childrenHost = e.NameScope.Find<StackPanel>("PART_ChildrenHost");

        if (_chevronIcon is not null)
        {
            var initialAngle = IsExpanded ? 0.0 : -90.0;
            _chevronAngle.Jump(initialAngle);
            _chevronRotate = new RotateTransform(initialAngle);
            _chevronIcon.RenderTransformOrigin = RelativePoint.Center;
            _chevronIcon.RenderTransform = _chevronRotate;
        }

        if (_rowBorder is not null)
            _rowBorder.PointerPressed += OnRowPointerPressed;

        SyncChildrenHost();
        PseudoClasses.Set(":has-icon", Icon is not null);
        PseudoClasses.Set(":has-badge", Badge is not null);
        UpdateExpandedState(animate: false);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _runner.Stop();
    }

    private void OnChildrenChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        SyncChildrenHost();
        Owner?.RebuildNavIndex();
    }

    internal void SyncChildrenHost()
    {
        HasChildren = Children.Count > 0;
        PseudoClasses.Set(":has-children", HasChildren);
        PseudoClasses.Set(":child", ParentItem is not null);
        PseudoClasses.Set(":footer-item", IsFooterItem);

        if (_childrenHost is null)
            return;

        _childrenHost.Children.Clear();
        foreach (var child in Children)
        {
            if (child.Parent is Panel oldP)
                oldP.Children.Remove(child);
            child.ParentItem = this;
            child.Owner = Owner;
            child.SectionHeader = SectionHeader;
            child.IsFooterItem = IsFooterItem;
            child.PseudoClasses.Set(":child", true);
            child.SetLayoutMode(_isCompact, _isTop, LabelOpacity);
            _childrenHost.Children.Add(child);
        }

        UpdateExpandedState(animate: false);
    }

    private void OnRowPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!IsEnabled) return;
        if (!e.GetCurrentPoint(_rowBorder ?? (Visual)this).Properties.IsLeftButtonPressed)
            return;

        Click?.Invoke(this, EventArgs.Empty);

        if (HasChildren && !IsNavigable)
        {
            if (Owner is not null && !Owner.IsPaneOpen && Owner.DisplayMode == VelaNavigationDisplayMode.Left)
            {
                Owner.IsPaneOpen = true;
                IsExpanded = true;
            }
            else
            {
                IsExpanded = !IsExpanded;
            }
            e.Handled = true;
            return;
        }

        if (HasChildren && IsNavigable)
        {
            IsExpanded = !IsExpanded;
        }

        Owner?.SelectNavItem(this);
        e.Handled = true;
    }

    private void OnExpandedChanged()
    {
        UpdateExpandedState(animate: true);
        Owner?.ScheduleIndicatorSync();
    }

    private void UpdateExpandedState(bool animate)
    {
        var expanded = HasChildren && IsExpanded && !_isCompact && !_isTop;
        EffectiveExpanded = expanded;
        PseudoClasses.Set(":expanded", expanded);

        var targetAngle = IsExpanded ? 0.0 : -90.0;
        if (!animate || VelaMotionOptions.ReducedMotion)
        {
            _chevronAngle.Jump(targetAngle);
            if (_chevronRotate is not null) _chevronRotate.Angle = targetAngle;
            return;
        }

        _chevronAngle.SetTarget(targetAngle, VelaMotionTokens.Springs.Snappy);
        _runner.Wake();
    }

    private bool OnTick(double dt)
    {
        var active = _chevronAngle.Step(dt);
        if (_chevronRotate is not null)
            _chevronRotate.Angle = _chevronAngle.Value;
        return active;
    }

    internal void SetLayoutMode(bool isCompact, bool isTop, double labelOpacity)
    {
        _isCompact = isCompact;
        _isTop = isTop;
        PseudoClasses.Set(":compact", isCompact);
        PseudoClasses.Set(":top", isTop);
        PseudoClasses.Set(":child", ParentItem is not null);
        PseudoClasses.Set(":footer-item", IsFooterItem);
        LabelOpacity = isTop ? 1.0 : labelOpacity;
        SyncCompactTooltip();
        UpdateExpandedState(animate: false);

        foreach (var child in Children)
            child.SetLayoutMode(isCompact, isTop, labelOpacity);
    }

    private void SyncCompactTooltip()
    {
        ToolTip.SetTip(this, _isCompact ? Header : null);
    }
}

/// <summary>
/// Arc App Rail-inspired, performance-first desktop navigation shell:
/// - Powered by <see cref="VelaPageDeck"/> for O(1) KeepAlive / LRU page caching (zero visual-tree teardown on switch)
/// - Section headers (<see cref="VelaNavSectionHeader"/>), collapsible nested tree groups (<see cref="VelaNavItem.Children"/>)
///   with 1px vertical guide lines, quiet right-aligned counters, sticky bottom <see cref="FooterItems"/>, and separated profile <see cref="Footer"/>
/// - Optional sleek TopBar (<see cref="ShowTopBar"/>) with sidebar toggle, auto-generated <see cref="VelaBreadcrumb"/>, and right-side <see cref="TopBarContent"/>
/// - Dual Left / Top display modes, Standard / Floating / Frosted pane variants, and smooth zero-reflow collapse to 60px icon rail
/// </summary>
[PseudoClasses(":left", ":top", ":open", ":compact", ":floating", ":frosted", ":topbar")]
public class VelaNavigationView : TemplatedControl
{
    public static readonly StyledProperty<VelaNavigationDisplayMode> DisplayModeProperty =
        AvaloniaProperty.Register<VelaNavigationView, VelaNavigationDisplayMode>(nameof(DisplayMode), VelaNavigationDisplayMode.Left);

    public static readonly StyledProperty<VelaNavigationPaneVariant> PaneVariantProperty =
        AvaloniaProperty.Register<VelaNavigationView, VelaNavigationPaneVariant>(nameof(PaneVariant), VelaNavigationPaneVariant.Standard);

    public static readonly StyledProperty<bool> IsPaneOpenProperty =
        AvaloniaProperty.Register<VelaNavigationView, bool>(nameof(IsPaneOpen), true, defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<double> OpenPaneLengthProperty =
        AvaloniaProperty.Register<VelaNavigationView, double>(nameof(OpenPaneLength), 244.0);

    public static readonly StyledProperty<double> CompactPaneLengthProperty =
        AvaloniaProperty.Register<VelaNavigationView, double>(nameof(CompactPaneLength), 60.0);

    public static readonly StyledProperty<bool> IsPaneToggleVisibleProperty =
        AvaloniaProperty.Register<VelaNavigationView, bool>(nameof(IsPaneToggleVisible), true);

    public static readonly StyledProperty<bool> ShowTopBarProperty =
        AvaloniaProperty.Register<VelaNavigationView, bool>(nameof(ShowTopBar), true);

    public static readonly StyledProperty<string?> BrandTitleProperty =
        AvaloniaProperty.Register<VelaNavigationView, string?>(nameof(BrandTitle), "Vela");

    public static readonly StyledProperty<object?> TopBarContentProperty =
        AvaloniaProperty.Register<VelaNavigationView, object?>(nameof(TopBarContent));

    public static readonly StyledProperty<bool> IsSearchVisibleProperty =
        AvaloniaProperty.Register<VelaNavigationView, bool>(nameof(IsSearchVisible), false);

    public static readonly StyledProperty<string?> SearchTextProperty =
        AvaloniaProperty.Register<VelaNavigationView, string?>(nameof(SearchText), null, defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<object?> HeaderProperty =
        AvaloniaProperty.Register<VelaNavigationView, object?>(nameof(Header));

    public static readonly StyledProperty<object?> CompactHeaderProperty =
        AvaloniaProperty.Register<VelaNavigationView, object?>(nameof(CompactHeader));

    public static readonly StyledProperty<object?> FooterProperty =
        AvaloniaProperty.Register<VelaNavigationView, object?>(nameof(Footer));

    public static readonly StyledProperty<object?> CompactFooterProperty =
        AvaloniaProperty.Register<VelaNavigationView, object?>(nameof(CompactFooter));

    public static readonly StyledProperty<object?> ContentProperty =
        AvaloniaProperty.Register<VelaNavigationView, object?>(nameof(Content));

    public static readonly StyledProperty<VelaNavItem?> SelectedItemProperty =
        AvaloniaProperty.Register<VelaNavigationView, VelaNavItem?>(nameof(SelectedItem), null, defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<int> SelectedIndexProperty =
        AvaloniaProperty.Register<VelaNavigationView, int>(nameof(SelectedIndex), 0, defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<VelaPageCacheMode> PageCacheModeProperty =
        AvaloniaProperty.Register<VelaNavigationView, VelaPageCacheMode>(nameof(PageCacheMode), VelaPageCacheMode.KeepAlive);

    public static readonly StyledProperty<int> MaxCachedPagesProperty =
        AvaloniaProperty.Register<VelaNavigationView, int>(nameof(MaxCachedPages), 8);

    public static readonly StyledProperty<VelaPageTransitionMode> PageTransitionModeProperty =
        AvaloniaProperty.Register<VelaNavigationView, VelaPageTransitionMode>(nameof(PageTransitionMode), VelaPageTransitionMode.Instant);

    public static readonly StyledProperty<IDataTemplate?> PageTemplateProperty =
        AvaloniaProperty.Register<VelaNavigationView, IDataTemplate?>(nameof(PageTemplate));

    public static readonly StyledProperty<Func<object, object?>?> PageFactoryProperty =
        AvaloniaProperty.Register<VelaNavigationView, Func<object, object?>?>(nameof(PageFactory));

    public VelaNavigationDisplayMode DisplayMode { get => GetValue(DisplayModeProperty); set => SetValue(DisplayModeProperty, value); }
    public VelaNavigationPaneVariant PaneVariant { get => GetValue(PaneVariantProperty); set => SetValue(PaneVariantProperty, value); }
    public bool IsPaneOpen { get => GetValue(IsPaneOpenProperty); set => SetValue(IsPaneOpenProperty, value); }
    public double OpenPaneLength { get => GetValue(OpenPaneLengthProperty); set => SetValue(OpenPaneLengthProperty, value); }
    public double CompactPaneLength { get => GetValue(CompactPaneLengthProperty); set => SetValue(CompactPaneLengthProperty, value); }
    public bool IsPaneToggleVisible { get => GetValue(IsPaneToggleVisibleProperty); set => SetValue(IsPaneToggleVisibleProperty, value); }
    public bool ShowTopBar { get => GetValue(ShowTopBarProperty); set => SetValue(ShowTopBarProperty, value); }
    public string? BrandTitle { get => GetValue(BrandTitleProperty); set => SetValue(BrandTitleProperty, value); }
    public object? TopBarContent { get => GetValue(TopBarContentProperty); set => SetValue(TopBarContentProperty, value); }
    public bool IsSearchVisible { get => GetValue(IsSearchVisibleProperty); set => SetValue(IsSearchVisibleProperty, value); }
    public string? SearchText { get => GetValue(SearchTextProperty); set => SetValue(SearchTextProperty, value); }
    public object? Header { get => GetValue(HeaderProperty); set => SetValue(HeaderProperty, value); }
    public object? CompactHeader { get => GetValue(CompactHeaderProperty); set => SetValue(CompactHeaderProperty, value); }
    public object? Footer { get => GetValue(FooterProperty); set => SetValue(FooterProperty, value); }
    public object? CompactFooter { get => GetValue(CompactFooterProperty); set => SetValue(CompactFooterProperty, value); }
    public object? Content { get => GetValue(ContentProperty); set => SetValue(ContentProperty, value); }
    public VelaNavItem? SelectedItem { get => GetValue(SelectedItemProperty); set => SetValue(SelectedItemProperty, value); }
    public int SelectedIndex { get => GetValue(SelectedIndexProperty); set => SetValue(SelectedIndexProperty, value); }
    public VelaPageCacheMode PageCacheMode { get => GetValue(PageCacheModeProperty); set => SetValue(PageCacheModeProperty, value); }
    public int MaxCachedPages { get => GetValue(MaxCachedPagesProperty); set => SetValue(MaxCachedPagesProperty, value); }
    public VelaPageTransitionMode PageTransitionMode { get => GetValue(PageTransitionModeProperty); set => SetValue(PageTransitionModeProperty, value); }
    public IDataTemplate? PageTemplate { get => GetValue(PageTemplateProperty); set => SetValue(PageTemplateProperty, value); }
    public Func<object, object?>? PageFactory { get => GetValue(PageFactoryProperty); set => SetValue(PageFactoryProperty, value); }

    [Content]
    public ObservableCollection<Control> Items { get; } = new();

    public ObservableCollection<VelaNavItem> FooterItems { get; } = new();

    public event EventHandler<VelaPageSwitchMetrics>? PageSwitched;

    protected override Type StyleKeyOverride => typeof(VelaNavigationView);

    private readonly SpringChannel _paneWidthSpring = new(244.0, VelaMotionTokens.Springs.Snappy);
    private readonly SpringChannel _openProgressSpring = new(1.0, VelaMotionTokens.Springs.Snappy);
    private readonly SpringChannel _searchFoldSpring = new(0.0, VelaMotionTokens.Springs.Snappy);
    private readonly SpringChannel _indX = new(0.0, VelaMotionTokens.Springs.Snappy);
    private readonly SpringChannel _indY = new(0.0, VelaMotionTokens.Springs.Snappy);
    private readonly SpringChannel _indW = new(0.0, VelaMotionTokens.Springs.Snappy);
    private readonly SpringChannel _indH = new(0.0, VelaMotionTokens.Springs.Snappy);
    private readonly SpringRunner _runner;

    private readonly List<VelaNavItem> _navigableItems = new();
    private bool _syncingSelection;

    // Template parts
    private Border? _paneBorder;
    private Panel? _navTrack;
    private StackPanel? _itemsHost;
    private StackPanel? _footerItemsHost;
    private Border? _activeIndicator;
    private Button? _paneToggleButton;
    private Button? _topBarToggleButton;
    private PathIcon? _toggleIcon;
    private Panel? _headerHost;
    private Panel? _compactHeaderHost;
    private Panel? _searchFold;
    private Panel? _footerHost;
    private Panel? _compactFooterHost;
    private VelaBreadcrumb? _topBreadcrumb;
    private VelaPageDeck? _pageDeck;

    private TranslateTransform? _indTranslate;
    private RotateTransform? _toggleRotate;

    private bool _initialized;
    private bool _hasPositionedIndicator;
    private VelaNavItem? _positionedItem;

    static VelaNavigationView()
    {
        DisplayModeProperty.Changed.AddClassHandler<VelaNavigationView>((v, _) => v.OnDisplayModeChanged());
        PaneVariantProperty.Changed.AddClassHandler<VelaNavigationView>((v, _) => v.OnPaneVariantChanged());
        ShowTopBarProperty.Changed.AddClassHandler<VelaNavigationView>((v, _) => v.UpdatePseudoClasses());
        BrandTitleProperty.Changed.AddClassHandler<VelaNavigationView>((v, _) => v.UpdateTopBreadcrumb());
        IsPaneOpenProperty.Changed.AddClassHandler<VelaNavigationView>((v, _) => v.OnPaneOpenChanged());
        IsSearchVisibleProperty.Changed.AddClassHandler<VelaNavigationView>((v, _) => v.OnSearchVisibleChanged());
        OpenPaneLengthProperty.Changed.AddClassHandler<VelaNavigationView>((v, _) => v.OnPaneOpenChanged());
        CompactPaneLengthProperty.Changed.AddClassHandler<VelaNavigationView>((v, _) => v.OnPaneOpenChanged());
        SearchTextProperty.Changed.AddClassHandler<VelaNavigationView>((v, _) => v.ApplySearchFilter());
        ContentProperty.Changed.AddClassHandler<VelaNavigationView>((v, _) => v.ActivateCurrentPage());
        SelectedIndexProperty.Changed.AddClassHandler<VelaNavigationView>((v, _) => v.OnSelectedIndexChanged());
        SelectedItemProperty.Changed.AddClassHandler<VelaNavigationView>((v, _) => v.OnSelectedItemChanged());
    }

    public VelaNavigationView()
    {
        _runner = new SpringRunner(this, OnMotionTick);
        Items.CollectionChanged += OnCollectionsChanged;
        FooterItems.CollectionChanged += OnCollectionsChanged;
        UpdatePseudoClasses();
    }

    public void ClearInactivePageCache() => _pageDeck?.ClearInactiveCache();

    internal void SelectNavItem(VelaNavItem item)
    {
        if (!item.IsNavigable)
            return;

        SelectedItem = item;
    }

    internal void ScheduleIndicatorSync()
    {
        Dispatcher.UIThread.Post(() => UpdateIndicator(animate: true), DispatcherPriority.Loaded);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        DetachTemplateHandlers();

        _paneBorder = e.NameScope.Find<Border>("PART_Pane");
        _navTrack = e.NameScope.Find<Panel>("PART_NavTrack");
        _itemsHost = e.NameScope.Find<StackPanel>("PART_ItemsHost");
        _footerItemsHost = e.NameScope.Find<StackPanel>("PART_FooterItemsHost");
        _activeIndicator = e.NameScope.Find<Border>("PART_ActiveIndicator");
        _paneToggleButton = e.NameScope.Find<Button>("PART_PaneToggle");
        _topBarToggleButton = e.NameScope.Find<Button>("PART_TopBarToggle");
        _toggleIcon = e.NameScope.Find<PathIcon>("PART_ToggleIcon");
        _headerHost = e.NameScope.Find<Panel>("PART_HeaderHost");
        _compactHeaderHost = e.NameScope.Find<Panel>("PART_CompactHeaderHost");
        _searchFold = e.NameScope.Find<Panel>("PART_SearchFold");
        _footerHost = e.NameScope.Find<Panel>("PART_FooterHost");
        _compactFooterHost = e.NameScope.Find<Panel>("PART_CompactFooterHost");
        _topBreadcrumb = e.NameScope.Find<VelaBreadcrumb>("PART_TopBreadcrumb");
        _pageDeck = e.NameScope.Find<VelaPageDeck>("PART_PageDeck");

        if (_activeIndicator is not null)
        {
            _indTranslate = new TranslateTransform();
            _activeIndicator.RenderTransform = _indTranslate;
            _activeIndicator.Opacity = 0;
        }

        if (_toggleIcon is not null)
        {
            _toggleRotate = new RotateTransform(IsPaneOpen ? 0 : 180);
            _toggleIcon.RenderTransformOrigin = RelativePoint.Center;
            _toggleIcon.RenderTransform = _toggleRotate;
        }

        if (_paneToggleButton is not null) _paneToggleButton.Click += OnTogglePaneClick;
        if (_topBarToggleButton is not null) _topBarToggleButton.Click += OnTogglePaneClick;
        if (_navTrack is not null) _navTrack.LayoutUpdated += OnNavTrackLayoutUpdated;
        if (_pageDeck is not null) _pageDeck.PageSwitched += OnDeckPageSwitched;

        var initialWidth = IsPaneOpen ? OpenPaneLength : CompactPaneLength;
        _paneWidthSpring.Jump(initialWidth);
        _openProgressSpring.Jump(IsPaneOpen ? 1.0 : 0.0);
        _searchFoldSpring.Jump(IsPaneOpen && IsSearchVisible ? 1.0 : 0.0);

        _initialized = true;
        _hasPositionedIndicator = false;
        _positionedItem = null;

        UpdatePseudoClasses();
        ApplyPaneBackdrop();
        RebuildVisualHosts();
        ApplyPaneVisuals();
        ApplySearchFilter();
        ActivateCurrentPage();
        UpdateTopBreadcrumb();

        Dispatcher.UIThread.Post(() =>
        {
            SyncAllNavItems();
            UpdateIndicator(animate: false);
        }, DispatcherPriority.Loaded);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _runner.Stop();
        DetachTemplateHandlers();
    }

    private void DetachTemplateHandlers()
    {
        if (_navTrack is not null) _navTrack.LayoutUpdated -= OnNavTrackLayoutUpdated;
        if (_paneToggleButton is not null) _paneToggleButton.Click -= OnTogglePaneClick;
        if (_topBarToggleButton is not null) _topBarToggleButton.Click -= OnTogglePaneClick;
        if (_pageDeck is not null) _pageDeck.PageSwitched -= OnDeckPageSwitched;
    }

    private void OnCollectionsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RebuildVisualHosts();
    }

    internal void RebuildNavIndex()
    {
        _navigableItems.Clear();
        VelaNavSectionHeader? currentSection = null;

        foreach (var entry in Items)
        {
            if (entry is VelaNavSectionHeader sec)
            {
                sec.Owner = this;
                currentSection = sec;
            }
            else if (entry is VelaNavItem nav)
            {
                WireNavItem(nav, parent: null, section: currentSection, isFooter: false);
            }
        }

        foreach (var footerNav in FooterItems)
        {
            WireNavItem(footerNav, parent: null, section: null, isFooter: true);
        }

        // Ensure valid selection
        if (SelectedItem is null || !_navigableItems.Contains(SelectedItem))
        {
            if (_navigableItems.Count > 0)
            {
                var idx = Math.Clamp(SelectedIndex, 0, _navigableItems.Count - 1);
                SetSelectionInternal(_navigableItems[idx], idx);
            }
        }
        else
        {
            var idx = _navigableItems.IndexOf(SelectedItem);
            if (idx >= 0 && idx != SelectedIndex)
                SetSelectionInternal(SelectedItem, idx);
        }
    }

    private void WireNavItem(VelaNavItem nav, VelaNavItem? parent, VelaNavSectionHeader? section, bool isFooter)
    {
        nav.Owner = this;
        nav.ParentItem = parent;
        nav.SectionHeader = section;
        nav.IsFooterItem = isFooter;

        if (nav.IsNavigable)
            _navigableItems.Add(nav);

        foreach (var child in nav.Children)
            WireNavItem(child, parent: nav, section: section, isFooter: isFooter);
    }

    private void RebuildVisualHosts()
    {
        RebuildNavIndex();

        if (_itemsHost is null)
            return;

        _itemsHost.Children.Clear();
        var isTop = DisplayMode == VelaNavigationDisplayMode.Top;
        _itemsHost.Orientation = isTop ? Orientation.Horizontal : Orientation.Vertical;
        _itemsHost.Spacing = isTop ? 2 : 2;

        if (isTop)
        {
            foreach (var nav in _navigableItems)
            {
                if (nav.Parent is Panel oldP)
                    oldP.Children.Remove(nav);
                _itemsHost.Children.Add(nav);
            }
        }
        else
        {
            foreach (var entry in Items)
            {
                if (entry.Parent is Panel oldP)
                    oldP.Children.Remove(entry);
                _itemsHost.Children.Add(entry);
                if (entry is VelaNavItem nav)
                    nav.SyncChildrenHost();
            }
        }

        if (_footerItemsHost is not null)
        {
            _footerItemsHost.Children.Clear();
            if (!isTop)
            {
                foreach (var fItem in FooterItems)
                {
                    if (fItem.Parent is Panel oldP)
                        oldP.Children.Remove(fItem);
                    _footerItemsHost.Children.Add(fItem);
                }
            }
            _footerItemsHost.IsVisible = !isTop && FooterItems.Count > 0;
        }

        SyncAllNavItems();
        UpdateSelectionFlags();
    }

    private void OnDeckPageSwitched(object? sender, VelaPageSwitchMetrics metrics)
        => PageSwitched?.Invoke(this, metrics);

    private void OnTogglePaneClick(object? sender, RoutedEventArgs e)
        => IsPaneOpen = !IsPaneOpen;

    private void OnDisplayModeChanged()
    {
        UpdatePseudoClasses();
        _hasPositionedIndicator = false;
        _positionedItem = null;

        if (_initialized)
        {
            RebuildVisualHosts();
            ApplyPaneVisuals();
            SyncAllNavItems();
            Dispatcher.UIThread.Post(() =>
            {
                SyncAllNavItems();
                UpdateIndicator(animate: false);
            }, DispatcherPriority.Loaded);
        }
    }

    private void OnPaneVariantChanged()
    {
        UpdatePseudoClasses();
        ApplyPaneBackdrop();
    }

    private void ApplyPaneBackdrop()
    {
        if (_paneBorder is null) return;
        _paneBorder.BackdropEffect = PaneVariant == VelaNavigationPaneVariant.Frosted
            ? new BlurEffect { Radius = 18 }
            : null;
    }

    private void OnPaneOpenChanged()
    {
        UpdatePseudoClasses();
        if (!_initialized || DisplayMode != VelaNavigationDisplayMode.Left)
            return;

        var targetW = IsPaneOpen ? OpenPaneLength : CompactPaneLength;
        var targetProgress = IsPaneOpen ? 1.0 : 0.0;
        var targetSearch = IsPaneOpen && IsSearchVisible ? 1.0 : 0.0;

        if (VelaMotionOptions.ReducedMotion)
        {
            _paneWidthSpring.Jump(targetW);
            _openProgressSpring.Jump(targetProgress);
            _searchFoldSpring.Jump(targetSearch);
            ApplyPaneVisuals();
            SyncAllNavItems();
            UpdateIndicator(animate: false);
            return;
        }

        _paneWidthSpring.SetTarget(targetW, VelaMotionTokens.Springs.Snappy);
        _openProgressSpring.SetTarget(targetProgress, VelaMotionTokens.Springs.Snappy);
        _searchFoldSpring.SetTarget(targetSearch, VelaMotionTokens.Springs.Snappy);
        SyncAllNavItems();
        _runner.Wake();
    }

    private void OnSearchVisibleChanged()
    {
        if (!_initialized) return;
        var target = DisplayMode == VelaNavigationDisplayMode.Left && IsPaneOpen && IsSearchVisible ? 1.0 : 0.0;
        if (VelaMotionOptions.ReducedMotion)
        {
            _searchFoldSpring.Jump(target);
            ApplyPaneVisuals();
            return;
        }

        _searchFoldSpring.SetTarget(target, VelaMotionTokens.Springs.Snappy);
        _runner.Wake();
    }

    private void OnSelectedIndexChanged()
    {
        if (_syncingSelection) return;
        if (SelectedIndex >= 0 && SelectedIndex < _navigableItems.Count)
            SetSelectionInternal(_navigableItems[SelectedIndex], SelectedIndex);
    }

    private void OnSelectedItemChanged()
    {
        if (_syncingSelection) return;
        var item = SelectedItem;
        if (item is not null)
        {
            var idx = _navigableItems.IndexOf(item);
            SetSelectionInternal(item, idx >= 0 ? idx : SelectedIndex);
        }
    }

    private void SetSelectionInternal(VelaNavItem item, int index)
    {
        _syncingSelection = true;
        try
        {
            if (!ReferenceEquals(SelectedItem, item))
                SetCurrentValue(SelectedItemProperty, item);
            if (index >= 0 && SelectedIndex != index)
                SetCurrentValue(SelectedIndexProperty, index);
        }
        finally
        {
            _syncingSelection = false;
        }

        // Auto-expand parent folder if a nested child is selected programmatically (e.g. via Command Palette)
        if (item.ParentItem is not null && !item.ParentItem.IsExpanded)
            item.ParentItem.IsExpanded = true;

        UpdateSelectionFlags();
        ActivateCurrentPage();
        UpdateTopBreadcrumb();

        if (!UpdateIndicator(animate: true))
        {
            Dispatcher.UIThread.Post(() => UpdateIndicator(animate: _hasPositionedIndicator), DispatcherPriority.Loaded);
        }
    }

    private void UpdateSelectionFlags()
    {
        var active = SelectedItem;
        foreach (var nav in _navigableItems)
            nav.IsSelected = ReferenceEquals(nav, active);
    }

    internal void UpdateTopBreadcrumb()
    {
        if (_topBreadcrumb is null)
            return;

        _topBreadcrumb.Items.Clear();

        if (!string.IsNullOrWhiteSpace(BrandTitle))
        {
            var rootCrumb = new VelaBreadcrumbItem { Label = BrandTitle };
            rootCrumb.Click += (_, _) =>
            {
                if (_navigableItems.Count > 0)
                    SelectedIndex = 0;
            };
            _topBreadcrumb.Items.Add(rootCrumb);
        }

        var active = SelectedItem;
        if (active is null)
            return;

        if (!string.IsNullOrWhiteSpace(active.SectionTitle))
        {
            _topBreadcrumb.Items.Add(new VelaBreadcrumbItem { Label = active.SectionTitle });
        }

        if (active.ParentItem is not null && !string.IsNullOrWhiteSpace(active.ParentItem.Header))
        {
            var parentRef = active.ParentItem;
            var parentCrumb = new VelaBreadcrumbItem { Label = parentRef.Header };
            parentCrumb.Click += (_, _) =>
            {
                if (parentRef.IsNavigable) SelectedItem = parentRef;
                else parentRef.IsExpanded = true;
            };
            _topBreadcrumb.Items.Add(parentCrumb);
        }

        if (!string.IsNullOrWhiteSpace(active.Header))
        {
            _topBreadcrumb.Items.Add(new VelaBreadcrumbItem { Label = active.Header });
        }
    }

    private void ActivateCurrentPage()
    {
        if (_pageDeck is null)
            return;

        var explicitContent = Content;
        if (explicitContent is not null)
        {
            var key = (object?)SelectedItem ?? explicitContent;
            _pageDeck.Activate(key, () => explicitContent);
            return;
        }

        if (SelectedItem is VelaNavItem navItem)
        {
            _pageDeck.Activate(navItem, () =>
            {
                if (navItem.PageFactory is not null)
                    return navItem.PageFactory();
                if (navItem.PageContent is not null)
                    return navItem.PageContent;
                return PageFactory?.Invoke(navItem);
            });
            return;
        }

        _pageDeck.Activate(null, static () => null);
    }

    private void ApplySearchFilter()
    {
        var query = SearchText?.Trim();
        var hasQuery = !string.IsNullOrEmpty(query);
        var isTop = DisplayMode == VelaNavigationDisplayMode.Top;

        VelaNavSectionHeader? pendingHeader = null;
        var visibleUnderHeader = false;

        foreach (var entry in Items)
        {
            if (entry is VelaNavSectionHeader sec)
            {
                if (pendingHeader is not null)
                    pendingHeader.IsVisible = !isTop && (!hasQuery || visibleUnderHeader);
                pendingHeader = sec;
                visibleUnderHeader = false;
                continue;
            }

            if (entry is VelaNavItem item)
            {
                var matchesSelf = !hasQuery || MatchesQuery(item, query!);
                var anyChildMatch = false;

                foreach (var child in item.Children)
                {
                    var cm = !hasQuery || MatchesQuery(child, query!) || matchesSelf;
                    child.IsVisible = cm;
                    if (cm) anyChildMatch = true;
                }

                var visible = matchesSelf || anyChildMatch;
                item.IsVisible = visible;
                if (hasQuery && anyChildMatch && !item.IsExpanded)
                    item.IsExpanded = true;

                if (visible) visibleUnderHeader = true;
            }
        }

        if (pendingHeader is not null)
            pendingHeader.IsVisible = !isTop && (!hasQuery || visibleUnderHeader);

        Dispatcher.UIThread.Post(() => UpdateIndicator(animate: true), DispatcherPriority.Loaded);
    }

    private static bool MatchesQuery(VelaNavItem item, string query) =>
        item.Header?.Contains(query, StringComparison.OrdinalIgnoreCase) == true ||
        item.Category?.Contains(query, StringComparison.OrdinalIgnoreCase) == true;

    private void OnNavTrackLayoutUpdated(object? sender, EventArgs e)
    {
        if (!_hasPositionedIndicator)
        {
            UpdateIndicator(animate: false);
            return;
        }

        if (ReferenceEquals(SelectedItem, _positionedItem) &&
            !_indX.IsAnimating && !_indY.IsAnimating && !_indW.IsAnimating && !_indH.IsAnimating)
        {
            UpdateIndicator(animate: false);
        }
    }

    private bool UpdateIndicator(bool animate)
    {
        if (_navTrack is null || _activeIndicator is null)
            return false;

        var active = SelectedItem;
        if (active is null || (active.IsFooterItem && DisplayMode == VelaNavigationDisplayMode.Left))
        {
            _activeIndicator.Opacity = 0;
            _positionedItem = active;
            return true;
        }

        // If selected item is a hidden child under a collapsed parent or compact rail, highlight its parent row cleanly!
        var visualItem = active;
        if (DisplayMode == VelaNavigationDisplayMode.Left && active.ParentItem is not null &&
            (!IsPaneOpen || !active.ParentItem.IsExpanded))
        {
            visualItem = active.ParentItem;
        }

        var targetControl = visualItem.GetIndicatorTarget();
        if (targetControl is null || !visualItem.IsVisible || targetControl.Bounds.Width <= 0 || targetControl.Bounds.Height <= 0)
        {
            _activeIndicator.Opacity = 0;
            return false;
        }

        var origin = targetControl.TranslatePoint(new Point(0, 0), _navTrack);
        if (!origin.HasValue)
            return false;

        var isTop = DisplayMode == VelaNavigationDisplayMode.Top;
        var targetX = origin.Value.X;
        var targetY = origin.Value.Y;
        var targetW = isTop
            ? targetControl.Bounds.Width
            : Math.Max(36.0, Math.Min(targetControl.Bounds.Width, _paneWidthSpring.Value - 16.0 - targetX));
        var targetH = targetControl.Bounds.Height;

        _activeIndicator.Opacity = 1.0;
        _positionedItem = active;

        var shouldAnimate = _hasPositionedIndicator && animate && !VelaMotionOptions.ReducedMotion;
        _hasPositionedIndicator = true;

        if (!shouldAnimate)
        {
            _indX.Jump(targetX);
            _indY.Jump(targetY);
            _indW.Jump(targetW);
            _indH.Jump(targetH);
            ApplyIndicatorVisuals();
            return true;
        }

        _indX.SetTarget(targetX, VelaMotionTokens.Springs.Snappy);
        _indY.SetTarget(targetY, VelaMotionTokens.Springs.Snappy);
        _indW.SetTarget(targetW, VelaMotionTokens.Springs.Snappy);
        _indH.SetTarget(targetH, VelaMotionTokens.Springs.Snappy);
        _runner.Wake();
        return true;
    }

    private bool OnMotionTick(double dt)
    {
        var active = false;

        var paneMoved = _paneWidthSpring.Step(dt) | _openProgressSpring.Step(dt) | _searchFoldSpring.Step(dt);
        if (paneMoved)
        {
            active = true;
            ApplyPaneVisuals();
            SyncAllNavItems();
            if (DisplayMode == VelaNavigationDisplayMode.Left && _hasPositionedIndicator)
            {
                var targetW = Math.Max(36.0, _paneWidthSpring.Value - 16.0 - _indX.Value);
                _indW.Jump(targetW);
            }
        }
        else if (_paneWidthSpring.IsAnimating || _openProgressSpring.IsAnimating || _searchFoldSpring.IsAnimating)
        {
            active = true;
        }

        active |= _indX.Step(dt);
        active |= _indY.Step(dt);
        active |= _indW.Step(dt);
        active |= _indH.Step(dt);
        ApplyIndicatorVisuals();

        return active;
    }

    private void ApplyPaneVisuals()
    {
        if (!_initialized) return;

        var isTop = DisplayMode == VelaNavigationDisplayMode.Top;
        var progress = Math.Clamp(_openProgressSpring.Value, 0.0, 1.0);

        if (_paneBorder is not null)
            _paneBorder.Width = isTop ? double.NaN : Math.Max(CompactPaneLength, _paneWidthSpring.Value);

        if (_toggleRotate is not null)
            _toggleRotate.Angle = (1.0 - progress) * 180.0;

        if (_headerHost is not null)
        {
            _headerHost.Opacity = isTop ? 1.0 : progress;
            _headerHost.IsHitTestVisible = isTop || progress > 0.4;
        }

        if (_compactHeaderHost is not null)
        {
            var compactOp = !isTop && CompactHeader is not null ? 1.0 - progress : 0.0;
            _compactHeaderHost.Opacity = compactOp;
            _compactHeaderHost.IsHitTestVisible = compactOp > 0.4;
        }

        if (_searchFold is not null)
        {
            var searchProgress = Math.Clamp(_searchFoldSpring.Value, 0.0, 1.0);
            _searchFold.Height = isTop ? 0.0 : 40.0 * searchProgress;
            _searchFold.Opacity = isTop ? 0.0 : searchProgress;
            _searchFold.IsHitTestVisible = !isTop && searchProgress > 0.5;
        }

        if (_footerHost is not null)
        {
            _footerHost.Opacity = isTop ? 1.0 : progress;
            _footerHost.IsHitTestVisible = isTop || progress > 0.4;
        }

        if (_compactFooterHost is not null)
        {
            var compactFooterOp = !isTop && CompactFooter is not null ? 1.0 - progress : 0.0;
            _compactFooterHost.Opacity = compactFooterOp;
            _compactFooterHost.IsHitTestVisible = compactFooterOp > 0.4;
        }
    }

    private void ApplyIndicatorVisuals()
    {
        if (_activeIndicator is null || _indTranslate is null)
            return;

        _indTranslate.X = _indX.Value;
        _indTranslate.Y = _indY.Value;
        _activeIndicator.Width = Math.Max(0, _indW.Value);
        _activeIndicator.Height = Math.Max(0, _indH.Value);
    }

    private void SyncAllNavItems()
    {
        var isTop = DisplayMode == VelaNavigationDisplayMode.Top;
        var isCompact = !isTop && !IsPaneOpen;
        var progress = Math.Clamp(_openProgressSpring.Value, 0.0, 1.0);

        foreach (var entry in Items)
        {
            if (entry is VelaNavSectionHeader sec)
                sec.SetLayoutMode(isCompact, isTop, progress);
            else if (entry is VelaNavItem nav)
                nav.SetLayoutMode(isCompact, isTop, progress);
        }

        foreach (var footerItem in FooterItems)
            footerItem.SetLayoutMode(isCompact, isTop, progress);
    }

    private void UpdatePseudoClasses()
    {
        var isTop = DisplayMode == VelaNavigationDisplayMode.Top;
        PseudoClasses.Set(":left", !isTop);
        PseudoClasses.Set(":top", isTop);
        PseudoClasses.Set(":open", !isTop && IsPaneOpen);
        PseudoClasses.Set(":compact", !isTop && !IsPaneOpen);
        PseudoClasses.Set(":floating", !isTop && PaneVariant == VelaNavigationPaneVariant.Floating);
        PseudoClasses.Set(":frosted", !isTop && PaneVariant == VelaNavigationPaneVariant.Frosted);
        PseudoClasses.Set(":topbar", !isTop && ShowTopBar);
    }
}
