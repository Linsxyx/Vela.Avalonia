using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Metadata;
using Vela.Avalonia.Motion;

namespace Vela.Avalonia.Controls;

/// <summary>
/// Action or toggle button inside <see cref="VelaFloatingBar"/>.
/// </summary>
public class VelaFloatingBarItem : AvaloniaObject
{
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<VelaFloatingBarItem, string?>(nameof(Label));

    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<VelaFloatingBarItem, Geometry?>(nameof(Icon));

    public static readonly StyledProperty<string?> ToolTipTextProperty =
        AvaloniaProperty.Register<VelaFloatingBarItem, string?>(nameof(ToolTipText));

    public static readonly StyledProperty<bool> IsToggleProperty =
        AvaloniaProperty.Register<VelaFloatingBarItem, bool>(nameof(IsToggle));

    public static readonly StyledProperty<bool> IsActiveProperty =
        AvaloniaProperty.Register<VelaFloatingBarItem, bool>(nameof(IsActive));

    public static readonly StyledProperty<bool> IsSeparatorProperty =
        AvaloniaProperty.Register<VelaFloatingBarItem, bool>(nameof(IsSeparator));

    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<VelaFloatingBarItem, ICommand?>(nameof(Command));

    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }
    public string? ToolTipText { get => GetValue(ToolTipTextProperty); set => SetValue(ToolTipTextProperty, value); }
    public bool IsToggle { get => GetValue(IsToggleProperty); set => SetValue(IsToggleProperty, value); }
    public bool IsActive { get => GetValue(IsActiveProperty); set => SetValue(IsActiveProperty, value); }
    public bool IsSeparator { get => GetValue(IsSeparatorProperty); set => SetValue(IsSeparatorProperty, value); }
    public ICommand? Command { get => GetValue(CommandProperty); set => SetValue(CommandProperty, value); }

    public event EventHandler? Click;

    internal void Trigger()
    {
        if (IsSeparator) return;
        if (IsToggle) IsActive = !IsActive;
        Click?.Invoke(this, EventArgs.Empty);
        if (Command?.CanExecute(null) == true)
            Command.Execute(null);
    }
}

/// <summary>
/// Port of Arc UI's FloatingButtonGroup fused with <see cref="VelaLiquidGlass"/>:
/// Separate soft buttons in a floating glass or surface tray with ONE shared highlight pill
/// that glides and morphs cleanly between buttons as the pointer moves.
/// </summary>
public class VelaFloatingBar : TemplatedControl
{
    public static readonly StyledProperty<bool> UseLiquidGlassProperty =
        AvaloniaProperty.Register<VelaFloatingBar, bool>(nameof(UseLiquidGlass), true);

    public static readonly StyledProperty<double> GlassBlurRadiusProperty =
        AvaloniaProperty.Register<VelaFloatingBar, double>(nameof(GlassBlurRadius), 8.0);

    public bool UseLiquidGlass { get => GetValue(UseLiquidGlassProperty); set => SetValue(UseLiquidGlassProperty, value); }
    public double GlassBlurRadius { get => GetValue(GlassBlurRadiusProperty); set => SetValue(GlassBlurRadiusProperty, value); }

    [Content]
    public ObservableCollection<VelaFloatingBarItem> Items { get; } = new();

    protected override Type StyleKeyOverride => typeof(VelaFloatingBar);

    private readonly SpringChannel _hlX = new(0.0, VelaMotionTokens.Springs.Morph);
    private readonly SpringChannel _hlW = new(36.0, VelaMotionTokens.Springs.Morph);
    private readonly SpringChannel _hlOpacity = new(0.0, VelaMotionTokens.Springs.Snappy);
    private readonly SpringRunner _runner;

    private Grid? _track;
    private StackPanel? _itemsHost;
    private Border? _highlight;
    private TranslateTransform? _hlTranslate;
    private bool _hasPlaced;

    public VelaFloatingBar()
    {
        _runner = new SpringRunner(this, OnTick);
        Items.CollectionChanged += (_, _) => RebuildItems();
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (_track is not null)
            _track.PointerExited -= OnTrackPointerExited;

        _track = e.NameScope.Find<Grid>("PART_Track");
        _itemsHost = e.NameScope.Find<StackPanel>("PART_ItemsHost");
        _highlight = e.NameScope.Find<Border>("PART_Highlight");

        if (_highlight is not null)
        {
            _hlTranslate = new TranslateTransform();
            _highlight.RenderTransform = _hlTranslate;
            _highlight.Opacity = 0;
        }

        if (_track is not null)
            _track.PointerExited += OnTrackPointerExited;

        RebuildItems();
    }

    private readonly List<(VelaFloatingBarItem Item, EventHandler<AvaloniaPropertyChangedEventArgs> Handler)> _itemSubscriptions = new();

