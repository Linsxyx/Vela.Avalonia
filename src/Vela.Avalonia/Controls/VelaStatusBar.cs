using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Metadata;

namespace Vela.Avalonia.Controls;

public enum VelaStatusTone
{
    Neutral,
    Accent,
    Success,
    Warning,
    Danger,
    Info,
}

/// <summary>
/// Thin vertical hairline divider inside <see cref="VelaStatusBar"/>.
/// </summary>
public sealed class VelaStatusBarSeparator : TemplatedControl
{
    protected override Type StyleKeyOverride => typeof(VelaStatusBarSeparator);
}

/// <summary>
/// Individual status readout or clickable status action inside <see cref="VelaStatusBar"/>.
/// Supports an optional status indicator dot, leading icon, semantic tone, and click/command feedback.
/// </summary>
[PseudoClasses(":interactive", ":has-icon", ":has-dot", ":accent", ":success", ":warning", ":danger", ":info")]
public class VelaStatusBarItem : Button
{
    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<VelaStatusBarItem, Geometry?>(nameof(Icon));

    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<VelaStatusBarItem, string?>(nameof(Text));

    public static readonly StyledProperty<VelaStatusTone> ToneProperty =
        AvaloniaProperty.Register<VelaStatusBarItem, VelaStatusTone>(nameof(Tone), VelaStatusTone.Neutral);

    public static readonly StyledProperty<bool> ShowIndicatorDotProperty =
        AvaloniaProperty.Register<VelaStatusBarItem, bool>(nameof(ShowIndicatorDot), false);

    public static readonly StyledProperty<bool> IsInteractiveProperty =
        AvaloniaProperty.Register<VelaStatusBarItem, bool>(nameof(IsInteractive), false);

    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }
    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public VelaStatusTone Tone { get => GetValue(ToneProperty); set => SetValue(ToneProperty, value); }
    public bool ShowIndicatorDot { get => GetValue(ShowIndicatorDotProperty); set => SetValue(ShowIndicatorDotProperty, value); }
    public bool IsInteractive { get => GetValue(IsInteractiveProperty); set => SetValue(IsInteractiveProperty, value); }

    protected override Type StyleKeyOverride => typeof(VelaStatusBarItem);

    static VelaStatusBarItem()
    {
        IconProperty.Changed.AddClassHandler<VelaStatusBarItem>((i, _) => i.SyncPseudoClasses());
        ToneProperty.Changed.AddClassHandler<VelaStatusBarItem>((i, _) => i.SyncPseudoClasses());
        ShowIndicatorDotProperty.Changed.AddClassHandler<VelaStatusBarItem>((i, _) => i.SyncPseudoClasses());
        IsInteractiveProperty.Changed.AddClassHandler<VelaStatusBarItem>((i, _) => i.SyncPseudoClasses());
        CommandProperty.Changed.AddClassHandler<VelaStatusBarItem>((i, _) => i.SyncPseudoClasses());
    }

    public VelaStatusBarItem()
    {
        SyncPseudoClasses();
    }

    private void SyncPseudoClasses()
    {
        var interactive = IsInteractive || Command is not null;
        PseudoClasses.Set(":interactive", interactive);
        PseudoClasses.Set(":has-icon", Icon is not null);
        PseudoClasses.Set(":has-dot", ShowIndicatorDot);
        PseudoClasses.Set(":accent", Tone == VelaStatusTone.Accent);
        PseudoClasses.Set(":success", Tone == VelaStatusTone.Success);
        PseudoClasses.Set(":warning", Tone == VelaStatusTone.Warning);
        PseudoClasses.Set(":danger", Tone == VelaStatusTone.Danger);
        PseudoClasses.Set(":info", Tone == VelaStatusTone.Info);
    }
}

/// <summary>
/// Desktop application status bar with left items, optional center slot, and right items.
/// Pairs naturally with <see cref="VelaWindow.BottomBarContent"/> or <see cref="VelaNavigationView.BottomBarContent"/>.
/// </summary>
[PseudoClasses(":compact", ":no-border")]
public class VelaStatusBar : TemplatedControl
{
    public static readonly StyledProperty<object?> CenterContentProperty =
        AvaloniaProperty.Register<VelaStatusBar, object?>(nameof(CenterContent));

    public static readonly StyledProperty<bool> ShowTopBorderProperty =
        AvaloniaProperty.Register<VelaStatusBar, bool>(nameof(ShowTopBorder), true);

    public static readonly StyledProperty<bool> IsCompactProperty =
        AvaloniaProperty.Register<VelaStatusBar, bool>(nameof(IsCompact), false);

    public object? CenterContent { get => GetValue(CenterContentProperty); set => SetValue(CenterContentProperty, value); }
    public bool ShowTopBorder { get => GetValue(ShowTopBorderProperty); set => SetValue(ShowTopBorderProperty, value); }
    public bool IsCompact { get => GetValue(IsCompactProperty); set => SetValue(IsCompactProperty, value); }

    [Content]
    public ObservableCollection<Control> LeftItems { get; } = new();

    public ObservableCollection<Control> RightItems { get; } = new();

    protected override Type StyleKeyOverride => typeof(VelaStatusBar);

    private StackPanel? _leftHost;
    private StackPanel? _rightHost;

    static VelaStatusBar()
    {
        ShowTopBorderProperty.Changed.AddClassHandler<VelaStatusBar>((b, _) => b.SyncPseudoClasses());
        IsCompactProperty.Changed.AddClassHandler<VelaStatusBar>((b, _) => b.SyncPseudoClasses());
    }

    public VelaStatusBar()
    {
        LeftItems.CollectionChanged += OnLeftItemsChanged;
        RightItems.CollectionChanged += OnRightItemsChanged;
        SyncPseudoClasses();
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _leftHost = e.NameScope.Find<StackPanel>("PART_LeftHost");
        _rightHost = e.NameScope.Find<StackPanel>("PART_RightHost");
        SyncHost(_leftHost, LeftItems);
        SyncHost(_rightHost, RightItems);
    }

    private void OnLeftItemsChanged(object? sender, NotifyCollectionChangedEventArgs e) => SyncHost(_leftHost, LeftItems);
    private void OnRightItemsChanged(object? sender, NotifyCollectionChangedEventArgs e) => SyncHost(_rightHost, RightItems);

    private static void SyncHost(StackPanel? host, ObservableCollection<Control> source)
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

    private void SyncPseudoClasses()
    {
        PseudoClasses.Set(":compact", IsCompact);
        PseudoClasses.Set(":no-border", !ShowTopBorder);
    }
}
