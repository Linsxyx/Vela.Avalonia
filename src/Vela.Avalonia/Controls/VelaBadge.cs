using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;

namespace Vela.Avalonia.Controls;

public enum VelaBadgeTone
{
    Neutral,
    Accent,
    Success,
    Warning,
    Danger,
    Info,
}

[PseudoClasses(":neutral", ":accent", ":success", ":warning", ":danger", ":info", ":pill", ":outline", ":sm", ":md", ":lg")]
public class VelaBadge : ContentControl
{
    public static readonly StyledProperty<VelaBadgeTone> ToneProperty =
        AvaloniaProperty.Register<VelaBadge, VelaBadgeTone>(nameof(Tone), VelaBadgeTone.Neutral);

    public static readonly StyledProperty<VelaControlSize> SizeProperty =
        AvaloniaProperty.Register<VelaBadge, VelaControlSize>(nameof(Size), VelaControlSize.Medium);

    public static readonly StyledProperty<bool> ShowDotProperty =
        AvaloniaProperty.Register<VelaBadge, bool>(nameof(ShowDot));

    public static readonly StyledProperty<bool> IsPillProperty =
        AvaloniaProperty.Register<VelaBadge, bool>(nameof(IsPill));

    public static readonly StyledProperty<bool> IsOutlineProperty =
        AvaloniaProperty.Register<VelaBadge, bool>(nameof(IsOutline));

    public VelaBadgeTone Tone { get => GetValue(ToneProperty); set => SetValue(ToneProperty, value); }
    public VelaControlSize Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }
    public bool ShowDot { get => GetValue(ShowDotProperty); set => SetValue(ShowDotProperty, value); }
    public bool IsPill { get => GetValue(IsPillProperty); set => SetValue(IsPillProperty, value); }
    public bool IsOutline { get => GetValue(IsOutlineProperty); set => SetValue(IsOutlineProperty, value); }

    protected override Type StyleKeyOverride => typeof(VelaBadge);

    static VelaBadge()
    {
        ToneProperty.Changed.AddClassHandler<VelaBadge>((b, _) => b.UpdatePseudoClasses());
        SizeProperty.Changed.AddClassHandler<VelaBadge>((b, _) => b.UpdatePseudoClasses());
        IsPillProperty.Changed.AddClassHandler<VelaBadge>((b, _) => b.UpdatePseudoClasses());
        IsOutlineProperty.Changed.AddClassHandler<VelaBadge>((b, _) => b.UpdatePseudoClasses());
    }

    public VelaBadge()
    {
        UpdatePseudoClasses();
    }

    private void UpdatePseudoClasses()
    {
        PseudoClasses.Set(":neutral", Tone == VelaBadgeTone.Neutral);
        PseudoClasses.Set(":accent", Tone == VelaBadgeTone.Accent);
        PseudoClasses.Set(":success", Tone == VelaBadgeTone.Success);
        PseudoClasses.Set(":warning", Tone == VelaBadgeTone.Warning);
        PseudoClasses.Set(":danger", Tone == VelaBadgeTone.Danger);
        PseudoClasses.Set(":info", Tone == VelaBadgeTone.Info);

        PseudoClasses.Set(":sm", Size == VelaControlSize.Small);
        PseudoClasses.Set(":md", Size == VelaControlSize.Medium);
        PseudoClasses.Set(":lg", Size == VelaControlSize.Large);

        PseudoClasses.Set(":pill", IsPill);
        PseudoClasses.Set(":outline", IsOutline);
    }
}
