using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Metadata;

namespace Vela.Avalonia.Controls;

public enum VelaToolBarVariant
{
    Standard,
    Floating,
    Borderless,
}

/// <summary>
/// Crisp 1px divider between grouped actions inside <see cref="VelaToolBar"/>.
/// </summary>
[PseudoClasses(":vertical")]
public sealed class VelaToolBarSeparator : TemplatedControl
{
    public static readonly StyledProperty<Orientation> OrientationProperty =
        AvaloniaProperty.Register<VelaToolBarSeparator, Orientation>(nameof(Orientation), Orientation.Horizontal);

    public Orientation Orientation { get => GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }

    protected override Type StyleKeyOverride => typeof(VelaToolBarSeparator);

    static VelaToolBarSeparator()
    {
        OrientationProperty.Changed.AddClassHandler<VelaToolBarSeparator>((s, _) =>
            s.PseudoClasses.Set(":vertical", s.Orientation == Orientation.Vertical));
    }
}

/// <summary>
/// Compact toolbar action button with optional icon, label, and keyboard shortcut hint.
/// </summary>
[PseudoClasses(":compact", ":has-icon", ":has-label")]
public class VelaToolBarButton : Button
{
    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<VelaToolBarButton, Geometry?>(nameof(Icon));

    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<VelaToolBarButton, string?>(nameof(Label));

    public static readonly StyledProperty<bool> IsCompactProperty =
        AvaloniaProperty.Register<VelaToolBarButton, bool>(nameof(IsCompact), false);

    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public bool IsCompact { get => GetValue(IsCompactProperty); set => SetValue(IsCompactProperty, value); }

    protected override Type StyleKeyOverride => typeof(VelaToolBarButton);

    static VelaToolBarButton()
    {
        IconProperty.Changed.AddClassHandler<VelaToolBarButton>((b, _) => b.SyncPseudoClasses());
        LabelProperty.Changed.AddClassHandler<VelaToolBarButton>((b, _) => b.SyncPseudoClasses());
        IsCompactProperty.Changed.AddClassHandler<VelaToolBarButton>((b, _) => b.SyncPseudoClasses());
    }

    public VelaToolBarButton()
    {
        SyncPseudoClasses();
    }

    private void SyncPseudoClasses()
    {
        PseudoClasses.Set(":compact", IsCompact);
        PseudoClasses.Set(":has-icon", Icon is not null);
        PseudoClasses.Set(":has-label", !string.IsNullOrEmpty(Label));
    }
}

/// <summary>
/// Toggleable toolbar action button (e.g. Bold, Grid Snap, Inspector) with active pill highlight.
/// </summary>
[PseudoClasses(":compact", ":has-icon", ":has-label")]
public class VelaToolBarToggleButton : ToggleButton
{
    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<VelaToolBarToggleButton, Geometry?>(nameof(Icon));

    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<VelaToolBarToggleButton, string?>(nameof(Label));

    public static readonly StyledProperty<bool> IsCompactProperty =
        AvaloniaProperty.Register<VelaToolBarToggleButton, bool>(nameof(IsCompact), false);

    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public bool IsCompact { get => GetValue(IsCompactProperty); set => SetValue(IsCompactProperty, value); }

    protected override Type StyleKeyOverride => typeof(VelaToolBarToggleButton);

    static VelaToolBarToggleButton()
    {
        IconProperty.Changed.AddClassHandler<VelaToolBarToggleButton>((b, _) => b.SyncPseudoClasses());
        LabelProperty.Changed.AddClassHandler<VelaToolBarToggleButton>((b, _) => b.SyncPseudoClasses());
        IsCompactProperty.Changed.AddClassHandler<VelaToolBarToggleButton>((b, _) => b.SyncPseudoClasses());
    }

    public VelaToolBarToggleButton()
    {
        SyncPseudoClasses();
    }

    private void SyncPseudoClasses()
    {
        PseudoClasses.Set(":compact", IsCompact);
        PseudoClasses.Set(":has-icon", Icon is not null);
        PseudoClasses.Set(":has-label", !string.IsNullOrEmpty(Label));
    }
}

