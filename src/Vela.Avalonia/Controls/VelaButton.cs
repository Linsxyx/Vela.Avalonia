using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Vela.Avalonia.Motion;

namespace Vela.Avalonia.Controls;

public enum VelaButtonVariant
{
    Primary,
    Secondary,
    Ghost,
    Danger,
}

public enum VelaControlSize
{
    Small,
    Medium,
    Large,
}

[PseudoClasses(":primary", ":secondary", ":ghost", ":danger", ":sm", ":md", ":lg", ":loading")]
public class VelaButton : Button
{
    public static readonly StyledProperty<VelaButtonVariant> VariantProperty =
        AvaloniaProperty.Register<VelaButton, VelaButtonVariant>(nameof(Variant), VelaButtonVariant.Primary);

    public static readonly StyledProperty<VelaControlSize> SizeProperty =
        AvaloniaProperty.Register<VelaButton, VelaControlSize>(nameof(Size), VelaControlSize.Medium);

    public static readonly StyledProperty<bool> IsLoadingProperty =
        AvaloniaProperty.Register<VelaButton, bool>(nameof(IsLoading));

    public VelaButtonVariant Variant
    {
        get => GetValue(VariantProperty);
        set => SetValue(VariantProperty, value);
    }

    public VelaControlSize Size
    {
        get => GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    public bool IsLoading
    {
        get => GetValue(IsLoadingProperty);
        set => SetValue(IsLoadingProperty, value);
    }

    private readonly SpringChannel _scaleSpring = new(1.0, VelaMotionTokens.Springs.Snappy);
    private readonly SpringRunner _runner;
    private ScaleTransform? _scaleTransform;

    static VelaButton()
    {
        VariantProperty.Changed.AddClassHandler<VelaButton>((b, _) => b.UpdatePseudoClasses());
        SizeProperty.Changed.AddClassHandler<VelaButton>((b, _) => b.UpdatePseudoClasses());
        IsLoadingProperty.Changed.AddClassHandler<VelaButton>((b, _) => b.UpdatePseudoClasses());
        IsPressedProperty.Changed.AddClassHandler<VelaButton>((b, _) => b.UpdatePressSpring());
    }

    public VelaButton()
    {
        _runner = new SpringRunner(this, OnMotionTick);
        UpdatePseudoClasses();
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _scaleTransform = new ScaleTransform(1.0, 1.0);
        RenderTransformOrigin = RelativePoint.Center;
        RenderTransform = _scaleTransform;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _runner.Stop();
    }

    protected override void OnClick()
    {
        if (IsLoading)
            return;
        base.OnClick();
    }

    private void UpdatePressSpring()
    {
        if (!IsEffectivelyEnabled || IsLoading || VelaMotionOptions.ReducedMotion)
        {
            _scaleSpring.Jump(1.0);
            if (_scaleTransform is not null)
            {
                _scaleTransform.ScaleX = 1.0;
                _scaleTransform.ScaleY = 1.0;
            }
            return;
        }

        var w = Bounds.Width;
        var pressedScale = w > 220 ? 0.985 : (w > 0 && w <= 48 ? 0.96 : 0.97);
        _scaleSpring.SetTarget(IsPressed ? pressedScale : 1.0, VelaMotionTokens.Springs.Snappy);
        _runner.Wake();
    }

    private bool OnMotionTick(double dt)
    {
        var active = _scaleSpring.Step(dt);
        if (_scaleTransform is not null)
        {
            _scaleTransform.ScaleX = _scaleSpring.Value;
            _scaleTransform.ScaleY = _scaleSpring.Value;
        }
        return active;
    }

    private void UpdatePseudoClasses()
    {
        PseudoClasses.Set(":primary", Variant == VelaButtonVariant.Primary);
        PseudoClasses.Set(":secondary", Variant == VelaButtonVariant.Secondary);
        PseudoClasses.Set(":ghost", Variant == VelaButtonVariant.Ghost);
        PseudoClasses.Set(":danger", Variant == VelaButtonVariant.Danger);

        PseudoClasses.Set(":sm", Size == VelaControlSize.Small);
        PseudoClasses.Set(":md", Size == VelaControlSize.Medium);
        PseudoClasses.Set(":lg", Size == VelaControlSize.Large);

        PseudoClasses.Set(":loading", IsLoading);
    }
}
