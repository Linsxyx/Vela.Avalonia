using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;
using Vela.Avalonia.Motion;

namespace Vela.Avalonia.Controls;

/// <summary>
/// Precision sliding ruler tape renderer used inside <see cref="VelaRulerSlider"/>.
/// Draws minor, half-major, and major ticks with numeric labels centered around a fixed needle.
/// </summary>
public sealed class VelaRulerTape : Control
{
    internal double Position { get; set; }
    internal double Minimum { get; set; }
    internal double Maximum { get; set; } = 100.0;
    internal double Step { get; set; } = 1.0;
    internal int MajorEvery { get; set; } = 10;
    internal double TickSpacing { get; set; } = 10.0;
    internal string FormatString { get; set; } = "0";
    internal bool IsDragging { get; set; }

    public VelaRulerTape()
    {
        IsHitTestVisible = false;
        ClipToBounds = true;
    }

    public override void Render(DrawingContext context)
    {
        var w = Bounds.Width;
        var h = Bounds.Height;
        if (w <= 4 || h <= 4 || Step <= 0 || TickSpacing <= 0)
            return;

        var centerX = w * 0.5;
        var perUnit = TickSpacing / Step;

        var minorBrush = ResolveBrush("VelaBorderStrongBrush", Brushes.Gray);
        var majorBrush = ResolveBrush("VelaTextSecondaryBrush", Brushes.Gray);
        var labelBrush = ResolveBrush("VelaTextMutedBrush", Brushes.Gray);
        var accentBrush = ResolveBrush("VelaAccentBrush", Brushes.DodgerBlue);

        var minorPen = new Pen(minorBrush, 1.0);
        var halfPen = new Pen(majorBrush, 1.15);
        var majorPen = new Pen(majorBrush, 1.4);

        var totalTicks = (int)Math.Round((Maximum - Minimum) / Step);
        var halfSpanUnits = (w * 0.5 + 40.0) / perUnit;
        var firstIdx = Math.Max(0, (int)Math.Floor((Position - halfSpanUnits - Minimum) / Step));
        var lastIdx = Math.Min(totalTicks, (int)Math.Ceiling((Position + halfSpanUnits - Minimum) / Step));

        var halfEvery = MajorEvery % 2 == 0 ? MajorEvery / 2 : -1;
        var baseLineY = 10.0;
        var typeface = new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.Medium);

        for (var i = firstIdx; i <= lastIdx; i++)
        {
            var tickVal = Minimum + i * Step;
            var x = centerX + (tickVal - Position) * perUnit;
            if (x < -30 || x > w + 30)
                continue;

            var isMajor = MajorEvery > 0 && i % MajorEvery == 0;
            var isHalf = !isMajor && halfEvery > 0 && i % halfEvery == 0;

            var distFromCenter = Math.Abs(x - centerX);
            var proximity = Math.Clamp(1.0 - distFromCenter / 44.0, 0.0, 1.0);
            var tickH = (isMajor ? 22.0 : isHalf ? 15.0 : 9.0) + proximity * (IsDragging ? 4.5 : 2.5);
            var edgeFade = Math.Clamp(Math.Min(x, w - x) / 48.0, 0.0, 1.0);

            using (context.PushOpacity(edgeFade * (isMajor ? 0.95 : 0.72)))
            {
                var pen = isMajor ? majorPen : isHalf ? halfPen : minorPen;
                context.DrawLine(pen, new Point(x, baseLineY), new Point(x, baseLineY + tickH));

                if (isMajor)
                {
                    var labelText = tickVal.ToString(FormatString, CultureInfo.InvariantCulture);
                    var ft = new FormattedText(
                        labelText,
                        CultureInfo.InvariantCulture,
                        FlowDirection.LeftToRight,
                        typeface,
                        10.5,
                        labelBrush);
                    context.DrawText(ft, new Point(x - ft.Width * 0.5, baseLineY + 28.0));
                }
            }
        }

        // Center fixed accent needle
        var needlePen = new Pen(accentBrush, 2.0, lineCap: PenLineCap.Round);
        context.DrawLine(needlePen, new Point(centerX, 4.0), new Point(centerX, 34.0));
        context.DrawEllipse(accentBrush, null, new Point(centerX, 5.0), 3.0, 3.0);
    }

    private IBrush ResolveBrush(string key, IBrush fallback) =>
        this.TryFindResource(key, ActualThemeVariant, out var res) && res is IBrush b ? b : fallback;
}

/// <summary>
/// Port of Arc UI's RulerSlider:
/// Precision tape-measure input where a ruler of ticks slides beneath a fixed accent needle.
/// Supports 1:1 dragging, elastic rubber-banding past bounds, flick inertia coasting onto ticks,
/// tap-to-glide, mouse wheel fine stepping, and full keyboard arrow/Page/Home/End navigation.
/// </summary>
[PseudoClasses(":dragging")]
public class VelaRulerSlider : TemplatedControl
{
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<VelaRulerSlider, string?>(nameof(Label));

