using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using Vela.Avalonia.Motion;

namespace Vela.Avalonia.Controls;

/// <summary>
/// Measures its child at unconstrained vertical height while smoothly springing its own
/// reported height between 0 and natural height (for Accordion / collapsible disclosure).
/// </summary>
public sealed class VelaCollapsePanel : Panel
{
    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<VelaCollapsePanel, bool>(nameof(IsExpanded));

    public bool IsExpanded { get => GetValue(IsExpandedProperty); set => SetValue(IsExpandedProperty, value); }

    private readonly SpringChannel _heightSpring = new(0.0, VelaMotionTokens.Springs.Smooth);
    private readonly SpringChannel _opacitySpring = new(0.0, VelaMotionTokens.Springs.Snappy);
    private readonly SpringRunner _runner;
    private double _naturalHeight;
    private bool _hasMeasured;

    static VelaCollapsePanel()
    {
        IsExpandedProperty.Changed.AddClassHandler<VelaCollapsePanel>((p, _) => p.OnExpandedChanged());
    }

    public VelaCollapsePanel()
    {
        ClipToBounds = true;
        _runner = new SpringRunner(this, OnTick);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _runner.Stop();
    }

    private void OnExpandedChanged()
    {
        var targetH = IsExpanded ? _naturalHeight : 0.0;
        var targetO = IsExpanded ? 1.0 : 0.0;

        if (!_hasMeasured || VelaMotionOptions.ReducedMotion)
        {
            _heightSpring.Jump(targetH);
            _opacitySpring.Jump(targetO);
            Opacity = targetO;
            InvalidateMeasure();
            return;
        }

        _heightSpring.SetTarget(targetH, VelaMotionTokens.Springs.Smooth);
        _opacitySpring.SetTarget(targetO, VelaMotionTokens.Springs.Snappy);
        _runner.Wake();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        double maxW = 0;
        double maxH = 0;

        foreach (var child in Children)
        {
            child.Measure(new Size(availableSize.Width, double.PositiveInfinity));
            maxW = Math.Max(maxW, child.DesiredSize.Width);
            maxH = Math.Max(maxH, child.DesiredSize.Height);
        }

        _naturalHeight = maxH;
        if (!_hasMeasured)
        {
            _hasMeasured = true;
            _heightSpring.Jump(IsExpanded ? maxH : 0.0);
            _opacitySpring.Jump(IsExpanded ? 1.0 : 0.0);
            Opacity = IsExpanded ? 1.0 : 0.0;
        }
        else if (IsExpanded && !_heightSpring.IsAnimating && Math.Abs(_heightSpring.Value - maxH) > 0.5)
        {
            _heightSpring.Jump(maxH);
        }

        var reportedH = Math.Max(0.0, _heightSpring.Value);
        return new Size(Math.Min(maxW, availableSize.Width), reportedH);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var child in Children)
            child.Arrange(new Rect(0, 0, finalSize.Width, _naturalHeight));
        return finalSize;
    }

    private bool OnTick(double dt)
    {
        var active = _heightSpring.Step(dt);
        active |= _opacitySpring.Step(dt);
        Opacity = Math.Clamp(_opacitySpring.Value, 0.0, 1.0);
        InvalidateMeasure();
        return active;
    }
}

/// <summary>
/// Port of Arc UI's Accordion item: hairline divider, spring-rotating chevron, and spring height disclosure.
/// </summary>
[PseudoClasses(":expanded")]
public class VelaAccordionItem : HeaderedContentControl
{
    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<VelaAccordionItem, bool>(nameof(IsExpanded), false, defaultBindingMode: BindingMode.TwoWay);

    public bool IsExpanded { get => GetValue(IsExpandedProperty); set => SetValue(IsExpandedProperty, value); }

    protected override Type StyleKeyOverride => typeof(VelaAccordionItem);

    private readonly SpringChannel _chevronAngle = new(0.0, VelaMotionTokens.Springs.Snappy);
    private readonly SpringRunner _runner;
    private Button? _trigger;
    private RotateTransform? _chevronRotate;

    static VelaAccordionItem()
    {
        IsExpandedProperty.Changed.AddClassHandler<VelaAccordionItem>((item, _) => item.OnExpandedChanged());
    }

    public VelaAccordionItem()
    {
        _runner = new SpringRunner(this, OnTick);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (_trigger is not null)
            _trigger.Click -= OnTriggerClick;

        _trigger = e.NameScope.Find<Button>("PART_Trigger");
        var chevron = e.NameScope.Find<PathIcon>("PART_Chevron");

        if (chevron is not null)
        {
            _chevronRotate = new RotateTransform(IsExpanded ? 180.0 : 0.0);
            chevron.RenderTransformOrigin = RelativePoint.Center;
            chevron.RenderTransform = _chevronRotate;
        }

        if (_trigger is not null)
            _trigger.Click += OnTriggerClick;

        _chevronAngle.Jump(IsExpanded ? 180.0 : 0.0);
        PseudoClasses.Set(":expanded", IsExpanded);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _runner.Stop();
        if (_trigger is not null)
            _trigger.Click -= OnTriggerClick;
    }

    private void OnTriggerClick(object? sender, RoutedEventArgs e)
    {
        IsExpanded = !IsExpanded;
    }

    private void OnExpandedChanged()
    {
        PseudoClasses.Set(":expanded", IsExpanded);

        if (IsExpanded && this.FindAncestorOfType<VelaAccordion>() is { AllowMultiple: false } accordion)
        {
            accordion.CollapseOthers(this);
        }

        var target = IsExpanded ? 180.0 : 0.0;
        if (VelaMotionOptions.ReducedMotion)
        {
            _chevronAngle.Jump(target);
            if (_chevronRotate is not null) _chevronRotate.Angle = target;
            return;
        }

        _chevronAngle.SetTarget(target, VelaMotionTokens.Springs.Snappy);
        _runner.Wake();
    }

    private bool OnTick(double dt)
    {
        var active = _chevronAngle.Step(dt);
        if (_chevronRotate is not null)
            _chevronRotate.Angle = _chevronAngle.Value;
        return active;
    }
}

/// <summary>
/// Groups <see cref="VelaAccordionItem"/> instances with optional single-open enforcement.
/// </summary>
public class VelaAccordion : ItemsControl
{
    public static readonly StyledProperty<bool> AllowMultipleProperty =
        AvaloniaProperty.Register<VelaAccordion, bool>(nameof(AllowMultiple), false);

    public bool AllowMultiple { get => GetValue(AllowMultipleProperty); set => SetValue(AllowMultipleProperty, value); }

    protected override Type StyleKeyOverride => typeof(VelaAccordion);

    internal void CollapseOthers(VelaAccordionItem active)
    {
        for (var i = 0; i < ItemCount; i++)
        {
            if (ContainerFromIndex(i) is VelaAccordionItem item && !ReferenceEquals(item, active) && item.IsExpanded)
                item.IsExpanded = false;
            else if (Items[i] is VelaAccordionItem directItem && !ReferenceEquals(directItem, active) && directItem.IsExpanded)
                directItem.IsExpanded = false;
        }
    }
}
