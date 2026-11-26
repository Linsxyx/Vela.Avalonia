using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Metadata;
using Vela.Avalonia.Motion;

namespace Vela.Avalonia.Controls;

public enum VelaAvatarSize
{
    Small,
    Medium,
    Large,
    XLarge,
}

public enum VelaAvatarStatus
{
    None,
    Online,
    Busy,
    Away,
    Offline,
}

public enum VelaAvatarTone
{
    Neutral,
    Accent,
    Teal,
    Sky,
    Violet,
    Rose,
    Amber,
    Green,
}

/// <summary>
/// Port of Arc UI's Avatar:
/// - Circular portrait with image support, clean auto-computed initials fallback, tonal surface options,
///   and a spring-animated presence status indicator dot.
/// </summary>
[PseudoClasses(":sm", ":md", ":lg", ":xl", ":has-image")]
public class VelaAvatar : TemplatedControl
{
    public static readonly StyledProperty<string?> DisplayNameProperty =
        AvaloniaProperty.Register<VelaAvatar, string?>(nameof(DisplayName));

    public static readonly StyledProperty<string?> InitialsProperty =
        AvaloniaProperty.Register<VelaAvatar, string?>(nameof(Initials));

    public static readonly StyledProperty<IImage?> SourceProperty =
        AvaloniaProperty.Register<VelaAvatar, IImage?>(nameof(Source));

    public static readonly StyledProperty<VelaAvatarSize> SizeProperty =
        AvaloniaProperty.Register<VelaAvatar, VelaAvatarSize>(nameof(Size), VelaAvatarSize.Medium);

    public static readonly StyledProperty<VelaAvatarStatus> StatusProperty =
        AvaloniaProperty.Register<VelaAvatar, VelaAvatarStatus>(nameof(Status), VelaAvatarStatus.None);

    public static readonly StyledProperty<VelaAvatarTone> ToneProperty =
        AvaloniaProperty.Register<VelaAvatar, VelaAvatarTone>(nameof(Tone), VelaAvatarTone.Neutral);

    public static readonly DirectProperty<VelaAvatar, string> ComputedInitialsProperty =
        AvaloniaProperty.RegisterDirect<VelaAvatar, string>(
            nameof(ComputedInitials),
            o => o.ComputedInitials);

    private string _computedInitials = string.Empty;

