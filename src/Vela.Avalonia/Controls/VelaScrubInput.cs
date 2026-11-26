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
/// Port of Arc UI's Figma-style ScrubInput:
/// - Drag the leading handle horizontally to scrub value smoothly (Shift = 10x, Alt = 0.1x)
/// - Elastic shell strain when scrubbing past min/max bounds with spring snap-back
/// - Quiet left-to-right fill wash showing progress within [Minimum, Maximum]
/// - Direct numeric typing including basic arithmetic (+, -, *, /)
/// </summary>
[PseudoClasses(":scrubbing", ":focused")]
public class VelaScrubInput : TemplatedControl
{
    private const double StrainLimit = 10.0;

    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<VelaScrubInput, string?>(nameof(Label));

    public static readonly StyledProperty<string> HandleTextProperty =
        AvaloniaProperty.Register<VelaScrubInput, string>(nameof(HandleText), "X");

    public static readonly StyledProperty<string?> UnitProperty =
        AvaloniaProperty.Register<VelaScrubInput, string?>(nameof(Unit));

    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<VelaScrubInput, double>(nameof(Value), 0.0, defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<double> MinimumProperty =
        AvaloniaProperty.Register<VelaScrubInput, double>(nameof(Minimum), 0.0);

    public static readonly StyledProperty<double> MaximumProperty =
        AvaloniaProperty.Register<VelaScrubInput, double>(nameof(Maximum), 100.0);

    public static readonly StyledProperty<double> StepProperty =
        AvaloniaProperty.Register<VelaScrubInput, double>(nameof(Step), 1.0);

    public static readonly StyledProperty<double> PixelsPerStepProperty =
        AvaloniaProperty.Register<VelaScrubInput, double>(nameof(PixelsPerStep), 3.0);

    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string HandleText { get => GetValue(HandleTextProperty); set => SetValue(HandleTextProperty, value); }
    public string? Unit { get => GetValue(UnitProperty); set => SetValue(UnitProperty, value); }
    public double Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public double Minimum { get => GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }
    public double Maximum { get => GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }
    public double Step { get => GetValue(StepProperty); set => SetValue(StepProperty, value); }
    public double PixelsPerStep { get => GetValue(PixelsPerStepProperty); set => SetValue(PixelsPerStepProperty, value); }

    public event EventHandler<double>? ValueChanged;

    protected override Type StyleKeyOverride => typeof(VelaScrubInput);

    private readonly SpringChannel _strainSpring = new(0.0, VelaMotionTokens.Springs.SnapBack);
    private readonly SpringChannel _fillSpring = new(0.0, VelaMotionTokens.Springs.Snappy);
    private readonly SpringRunner _runner;

    private Control? _handle;
    private Border? _shell;
    private Border? _fill;
    private TextBox? _inputBox;
    private TranslateTransform? _shellTranslate;
    private ScaleTransform? _fillScale;

    private bool _scrubbing;
    private double _dragStartX;
    private double _dragStartValue;
    private bool _initialized;

    static VelaScrubInput()
    {
        ValueProperty.Changed.AddClassHandler<VelaScrubInput>((s, _) => s.SyncFromValue(animateFill: true));
        MinimumProperty.Changed.AddClassHandler<VelaScrubInput>((s, _) => s.SyncFromValue(animateFill: false));
        MaximumProperty.Changed.AddClassHandler<VelaScrubInput>((s, _) => s.SyncFromValue(animateFill: false));
    }

    public VelaScrubInput()
    {
        _runner = new SpringRunner(this, OnTick);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (_handle is not null)
        {
            _handle.PointerPressed -= OnHandlePointerPressed;
            _handle.PointerMoved -= OnHandlePointerMoved;
            _handle.PointerReleased -= OnHandlePointerReleased;
            _handle.PointerCaptureLost -= OnHandlePointerCaptureLost;
        }
        if (_inputBox is not null)
        {
            _inputBox.KeyDown -= OnInputKeyDown;
            _inputBox.LostFocus -= OnInputLostFocus;
            _inputBox.GotFocus -= OnInputGotFocus;
        }

        _handle = e.NameScope.Find<Control>("PART_Handle");
        _shell = e.NameScope.Find<Border>("PART_Shell");
        _fill = e.NameScope.Find<Border>("PART_Fill");
        _inputBox = e.NameScope.Find<TextBox>("PART_Input");

        if (_shell is not null)
        {
            _shellTranslate = new TranslateTransform();
            _shell.RenderTransform = _shellTranslate;
        }

        if (_fill is not null)
        {
            _fillScale = new ScaleTransform(0, 1);
            _fill.RenderTransformOrigin = new RelativePoint(0, 0.5, RelativeUnit.Relative);
            _fill.RenderTransform = _fillScale;
        }

        if (_handle is not null)
        {
            _handle.PointerPressed += OnHandlePointerPressed;
            _handle.PointerMoved += OnHandlePointerMoved;
            _handle.PointerReleased += OnHandlePointerReleased;
            _handle.PointerCaptureLost += OnHandlePointerCaptureLost;
        }

        if (_inputBox is not null)
        {
            _inputBox.KeyDown += OnInputKeyDown;
            _inputBox.LostFocus += OnInputLostFocus;
            _inputBox.GotFocus += OnInputGotFocus;
        }

        _initialized = true;
        SyncFromValue(animateFill: false);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _runner.Stop();
    }

    private void OnHandlePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!IsEffectivelyEnabled || _handle is null) return;
        if (!e.GetCurrentPoint(_handle).Properties.IsLeftButtonPressed) return;

