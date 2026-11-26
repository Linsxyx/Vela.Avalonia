using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Metadata;
using Vela.Avalonia.Motion;

namespace Vela.Avalonia.Controls;

public enum VelaSplitButtonVariant
{
    Primary,
    Secondary,
}

/// <summary>
/// Port of Arc UI's SplitButton:
/// - Joined primary action button (with width-morphing slot) + hairline divider + chevron menu trigger
/// - Floating <see cref="VelaPopupSurface"/> menu with spring-gliding hover highlight over <see cref="VelaMenuItem"/> rows
/// </summary>
[PseudoClasses(":open", ":primary", ":secondary")]
public class VelaSplitButton : TemplatedControl
{
    public static readonly StyledProperty<object?> ContentProperty =
        AvaloniaProperty.Register<VelaSplitButton, object?>(nameof(Content));

    public static readonly StyledProperty<Geometry?> LeadingIconProperty =
        AvaloniaProperty.Register<VelaSplitButton, Geometry?>(nameof(LeadingIcon));

    public static readonly StyledProperty<VelaSplitButtonVariant> VariantProperty =
        AvaloniaProperty.Register<VelaSplitButton, VelaSplitButtonVariant>(nameof(Variant), VelaSplitButtonVariant.Primary);

    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<VelaSplitButton, bool>(nameof(IsOpen), false, defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<double> MenuMinWidthProperty =
        AvaloniaProperty.Register<VelaSplitButton, double>(nameof(MenuMinWidth), 204.0);

    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<VelaSplitButton, ICommand?>(nameof(Command));

    public static readonly StyledProperty<object?> CommandParameterProperty =
        AvaloniaProperty.Register<VelaSplitButton, object?>(nameof(CommandParameter));

    public object? Content { get => GetValue(ContentProperty); set => SetValue(ContentProperty, value); }
    public Geometry? LeadingIcon { get => GetValue(LeadingIconProperty); set => SetValue(LeadingIconProperty, value); }
    public VelaSplitButtonVariant Variant { get => GetValue(VariantProperty); set => SetValue(VariantProperty, value); }
    public bool IsOpen { get => GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }
    public double MenuMinWidth { get => GetValue(MenuMinWidthProperty); set => SetValue(MenuMinWidthProperty, value); }
    public ICommand? Command { get => GetValue(CommandProperty); set => SetValue(CommandProperty, value); }
    public object? CommandParameter { get => GetValue(CommandParameterProperty); set => SetValue(CommandParameterProperty, value); }

    [Content]
    public ObservableCollection<VelaMenuItem> MenuItems { get; } = new();

    public event EventHandler<RoutedEventArgs>? Click;

    protected override Type StyleKeyOverride => typeof(VelaSplitButton);

    private readonly SpringChannel _chevronAngle = new(0.0, VelaMotionTokens.Springs.Snappy);
    private readonly SpringChannel _hlY = new(0.0, VelaMotionTokens.Springs.Snappy);
    private readonly SpringChannel _hlH = new(34.0, VelaMotionTokens.Springs.Snappy);
    private readonly SpringChannel _hlOpacity = new(0.0, VelaMotionTokens.Springs.Snappy);
    private readonly SpringRunner _runner;

    private Button? _primaryButton;
    private Button? _menuTrigger;
    private RotateTransform? _chevronRotate;
    private Grid? _menuTrack;
    private StackPanel? _itemsHost;
    private Border? _highlight;
    private TranslateTransform? _hlTranslate;
    private bool _highlightPlaced;

    static VelaSplitButton()
    {
        IsOpenProperty.Changed.AddClassHandler<VelaSplitButton>((s, _) => s.OnOpenChanged());
        VariantProperty.Changed.AddClassHandler<VelaSplitButton>((s, _) => s.UpdateVariantPseudoClasses());
    }

    public VelaSplitButton()
    {
        _runner = new SpringRunner(this, OnTick);
        MenuItems.CollectionChanged += (_, _) => RebuildMenuRows();
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (_primaryButton is not null)
            _primaryButton.Click -= OnPrimaryClick;
        if (_menuTrigger is not null)
            _menuTrigger.Click -= OnMenuTriggerClick;
        if (_menuTrack is not null)
            _menuTrack.PointerExited -= OnMenuPointerExited;

        _primaryButton = e.NameScope.Find<Button>("PART_PrimaryButton");
        _menuTrigger = e.NameScope.Find<Button>("PART_MenuTrigger");
        _menuTrack = e.NameScope.Find<Grid>("PART_MenuTrack");
        _itemsHost = e.NameScope.Find<StackPanel>("PART_ItemsHost");
        _highlight = e.NameScope.Find<Border>("PART_Highlight");

        var chevron = e.NameScope.Find<PathIcon>("PART_Chevron");
        if (chevron is not null)
        {
            _chevronRotate = new RotateTransform(IsOpen ? 180.0 : 0.0);
            chevron.RenderTransformOrigin = RelativePoint.Center;
            chevron.RenderTransform = _chevronRotate;
        }

        if (_highlight is not null)
        {
            _hlTranslate = new TranslateTransform();
            _highlight.RenderTransform = _hlTranslate;
            _highlight.Opacity = 0;
        }

        if (_primaryButton is not null)
            _primaryButton.Click += OnPrimaryClick;
        if (_menuTrigger is not null)
            _menuTrigger.Click += OnMenuTriggerClick;
        if (_menuTrack is not null)
            _menuTrack.PointerExited += OnMenuPointerExited;

        UpdateVariantPseudoClasses();
        RebuildMenuRows();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _runner.Stop();
    }

    private void UpdateVariantPseudoClasses()
    {
        PseudoClasses.Set(":primary", Variant == VelaSplitButtonVariant.Primary);
        PseudoClasses.Set(":secondary", Variant == VelaSplitButtonVariant.Secondary);
    }

    private void OnPrimaryClick(object? sender, RoutedEventArgs e)
    {
        Click?.Invoke(this, e);
        if (Command?.CanExecute(CommandParameter) == true)
            Command.Execute(CommandParameter);
    }

    private void OnMenuTriggerClick(object? sender, RoutedEventArgs e)
    {
        IsOpen = !IsOpen;
    }

    private void OnOpenChanged()
    {
        PseudoClasses.Set(":open", IsOpen);
        if (!IsOpen)
        {
            _highlightPlaced = false;
            _hlOpacity.Jump(0.0);
            if (_highlight is not null) _highlight.Opacity = 0.0;
        }

        var target = IsOpen ? 180.0 : 0.0;
        if (VelaMotionOptions.ReducedMotion)
        {
            _chevronAngle.Jump(target);
            if (_chevronRotate is not null) _chevronRotate.Angle = target;
            return;
        }

        _chevronAngle.SetTarget(target, VelaMotionTokens.Springs.Snappy);
        _runner.Wake();
    }

    private void OnMenuPointerExited(object? sender, PointerEventArgs e)
    {
        _hlOpacity.SetTarget(0.0, VelaMotionTokens.Springs.Snappy);
        _runner.Wake();
    }

    private void RebuildMenuRows()
    {
        if (_itemsHost is null)
            return;

        _itemsHost.Children.Clear();
        foreach (var item in MenuItems)
        {
            if (item.IsSeparator)
            {
                _itemsHost.Children.Add(new Border
                {
                    Height = 1,
                    Margin = new Thickness(4, 4),
                    [!Border.BackgroundProperty] = this.GetResourceObservable("VelaBorderSubtleBrush").ToBinding(),
                });
                continue;
            }

            _itemsHost.Children.Add(BuildMenuRow(item));
        }
    }

    private Control BuildMenuRow(VelaMenuItem item)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
            ColumnSpacing = 9,
            MinHeight = 34,
        };

