using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Vela.Avalonia.Motion;

namespace Vela.Avalonia.Controls;

public enum VelaTabsVariant
{
    Pill,
    Underline,
}

public class VelaTabItem : ListBoxItem
{
    public static readonly StyledProperty<string?> HeaderProperty =
        AvaloniaProperty.Register<VelaTabItem, string?>(nameof(Header));

    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<VelaTabItem, Geometry?>(nameof(Icon));

    public static readonly StyledProperty<object?> BadgeProperty =
        AvaloniaProperty.Register<VelaTabItem, object?>(nameof(Badge));

    public static readonly StyledProperty<object?> PageContentProperty =
        AvaloniaProperty.Register<VelaTabItem, object?>(nameof(PageContent));

    public static readonly StyledProperty<Func<object?>?> PageFactoryProperty =
        AvaloniaProperty.Register<VelaTabItem, Func<object?>?>(nameof(PageFactory));

    public string? Header { get => GetValue(HeaderProperty); set => SetValue(HeaderProperty, value); }
    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }
    public object? Badge { get => GetValue(BadgeProperty); set => SetValue(BadgeProperty, value); }
    public object? PageContent { get => GetValue(PageContentProperty); set => SetValue(PageContentProperty, value); }
    public Func<object?>? PageFactory { get => GetValue(PageFactoryProperty); set => SetValue(PageFactoryProperty, value); }

    protected override Type StyleKeyOverride => typeof(VelaTabItem);
}

/// <summary>
/// High-performance standalone Tabs control:
/// - Supports Pill (gliding capsule) and Underline (gliding 2px bar) variants
/// - Powered by <see cref="VelaPageDeck"/> for O(1) KeepAlive/LRU switching without tearing down visual trees
/// - Wrap-around Left/Right/Home/End keyboard navigation
/// </summary>
[PseudoClasses(":pill", ":underline")]
public class VelaTabs : ListBox
{
    public static readonly StyledProperty<VelaTabsVariant> VariantProperty =
        AvaloniaProperty.Register<VelaTabs, VelaTabsVariant>(nameof(Variant), VelaTabsVariant.Pill);

    public static readonly StyledProperty<VelaPageCacheMode> PageCacheModeProperty =
        AvaloniaProperty.Register<VelaTabs, VelaPageCacheMode>(nameof(PageCacheMode), VelaPageCacheMode.KeepAlive);

    public static readonly StyledProperty<VelaPageTransitionMode> PageTransitionModeProperty =
        AvaloniaProperty.Register<VelaTabs, VelaPageTransitionMode>(nameof(PageTransitionMode), VelaPageTransitionMode.Instant);

    public static readonly StyledProperty<IDataTemplate?> PageTemplateProperty =
        AvaloniaProperty.Register<VelaTabs, IDataTemplate?>(nameof(PageTemplate));

    public VelaTabsVariant Variant { get => GetValue(VariantProperty); set => SetValue(VariantProperty, value); }
    public VelaPageCacheMode PageCacheMode { get => GetValue(PageCacheModeProperty); set => SetValue(PageCacheModeProperty, value); }
    public VelaPageTransitionMode PageTransitionMode { get => GetValue(PageTransitionModeProperty); set => SetValue(PageTransitionModeProperty, value); }
    public IDataTemplate? PageTemplate { get => GetValue(PageTemplateProperty); set => SetValue(PageTemplateProperty, value); }

    protected override Type StyleKeyOverride => typeof(VelaTabs);

    private readonly SpringChannel _indX = new(0.0, VelaMotionTokens.Springs.Morph);
    private readonly SpringChannel _indW = new(0.0, VelaMotionTokens.Springs.Morph);
    private readonly SpringRunner _runner;

    private Panel? _track;
    private Border? _indicator;
    private TranslateTransform? _indTranslate;
    private VelaPageDeck? _pageDeck;
    private bool _hasPositioned;
    private int _positionedIndex = -1;

    static VelaTabs()
    {
        SelectionModeProperty.OverrideDefaultValue<VelaTabs>(SelectionMode.Single | SelectionMode.AlwaysSelected);
        VariantProperty.Changed.AddClassHandler<VelaTabs>((t, _) => t.OnVariantChanged());
        SelectedIndexProperty.Changed.AddClassHandler<VelaTabs>((t, _) => t.OnSelectionChangedInternal());
        SelectedItemProperty.Changed.AddClassHandler<VelaTabs>((t, _) => t.ActivatePage());
    }

