using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Metadata;
using Vela.Avalonia.Motion;

namespace Vela.Avalonia.Controls;

/// <summary>
/// Port of Arc UI's EmptyState:
/// Clean placeholder panel for empty lists, search misses, or onboarding states,
/// with an icon badge, crisp title/description, optional primary action slot,
/// and subtle spring morph feedback when state changes in place.
/// </summary>
public class VelaEmptyState : TemplatedControl
{
    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<VelaEmptyState, Geometry?>(nameof(Icon));

    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<VelaEmptyState, string?>(nameof(Title));

    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<VelaEmptyState, string?>(nameof(Description));

    public static readonly StyledProperty<object?> ActionContentProperty =
        AvaloniaProperty.Register<VelaEmptyState, object?>(nameof(ActionContent));

    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    [Content]
    public object? ActionContent { get => GetValue(ActionContentProperty); set => SetValue(ActionContentProperty, value); }

    protected override Type StyleKeyOverride => typeof(VelaEmptyState);

    private readonly SpringChannel _morph = new(1.0, VelaMotionTokens.Springs.Snappy);
    private readonly SpringRunner _runner;
    private Border? _iconBadge;
    private PathIcon? _iconPath;
    private ScaleTransform? _iconScale;
    private StackPanel? _copyStack;
    private TranslateTransform? _copyTranslate;

    static VelaEmptyState()
    {
        IconProperty.Changed.AddClassHandler<VelaEmptyState>((s, _) =>
        {
            s.SyncIcon();
            s.TriggerMorph();
        });
        TitleProperty.Changed.AddClassHandler<VelaEmptyState>((s, _) => s.TriggerMorph());
    }

    public VelaEmptyState()
    {
        _runner = new SpringRunner(this, OnTick);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _iconBadge = e.NameScope.Find<Border>("PART_IconBadge");
        _iconPath = e.NameScope.Find<PathIcon>("PART_Icon");
        _copyStack = e.NameScope.Find<StackPanel>("PART_CopyStack");

        if (_iconBadge is not null)
        {
            _iconScale = new ScaleTransform(1, 1);
            _iconBadge.RenderTransformOrigin = RelativePoint.Center;
            _iconBadge.RenderTransform = _iconScale;
        }

        if (_copyStack is not null)
        {
            _copyTranslate = new TranslateTransform(0, 0);
            _copyStack.RenderTransform = _copyTranslate;
        }

        SyncIcon();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _runner.Stop();
    }

    private void SyncIcon()
    {
        if (_iconPath is null)
            return;

        if (Icon is not null)
        {
            _iconPath.Data = Icon;
            return;
        }

        if (this.TryFindResource("VelaIconFolder", ActualThemeVariant, out var res) && res is Geometry fallback)
            _iconPath.Data = fallback;
    }

    private void TriggerMorph()
    {
        if (_iconBadge is null || VelaMotionOptions.ReducedMotion)
            return;

        _morph.Jump(0.0);
        _morph.SetTarget(1.0, VelaMotionTokens.Springs.Snappy);
        ApplyMorph();
        _runner.Wake();
    }

    private bool OnTick(double dt)
    {
        var active = _morph.Step(dt);
        ApplyMorph();
        return active;
    }

    private void ApplyMorph()
    {
        var t = Math.Clamp(_morph.Value, 0.0, 1.15);
        var op = Math.Clamp(t, 0.0, 1.0);

        if (_iconBadge is not null && _iconScale is not null)
        {
            var s = 0.84 + 0.16 * t;
            _iconScale.ScaleX = s;
            _iconScale.ScaleY = s;
            _iconBadge.Opacity = op;
        }

        if (_copyStack is not null && _copyTranslate is not null)
        {
            _copyTranslate.Y = (1.0 - op) * 6.0;
            _copyStack.Opacity = op;
        }
    }
}
