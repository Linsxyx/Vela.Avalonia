using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Vela.Avalonia.Motion;

namespace Vela.Avalonia.Controls;

/// <summary>
/// Port of Arc UI's Progress bar:
/// - Smooth critically damped spring fill and rolling percentage readout
/// - Automatic completion transition (accent -> success green + check icon at 100%)
/// - Optional indeterminate sliding animation
/// </summary>
[PseudoClasses(":complete", ":indeterminate")]
public class VelaProgress : TemplatedControl
{
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<VelaProgress, string?>(nameof(Label));

    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<VelaProgress, double>(nameof(Value), 0.0);

    public static readonly StyledProperty<double> MinimumProperty =
        AvaloniaProperty.Register<VelaProgress, double>(nameof(Minimum), 0.0);

    public static readonly StyledProperty<double> MaximumProperty =
        AvaloniaProperty.Register<VelaProgress, double>(nameof(Maximum), 100.0);

    public static readonly StyledProperty<bool> ShowValueProperty =
        AvaloniaProperty.Register<VelaProgress, bool>(nameof(ShowValue), true);

    public static readonly StyledProperty<bool> IsIndeterminateProperty =
        AvaloniaProperty.Register<VelaProgress, bool>(nameof(IsIndeterminate));

    public static readonly DirectProperty<VelaProgress, string> DisplayPercentProperty =
        AvaloniaProperty.RegisterDirect<VelaProgress, string>(nameof(DisplayPercent), o => o.DisplayPercent);

    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public double Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public double Minimum { get => GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }
    public double Maximum { get => GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }
    public bool ShowValue { get => GetValue(ShowValueProperty); set => SetValue(ShowValueProperty, value); }
    public bool IsIndeterminate { get => GetValue(IsIndeterminateProperty); set => SetValue(IsIndeterminateProperty, value); }

    private string _displayPercent = "0%";
    public string DisplayPercent
    {
        get => _displayPercent;
        private set => SetAndRaise(DisplayPercentProperty, ref _displayPercent, value);
    }

    protected override Type StyleKeyOverride => typeof(VelaProgress);

    private readonly SpringChannel _ratioSpring = new(0.0, VelaMotionTokens.Springs.Smooth);
    private readonly SpringRunner _runner;

    private Border? _track;
    private Border? _fill;
    private ScaleTransform? _fillScale;
    private bool _initialized;

    static VelaProgress()
    {
        ValueProperty.Changed.AddClassHandler<VelaProgress>((p, _) => p.SyncProgress(animate: true));
        MinimumProperty.Changed.AddClassHandler<VelaProgress>((p, _) => p.SyncProgress(animate: true));
        MaximumProperty.Changed.AddClassHandler<VelaProgress>((p, _) => p.SyncProgress(animate: true));
        IsIndeterminateProperty.Changed.AddClassHandler<VelaProgress>((p, _) => p.UpdatePseudoClasses());
    }

    public VelaProgress()
    {
        _runner = new SpringRunner(this, OnTick);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        _track = e.NameScope.Find<Border>("PART_Track");
        _fill = e.NameScope.Find<Border>("PART_Fill");

        if (_fill is not null)
        {
            _fillScale = new ScaleTransform(0.0, 1.0);
            _fill.RenderTransformOrigin = new RelativePoint(0.0, 0.5, RelativeUnit.Relative);
            _fill.RenderTransform = _fillScale;
        }

        _initialized = true;
        UpdatePseudoClasses();
        SyncProgress(animate: false);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _runner.Stop();
    }

    private double ComputeRatio()
    {
        var span = Maximum - Minimum;
        if (Math.Abs(span) < 1e-9) return 0.0;
        return Math.Clamp((Value - Minimum) / span, 0.0, 1.0);
    }

    private void SyncProgress(bool animate)
    {
        var ratio = ComputeRatio();
        UpdatePseudoClasses();

        if (!_initialized)
        {
            DisplayPercent = $"{Math.Round(ratio * 100)}%";
            return;
        }

        if (!animate || VelaMotionOptions.ReducedMotion)
        {
            _ratioSpring.Jump(ratio);
            ApplyVisuals();
            return;
        }

        _ratioSpring.SetTarget(ratio, VelaMotionTokens.Springs.Smooth);
        _runner.Wake();
    }

    private bool OnTick(double dt)
    {
        var active = _ratioSpring.Step(dt);
        ApplyVisuals();
        return active;
    }

    private void ApplyVisuals()
    {
        var ratio = Math.Clamp(_ratioSpring.Value, 0.0, 1.0);
        if (_fillScale is not null && !IsIndeterminate)
            _fillScale.ScaleX = ratio;

        DisplayPercent = string.Format(CultureInfo.InvariantCulture, "{0:0}%", Math.Round(ratio * 100.0));
    }

    private void UpdatePseudoClasses()
    {
        var isComplete = !IsIndeterminate && ComputeRatio() >= 1.0 - 1e-6;
        PseudoClasses.Set(":complete", isComplete);
        PseudoClasses.Set(":indeterminate", IsIndeterminate);
    }
}
