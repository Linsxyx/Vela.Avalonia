using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Vela.Avalonia.Motion;

namespace Vela.Avalonia.Controls;

public enum VelaNavigationDisplayMode
{
    Left,
    Top,
}

/// <summary>
/// A navigation destination inside <see cref="VelaNavigationView"/>.
/// Supports both inline <see cref="PageContent"/> and lazy <see cref="PageFactory"/> for fast startup.
/// </summary>
[PseudoClasses(":compact", ":top", ":has-badge")]
public class VelaNavItem : ListBoxItem
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

    public static readonly DirectProperty<VelaNavItem, double> LabelOpacityProperty =
        AvaloniaProperty.RegisterDirect<VelaNavItem, double>(nameof(LabelOpacity), o => o.LabelOpacity);

    public string? Header { get => GetValue(HeaderProperty); set => SetValue(HeaderProperty, value); }
    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }
    public object? Badge { get => GetValue(BadgeProperty); set => SetValue(BadgeProperty, value); }
    public object? PageContent { get => GetValue(PageContentProperty); set => SetValue(PageContentProperty, value); }
    public Func<object?>? PageFactory { get => GetValue(PageFactoryProperty); set => SetValue(PageFactoryProperty, value); }
    public string? Category { get => GetValue(CategoryProperty); set => SetValue(CategoryProperty, value); }

    private double _labelOpacity = 1.0;
    public double LabelOpacity
    {
        get => _labelOpacity;
        internal set => SetAndRaise(LabelOpacityProperty, ref _labelOpacity, value);
    }

    protected override Type StyleKeyOverride => typeof(VelaNavItem);

    static VelaNavItem()
    {
        BadgeProperty.Changed.AddClassHandler<VelaNavItem>((item, _) =>
            item.PseudoClasses.Set(":has-badge", item.Badge is not null));
        HeaderProperty.Changed.AddClassHandler<VelaNavItem>((item, _) => item.SyncCompactTooltip());
    }

    internal void SetLayoutMode(bool isCompact, bool isTop, double labelOpacity)
    {
        PseudoClasses.Set(":compact", isCompact);
        PseudoClasses.Set(":top", isTop);
        LabelOpacity = isTop ? 1.0 : labelOpacity;
        SyncCompactTooltip();
    }

    private void SyncCompactTooltip()
    {
        var isCompact = PseudoClasses.Contains(":compact");
        ToolTip.SetTip(this, isCompact ? Header : null);
    }
}

/// <summary>
/// Performance-first, restrained desktop navigation shell:
/// - Powered by <see cref="VelaPageDeck"/> for O(1) KeepAlive / LRU page caching (zero visual-tree teardown on switch)
/// - Lazy page instantiation via <see cref="VelaNavItem.PageFactory"/> or <see cref="PageFactory"/>
/// - Optional zero-UI-thread GPU Compositor fade (default: Instant 0ms switching)
/// - Clean neutral monochrome aesthetic with dual Left / Top display modes and zero-reflow collapse
/// </summary>
[PseudoClasses(":left", ":top", ":open", ":compact")]
public class VelaNavigationView : ListBox
{
    public static readonly StyledProperty<VelaNavigationDisplayMode> DisplayModeProperty =
        AvaloniaProperty.Register<VelaNavigationView, VelaNavigationDisplayMode>(nameof(DisplayMode), VelaNavigationDisplayMode.Left);