    public string? DisplayName { get => GetValue(DisplayNameProperty); set => SetValue(DisplayNameProperty, value); }
    public string? Initials { get => GetValue(InitialsProperty); set => SetValue(InitialsProperty, value); }
    public IImage? Source { get => GetValue(SourceProperty); set => SetValue(SourceProperty, value); }
    public VelaAvatarSize Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }
    public VelaAvatarStatus Status { get => GetValue(StatusProperty); set => SetValue(StatusProperty, value); }
    public VelaAvatarTone Tone { get => GetValue(ToneProperty); set => SetValue(ToneProperty, value); }

    public string ComputedInitials
    {
        get => _computedInitials;
        private set => SetAndRaise(ComputedInitialsProperty, ref _computedInitials, value);
    }

    protected override Type StyleKeyOverride => typeof(VelaAvatar);

    private readonly SpringChannel _statusScale = new(0.0, VelaMotionTokens.Springs.Snappy);
    private readonly SpringRunner _runner;
    private Border? _circle;
    private TextBlock? _initialsText;
    private Border? _statusDot;
    private ScaleTransform? _dotScale;

    static VelaAvatar()
    {
        DisplayNameProperty.Changed.AddClassHandler<VelaAvatar>((a, _) => a.UpdateInitials());
        InitialsProperty.Changed.AddClassHandler<VelaAvatar>((a, _) => a.UpdateInitials());
        SourceProperty.Changed.AddClassHandler<VelaAvatar>((a, _) => a.UpdateImageState());
        SizeProperty.Changed.AddClassHandler<VelaAvatar>((a, _) => a.UpdateSize());
        StatusProperty.Changed.AddClassHandler<VelaAvatar>((a, _) => a.UpdateStatus(animate: true));
        ToneProperty.Changed.AddClassHandler<VelaAvatar>((a, _) => a.UpdateTone());
    }

    public VelaAvatar()
    {
        _runner = new SpringRunner(this, OnTick);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _circle = e.NameScope.Find<Border>("PART_Circle");
        _initialsText = e.NameScope.Find<TextBlock>("PART_Initials");
        _statusDot = e.NameScope.Find<Border>("PART_StatusDot");

        if (_statusDot is not null)
        {
            _dotScale = new ScaleTransform(Status == VelaAvatarStatus.None ? 0.0 : 1.0, Status == VelaAvatarStatus.None ? 0.0 : 1.0);
            _statusDot.RenderTransformOrigin = RelativePoint.Center;
            _statusDot.RenderTransform = _dotScale;
        }

        UpdateInitials();
        UpdateImageState();
        UpdateSize();
        UpdateTone();
        UpdateStatus(animate: false);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        VelaTheme.AccentChanged += OnAccentChanged;
        ActualThemeVariantChanged += OnThemeChanged;
        UpdateTone();
        UpdateStatus(animate: false);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        VelaTheme.AccentChanged -= OnAccentChanged;
        ActualThemeVariantChanged -= OnThemeChanged;
        _runner.Stop();
    }

    private void OnAccentChanged(object? sender, Tokens.VelaAccent accent) => UpdateTone();
    private void OnThemeChanged(object? sender, EventArgs e)
    {
        UpdateTone();
        UpdateStatus(animate: false);
    }

    private void UpdateInitials()
    {
        if (!string.IsNullOrWhiteSpace(Initials))
        {
            ComputedInitials = Initials.Trim().ToUpperInvariant();
            return;
        }

        if (string.IsNullOrWhiteSpace(DisplayName))
        {
            ComputedInitials = "?";
            return;
        }

        var parts = DisplayName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
        {
            ComputedInitials = $"{char.ToUpperInvariant(parts[0][0])}{char.ToUpperInvariant(parts[1][0])}";
        }
        else
        {
            var word = parts[0];
            ComputedInitials = word.Length >= 2
                ? word[..2].ToUpperInvariant()
                : word.ToUpperInvariant();
        }
    }

    private void UpdateImageState()
    {
        PseudoClasses.Set(":has-image", Source is not null);
    }

    internal double PixelSize => Size switch
    {
        VelaAvatarSize.Small => 28,
        VelaAvatarSize.Large => 48,
        VelaAvatarSize.XLarge => 64,
        _ => 36,
    };

    private void UpdateSize()
    {
        PseudoClasses.Set(":sm", Size == VelaAvatarSize.Small);
        PseudoClasses.Set(":md", Size == VelaAvatarSize.Medium);
        PseudoClasses.Set(":lg", Size == VelaAvatarSize.Large);
        PseudoClasses.Set(":xl", Size == VelaAvatarSize.XLarge);

        var px = PixelSize;
        Width = px;
        Height = px;

        if (_initialsText is not null)
        {
            _initialsText.FontSize = Size switch
            {
                VelaAvatarSize.Small => 11,
                VelaAvatarSize.Large => 16,
                VelaAvatarSize.XLarge => 21,
                _ => 13,
            };
        }

        if (_statusDot is not null)
        {
            var dot = Size switch
            {
                VelaAvatarSize.Small => 9,
                VelaAvatarSize.Large => 13,
                VelaAvatarSize.XLarge => 15,
                _ => 11,
            };
            _statusDot.Width = dot;
            _statusDot.Height = dot;
        }
    }

    private void UpdateTone()
    {
        if (_circle is null || _initialsText is null)
            return;

        (string bgKey, string fgKey) = Tone switch
        {
            VelaAvatarTone.Accent => ("VelaAccentSubtleBrush", "VelaAccentBrush"),
            VelaAvatarTone.Teal => ("VelaSuccessSubtleBrush", "VelaToneTealBrush"),
            VelaAvatarTone.Sky => ("VelaInfoSubtleBrush", "VelaToneSkyBrush"),
            VelaAvatarTone.Violet => ("VelaSurfaceMutedBrush", "VelaToneVioletBrush"),
            VelaAvatarTone.Rose => ("VelaDangerSubtleBrush", "VelaToneRoseBrush"),
            VelaAvatarTone.Amber => ("VelaWarningSubtleBrush", "VelaToneAmberBrush"),
            VelaAvatarTone.Green => ("VelaSuccessSubtleBrush", "VelaToneGreenBrush"),
            _ => ("VelaSurfaceMutedBrush", "VelaForegroundBrush"),
        };

        if (this.TryFindResource(bgKey, ActualThemeVariant, out var bg) && bg is IBrush bgBrush)
            _circle.Background = bgBrush;
        if (this.TryFindResource(fgKey, ActualThemeVariant, out var fg) && fg is IBrush fgBrush)
            _initialsText.Foreground = fgBrush;
    }

    private void UpdateStatus(bool animate)
    {
        if (_statusDot is null)
            return;

        if (Status != VelaAvatarStatus.None)
        {
            var colorKey = Status switch
            {
                VelaAvatarStatus.Online => "VelaSuccessBrush",
                VelaAvatarStatus.Busy => "VelaDangerBrush",
                VelaAvatarStatus.Away => "VelaWarningBrush",
                _ => "VelaTextMutedBrush",
            };

            if (this.TryFindResource(colorKey, ActualThemeVariant, out var res) && res is IBrush b)
                _statusDot.Background = b;
        }

        var target = Status == VelaAvatarStatus.None ? 0.0 : 1.0;
        if (!animate || VelaMotionOptions.ReducedMotion)
        {
            _statusScale.Jump(target);
            ApplyStatus();
            return;
        }

        if (target > 0.5 && _statusScale.Value > 0.8)
            _statusScale.Jump(0.55);

        _statusScale.SetTarget(target, VelaMotionTokens.Springs.Snappy);
        _runner.Wake();
    }

    private bool OnTick(double dt)
    {
        var active = _statusScale.Step(dt);
        ApplyStatus();
        return active;
    }

    private void ApplyStatus()
    {
        if (_statusDot is null || _dotScale is null)
            return;

        var v = Math.Clamp(_statusScale.Value, 0.0, 1.3);
        _statusDot.IsVisible = v > 0.02 || Status != VelaAvatarStatus.None;
        _statusDot.Opacity = Math.Clamp(v, 0.0, 1.0);
        var s = 0.5 + 0.5 * v;
        _dotScale.ScaleX = s;
        _dotScale.ScaleY = s;
    }
}

