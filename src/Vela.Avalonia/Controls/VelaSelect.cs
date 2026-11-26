using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Vela.Avalonia.Motion;

namespace Vela.Avalonia.Controls;

/// <summary>
/// Port of Arc UI's Select built on Avalonia's native ComboBox:
/// - Static anchor trigger (press feedback stays in color, never shifts popup measurement)
/// - Spring-rotating chevron (0 <-> 180 deg)
/// - Floating VelaPopupSurface with selected checkmark indicators and Label/Description support
/// </summary>
[PseudoClasses(":sm", ":md", ":lg")]
public class VelaSelect : ComboBox
{
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<VelaSelect, string?>(nameof(Label));

    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<VelaSelect, string?>(nameof(Description));

    public static readonly StyledProperty<VelaControlSize> SizeProperty =
        AvaloniaProperty.Register<VelaSelect, VelaControlSize>(nameof(Size), VelaControlSize.Medium);

    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }
    public VelaControlSize Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    protected override Type StyleKeyOverride => typeof(VelaSelect);

    private readonly SpringChannel _chevronAngle = new(0.0, VelaMotionTokens.Springs.Snappy);
    private readonly SpringRunner _runner;
    private RotateTransform? _chevronRotate;

    static VelaSelect()
    {
        SizeProperty.Changed.AddClassHandler<VelaSelect>((s, _) => s.UpdateSizeClasses());
        IsDropDownOpenProperty.Changed.AddClassHandler<VelaSelect>((s, _) => s.OnDropDownStateChanged());
    }

    public VelaSelect()
    {
        _runner = new SpringRunner(this, OnTick);
        UpdateSizeClasses();
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        var chevron = e.NameScope.Find<PathIcon>("PART_Chevron");
        if (chevron is not null)
        {
            _chevronRotate = new RotateTransform(IsDropDownOpen ? 180.0 : 0.0);
            chevron.RenderTransformOrigin = RelativePoint.Center;
            chevron.RenderTransform = _chevronRotate;
        }
        _chevronAngle.Jump(IsDropDownOpen ? 180.0 : 0.0);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _runner.Stop();
    }

    private void OnDropDownStateChanged()
    {
        var target = IsDropDownOpen ? 180.0 : 0.0;
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

    private void UpdateSizeClasses()
    {
        PseudoClasses.Set(":sm", Size == VelaControlSize.Small);
        PseudoClasses.Set(":md", Size == VelaControlSize.Medium);
        PseudoClasses.Set(":lg", Size == VelaControlSize.Large);
    }
}