    public static readonly StyledProperty<string?> UnitProperty =
        AvaloniaProperty.Register<VelaRulerSlider, string?>(nameof(Unit));

    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<VelaRulerSlider, double>(
            nameof(Value),
            50.0,
            defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<double> MinimumProperty =
        AvaloniaProperty.Register<VelaRulerSlider, double>(nameof(Minimum), 0.0);

    public static readonly StyledProperty<double> MaximumProperty =
        AvaloniaProperty.Register<VelaRulerSlider, double>(nameof(Maximum), 100.0);

    public static readonly StyledProperty<double> StepProperty =
        AvaloniaProperty.Register<VelaRulerSlider, double>(nameof(Step), 1.0);

    public static readonly StyledProperty<int> MajorEveryProperty =
        AvaloniaProperty.Register<VelaRulerSlider, int>(nameof(MajorEvery), 10);

    public static readonly StyledProperty<double> TickSpacingProperty =
        AvaloniaProperty.Register<VelaRulerSlider, double>(nameof(TickSpacing), 10.0);

    public static readonly StyledProperty<string> FormatStringProperty =
        AvaloniaProperty.Register<VelaRulerSlider, string>(nameof(FormatString), "0");

    public static readonly DirectProperty<VelaRulerSlider, string> FormattedValueProperty =
        AvaloniaProperty.RegisterDirect<VelaRulerSlider, string>(
            nameof(FormattedValue),
            o => o.FormattedValue);

    private string _formattedValue = "50";

    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string? Unit { get => GetValue(UnitProperty); set => SetValue(UnitProperty, value); }
    public double Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public double Minimum { get => GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }
    public double Maximum { get => GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }
    public double Step { get => GetValue(StepProperty); set => SetValue(StepProperty, value); }
    public int MajorEvery { get => GetValue(MajorEveryProperty); set => SetValue(MajorEveryProperty, value); }
    public double TickSpacing { get => GetValue(TickSpacingProperty); set => SetValue(TickSpacingProperty, value); }
    public string FormatString { get => GetValue(FormatStringProperty); set => SetValue(FormatStringProperty, value); }

    public string FormattedValue
    {
        get => _formattedValue;
        private set => SetAndRaise(FormattedValueProperty, ref _formattedValue, value);
    }

    public event EventHandler<double>? ValueChanged;
    public event EventHandler<double>? ValueCommitted;

    protected override Type StyleKeyOverride => typeof(VelaRulerSlider);

    private const double RubberLimitPx = 44.0;
    private const double TapSlopPx = 4.0;

    private readonly SpringChannel _posSpring = new(50.0, VelaMotionTokens.Springs.Snappy);
    private readonly SpringRunner _runner;
    private Border? _viewport;
    private VelaRulerTape? _tape;

    private bool _dragging;
    private bool _moved;
    private bool _coasting;
    private double _coastVelocity; // units/sec
    private double _dragStartX;
    private double _dragStartPos;
    private readonly List<(long ticks, double pos)> _samples = new(8);
    private double _internalEmitted;

    static VelaRulerSlider()
    {
        FocusableProperty.OverrideDefaultValue<VelaRulerSlider>(true);
        ValueProperty.Changed.AddClassHandler<VelaRulerSlider>((s, _) => s.OnExternalValueChanged());
        MinimumProperty.Changed.AddClassHandler<VelaRulerSlider>((s, _) => s.SyncTape(redraw: true));
        MaximumProperty.Changed.AddClassHandler<VelaRulerSlider>((s, _) => s.SyncTape(redraw: true));
        StepProperty.Changed.AddClassHandler<VelaRulerSlider>((s, _) => s.SyncTape(redraw: true));
        MajorEveryProperty.Changed.AddClassHandler<VelaRulerSlider>((s, _) => s.SyncTape(redraw: true));
        TickSpacingProperty.Changed.AddClassHandler<VelaRulerSlider>((s, _) => s.SyncTape(redraw: true));
        FormatStringProperty.Changed.AddClassHandler<VelaRulerSlider>((s, _) => s.SyncTape(redraw: true));
    }

    public VelaRulerSlider()
    {
        _runner = new SpringRunner(this, OnTick);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (_viewport is not null)
        {
            _viewport.PointerPressed -= OnViewportPointerPressed;
            _viewport.PointerMoved -= OnViewportPointerMoved;
            _viewport.PointerReleased -= OnViewportPointerReleased;
            _viewport.PointerCaptureLost -= OnViewportPointerCaptureLost;
            _viewport.PointerWheelChanged -= OnViewportWheelChanged;
        }

        _viewport = e.NameScope.Find<Border>("PART_Viewport");
        _tape = e.NameScope.Find<VelaRulerTape>("PART_Tape");

        if (_viewport is not null)
        {
            _viewport.PointerPressed += OnViewportPointerPressed;
            _viewport.PointerMoved += OnViewportPointerMoved;
            _viewport.PointerReleased += OnViewportPointerReleased;
            _viewport.PointerCaptureLost += OnViewportPointerCaptureLost;
            _viewport.PointerWheelChanged += OnViewportWheelChanged;
        }

        var snapped = Snap(Value);
        _internalEmitted = snapped;
        _posSpring.Jump(snapped);
        SyncTape(redraw: true);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _runner.Stop();
    }

    private double SafeStep => Step > 0 ? Step : 1.0;
    private double SafeMax => Math.Max(Minimum, Maximum);
    private double PerUnit => Math.Max(1.0, TickSpacing) / SafeStep;

    private double Snap(double raw)
    {
        var step = SafeStep;
        var min = Minimum;
        var max = SafeMax;
        var clamped = Math.Clamp(raw, min, max);
        var snapped = min + Math.Round((clamped - min) / step) * step;
        return Math.Clamp(Math.Round(snapped, 6), min, max);
    }

    private static double Rubber(double distancePx)
    {
        var abs = Math.Abs(distancePx);
        return Math.Sign(distancePx) * (1.0 - 1.0 / (abs * 0.55 / RubberLimitPx + 1.0)) * RubberLimitPx;
    }

    private void EmitValue(double next)
    {
        var snapped = Snap(next);
        if (Math.Abs(_internalEmitted - snapped) < 1e-7 && Math.Abs(Value - snapped) < 1e-7)
            return;

        _internalEmitted = snapped;
        SetCurrentValue(ValueProperty, snapped);
        FormattedValue = snapped.ToString(FormatString, CultureInfo.InvariantCulture);
        ValueChanged?.Invoke(this, snapped);
    }

    private void OnExternalValueChanged()
    {
        var snapped = Snap(Value);
        FormattedValue = snapped.ToString(FormatString, CultureInfo.InvariantCulture);
        if (Math.Abs(snapped - _internalEmitted) < 1e-7)
            return;

        _internalEmitted = snapped;
        _coasting = false;
        if (VelaMotionOptions.ReducedMotion)
        {
            _posSpring.Jump(snapped);
            SyncTape(redraw: true);
        }
        else
        {
            _posSpring.SetTarget(snapped, VelaMotionTokens.Springs.Snappy);
            _runner.Wake();
        }
    }

    private void GlideTo(double target, bool commit)
    {
        var snapped = Snap(target);
        _coasting = false;
        EmitValue(snapped);

        if (VelaMotionOptions.ReducedMotion)
        {
            _posSpring.Jump(snapped);
            SyncTape(redraw: true);
        }
        else
        {
            _posSpring.SetTarget(snapped, VelaMotionTokens.Springs.Snappy);
            _runner.Wake();
        }

        if (commit)
            ValueCommitted?.Invoke(this, snapped);
    }

    private void OnViewportPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!IsEnabled || _viewport is null) return;
        if (!e.GetCurrentPoint(_viewport).Properties.IsLeftButtonPressed) return;