/// <summary>
/// Port of Arc UI's AvatarGroup:
/// Overlapping stack of <see cref="VelaAvatar"/> items with crisp cutout rings,
/// subtle hover lift, and an automatic "+N" overflow counter badge.
/// </summary>
public class VelaAvatarGroup : TemplatedControl
{
    public static readonly StyledProperty<int> MaxVisibleProperty =
        AvaloniaProperty.Register<VelaAvatarGroup, int>(nameof(MaxVisible), 4);

    public static readonly StyledProperty<VelaAvatarSize> SizeProperty =
        AvaloniaProperty.Register<VelaAvatarGroup, VelaAvatarSize>(nameof(Size), VelaAvatarSize.Medium);

    public static readonly StyledProperty<double> OverlapProperty =
        AvaloniaProperty.Register<VelaAvatarGroup, double>(nameof(Overlap), 10.0);

    public int MaxVisible { get => GetValue(MaxVisibleProperty); set => SetValue(MaxVisibleProperty, value); }
    public VelaAvatarSize Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }
    public double Overlap { get => GetValue(OverlapProperty); set => SetValue(OverlapProperty, value); }

    [Content]
    public ObservableCollection<VelaAvatar> Items { get; } = new();

    protected override Type StyleKeyOverride => typeof(VelaAvatarGroup);

    private StackPanel? _host;

    static VelaAvatarGroup()
    {
        MaxVisibleProperty.Changed.AddClassHandler<VelaAvatarGroup>((g, _) => g.Rebuild());
        SizeProperty.Changed.AddClassHandler<VelaAvatarGroup>((g, _) => g.Rebuild());
        OverlapProperty.Changed.AddClassHandler<VelaAvatarGroup>((g, _) => g.Rebuild());
    }

    public VelaAvatarGroup()
    {
        Items.CollectionChanged += OnItemsChanged;
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _host = e.NameScope.Find<StackPanel>("PART_Host");
        Rebuild();
    }

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Rebuild();
    }

    private void Rebuild()
    {
        if (_host is null)
            return;

        foreach (var child in _host.Children)
        {
            if (child is Border b)
                b.Child = null;
        }
        _host.Children.Clear();

        var limit = Math.Max(1, MaxVisible);
        var visibleCount = Math.Min(Items.Count, limit);
        var overflow = Items.Count - visibleCount;

        for (var i = 0; i < visibleCount; i++)
        {
            var avatar = Items[i];
            avatar.Size = Size;

            var ring = new Border
            {
                CornerRadius = new CornerRadius(999),
                BorderThickness = new Thickness(2),
                Padding = new Thickness(0),
                Margin = i == 0 ? new Thickness(0) : new Thickness(-Overlap, 0, 0, 0),
                ZIndex = i,
                [!Border.BorderBrushProperty] = this.GetResourceObservable("VelaSurfaceBrush").ToBinding(),
                Child = avatar,
            };

            AttachHoverLift(ring, i);
            _host.Children.Add(ring);
        }

        if (overflow > 0)
        {
            var px = Size switch
            {
                VelaAvatarSize.Small => 28.0,
                VelaAvatarSize.Large => 48.0,
                VelaAvatarSize.XLarge => 64.0,
                _ => 36.0,
            };
            var fontSize = Size switch
            {
                VelaAvatarSize.Small => 10.5,
                VelaAvatarSize.Large => 14.0,
                VelaAvatarSize.XLarge => 16.0,
                _ => 12.0,
            };

            var badge = new Border
            {
                Width = px + 4,
                Height = px + 4,
                CornerRadius = new CornerRadius(999),
                BorderThickness = new Thickness(2),
                Margin = new Thickness(-Overlap, 0, 0, 0),
                ZIndex = visibleCount + 1,
                [!Border.BackgroundProperty] = this.GetResourceObservable("VelaSurfaceMutedBrush").ToBinding(),
                [!Border.BorderBrushProperty] = this.GetResourceObservable("VelaSurfaceBrush").ToBinding(),
                Child = new TextBlock
                {
                    Text = $"+{overflow}",
                    FontSize = fontSize,
                    FontWeight = FontWeight.SemiBold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    [!TextBlock.ForegroundProperty] = this.GetResourceObservable("VelaTextSecondaryBrush").ToBinding(),
                },
            };
            _host.Children.Add(badge);
        }
    }

    private static void AttachHoverLift(Border ring, int baseZ)
    {
        var translate = new TranslateTransform(0, 0);
        ring.RenderTransform = translate;
        ring.PointerEntered += (_, _) =>
        {
            ring.ZIndex = baseZ + 20;
            if (!VelaMotionOptions.ReducedMotion)
                translate.Y = -3;
        };
        ring.PointerExited += (_, _) =>
        {
            ring.ZIndex = baseZ;
            translate.Y = 0;
        };
    }
}