    private void ClearItemSubscriptions()
    {
        foreach (var (item, handler) in _itemSubscriptions)
            item.PropertyChanged -= handler;
        _itemSubscriptions.Clear();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _runner.Stop();
        ClearItemSubscriptions();
    }

    private void OnTrackPointerExited(object? sender, PointerEventArgs e)
    {
        _hlOpacity.SetTarget(0.0, VelaMotionTokens.Springs.Snappy);
        _runner.Wake();
    }

    private void RebuildItems()
    {
        ClearItemSubscriptions();
        if (_itemsHost is null) return;
        _itemsHost.Children.Clear();

        foreach (var item in Items)
        {
            if (item.IsSeparator)
            {
                _itemsHost.Children.Add(new Border
                {
                    Width = 1,
                    Height = 18,
                    Margin = new Thickness(3, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    [!Border.BackgroundProperty] = VelaResourceBinding.Create("VelaBorderBrush"),
                });
                continue;
            }

            _itemsHost.Children.Add(BuildItemButton(item));
        }
    }

    private Control BuildItemButton(VelaFloatingBarItem item)
    {
        var content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
        };

        if (item.Icon is not null)
        {
            content.Children.Add(new PathIcon
            {
                Width = 15,
                Height = 15,
                Data = item.Icon,
                VerticalAlignment = VerticalAlignment.Center,
                [!PathIcon.ForegroundProperty] = VelaResourceBinding.Create("VelaForegroundBrush"),
            });
        }

        if (!string.IsNullOrEmpty(item.Label))
        {
            content.Children.Add(new TextBlock
            {
                Text = item.Label,
                FontSize = 13,
                FontWeight = FontWeight.Medium,
                VerticalAlignment = VerticalAlignment.Center,
                [!TextBlock.ForegroundProperty] = VelaResourceBinding.Create("VelaForegroundBrush"),
            });
        }

        var iconOnly = string.IsNullOrEmpty(item.Label);
        var pill = new Border
        {
            Height = 34,
            MinWidth = 34,
            Padding = iconOnly ? new Thickness(0) : new Thickness(11, 0),
            CornerRadius = new CornerRadius(11),
            Background = Brushes.Transparent,
            Cursor = new Cursor(StandardCursorType.Hand),
            Child = content,
        };

        void SyncActiveBg()
        {
            if (item.IsActive && this.TryFindResource("VelaAccentSubtleBrush", ActualThemeVariant, out var res) && res is IBrush b)
                pill.Background = b;
            else
                pill.Background = Brushes.Transparent;
        }

        SyncActiveBg();
        EventHandler<AvaloniaPropertyChangedEventArgs> handler = (_, ev) =>
        {
            if (ev.Property == VelaFloatingBarItem.IsActiveProperty)
                SyncActiveBg();
        };
        item.PropertyChanged += handler;
        _itemSubscriptions.Add((item, handler));

        if (!string.IsNullOrEmpty(item.ToolTipText))
            ToolTip.SetTip(pill, item.ToolTipText);

        pill.PointerEntered += (_, _) => MoveHighlightTo(pill);
        pill.PointerPressed += (_, ev) =>
        {
            if (ev.GetCurrentPoint(pill).Properties.IsLeftButtonPressed)
            {
                item.Trigger();
                SyncActiveBg();
                ev.Handled = true;
            }
        };

        return pill;
    }

    private void MoveHighlightTo(Control target)
    {
        if (_track is null || _highlight is null) return;
        var origin = target.TranslatePoint(new Point(0, 0), _track);
        if (!origin.HasValue) return;

        _highlight.Height = target.Bounds.Height;

        if (!_hasPlaced || VelaMotionOptions.ReducedMotion)
        {
            _hasPlaced = true;
            _hlX.Jump(origin.Value.X);
            _hlW.Jump(target.Bounds.Width);
            _hlOpacity.SetTarget(1.0, VelaMotionTokens.Springs.Snappy);
            ApplyHighlight();
            _runner.Wake();
            return;
        }

        _hlX.SetTarget(origin.Value.X, VelaMotionTokens.Springs.Morph);
        _hlW.SetTarget(target.Bounds.Width, VelaMotionTokens.Springs.Morph);
        _hlOpacity.SetTarget(1.0, VelaMotionTokens.Springs.Snappy);
        _runner.Wake();
    }

    private bool OnTick(double dt)
    {
        var active = _hlX.Step(dt);
        active |= _hlW.Step(dt);
        active |= _hlOpacity.Step(dt);
        ApplyHighlight();
        return active;
    }

    private void ApplyHighlight()
    {
        if (_highlight is null || _hlTranslate is null) return;
        _hlTranslate.X = _hlX.Value;
        _highlight.Width = Math.Max(0, _hlW.Value);
        _highlight.Opacity = Math.Clamp(_hlOpacity.Value, 0.0, 1.0);
    }
}