        Focus();
        e.Pointer.Capture(_viewport);
        _coasting = false;
        _dragging = true;
        _moved = false;
        _dragStartX = e.GetPosition(_viewport).X;
        _dragStartPos = _posSpring.Value;
        _samples.Clear();
        _samples.Add((Environment.TickCount64, _dragStartPos));
        PseudoClasses.Set(":dragging", true);
        if (_tape is not null) _tape.IsDragging = true;
        e.Handled = true;
    }

    private void OnViewportPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_dragging || _viewport is null) return;

        var x = e.GetPosition(_viewport).X;
        var dx = x - _dragStartX;
        if (!_moved)
        {
            if (Math.Abs(dx) < TapSlopPx) return;
            _moved = true;
        }

        var raw = _dragStartPos - dx / PerUnit;
        var edge = Math.Clamp(raw, Minimum, SafeMax);
        var placed = VelaMotionOptions.ReducedMotion
            ? edge
            : edge + Rubber((raw - edge) * PerUnit) / PerUnit;

        _posSpring.Jump(placed);
        _samples.Add((Environment.TickCount64, placed));
        if (_samples.Count > 6)
            _samples.RemoveAt(0);

        EmitValue(edge);
        SyncTape(redraw: true);
        e.Handled = true;
    }

    private void OnViewportPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_dragging || _viewport is null) return;
        var endX = e.GetPosition(_viewport).X;
        e.Pointer.Capture(null);
        FinishDrag(endX, cancelled: false);
        e.Handled = true;
    }

    private void OnViewportPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (!_dragging) return;
        FinishDrag(_dragStartX, cancelled: true);
    }

    private void FinishDrag(double endX, bool cancelled)
    {
        _dragging = false;
        PseudoClasses.Set(":dragging", false);
        if (_tape is not null) _tape.IsDragging = false;

        if (!_moved && !cancelled && _viewport is not null)
        {
            // Tap to glide tapped tick under the needle
            var offsetPx = endX - _viewport.Bounds.Width * 0.5;
            GlideTo(_posSpring.Value + offsetPx / PerUnit, commit: true);
            return;
        }

        if (VelaMotionOptions.ReducedMotion || cancelled)
        {
            GlideTo(_posSpring.Value, commit: true);
            return;
        }

        // Compute flick velocity from recent samples
        double velocity = 0;
        if (_samples.Count >= 2)
        {
            var first = _samples[0];
            var last = _samples[^1];
            var dtSec = (last.ticks - first.ticks) / 1000.0;
            if (dtSec > 0.008 && Environment.TickCount64 - last.ticks < 70)
                velocity = (last.pos - first.pos) / dtSec;
        }

        var cur = _posSpring.Value;
        var max = SafeMax;
        if (cur < Minimum || cur > max)
        {
            _coasting = false;
            var edge = Math.Clamp(cur, Minimum, max);
            _posSpring.SetTarget(edge, VelaMotionTokens.Springs.Snappy);
            EmitValue(edge);
            ValueCommitted?.Invoke(this, edge);
            _runner.Wake();
            return;
        }

        if (Math.Abs(velocity) > SafeStep * 3.0)
        {
            _coasting = true;
            _coastVelocity = Math.Clamp(velocity, -SafeStep * 220, SafeStep * 220);
            _runner.Wake();
        }
        else
        {
            GlideTo(cur, commit: true);
        }
    }

    private void OnViewportWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (!IsEnabled) return;
        var delta = Math.Abs(e.Delta.X) > Math.Abs(e.Delta.Y) ? -e.Delta.X : -e.Delta.Y;
        if (Math.Abs(delta) < 0.01) return;

        var step = e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? SafeStep * Math.Max(1, MajorEvery) : SafeStep;
        GlideTo(Value + Math.Sign(delta) * step, commit: true);
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!IsEnabled) return;

        var big = SafeStep * Math.Max(1, MajorEvery);
        var step = e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? big : SafeStep;

        double? next = e.Key switch
        {
            Key.Right or Key.Up => Value + step,
            Key.Left or Key.Down => Value - step,
            Key.PageUp => Value + big,
            Key.PageDown => Value - big,
            Key.Home => Minimum,
            Key.End => Maximum,
            _ => null,
        };

        if (next.HasValue)
        {
            GlideTo(next.Value, commit: true);
            e.Handled = true;
        }
    }

    private bool OnTick(double dt)
    {
        if (_coasting)
        {
            var next = _posSpring.Value + _coastVelocity * dt;
            var decay = Math.Exp(-dt * 4.8);
            _coastVelocity *= decay;

            var max = SafeMax;
            if (next < Minimum || next > max)
            {
                _coasting = false;
                var clamped = Math.Clamp(next, Minimum, max);
                _posSpring.Jump(clamped);
                _posSpring.SetTarget(clamped, VelaMotionTokens.Springs.Snappy);
                EmitValue(clamped);
                ValueCommitted?.Invoke(this, clamped);
                SyncTape(redraw: true);
                return true;
            }

            _posSpring.Jump(next);
            EmitValue(next);
            SyncTape(redraw: true);

            if (Math.Abs(_coastVelocity) < SafeStep * 1.4)
            {
                _coasting = false;
                var landed = Snap(next);
                _posSpring.SetTarget(landed, VelaMotionTokens.Springs.Snappy);
                EmitValue(landed);
                ValueCommitted?.Invoke(this, landed);
            }

            return true;
        }

        var active = _posSpring.Step(dt);
        SyncTape(redraw: true);
        return active;
    }

    private void SyncTape(bool redraw)
    {
        FormattedValue = Snap(Value).ToString(FormatString, CultureInfo.InvariantCulture);

        if (_tape is null) return;
        _tape.Position = _posSpring.Value;
        _tape.Minimum = Minimum;
        _tape.Maximum = Maximum;
        _tape.Step = SafeStep;
        _tape.MajorEvery = Math.Max(1, MajorEvery);
        _tape.TickSpacing = Math.Max(4.0, TickSpacing);
        _tape.FormatString = FormatString;

        if (redraw)
            _tape.InvalidateVisual();
    }
}
