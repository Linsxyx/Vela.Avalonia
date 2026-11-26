using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Vela.Avalonia.Motion;

namespace Vela.Avalonia.Controls;

/// <summary>
/// Port of Arc UI's AnimatedCounter: smoothly rolls a numeric value via physical spring physics.
/// </summary>
public class VelaAnimatedCounter : TextBlock
{
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<VelaAnimatedCounter, double>(nameof(Value), 0.0);

    public static readonly StyledProperty<string> PrefixProperty =
        AvaloniaProperty.Register<VelaAnimatedCounter, string>(nameof(Prefix), string.Empty);

    public static readonly StyledProperty<string> SuffixProperty =
        AvaloniaProperty.Register<VelaAnimatedCounter, string>(nameof(Suffix), string.Empty);

    public static readonly StyledProperty<int> DecimalsProperty =
        AvaloniaProperty.Register<VelaAnimatedCounter, int>(nameof(Decimals), 0);

    public double Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public string Prefix { get => GetValue(PrefixProperty); set => SetValue(PrefixProperty, value); }
    public string Suffix { get => GetValue(SuffixProperty); set => SetValue(SuffixProperty, value); }
    public int Decimals { get => GetValue(DecimalsProperty); set => SetValue(DecimalsProperty, Math.Clamp(value, 0, 4)); }

    private readonly SpringChannel _spring = new(0.0, VelaMotionTokens.Springs.Smooth);
    private readonly SpringRunner _runner;
    private bool _hasInitialized;

    static VelaAnimatedCounter()
    {
        ValueProperty.Changed.AddClassHandler<VelaAnimatedCounter>((c, _) => c.OnTargetChanged());
        PrefixProperty.Changed.AddClassHandler<VelaAnimatedCounter>((c, _) => c.RenderCurrent());
        SuffixProperty.Changed.AddClassHandler<VelaAnimatedCounter>((c, _) => c.RenderCurrent());
        DecimalsProperty.Changed.AddClassHandler<VelaAnimatedCounter>((c, _) => c.RenderCurrent());
    }

    public VelaAnimatedCounter()
    {
        _runner = new SpringRunner(this, OnTick);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (!_hasInitialized)
        {
            _hasInitialized = true;
            _spring.Jump(Value);
            RenderCurrent();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _runner.Stop();
    }

    private void OnTargetChanged()
    {
        if (!_hasInitialized || VelaMotionOptions.ReducedMotion)
        {
            _hasInitialized = true;
            _spring.Jump(Value);
            RenderCurrent();
            return;
        }

        var delta = Math.Max(Math.Abs(Value - _spring.Value) * 0.001, 1e-4);
        var spec = SpringSpec.FromVisualDuration(0.45, bounce: 0.0, restDelta: delta, restSpeed: delta * 4);
        _spring.SetTarget(Value, spec);
        _runner.Wake();
    }

    private bool OnTick(double dt)
    {
        var active = _spring.Step(dt);
        RenderCurrent();
        return active;
    }

    private void RenderCurrent()
    {
        var v = _spring.Value;
        var num = Decimals == 0
            ? ((long)Math.Round(v)).ToString("N0", CultureInfo.InvariantCulture)
            : v.ToString($"N{Decimals}", CultureInfo.InvariantCulture);
        Text = $"{Prefix}{num}{Suffix}";
    }
}

/// <summary>
/// Port of Arc UI's MetricCard: compact KPI summary card featuring a rolling <see cref="VelaAnimatedCounter"/>,
/// optional delta badge, icon, and supporting caption.
/// </summary>
public class VelaMetricCard : TemplatedControl
{
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<VelaMetricCard, string?>(nameof(Label));

    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<VelaMetricCard, double>(nameof(Value), 0.0);

    public static readonly StyledProperty<string> PrefixProperty =
        AvaloniaProperty.Register<VelaMetricCard, string>(nameof(Prefix), string.Empty);

    public static readonly StyledProperty<string> SuffixProperty =
        AvaloniaProperty.Register<VelaMetricCard, string>(nameof(Suffix), string.Empty);

    public static readonly StyledProperty<int> DecimalsProperty =
        AvaloniaProperty.Register<VelaMetricCard, int>(nameof(Decimals), 0);

    public static readonly StyledProperty<string?> DeltaProperty =
        AvaloniaProperty.Register<VelaMetricCard, string?>(nameof(Delta));

    public static readonly StyledProperty<VelaBadgeTone> DeltaToneProperty =
        AvaloniaProperty.Register<VelaMetricCard, VelaBadgeTone>(nameof(DeltaTone), VelaBadgeTone.Success);

    public static readonly StyledProperty<string?> CaptionProperty =
        AvaloniaProperty.Register<VelaMetricCard, string?>(nameof(Caption));

    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<VelaMetricCard, Geometry?>(nameof(Icon));

    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public double Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public string Prefix { get => GetValue(PrefixProperty); set => SetValue(PrefixProperty, value); }
    public string Suffix { get => GetValue(SuffixProperty); set => SetValue(SuffixProperty, value); }
    public int Decimals { get => GetValue(DecimalsProperty); set => SetValue(DecimalsProperty, value); }
    public string? Delta { get => GetValue(DeltaProperty); set => SetValue(DeltaProperty, value); }
    public VelaBadgeTone DeltaTone { get => GetValue(DeltaToneProperty); set => SetValue(DeltaToneProperty, value); }
    public string? Caption { get => GetValue(CaptionProperty); set => SetValue(CaptionProperty, value); }
    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    protected override Type StyleKeyOverride => typeof(VelaMetricCard);
}
