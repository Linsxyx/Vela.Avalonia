using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Vela.Avalonia.Motion;

namespace Vela.Avalonia.Controls;

public class VelaSwitch : ToggleButton
{
    private const double ThumbSize = 18.0;
    private const double Travel = 18.0;
    private const double PressStretch = 5.0;

    private readonly SpringChannel _xSpring = new(0.0, VelaMotionTokens.Springs.SwitchGlide);
    private readonly SpringChannel _widthSpring = new(ThumbSize, VelaMotionTokens.Springs.Snappy);
    private readonly SpringChannel _trackOnOpacity = new(0.0, VelaMotionTokens.Springs.Snappy);
    private readonly SpringRunner _runner;

    private Border? _thumb;
    private Border? _trackOn;
    private TranslateTransform? _thumbTranslate;
    private bool _isHolding;
    private bool _initialized;

    static VelaSwitch()
    {
        IsCheckedProperty.Changed.AddClassHandler<VelaSwitch>((s, _) => s.SyncTargets(animate: true));
    }

    public VelaSwitch()
    {
        _runner = new SpringRunner(this, OnMotionTick);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        _thumb = e.NameScope.Find<Border>("PART_Thumb");
        _trackOn = e.NameScope.Find<Border>("PART_TrackOn");

        if (_thumb is not null)
        {
            _thumbTranslate = new TranslateTransform();
            _thumb.RenderTransform = _thumbTranslate;
        }

        _initialized = true;
        SyncTargets(animate: false);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _runner.Stop();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed && IsEffectivelyEnabled)
        {
            _isHolding = true;
            SyncTargets(animate: true);
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        _isHolding = false;
        base.OnPointerReleased(e);
        SyncTargets(animate: true);
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (_isHolding)
        {
            _isHolding = false;
            SyncTargets(animate: true);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Space && IsEffectivelyEnabled)
        {
            _isHolding = true;
            SyncTargets(animate: true);
        }
        base.OnKeyDown(e);
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        if (e.Key == Key.Space)
        {
            _isHolding = false;
            SyncTargets(animate: true);
        }
        base.OnKeyUp(e);
    }

    private void SyncTargets(bool animate)
    {
        if (!_initialized)
            return;

        var on = IsChecked == true;
        var extra = _isHolding && !VelaMotionOptions.ReducedMotion && IsEffectivelyEnabled ? PressStretch : 0.0;
        var targetX = on ? Travel - extra : 0.0;
        var targetW = ThumbSize + extra;
        var targetOpacity = on ? 1.0 : 0.0;

        if (!animate || VelaMotionOptions.ReducedMotion)
        {
            _xSpring.Jump(targetX);
            _widthSpring.Jump(targetW);
            _trackOnOpacity.Jump(targetOpacity);
            ApplyVisuals();
            return;
        }

        _xSpring.SetTarget(targetX, VelaMotionTokens.Springs.SwitchGlide);
        _widthSpring.SetTarget(targetW, VelaMotionTokens.Springs.Snappy);
        _trackOnOpacity.SetTarget(targetOpacity, VelaMotionTokens.Springs.Snappy);
        _runner.Wake();
    }

    private bool OnMotionTick(double dt)
    {
        var active = _xSpring.Step(dt);
        active |= _widthSpring.Step(dt);
        active |= _trackOnOpacity.Step(dt);
        ApplyVisuals();
        return active;
    }

    private void ApplyVisuals()
    {
        if (_thumbTranslate is not null)
            _thumbTranslate.X = _xSpring.Value;
        if (_thumb is not null)
            _thumb.Width = _widthSpring.Value;
        if (_trackOn is not null)
            _trackOn.Opacity = Math.Clamp(_trackOnOpacity.Value, 0.0, 1.0);
    }
}