        _scrubbing = true;
        _dragStartX = e.GetPosition(this).X;
        _dragStartValue = Value;
        e.Pointer.Capture(_handle);
        PseudoClasses.Set(":scrubbing", true);
        e.Handled = true;
    }

    private void OnHandlePointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_scrubbing) return;

        var dx = e.GetPosition(this).X - _dragStartX;
        var multiplier = e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? 10.0
            : e.KeyModifiers.HasFlag(KeyModifiers.Alt) ? 0.1
            : 1.0;

        var step = Math.Max(1e-6, Step);
        var pps = Math.Max(0.5, PixelsPerStep);
        var raw = _dragStartValue + (dx / pps) * step * multiplier;

        var clamped = Math.Clamp(raw, Minimum, Maximum);
        var snapped = Snap(clamped);

        // Compute subtle rubber-band shell strain past bounds
        var overPx = 0.0;
        if (raw < Minimum)
            overPx = RubberBand((raw - Minimum) / step * pps * 0.4, StrainLimit);
        else if (raw > Maximum)
            overPx = RubberBand((raw - Maximum) / step * pps * 0.4, StrainLimit);

        if (VelaMotionOptions.ReducedMotion)
            overPx = 0.0;

        _strainSpring.Jump(overPx);
        CommitValue(snapped);
        ApplyVisuals();
        e.Handled = true;
    }

    private void OnHandlePointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_scrubbing) return;
        EndScrub(e.Pointer);
        e.Handled = true;
    }

    private void OnHandlePointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (_scrubbing) EndScrub(null);
    }

    private void EndScrub(IPointer? pointer)
    {
        _scrubbing = false;
        pointer?.Capture(null);
        PseudoClasses.Set(":scrubbing", false);
        _strainSpring.SetTarget(0.0, VelaMotionTokens.Springs.SnapBack);
        _runner.Wake();
    }

    private void OnInputGotFocus(object? sender, FocusChangedEventArgs e)
    {
        PseudoClasses.Set(":focused", true);
        _inputBox?.SelectAll();
    }

    private void OnInputLostFocus(object? sender, FocusChangedEventArgs e)
    {
        PseudoClasses.Set(":focused", false);
        ParseAndCommitInput();
    }

    private void OnInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ParseAndCommitInput();
            e.Handled = true;
        }
        else if (e.Key == Key.Up || e.Key == Key.Down)
        {
            var dir = e.Key == Key.Up ? 1.0 : -1.0;
            var mult = e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? 10.0 : 1.0;
            CommitValue(Snap(Math.Clamp(Value + dir * Step * mult, Minimum, Maximum)));
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            SyncText();
            e.Handled = true;
        }
    }

    private void ParseAndCommitInput()
    {
        if (_inputBox is null) return;
        if (TryEvaluateExpression(_inputBox.Text, out var parsed))
        {
            CommitValue(Snap(Math.Clamp(parsed, Minimum, Maximum)));
        }
        else
        {
            SyncText();
        }
    }

    /// <summary>Supports plain numbers as well as simple Figma-style binary arithmetic (e.g. "120+24", "360/2").</summary>
    private static bool TryEvaluateExpression(string? text, out double result)
    {
        result = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var s = text.Trim();

        if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out result))
            return double.IsFinite(result);

        // Find last operator after index 0
        for (var i = s.Length - 1; i >= 1; i--)
        {
            var op = s[i];
            if (op is '+' or '-' or '*' or '/')
            {
                if (double.TryParse(s[..i].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var left) &&
                    double.TryParse(s[(i + 1)..].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var right))
                {
                    result = op switch
                    {
                        '+' => left + right,
                        '-' => left - right,
                        '*' => left * right,
                        '/' => Math.Abs(right) > 1e-9 ? left / right : double.NaN,
                        _ => double.NaN,
                    };
                    return double.IsFinite(result);
                }
            }
        }

        return false;
    }

    private void CommitValue(double next)
    {
        if (Math.Abs(next - Value) > 1e-9)
        {
            SetCurrentValue(ValueProperty, next);
            ValueChanged?.Invoke(this, next);
        }
        SyncFromValue(animateFill: !_scrubbing);
    }

    private void SyncFromValue(bool animateFill)
    {
        if (!_initialized) return;
        SyncText();

        var span = Maximum - Minimum;
        var ratio = Math.Abs(span) < 1e-9 ? 0.0 : Math.Clamp((Value - Minimum) / span, 0.0, 1.0);

        if (!animateFill || VelaMotionOptions.ReducedMotion)
        {
            _fillSpring.Jump(ratio);
            ApplyVisuals();
            return;
        }

        _fillSpring.SetTarget(ratio, VelaMotionTokens.Springs.Snappy);
        _runner.Wake();
    }

    private void SyncText()
    {
        if (_inputBox is null) return;
        var formatted = Math.Abs(Value - Math.Round(Value)) < 1e-6
            ? ((long)Math.Round(Value)).ToString(CultureInfo.InvariantCulture)
            : Value.ToString("0.##", CultureInfo.InvariantCulture);
        if (_inputBox.Text != formatted)
            _inputBox.Text = formatted;
    }

    private double Snap(double v)
    {
        var step = Step > 0 ? Step : 1.0;
        var snapped = Minimum + Math.Round((v - Minimum) / step) * step;
        return Math.Clamp(Math.Round(snapped, 4), Minimum, Maximum);
    }

    private static double RubberBand(double distance, double limit)
        => Math.Sign(distance) * (1.0 - 1.0 / (Math.Abs(distance) * 0.55 / limit + 1.0)) * limit;

    private bool OnTick(double dt)
    {
        var active = _strainSpring.Step(dt);
        active |= _fillSpring.Step(dt);
        ApplyVisuals();
        return active;
    }

    private void ApplyVisuals()
    {
        if (_shellTranslate is not null)
            _shellTranslate.X = _strainSpring.Value;
        if (_fillScale is not null)
            _fillScale.ScaleX = Math.Clamp(_fillSpring.Value, 0.0, 1.0);
    }
}