/// <summary>
/// Desktop action toolbar supporting horizontal or vertical layout, Standard / Floating / Borderless variants,
/// primary <see cref="Items"/>, and optional <see cref="TrailingItems"/>.
/// </summary>
[PseudoClasses(":vertical", ":floating", ":borderless", ":compact")]
public class VelaToolBar : TemplatedControl
{
    public static readonly StyledProperty<Orientation> OrientationProperty =
        AvaloniaProperty.Register<VelaToolBar, Orientation>(nameof(Orientation), Orientation.Horizontal);

    public static readonly StyledProperty<VelaToolBarVariant> VariantProperty =
        AvaloniaProperty.Register<VelaToolBar, VelaToolBarVariant>(nameof(Variant), VelaToolBarVariant.Standard);

    public static readonly StyledProperty<bool> IsCompactProperty =
        AvaloniaProperty.Register<VelaToolBar, bool>(nameof(IsCompact), false);

    public static readonly StyledProperty<double> ItemSpacingProperty =
        AvaloniaProperty.Register<VelaToolBar, double>(nameof(ItemSpacing), 2.0);

    public Orientation Orientation { get => GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }
    public VelaToolBarVariant Variant { get => GetValue(VariantProperty); set => SetValue(VariantProperty, value); }
    public bool IsCompact { get => GetValue(IsCompactProperty); set => SetValue(IsCompactProperty, value); }
    public double ItemSpacing { get => GetValue(ItemSpacingProperty); set => SetValue(ItemSpacingProperty, value); }

    [Content]
    public ObservableCollection<Control> Items { get; } = new();

    public ObservableCollection<Control> TrailingItems { get; } = new();

    protected override Type StyleKeyOverride => typeof(VelaToolBar);

    private StackPanel? _itemsHost;
    private StackPanel? _trailingHost;

    static VelaToolBar()
    {
        OrientationProperty.Changed.AddClassHandler<VelaToolBar>((t, _) => t.SyncState());
        VariantProperty.Changed.AddClassHandler<VelaToolBar>((t, _) => t.SyncState());
        IsCompactProperty.Changed.AddClassHandler<VelaToolBar>((t, _) => t.SyncState());
    }

    public VelaToolBar()
    {
        Items.CollectionChanged += OnCollectionChanged;
        TrailingItems.CollectionChanged += OnCollectionChanged;
        SyncState();
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _itemsHost = e.NameScope.Find<StackPanel>("PART_ItemsHost");
        _trailingHost = e.NameScope.Find<StackPanel>("PART_TrailingHost");
        SyncHosts();
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => SyncHosts();

    private void SyncState()
    {
        PseudoClasses.Set(":vertical", Orientation == Orientation.Vertical);
        PseudoClasses.Set(":floating", Variant == VelaToolBarVariant.Floating);
        PseudoClasses.Set(":borderless", Variant == VelaToolBarVariant.Borderless);
        PseudoClasses.Set(":compact", IsCompact);
        SyncChildProperties();
    }

    private void SyncHosts()
    {
        RebuildHost(_itemsHost, Items);
        RebuildHost(_trailingHost, TrailingItems);
        SyncChildProperties();
    }

    private static void RebuildHost(StackPanel? host, ObservableCollection<Control> source)
    {
        if (host is null) return;
        host.Children.Clear();
        foreach (var item in source)
        {
            if (item.Parent is Panel oldPanel)
                oldPanel.Children.Remove(item);
            host.Children.Add(item);
        }
    }

    private void SyncChildProperties()
    {
        SyncCollection(Items);
        SyncCollection(TrailingItems);
    }

    private void SyncCollection(ObservableCollection<Control> collection)
    {
        foreach (var item in collection)
        {
            if (item is VelaToolBarSeparator sep)
                sep.Orientation = Orientation;
            else if (item is VelaToolBarButton btn)
                btn.IsCompact = IsCompact;
            else if (item is VelaToolBarToggleButton toggle)
                toggle.IsCompact = IsCompact;
        }
    }
}