    public VelaTabs()
    {
        _runner = new SpringRunner(this, OnTick);
        UpdatePseudoClasses();
    }

    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey)
        => new VelaTabItem();

    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey)
        => NeedsContainer<VelaTabItem>(item, out recycleKey);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (_track is not null)
            _track.LayoutUpdated -= OnTrackLayoutUpdated;

        _track = e.NameScope.Find<Panel>("PART_Track");
        _indicator = e.NameScope.Find<Border>("PART_Indicator");
        _pageDeck = e.NameScope.Find<VelaPageDeck>("PART_PageDeck");

        if (_indicator is not null)
        {
            _indScale = new ScaleTransform(1.0, 1.0);
            _indTranslate = new TranslateTransform();
            var group = new TransformGroup();
            group.Children.Add(_indScale);
            group.Children.Add(_indTranslate);
            _indicator.RenderTransformOrigin = RelativePoint.TopLeft;
            _indicator.RenderTransform = group;
            _indicator.Opacity = 0;
        }

        if (_track is not null)
            _track.LayoutUpdated += OnTrackLayoutUpdated;

        _hasPositioned = false;
        _positionedIndex = -1;
        UpdatePseudoClasses();
        ActivatePage();

        Dispatcher.UIThread.Post(() => UpdateIndicator(animate: false), DispatcherPriority.Loaded);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _runner.Stop();
        if (_track is not null)
            _track.LayoutUpdated -= OnTrackLayoutUpdated;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        var count = ItemCount;
        if (count > 0)
        {
            var idx = Math.Max(0, SelectedIndex);
            var target = e.Key switch
            {
                Key.Right => idx == count - 1 ? 0 : idx + 1,
                Key.Left => idx == 0 ? count - 1 : idx - 1,
                Key.Home => 0,
                Key.End => count - 1,
                _ => -1,
            };

            if (target >= 0)
            {
                SelectedIndex = target;
                ContainerFromIndex(target)?.Focus(NavigationMethod.Directional);
                e.Handled = true;
                return;
            }
        }
        base.OnKeyDown(e);
    }

    private void OnVariantChanged()
    {
        UpdatePseudoClasses();
        _hasPositioned = false;
        Dispatcher.UIThread.Post(() => UpdateIndicator(animate: false), DispatcherPriority.Loaded);
    }

    private void OnSelectionChangedInternal()
    {
        ActivatePage();
        if (!UpdateIndicator(animate: true))
            Dispatcher.UIThread.Post(() => UpdateIndicator(animate: _hasPositioned), DispatcherPriority.Loaded);
    }

    private void ActivatePage()
    {
        if (_pageDeck is null) return;
        if (SelectedItem is VelaTabItem tab)
        {
            _pageDeck.Activate(tab, () => tab.PageFactory is not null ? tab.PageFactory() : tab.PageContent);
            return;
        }
        if (SelectedItem is not null)
        {
            var item = SelectedItem;
            _pageDeck.Activate(item, () => item);
            return;
        }
        _pageDeck.Activate(null, static () => null);
    }

    private void OnTrackLayoutUpdated(object? sender, EventArgs e)
    {
        if (!_hasPositioned)
        {
            UpdateIndicator(animate: false);
            return;
        }

        if (SelectedIndex == _positionedIndex && !_indX.IsAnimating && !_indW.IsAnimating)
            UpdateIndicator(animate: false);
    }

    private bool UpdateIndicator(bool animate)
    {
        if (_track is null || _indicator is null) return false;
        if (SelectedIndex < 0 || SelectedIndex >= ItemCount)
        {
            _indicator.Opacity = 0;
            return true;
        }

        var container = ContainerFromIndex(SelectedIndex);
        if (container is null || container.Bounds.Width <= 0)
            return false;

        var origin = container.TranslatePoint(new Point(0, 0), _track);
        if (!origin.HasValue)
            return false;

        var isUnderline = Variant == VelaTabsVariant.Underline;
        _indicator.Height = isUnderline ? 2.0 : container.Bounds.Height;
        Canvas.SetTop(_indicator, isUnderline ? Math.Max(0, container.Bounds.Height - 2.0) : 0.0);
        _indicator.Opacity = 1.0;
        _positionedIndex = SelectedIndex;

        var shouldAnimate = _hasPositioned && animate && !VelaMotionOptions.ReducedMotion;
        _hasPositioned = true;

        if (!shouldAnimate)
        {
            _indX.Jump(origin.Value.X);
            _indW.Jump(container.Bounds.Width);
            ApplyIndicator();
            return true;
        }

        _indX.SetTarget(origin.Value.X, VelaMotionTokens.Springs.Morph);
        _indW.SetTarget(container.Bounds.Width, VelaMotionTokens.Springs.Morph);
        _runner.Wake();
        return true;
    }

    private bool OnTick(double dt)
    {
        var active = _indX.Step(dt);
        active |= _indW.Step(dt);
        ApplyIndicator();
        return active;
    }

    private ScaleTransform? _indScale;

    private void ApplyIndicator()
    {
        if (_indicator is null || _indTranslate is null) return;
        var w = Math.Max(0, _indW.Value);
        _indTranslate.X = _indX.Value;

        if (Variant == VelaTabsVariant.Underline && _indScale is not null)
        {
            // Unit-width bar scaled on the GPU compositor layer: zero per-frame layout passes.
            if (Math.Abs(_indicator.Width - 1.0) > 1e-3)
                _indicator.Width = 1.0;
            _indScale.ScaleX = w;
        }
        else
        {
            if (_indScale is not null && Math.Abs(_indScale.ScaleX - 1.0) > 1e-3)
                _indScale.ScaleX = 1.0;
            if (Math.Abs(_indicator.Width - w) > 0.25)
                _indicator.Width = w;
        }
    }

    private void UpdatePseudoClasses()
    {
        PseudoClasses.Set(":pill", Variant == VelaTabsVariant.Pill);
        PseudoClasses.Set(":underline", Variant == VelaTabsVariant.Underline);
    }
}