    public static readonly StyledProperty<bool> IsPaneOpenProperty =
        AvaloniaProperty.Register<VelaNavigationView, bool>(nameof(IsPaneOpen), true, defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<double> OpenPaneLengthProperty =
        AvaloniaProperty.Register<VelaNavigationView, double>(nameof(OpenPaneLength), 228.0);

    public static readonly StyledProperty<double> CompactPaneLengthProperty =
        AvaloniaProperty.Register<VelaNavigationView, double>(nameof(CompactPaneLength), 60.0);

    public static readonly StyledProperty<bool> IsPaneToggleVisibleProperty =
        AvaloniaProperty.Register<VelaNavigationView, bool>(nameof(IsPaneToggleVisible), true);

    public static readonly StyledProperty<bool> IsSearchVisibleProperty =
        AvaloniaProperty.Register<VelaNavigationView, bool>(nameof(IsSearchVisible), false);

    public static readonly StyledProperty<string?> SearchTextProperty =
        AvaloniaProperty.Register<VelaNavigationView, string?>(nameof(SearchText), null, defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<object?> HeaderProperty =
        AvaloniaProperty.Register<VelaNavigationView, object?>(nameof(Header));

    public static readonly StyledProperty<object?> FooterProperty =
        AvaloniaProperty.Register<VelaNavigationView, object?>(nameof(Footer));

    public static readonly StyledProperty<object?> ContentProperty =
        AvaloniaProperty.Register<VelaNavigationView, object?>(nameof(Content));

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
    public bool IsPaneOpen { get => GetValue(IsPaneOpenProperty); set => SetValue(IsPaneOpenProperty, value); }
    public double OpenPaneLength { get => GetValue(OpenPaneLengthProperty); set => SetValue(OpenPaneLengthProperty, value); }
    public double CompactPaneLength { get => GetValue(CompactPaneLengthProperty); set => SetValue(CompactPaneLengthProperty, value); }
    public bool IsPaneToggleVisible { get => GetValue(IsPaneToggleVisibleProperty); set => SetValue(IsPaneToggleVisibleProperty, value); }
    public bool IsSearchVisible { get => GetValue(IsSearchVisibleProperty); set => SetValue(IsSearchVisibleProperty, value); }
    public string? SearchText { get => GetValue(SearchTextProperty); set => SetValue(SearchTextProperty, value); }
    public object? Header { get => GetValue(HeaderProperty); set => SetValue(HeaderProperty, value); }
    public object? Footer { get => GetValue(FooterProperty); set => SetValue(FooterProperty, value); }
    public object? Content { get => GetValue(ContentProperty); set => SetValue(ContentProperty, value); }
    public VelaPageCacheMode PageCacheMode { get => GetValue(PageCacheModeProperty); set => SetValue(PageCacheModeProperty, value); }
    public int MaxCachedPages { get => GetValue(MaxCachedPagesProperty); set => SetValue(MaxCachedPagesProperty, value); }
    public VelaPageTransitionMode PageTransitionMode { get => GetValue(PageTransitionModeProperty); set => SetValue(PageTransitionModeProperty, value); }
    public IDataTemplate? PageTemplate { get => GetValue(PageTemplateProperty); set => SetValue(PageTemplateProperty, value); }
    public Func<object, object?>? PageFactory { get => GetValue(PageFactoryProperty); set => SetValue(PageFactoryProperty, value); }

    public event EventHandler<VelaPageSwitchMetrics>? PageSwitched;

    protected override Type StyleKeyOverride => typeof(VelaNavigationView);

    // Lightweight spring channels (only for sidebar collapse & active pill; page switching itself runs with 0 UI-thread animation!)
    private readonly SpringChannel _paneWidthSpring = new(228.0, VelaMotionTokens.Springs.Snappy);
    private readonly SpringChannel _openProgressSpring = new(1.0, VelaMotionTokens.Springs.Snappy);
    private readonly SpringChannel _searchFoldSpring = new(0.0, VelaMotionTokens.Springs.Snappy);
    private readonly SpringChannel _indX = new(0.0, VelaMotionTokens.Springs.Snappy);
    private readonly SpringChannel _indY = new(0.0, VelaMotionTokens.Springs.Snappy);
    private readonly SpringChannel _indW = new(0.0, VelaMotionTokens.Springs.Snappy);
    private readonly SpringChannel _indH = new(0.0, VelaMotionTokens.Springs.Snappy);
    private readonly SpringRunner _runner;

    // Template parts
    private Border? _paneBorder;
    private Panel? _navTrack;
    private Border? _activeIndicator;
    private Button? _toggleButton;
    private PathIcon? _toggleIcon;
    private Panel? _headerHost;
    private Panel? _searchFold;
    private Panel? _footerHost;
    private VelaPageDeck? _pageDeck;

    private TranslateTransform? _indTranslate;
    private RotateTransform? _toggleRotate;

    private bool _initialized;
    private bool _hasPositionedIndicator;
    private int _positionedIndex = -1;

    static VelaNavigationView()
    {
        SelectionModeProperty.OverrideDefaultValue<VelaNavigationView>(SelectionMode.Single | SelectionMode.AlwaysSelected);
        DisplayModeProperty.Changed.AddClassHandler<VelaNavigationView>((v, _) => v.OnDisplayModeChanged());
        IsPaneOpenProperty.Changed.AddClassHandler<VelaNavigationView>((v, _) => v.OnPaneOpenChanged());
        IsSearchVisibleProperty.Changed.AddClassHandler<VelaNavigationView>((v, _) => v.OnSearchVisibleChanged());
        OpenPaneLengthProperty.Changed.AddClassHandler<VelaNavigationView>((v, _) => v.OnPaneOpenChanged());
        CompactPaneLengthProperty.Changed.AddClassHandler<VelaNavigationView>((v, _) => v.OnPaneOpenChanged());
        SearchTextProperty.Changed.AddClassHandler<VelaNavigationView>((v, _) => v.ApplySearchFilter());
        ContentProperty.Changed.AddClassHandler<VelaNavigationView>((v, _) => v.ActivateCurrentPage());
        SelectedIndexProperty.Changed.AddClassHandler<VelaNavigationView>((v, _) => v.OnSelectionChangedInternal());
        SelectedItemProperty.Changed.AddClassHandler<VelaNavigationView>((v, _) => v.ActivateCurrentPage());
    }

    public VelaNavigationView()
    {
        _runner = new SpringRunner(this, OnMotionTick);
        UpdatePseudoClasses();
    }

    public void ClearInactivePageCache() => _pageDeck?.ClearInactiveCache();

    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey)
        => new VelaNavItem();

    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey)
        => NeedsContainer<VelaNavItem>(item, out recycleKey);

