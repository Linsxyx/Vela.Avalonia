using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Vela.Avalonia.Motion;

namespace Vela.Avalonia.Controls;

/// <summary>
/// Shared floating surface container used inside Popup controls (Select, ComboBox, DropdownMenu, Popover).
/// Plays a restrained spring entrance from the trigger edge whenever attached to a Popup's visual tree.
/// </summary>
public class VelaPopupSurface : Border
{
    public static readonly StyledProperty<double> EnterOffsetXProperty =
        AvaloniaProperty.Register<VelaPopupSurface, double>(nameof(EnterOffsetX), 0.0);

    public static readonly StyledProperty<double> EnterOffsetYProperty =
        AvaloniaProperty.Register<VelaPopupSurface, double>(nameof(EnterOffsetY), -5.0);

    public double EnterOffsetX { get => GetValue(EnterOffsetXProperty); set => SetValue(EnterOffsetXProperty, value); }
    public double EnterOffsetY { get => GetValue(EnterOffsetYProperty); set => SetValue(EnterOffsetYProperty, value); }

    private readonly SpringChannel _opacity = new(0.0, VelaMotionTokens.Springs.Snappy);
    private readonly SpringChannel _scale = new(0.97, VelaMotionTokens.Springs.Snappy);
    private readonly SpringChannel _x = new(0.0, VelaMotionTokens.Springs.Snappy);
    private readonly SpringChannel _y = new(-5.0, VelaMotionTokens.Springs.Snappy);
    private readonly SpringRunner _runner;
    private readonly ScaleTransform _scaleTransform = new(1.0, 1.0);
    private readonly TranslateTransform _translateTransform = new(0.0, 0.0);

    public VelaPopupSurface()
    {
        _runner = new SpringRunner(this, OnTick);
        RenderTransformOrigin = new RelativePoint(0.5, 0.0, RelativeUnit.Relative);
        RenderTransform = new TransformGroup { Children = { _scaleTransform, _translateTransform } };
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        PlayEnter();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _runner.Stop();
    }

    public void PlayEnter()
    {
        if (VelaMotionOptions.ReducedMotion)
        {
            _opacity.Jump(1.0);
            _scale.Jump(1.0);
            _x.Jump(0.0);
            _y.Jump(0.0);
            ApplyVisuals();
            return;
        }

        _opacity.Jump(0.0);
        _scale.Jump(0.97);
        _x.Jump(EnterOffsetX);
        _y.Jump(EnterOffsetY);
        ApplyVisuals();

        _opacity.SetTarget(1.0, VelaMotionTokens.Springs.Snappy);
        _scale.SetTarget(1.0, VelaMotionTokens.Springs.Snappy);
        _x.SetTarget(0.0, VelaMotionTokens.Springs.Snappy);
        _y.SetTarget(0.0, VelaMotionTokens.Springs.Snappy);
        _runner.Wake();
    }

    private bool OnTick(double dt)
    {
        var active = _opacity.Step(dt);
        active |= _scale.Step(dt);
        active |= _x.Step(dt);
        active |= _y.Step(dt);
        ApplyVisuals();
        return active;
    }

    private void ApplyVisuals()
    {
        Opacity = Math.Clamp(_opacity.Value, 0.0, 1.0);
        var s = Math.Max(0.5, _scale.Value);
        _scaleTransform.ScaleX = s;
        _scaleTransform.ScaleY = s;
        _translateTransform.X = _x.Value;
        _translateTransform.Y = _y.Value;
    }
}
