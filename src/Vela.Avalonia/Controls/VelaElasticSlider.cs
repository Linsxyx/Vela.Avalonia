using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;
using Vela.Avalonia.Motion;

namespace Vela.Avalonia.Controls;

public class VelaElasticSlider : TemplatedControl
{
    private const double ThumbSize = 24.0;
    private const double StretchLimit = 26.0;
    private const double DetentPx = 7.0;
    private const double BumpPx = 260.0;

    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<VelaElasticSlider, string>(nameof(Label), string.Empty);

    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<VelaElasticSlider, double>(nameof(Value), 50.0, defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<double> MinimumProperty =
        AvaloniaProperty.Register<VelaElasticSlider, double>(nameof(Minimum), 0.0);

    public static readonly StyledProperty<double> MaximumProperty =
        AvaloniaProperty.Register<VelaElasticSlider, double>(nameof(Maximum), 100.0);

    public static readonly StyledProperty<double> StepProperty =
        AvaloniaProperty.Register<VelaElasticSlider, double>(nameof(Step), 1.0);

    public static readonly StyledProperty<bool> ShowValueProperty =
        AvaloniaProperty.Register<VelaElasticSlider, bool>(nameof(ShowValue), true);

    public static readonly StyledProperty<string> ValueSuffixProperty =
        AvaloniaProperty.Register<VelaElasticSlider, string>(nameof(ValueSuffix), string.Empty);

    public static readonly StyledProperty<IReadOnlyList<double>?> MarksProperty =
        AvaloniaProperty.Register<VelaElasticSlider, IReadOnlyList<double>?>(nameof(Marks));

    public static readonly DirectProperty<VelaElasticSlider, string> FormattedValueProperty =
        AvaloniaProperty.RegisterDirect<VelaElasticSlider, string>(nameof(FormattedValue), o => o.FormattedValue);

    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public double Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public double Minimum { get => GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }
    public double Maximum { get => GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }
    public double Step { get => GetValue(StepProperty); set => SetValue(StepProperty, value); }
    public bool ShowValue { get => GetValue(ShowValueProperty); set => SetValue(ShowValueProperty, value); }
    public string ValueSuffix { get => GetValue(ValueSuffixProperty); set => SetValue(ValueSuffixProperty, value); }
    public IReadOnlyList<double>? Marks { get => GetValue(MarksProperty); set => SetValue(MarksProperty, value); }

    private string _formattedValue = "50";
    public string FormattedValue
    {
        get => _formattedValue;
        private set => SetAndRaise(FormattedValueProperty, ref _formattedValue, value);
    }

    public event EventHandler<double>? ValueChanged;

    private readonly SpringChannel _posSpring = new(50.0, VelaMotionTokens.Springs.Snappy);
    private readonly SpringChannel _overSpring = new(0.0, VelaMotionTokens.Springs.SnapBack);
    private readonly SpringChannel _pressSpring = new(0.0, VelaMotionTokens.Springs.Snappy);
    private readonly SpringChannel _bubblePosSpring = new(50.0, VelaMotionTokens.Springs.TrailingBubble);
    private readonly SpringChannel _bubbleOpacitySpring = new(0.0, VelaMotionTokens.Springs.Snappy);
    private readonly SpringChannel _bubbleScaleSpring = new(0.75, VelaMotionTokens.Springs.Snappy);
    private readonly SpringRunner _runner;

    private Control? _hitArea;
    private Border? _track;
    private Border? _fill;
    private Border? _thumb;
    private Border? _bubble;
    private TextBlock? _readout;
    private Canvas? _ticksCanvas;

    private ScaleTransform? _trackScale;
    private TranslateTransform? _thumbTranslate;
    private ScaleTransform? _thumbScale;
    private TranslateTransform? _bubbleTranslate;
    private RotateTransform? _bubbleRotate;
    private ScaleTransform? _bubbleScale;

    private bool _dragging;
    private double? _activeDetent;
    private double _lastPointerX;
    private double _lastPointerTime;
    private double _releaseVelocity;
    private double _railWidth = 200.0;
    private bool _initialized;

    static VelaElasticSlider()
    {
        FocusableProperty.OverrideDefaultValue<VelaElasticSlider>(true);
        ValueProperty.Changed.AddClassHandler<VelaElasticSlider>((s, _) => s.OnValuePropertyChanged());
        MinimumProperty.Changed.AddClassHandler<VelaElasticSlider>((s, _) => s.OnValuePropertyChanged());
        MaximumProperty.Changed.AddClassHandler<VelaElasticSlider>((s, _) => s.OnValuePropertyChanged());
        ValueSuffixProperty.Changed.AddClassHandler<VelaElasticSlider>((s, _) => s.UpdateFormattedValue());
        MarksProperty.Changed.AddClassHandler<VelaElasticSlider>((s, _) => s.RebuildTicks());
    }

    public VelaElasticSlider()
    {
        _runner = new SpringRunner(this, OnMotionTick);
        UpdateFormattedValue();
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (_hitArea is not null)
        {
            _hitArea.PointerPressed -= OnHitPointerPressed;
            _hitArea.PointerMoved -= OnHitPointerMoved;
            _hitArea.PointerReleased -= OnHitPointerReleased;
            _hitArea.PointerCaptureLost -= OnHitPointerCaptureLost;
            _hitArea.PropertyChanged -= OnHitAreaPropertyChanged;
        }

        _hitArea = e.NameScope.Find<Control>("PART_HitArea");
        _track = e.NameScope.Find<Border>("PART_Track");
        _fill = e.NameScope.Find<Border>("PART_Fill");
        _thumb = e.NameScope.Find<Border>("PART_Thumb");
        _bubble = e.NameScope.Find<Border>("PART_Bubble");
        _readout = e.NameScope.Find<TextBlock>("PART_Readout");
        _ticksCanvas = e.NameScope.Find<Canvas>("PART_Ticks");

        if (_track is not null)
        {
            _trackScale = new ScaleTransform(1, 1);
            _track.RenderTransform = _trackScale;
            _track.RenderTransformOrigin = new RelativePoint(0, 0.5, RelativeUnit.Relative);
        }

        if (_thumb is not null)
        {
            _thumbScale = new ScaleTransform(1, 1);
            _thumbTranslate = new TranslateTransform();
            _thumb.RenderTransformOrigin = RelativePoint.Center;
            _thumb.RenderTransform = new TransformGroup { Children = { _thumbScale, _thumbTranslate } };
        }

        if (_bubble is not null)
        {
            _bubbleScale = new ScaleTransform(0.75, 0.75);
            _bubbleRotate = new RotateTransform(0);
            _bubbleTranslate = new TranslateTransform();
            _bubble.RenderTransformOrigin = new RelativePoint(0.5, 1.3, RelativeUnit.Relative);
            _bubble.RenderTransform = new TransformGroup { Children = { _bubbleScale, _bubbleRotate, _bubbleTranslate } };
            _bubble.Opacity = 0;
        }

        if (_hitArea is not null)
        {
            _hitArea.PointerPressed += OnHitPointerPressed;
            _hitArea.PointerMoved += OnHitPointerMoved;
            _hitArea.PointerReleased += OnHitPointerReleased;
            _hitArea.PointerCaptureLost += OnHitPointerCaptureLost;
            _hitArea.PropertyChanged += OnHitAreaPropertyChanged;
        }

        var pct = ToPercent(Value);
        _posSpring.Jump(pct);
        _bubblePosSpring.Jump(pct);
        _overSpring.Jump(0);
        _pressSpring.Jump(0);
        _bubbleOpacitySpring.Jump(0);
        _bubbleScaleSpring.Jump(0.75);

        _initialized = true;
        UpdateFormattedValue();
        RebuildTicks();
        ApplyVisuals();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ActualThemeVariantChanged += OnThemeVariantChanged;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        ActualThemeVariantChanged -= OnThemeVariantChanged;
        _runner.Stop();
    }

    private void OnThemeVariantChanged(object? sender, EventArgs e)
        => UpdateTickStates(Math.Clamp(_posSpring.Value, 0.0, 100.0));

    private void OnHitAreaPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == BoundsProperty && _hitArea is not null)
        {
            _railWidth = Math.Max(1.0, _hitArea.Bounds.Width - ThumbSize);
            RebuildTicks();
            ApplyVisuals();
        }
    }

    private void OnValuePropertyChanged()
    {
        UpdateFormattedValue();
        if (!_initialized || _dragging)
            return;

        var pct = ToPercent(Value);
        if (VelaMotionOptions.ReducedMotion)
        {
            _posSpring.Jump(pct);
            _bubblePosSpring.Jump(pct);
            ApplyVisuals();
            return;
        }

        _posSpring.SetTarget(pct, VelaMotionTokens.Springs.Snappy);
        _bubblePosSpring.SetTarget(pct, VelaMotionTokens.Springs.TrailingBubble);
        _runner.Wake();
    }

    private void OnHitPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!IsEffectivelyEnabled || _hitArea is null)
            return;

        var pt = e.GetCurrentPoint(_hitArea);
        if (!pt.Properties.IsLeftButtonPressed)
            return;

        Focus(NavigationMethod.Pointer);
        e.Pointer.Capture(_hitArea);

        _dragging = true;
        _activeDetent = null;
        _releaseVelocity = 0;
        _railWidth = Math.Max(1.0, _hitArea.Bounds.Width - ThumbSize);

        var x = pt.Position.X - ThumbSize / 2.0;
        _lastPointerX = x;
        _lastPointerTime = Environment.TickCount64 / 1000.0;

        _pressSpring.SetTarget(1.0, VelaMotionTokens.Springs.Snappy);
        _bubbleOpacitySpring.SetTarget(1.0, VelaMotionTokens.Springs.Snappy);
        _bubbleScaleSpring.SetTarget(1.0, VelaMotionTokens.Springs.Snappy);

        UpdateFromPointerX(x, snapSmoothly: true);
        _runner.Wake();
        e.Handled = true;
    }

    private void OnHitPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_dragging || _hitArea is null)
            return;

        var x = e.GetPosition(_hitArea).X - ThumbSize / 2.0;
        var now = Environment.TickCount64 / 1000.0;
        var dt = now - _lastPointerTime;
        if (dt > 0.002)
        {
            var instVel = (x - _lastPointerX) / dt;
            _releaseVelocity = _releaseVelocity * 0.4 + instVel * 0.6;
            _lastPointerX = x;
            _lastPointerTime = now;
        }

        UpdateFromPointerX(x, snapSmoothly: false);
        _runner.Wake();
        e.Handled = true;
    }

    private void OnHitPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_dragging) return;
        FinishDrag(e.Pointer);
        e.Handled = true;
    }

    private void OnHitPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (_dragging) FinishDrag(null);
    }

    private void FinishDrag(IPointer? pointer)
    {
        _dragging = false;
        _activeDetent = null;
        pointer?.Capture(null);

        _pressSpring.SetTarget(0.0, VelaMotionTokens.Springs.Snappy);
        _bubbleOpacitySpring.SetTarget(0.0, VelaMotionTokens.Springs.Snappy);
        _bubbleScaleSpring.SetTarget(0.8, VelaMotionTokens.Springs.Snappy);

        var snapVel = Math.Clamp(_releaseVelocity * 0.35, -800, 800);
        _overSpring.SetTarget(0.0, VelaMotionTokens.Springs.SnapBack, initialVelocity: snapVel);
        _runner.Wake();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (!IsEffectivelyEnabled) return;

        var step = Step > 0 ? Step : 1.0;
        var large = (Maximum - Minimum) / 10.0;
        var delta = e.Key switch
        {
            Key.Right or Key.Up => e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? large : step,
            Key.Left or Key.Down => -(e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? large : step),
            Key.PageUp => large,
            Key.PageDown => -large,
            Key.Home => Minimum - Value,
            Key.End => Maximum - Value,
            _ => 0.0,
        };

        if (delta == 0.0)
        {
            base.OnKeyDown(e);
            return;
        }

        var next = SnapValue(Value + delta);
        if (Math.Abs(next - Value) < 1e-9 && !VelaMotionOptions.ReducedMotion)
        {
            var dir = Math.Sign(delta);
            _overSpring.SetTarget(0.0, VelaMotionTokens.Springs.SnapBack, initialVelocity: dir * BumpPx);
            _runner.Wake();
        }
        else
        {
            CommitValue(next);
            var pct = ToPercent(next);
            _posSpring.SetTarget(pct, VelaMotionTokens.Springs.Snappy);
            _bubblePosSpring.SetTarget(pct, VelaMotionTokens.Springs.TrailingBubble);
            _runner.Wake();
        }

        e.Handled = true;
    }

    private void UpdateFromPointerX(double x, bool snapSmoothly)
    {
        var rawPx = x;
        var overPx = 0.0;
        if (rawPx < 0)
            overPx = RubberBand(rawPx, StretchLimit);
        else if (rawPx > _railWidth)
            overPx = RubberBand(rawPx - _railWidth, StretchLimit);

        if (VelaMotionOptions.ReducedMotion)
            overPx = 0;

        _overSpring.Jump(overPx);

        var clampedPx = Math.Clamp(rawPx, 0.0, _railWidth);

        var marks = Marks;
        if (marks is { Count: > 0 })
        {
            if (_activeDetent.HasValue)
            {
                var detentPx = (ToPercent(_activeDetent.Value) / 100.0) * _railWidth;
                if (Math.Abs(clampedPx - detentPx) <= DetentPx * 1.4)
                    clampedPx = detentPx;
                else
                    _activeDetent = null;
            }

            if (!_activeDetent.HasValue)
            {
                foreach (var mark in marks)
                {
                    if (mark < Minimum || mark > Maximum) continue;
                    var markPx = (ToPercent(mark) / 100.0) * _railWidth;
                    if (Math.Abs(clampedPx - markPx) <= DetentPx)
                    {
                        _activeDetent = mark;
                        clampedPx = markPx;
                        break;
                    }
                }
            }
        }

        var pct = (clampedPx / _railWidth) * 100.0;
        var snapped = _activeDetent ?? SnapValue(FromPercent(pct));
        var snappedPct = ToPercent(snapped);

        CommitValue(snapped);

        if (snapSmoothly && !VelaMotionOptions.ReducedMotion)
            _posSpring.SetTarget(snappedPct, VelaMotionTokens.Springs.Snappy);
        else
            _posSpring.Jump(snappedPct);

        if (VelaMotionOptions.ReducedMotion)
            _bubblePosSpring.Jump(snappedPct);
        else
            _bubblePosSpring.SetTarget(snappedPct, VelaMotionTokens.Springs.TrailingBubble);

        ApplyVisuals();
    }

    private void CommitValue(double next)
    {
        if (Math.Abs(next - Value) < 1e-9) return;
        SetCurrentValue(ValueProperty, next);
        UpdateFormattedValue();
        ValueChanged?.Invoke(this, next);
    }

    private bool OnMotionTick(double dt)
    {
        var active = _posSpring.Step(dt);
        active |= _overSpring.Step(dt);
        active |= _pressSpring.Step(dt);
        active |= _bubblePosSpring.Step(dt);
        active |= _bubbleOpacitySpring.Step(dt);
        active |= _bubbleScaleSpring.Step(dt);

        ApplyVisuals();
        return active;
    }

    private void ApplyVisuals()
    {
        if (!_initialized) return;

        var pct = Math.Clamp(_posSpring.Value, 0.0, 100.0);
        var over = _overSpring.Value;
        var press = Math.Clamp(_pressSpring.Value, 0.0, 1.0);
        var thumbPx = (pct / 100.0) * _railWidth;

        if (_track is not null && _trackScale is not null)
        {
            var scaleX = 1.0 + Math.Abs(over) / Math.Max(1.0, _railWidth);
            var scaleY = (1.0 + press * 0.45) * (1.0 - (Math.Abs(over) / StretchLimit) * 0.32);
            _track.RenderTransformOrigin = new RelativePoint(over < 0 ? 1.0 : 0.0, 0.5, RelativeUnit.Relative);
            _trackScale.ScaleX = scaleX;
            _trackScale.ScaleY = scaleY;
        }

        if (_fill is not null)
            _fill.Width = Math.Max(0.0, thumbPx);

        if (_thumbTranslate is not null && _thumbScale is not null)
        {
            _thumbTranslate.X = thumbPx + over;
            var s = 1.0 + press * 0.14;
            _thumbScale.ScaleX = s;
            _thumbScale.ScaleY = s;
        }

        if (_bubble is not null && _bubbleTranslate is not null && _bubbleRotate is not null && _bubbleScale is not null)
        {
            var bubblePct = Math.Clamp(_bubblePosSpring.Value, 0.0, 100.0);
            var bubbleX = (bubblePct / 100.0) * _railWidth + over;
            var bubbleW = _bubble.Bounds.Width > 0 ? _bubble.Bounds.Width : 44.0;
            var clampedX = Math.Clamp(bubbleX + ThumbSize / 2.0 - bubbleW / 2.0, 0.0, Math.Max(0.0, _railWidth + ThumbSize - bubbleW));

            _bubbleTranslate.X = clampedX;
            _bubbleRotate.Angle = VelaMotionOptions.ReducedMotion
                ? 0.0
                : Math.Clamp((-_bubblePosSpring.Velocity / 100.0) * _railWidth * 0.009, -11.0, 11.0);

            var bs = _bubbleScaleSpring.Value;
            _bubbleScale.ScaleX = bs;
            _bubbleScale.ScaleY = bs;
            _bubble.Opacity = Math.Clamp(_bubbleOpacitySpring.Value, 0.0, 1.0);
        }

        if (_readout is not null)
            _readout.Opacity = Math.Clamp(1.0 - _bubbleOpacitySpring.Value, 0.0, 1.0);

        UpdateTickStates(pct);
    }

    private void RebuildTicks()
    {
        if (_ticksCanvas is null) return;
        _ticksCanvas.Children.Clear();
        var marks = Marks;
        if (marks is null || marks.Count == 0) return;

        foreach (var mark in marks)
        {
            if (mark < Minimum || mark > Maximum) continue;
            var pct = ToPercent(mark);
            var x = (pct / 100.0) * _railWidth;
            var dot = new Border
            {
                Width = 4,
                Height = 4,
                CornerRadius = new CornerRadius(999),
                Tag = pct,
                IsHitTestVisible = false,
            };
            Canvas.SetLeft(dot, x - 2);
            Canvas.SetTop(dot, 2);
            _ticksCanvas.Children.Add(dot);
        }

        UpdateTickStates(ToPercent(Value));
    }

    private void UpdateTickStates(double currentPct)
    {
        if (_ticksCanvas is null || _ticksCanvas.Children.Count == 0) return;
        this.TryFindResource("VelaTickOnBrush", ActualThemeVariant, out var onBrushObj);
        this.TryFindResource("VelaTickOffBrush", ActualThemeVariant, out var offBrushObj);
        var onBrush = onBrushObj as IBrush;
        var offBrush = offBrushObj as IBrush;

        foreach (var child in _ticksCanvas.Children)
        {
            if (child is Border dot && dot.Tag is double pct)
                dot.Background = pct <= currentPct + 0.01 ? onBrush : offBrush;
        }
    }

    private static double RubberBand(double distance, double limit)
        => Math.Sign(distance) * (1.0 - 1.0 / (Math.Abs(distance) * 0.55 / limit + 1.0)) * limit;

    private double ToPercent(double value)
    {
        var span = Maximum - Minimum;
        return Math.Abs(span) < 1e-9 ? 0 : Math.Clamp((value - Minimum) / span * 100.0, 0.0, 100.0);
    }

    private double FromPercent(double pct) => Minimum + (pct / 100.0) * (Maximum - Minimum);

    private double SnapValue(double value)
    {
        var step = Step > 0 ? Step : 1.0;
        var snapped = Minimum + Math.Round((value - Minimum) / step) * step;
        return Math.Clamp(Math.Round(snapped, 6), Minimum, Maximum);
    }

    private void UpdateFormattedValue()
    {
        var text = Math.Abs(Value - Math.Round(Value)) < 1e-6
            ? ((long)Math.Round(Value)).ToString("N0", CultureInfo.InvariantCulture)
            : Value.ToString("0.##", CultureInfo.InvariantCulture);
        FormattedValue = string.IsNullOrEmpty(ValueSuffix) ? text : $"{text}{ValueSuffix}";
    }
}