    protected override void PrepareContainerForItemOverride(Control container, object? item, int index)
    {
        base.PrepareContainerForItemOverride(container, item, index);
        if (container is VelaNavItem navItem)
        {
            var isTop = DisplayMode == VelaNavigationDisplayMode.Top;
            var isCompact = !isTop && !IsPaneOpen;
            navItem.SetLayoutMode(isCompact, isTop, _openProgressSpring.Value);
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (_navTrack is not null)
            _navTrack.LayoutUpdated -= OnNavTrackLayoutUpdated;
        if (_toggleButton is not null)
            _toggleButton.Click -= OnTogglePaneClick;
        if (_pageDeck is not null)
            _pageDeck.PageSwitched -= OnDeckPageSwitched;

        _paneBorder = e.NameScope.Find<Border>("PART_Pane");
        _navTrack = e.NameScope.Find<Panel>("PART_NavTrack");
        _activeIndicator = e.NameScope.Find<Border>("PART_ActiveIndicator");
        _toggleButton = e.NameScope.Find<Button>("PART_PaneToggle");
        _toggleIcon = e.NameScope.Find<PathIcon>("PART_ToggleIcon");
        _headerHost = e.NameScope.Find<Panel>("PART_HeaderHost");
        _searchFold = e.NameScope.Find<Panel>("PART_SearchFold");
        _footerHost = e.NameScope.Find<Panel>("PART_FooterHost");
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

        if (_toggleButton is not null)
            _toggleButton.Click += OnTogglePaneClick;

        if (_navTrack is not null)
            _navTrack.LayoutUpdated += OnNavTrackLayoutUpdated;

        if (_pageDeck is not null)
            _pageDeck.PageSwitched += OnDeckPageSwitched;

        var initialWidth = IsPaneOpen ? OpenPaneLength : CompactPaneLength;
        _paneWidthSpring.Jump(initialWidth);
        _openProgressSpring.Jump(IsPaneOpen ? 1.0 : 0.0);
        _searchFoldSpring.Jump(IsPaneOpen && IsSearchVisible ? 1.0 : 0.0);

        _initialized = true;
        _hasPositionedIndicator = false;
        _positionedIndex = -1;

        UpdatePseudoClasses();
        ApplyPaneVisuals();
        ActivateCurrentPage();
        ApplySearchFilter();

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
        if (_navTrack is not null)
            _navTrack.LayoutUpdated -= OnNavTrackLayoutUpdated;
        if (_toggleButton is not null)
            _toggleButton.Click -= OnTogglePaneClick;
        if (_pageDeck is not null)
            _pageDeck.PageSwitched -= OnDeckPageSwitched;
    }

    private void OnDeckPageSwitched(object? sender, VelaPageSwitchMetrics metrics)
        => PageSwitched?.Invoke(this, metrics);

    private void OnTogglePaneClick(object? sender, RoutedEventArgs e)
        => IsPaneOpen = !IsPaneOpen;

    private void OnDisplayModeChanged()
    {
        UpdatePseudoClasses();
        _hasPositionedIndicator = false;
        _positionedIndex = -1;

        if (_initialized)
        {
            ApplyPaneVisuals();
            SyncAllNavItems();
            Dispatcher.UIThread.Post(() =>
            {
                SyncAllNavItems();
                UpdateIndicator(animate: false);
            }, DispatcherPriority.Loaded);
        }
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

    private void OnSelectionChangedInternal()
    {
        ActivateCurrentPage();

        if (!UpdateIndicator(animate: true))
        {
            Dispatcher.UIThread.Post(() => UpdateIndicator(animate: _hasPositionedIndicator), DispatcherPriority.Loaded);
        }
    }

    private void ActivateCurrentPage()
    {
        if (_pageDeck is null)
            return;

        // 1. Explicit Content binding (MVVM direct View/ViewModel)
        var explicitContent = Content;
        if (explicitContent is not null)
        {
            var key = SelectedItem ?? explicitContent;
            _pageDeck.Activate(key, () => explicitContent);
            return;
        }

        // 2. Selected VelaNavItem with lazy PageFactory or inline PageContent
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

        // 3. Bound MVVM item with global PageFactory or DataTemplate
        if (SelectedItem is not null)
        {
            var item = SelectedItem;
            _pageDeck.Activate(item, () => PageFactory is not null ? PageFactory(item) : item);
            return;
        }

        _pageDeck.Activate(null, static () => null);
    }

    private void ApplySearchFilter()
    {
        var query = SearchText?.Trim();
        var hasQuery = !string.IsNullOrEmpty(query);

        for (var i = 0; i < ItemCount; i++)
        {
            if (ContainerFromIndex(i) is VelaNavItem item)
            {
                if (!hasQuery)
                {
                    item.IsVisible = true;
                }
                else
                {
                    var matchHeader = item.Header?.Contains(query!, StringComparison.OrdinalIgnoreCase) == true;
                    var matchCat = item.Category?.Contains(query!, StringComparison.OrdinalIgnoreCase) == true;
                    item.IsVisible = matchHeader || matchCat;
                }
            }
        }

        Dispatcher.UIThread.Post(() => UpdateIndicator(animate: true), DispatcherPriority.Loaded);
    }

    private void OnNavTrackLayoutUpdated(object? sender, EventArgs e)
    {
        if (!_hasPositionedIndicator)
        {
            UpdateIndicator(animate: false);
            return;
        }

        if (SelectedIndex == _positionedIndex && !_indX.IsAnimating && !_indY.IsAnimating && !_indW.IsAnimating && !_indH.IsAnimating)
        {
            UpdateIndicator(animate: false);
        }
    }

    private bool UpdateIndicator(bool animate)
    {
        if (_navTrack is null || _activeIndicator is null)
            return false;

        if (SelectedIndex < 0 || SelectedIndex >= ItemCount)
        {
            _activeIndicator.Opacity = 0;
            return true;
        }

        var container = ContainerFromIndex(SelectedIndex);
        if (container is null || !container.IsVisible || container.Bounds.Width <= 0 || container.Bounds.Height <= 0)
        {
            _activeIndicator.Opacity = 0;
            return false;
        }

        var origin = container.TranslatePoint(new Point(0, 0), _navTrack);
        if (!origin.HasValue)
            return false;

        var isTop = DisplayMode == VelaNavigationDisplayMode.Top;
        var targetX = origin.Value.X;
        var targetY = origin.Value.Y;
        var targetW = isTop ? container.Bounds.Width : Math.Max(36.0, _paneWidthSpring.Value - 16.0);
        var targetH = container.Bounds.Height;

        _activeIndicator.Opacity = 1.0;
        _positionedIndex = SelectedIndex;

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
                _indW.Jump(Math.Max(36.0, _paneWidthSpring.Value - 16.0));
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
        var opacity = Math.Clamp(_openProgressSpring.Value, 0.0, 1.0);

        for (var i = 0; i < ItemCount; i++)
        {
            if (ContainerFromIndex(i) is VelaNavItem navItem)
                navItem.SetLayoutMode(isCompact, isTop, opacity);
        }
    }

    private void UpdatePseudoClasses()
    {
        var isTop = DisplayMode == VelaNavigationDisplayMode.Top;
        PseudoClasses.Set(":left", !isTop);
        PseudoClasses.Set(":top", isTop);
        PseudoClasses.Set(":open", !isTop && IsPaneOpen);
        PseudoClasses.Set(":compact", !isTop && !IsPaneOpen);
    }
}