        var fgKey = item.IsDestructive ? "VelaDangerBrush" : "VelaForegroundBrush";
        var iconKey = item.IsDestructive ? "VelaDangerBrush" : "VelaTextSecondaryBrush";

        if (item.Icon is not null)
        {
            grid.Children.Add(new PathIcon
            {
                Width = 15,
                Height = 15,
                Data = item.Icon,
                VerticalAlignment = VerticalAlignment.Center,
                [!PathIcon.ForegroundProperty] = this.GetResourceObservable(iconKey).ToBinding(),
            });
        }

        var label = new TextBlock
        {
            Text = item.Header,
            FontSize = 13.5,
            VerticalAlignment = VerticalAlignment.Center,
            [!TextBlock.ForegroundProperty] = this.GetResourceObservable(fgKey).ToBinding(),
        };
        Grid.SetColumn(label, 1);
        grid.Children.Add(label);

        if (!string.IsNullOrEmpty(item.Shortcut))
        {
            var shortcut = new TextBlock
            {
                Text = item.Shortcut,
                FontSize = 11.5,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 0, 0, 0),
                [!TextBlock.ForegroundProperty] = this.GetResourceObservable("VelaTextMutedBrush").ToBinding(),
            };
            Grid.SetColumn(shortcut, 2);
            grid.Children.Add(shortcut);
        }

        var rowBorder = new Border
        {
            Padding = new Thickness(10, 0),
            CornerRadius = new CornerRadius(10),
            Background = Brushes.Transparent,
            Cursor = item.IsEnabled ? new Cursor(StandardCursorType.Hand) : Cursor.Default,
            Opacity = item.IsEnabled ? 1.0 : 0.45,
            Child = grid,
        };

        if (item.IsEnabled)
        {
            rowBorder.PointerEntered += (_, _) => HighlightRow(rowBorder, item.IsDestructive);
            rowBorder.PointerPressed += (_, ev) =>
            {
                if (ev.GetCurrentPoint(rowBorder).Properties.IsLeftButtonPressed)
                {
                    IsOpen = false;
                    item.Invoke();
                    ev.Handled = true;
                }
            };
        }

        return rowBorder;
    }

    private void HighlightRow(Control row, bool destructive)
    {
        if (_menuTrack is null || _highlight is null)
            return;

        var pos = row.TranslatePoint(new Point(0, 0), _menuTrack);
        if (!pos.HasValue)
            return;

        var bgKey = destructive ? "VelaDangerSubtleBrush" : "VelaSurfaceMutedBrush";
        if (this.TryFindResource(bgKey, ActualThemeVariant, out var res) && res is IBrush b)
            _highlight.Background = b;

        _highlight.Width = _menuTrack.Bounds.Width;

        if (!_highlightPlaced || VelaMotionOptions.ReducedMotion)
        {
            _highlightPlaced = true;
            _hlY.Jump(pos.Value.Y);
            _hlH.Jump(row.Bounds.Height);
            _hlOpacity.SetTarget(1.0, VelaMotionTokens.Springs.Snappy);
            ApplyHighlight();
            _runner.Wake();
            return;
        }

        _hlY.SetTarget(pos.Value.Y, VelaMotionTokens.Springs.Snappy);
        _hlH.SetTarget(row.Bounds.Height, VelaMotionTokens.Springs.Snappy);
        _hlOpacity.SetTarget(1.0, VelaMotionTokens.Springs.Snappy);
        _runner.Wake();
    }

    private bool OnTick(double dt)
    {
        var active = _chevronAngle.Step(dt);
        if (_chevronRotate is not null)
            _chevronRotate.Angle = _chevronAngle.Value;

        active |= _hlY.Step(dt);
        active |= _hlH.Step(dt);
        active |= _hlOpacity.Step(dt);
        ApplyHighlight();

        return active;
    }

    private void ApplyHighlight()
    {
        if (_highlight is null || _hlTranslate is null) return;
        _hlTranslate.Y = _hlY.Value;
        _highlight.Height = Math.Max(0, _hlH.Value);
        _highlight.Opacity = Math.Clamp(_hlOpacity.Value, 0.0, 1.0);
    }
}
