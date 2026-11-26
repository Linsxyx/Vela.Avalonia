using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;

namespace Vela.Avalonia.Controls;

/// <summary>
/// Port of Arc UI's Popover: an anchored floating surface that opens with directional spring entrance
/// relative to its trigger, with light-dismiss and programmatic IsOpen binding.
/// </summary>
public class VelaPopover : ContentControl
{
    public static readonly StyledProperty<object?> PopoverContentProperty =
        AvaloniaProperty.Register<VelaPopover, object?>(nameof(PopoverContent));

    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<VelaPopover, bool>(nameof(IsOpen), false, defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<PlacementMode> PlacementProperty =
        AvaloniaProperty.Register<VelaPopover, PlacementMode>(nameof(Placement), PlacementMode.BottomEdgeAlignedLeft);

    public static readonly StyledProperty<double> VerticalOffsetProperty =
        AvaloniaProperty.Register<VelaPopover, double>(nameof(VerticalOffset), 6.0);

    public static readonly StyledProperty<double> HorizontalOffsetProperty =
        AvaloniaProperty.Register<VelaPopover, double>(nameof(HorizontalOffset), 0.0);

    public object? PopoverContent { get => GetValue(PopoverContentProperty); set => SetValue(PopoverContentProperty, value); }
    public bool IsOpen { get => GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }
    public PlacementMode Placement { get => GetValue(PlacementProperty); set => SetValue(PlacementProperty, value); }
    public double VerticalOffset { get => GetValue(VerticalOffsetProperty); set => SetValue(VerticalOffsetProperty, value); }
    public double HorizontalOffset { get => GetValue(HorizontalOffsetProperty); set => SetValue(HorizontalOffsetProperty, value); }

    protected override Type StyleKeyOverride => typeof(VelaPopover);

    private Control? _triggerHost;
    private VelaPopupSurface? _surface;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (_triggerHost is not null)
            _triggerHost.RemoveHandler(Button.ClickEvent, OnTriggerClick);

        _triggerHost = e.NameScope.Find<Control>("PART_TriggerHost");
        _surface = e.NameScope.Find<VelaPopupSurface>("PART_Surface");

        if (_triggerHost is not null)
            _triggerHost.AddHandler(Button.ClickEvent, OnTriggerClick);

        SyncSurfaceDirection();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_triggerHost is not null)
            _triggerHost.RemoveHandler(Button.ClickEvent, OnTriggerClick);
    }

    private void OnTriggerClick(object? sender, RoutedEventArgs e)
    {
        SyncSurfaceDirection();
        IsOpen = !IsOpen;
    }

    private void SyncSurfaceDirection()
    {
        if (_surface is null) return;
        (_surface.EnterOffsetX, _surface.EnterOffsetY) = Placement switch
        {
            PlacementMode.Top or PlacementMode.TopEdgeAlignedLeft or PlacementMode.TopEdgeAlignedRight => (0.0, 5.0),
            PlacementMode.Left or PlacementMode.LeftEdgeAlignedTop or PlacementMode.LeftEdgeAlignedBottom => (5.0, 0.0),
            PlacementMode.Right or PlacementMode.RightEdgeAlignedTop or PlacementMode.RightEdgeAlignedBottom => (-5.0, 0.0),
            _ => (0.0, -5.0),
        };
    }
}
