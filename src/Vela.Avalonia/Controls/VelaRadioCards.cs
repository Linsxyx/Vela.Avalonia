using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Threading;
using Vela.Avalonia.Motion;

namespace Vela.Avalonia.Controls;

public enum VelaRadioCardsLayout
{
    Grid,
    List,
}

public class VelaRadioCardItem : ListBoxItem
{
    public static readonly StyledProperty<string?> HeaderProperty =
        AvaloniaProperty.Register<VelaRadioCardItem, string?>(nameof(Header));

    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<VelaRadioCardItem, string?>(nameof(Description));

    public static readonly StyledProperty<string?> MetaProperty =
        AvaloniaProperty.Register<VelaRadioCardItem, string?>(nameof(Meta));

    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<VelaRadioCardItem, Geometry?>(nameof(Icon));

    public string? Header { get => GetValue(HeaderProperty); set => SetValue(HeaderProperty, value); }
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }
    public string? Meta { get => GetValue(MetaProperty); set => SetValue(MetaProperty, value); }
    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    protected override Type StyleKeyOverride => typeof(VelaRadioCardItem);
}

/// <summary>
/// Port of Arc UI's RadioCards:
/// Selectable option cards in Grid or List layout with a single shared accent selection ring
/// that glides smoothly from card to card on physical springs.
/// </summary>
[PseudoClasses(":grid", ":list")]
public class VelaRadioCards : ListBox
{
    public static readonly StyledProperty<VelaRadioCardsLayout> LayoutProperty =
        AvaloniaProperty.Register<VelaRadioCards, VelaRadioCardsLayout>(nameof(Layout), VelaRadioCardsLayout.Grid);

    public VelaRadioCardsLayout Layout { get => GetValue(LayoutProperty); set => SetValue(LayoutProperty, value); }

    protected override Type StyleKeyOverride => typeof(VelaRadioCards);

    private readonly SpringChannel _ringX = new(0.0, VelaMotionTokens.Springs.Morph);
    private readonly SpringChannel _ringY = new(0.0, VelaMotionTokens.Springs.Morph);
    private readonly SpringChannel _ringW = new(0.0, VelaMotionTokens.Springs.Morph);
    private readonly SpringChannel _ringH = new(0.0, VelaMotionTokens.Springs.Morph);
    private readonly SpringRunner _runner;

    private Panel? _track;
    private Border? _ring;
    private TranslateTransform? _ringTranslate;
    private bool _hasPositioned;
    private int _positionedIndex = -1;

    static VelaRadioCards()
    {
        SelectionModeProperty.OverrideDefaultValue<VelaRadioCards>(SelectionMode.Single | SelectionMode.AlwaysSelected);
        LayoutProperty.Changed.AddClassHandler<VelaRadioCards>((r, _) => r.OnLayoutModeChanged());
        SelectedIndexProperty.Changed.AddClassHandler<VelaRadioCards>((r, _) => r.OnSelectionChangedInternal());
    }

    public VelaRadioCards()
    {
        _runner = new SpringRunner(this, OnTick);
        UpdateLayoutClasses();
    }

    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey)
        => new VelaRadioCardItem();

    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey)
        => NeedsContainer<VelaRadioCardItem>(item, out recycleKey);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (_track is not null)
            _track.LayoutUpdated -= OnTrackLayoutUpdated;

        _track = e.NameScope.Find<Panel>("PART_Track");
        _ring = e.NameScope.Find<Border>("PART_Ring");

        if (_ring is not null)
        {
            _ringTranslate = new TranslateTransform();
            _ring.RenderTransform = _ringTranslate;
            _ring.Opacity = 0;
        }

        if (_track is not null)
            _track.LayoutUpdated += OnTrackLayoutUpdated;

        _hasPositioned = false;
        _positionedIndex = -1;
        UpdateLayoutClasses();
        Dispatcher.UIThread.Post(() => UpdateRing(animate: false), DispatcherPriority.Loaded);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _runner.Stop();
        if (_track is not null)
            _track.LayoutUpdated -= OnTrackLayoutUpdated;
    }

    private void OnLayoutModeChanged()
    {
        UpdateLayoutClasses();
        _hasPositioned = false;
        Dispatcher.UIThread.Post(() => UpdateRing(animate: false), DispatcherPriority.Loaded);
    }

    private void OnSelectionChangedInternal()
    {
        if (!UpdateRing(animate: true))
            Dispatcher.UIThread.Post(() => UpdateRing(animate: _hasPositioned), DispatcherPriority.Loaded);
    }

    private void OnTrackLayoutUpdated(object? sender, EventArgs e)
    {
        if (!_hasPositioned)
        {
            UpdateRing(animate: false);
            return;
        }

        if (SelectedIndex == _positionedIndex && !_ringX.IsAnimating && !_ringY.IsAnimating && !_ringW.IsAnimating && !_ringH.IsAnimating)
            UpdateRing(animate: false);
    }

    private bool UpdateRing(bool animate)
    {
        if (_track is null || _ring is null) return false;
        if (SelectedIndex < 0 || SelectedIndex >= ItemCount)
        {
            _ring.Opacity = 0;
            return true;
        }

        var container = ContainerFromIndex(SelectedIndex);
        if (container is null || container.Bounds.Width <= 0 || container.Bounds.Height <= 0)
            return false;

        var origin = container.TranslatePoint(new Point(0, 0), _track);
        if (!origin.HasValue) return false;

        _ring.Opacity = 1.0;
        _positionedIndex = SelectedIndex;

        var shouldAnimate = _hasPositioned && animate && !VelaMotionOptions.ReducedMotion;
        _hasPositioned = true;

        if (!shouldAnimate)
        {
            _ringX.Jump(origin.Value.X);
            _ringY.Jump(origin.Value.Y);
            _ringW.Jump(container.Bounds.Width);
            _ringH.Jump(container.Bounds.Height);
            ApplyRing();
            return true;
        }

        _ringX.SetTarget(origin.Value.X, VelaMotionTokens.Springs.Morph);
        _ringY.SetTarget(origin.Value.Y, VelaMotionTokens.Springs.Morph);
        _ringW.SetTarget(container.Bounds.Width, VelaMotionTokens.Springs.Morph);
        _ringH.SetTarget(container.Bounds.Height, VelaMotionTokens.Springs.Morph);
        _runner.Wake();
        return true;
    }

    private bool OnTick(double dt)
    {
        var active = _ringX.Step(dt);
        active |= _ringY.Step(dt);
        active |= _ringW.Step(dt);
        active |= _ringH.Step(dt);
        ApplyRing();
        return active;
    }

    private void ApplyRing()
    {
        if (_ring is null || _ringTranslate is null) return;
        _ringTranslate.X = _ringX.Value;
        _ringTranslate.Y = _ringY.Value;
        _ring.Width = Math.Max(0, _ringW.Value);
        _ring.Height = Math.Max(0, _ringH.Value);
    }

    private void UpdateLayoutClasses()
    {
        PseudoClasses.Set(":grid", Layout == VelaRadioCardsLayout.Grid);
        PseudoClasses.Set(":list", Layout == VelaRadioCardsLayout.List);
    }
}
