using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Vela.Avalonia.Motion;

namespace Vela.Avalonia.Controls;

/// <summary>
/// Port of Arc UI's Skeleton: calm, restrained opacity pulse for loading placeholders.
/// Can be used either as a standalone shape block or wrapping real content with <see cref="IsLoading"/>.
/// </summary>
[PseudoClasses(":loading", ":circle", ":reduced")]
public class VelaSkeleton : ContentControl
{
    public static readonly StyledProperty<bool> IsLoadingProperty =
        AvaloniaProperty.Register<VelaSkeleton, bool>(nameof(IsLoading), true);

    public static readonly StyledProperty<bool> IsCircleProperty =
        AvaloniaProperty.Register<VelaSkeleton, bool>(nameof(IsCircle));

    public bool IsLoading { get => GetValue(IsLoadingProperty); set => SetValue(IsLoadingProperty, value); }
    public bool IsCircle { get => GetValue(IsCircleProperty); set => SetValue(IsCircleProperty, value); }

    protected override Type StyleKeyOverride => typeof(VelaSkeleton);

    static VelaSkeleton()
    {
        IsLoadingProperty.Changed.AddClassHandler<VelaSkeleton>((s, _) => s.UpdatePseudoClasses());
        IsCircleProperty.Changed.AddClassHandler<VelaSkeleton>((s, _) => s.UpdatePseudoClasses());
    }

    public VelaSkeleton()
    {
        UpdatePseudoClasses();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        VelaMotionOptions.ReducedMotionChanged += OnReducedMotionChanged;
        UpdatePseudoClasses();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        VelaMotionOptions.ReducedMotionChanged -= OnReducedMotionChanged;
    }

    private void OnReducedMotionChanged(object? sender, EventArgs e) => UpdatePseudoClasses();

    private void UpdatePseudoClasses()
    {
        PseudoClasses.Set(":loading", IsLoading);
        PseudoClasses.Set(":circle", IsCircle);
        PseudoClasses.Set(":reduced", VelaMotionOptions.ReducedMotion);
    }
}
