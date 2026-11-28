using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
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
using Avalonia.Styling;
using Avalonia.VisualTree;
using Vela.Avalonia.Motion;

namespace Vela.Avalonia.Controls;

/// <summary>
/// Frosted glass trigger that morphs directly into its menu surface with a gliding spring highlight:
/// - Retained GPU <see cref="BlurEffect"/> backdrop + diagonal luminous specular rim (<see cref="VelaGlassRimBorder"/>)
/// - Spring-driven morph between compact glass trigger (circle or pill) and expanded menu
/// - Gliding highlight pill tracking pointer hover and keyboard focus, with soft danger tint on destructive items
/// - In-place submenu drill-in with smooth height morphing, checkmarks, shortcuts, and click-outside dismissal
/// </summary>
[PseudoClasses(":open")]
public class VelaGlassMenu : TemplatedControl
{
    private static Geometry? s_defaultMoreIcon;
    private static Geometry? s_checkIconGeometry;
    private static Geometry? s_chevronRightGeometry;
    private static Geometry? s_chevronLeftGeometry;

    private static Geometry DefaultMoreIcon => s_defaultMoreIcon ??= Geometry.Parse(
        "M6 10C4.9 10 4 10.9 4 12C4 13.1 4.9 14 6 14C7.1 14 8 13.1 8 12C8 10.9 7.1 10 6 10ZM18 10C16.9 10 16 10.9 16 12C16 13.1 16.9 14 18 14C19.1 14 20 13.1 20 12C20 10.9 19.1 10 18 10ZM12 10C10.9 10 10 10.9 10 12C10 13.1 10.9 14 12 14C13.1 14 14 13.1 14 12C14 10.9 13.1 10 12 10Z");

    private static Geometry CheckIconGeometry => s_checkIconGeometry ??= Geometry.Parse(
        "M9 16.17L4.83 12L3.41 13.41L9 19L21 7L19.59 5.59L9 16.17Z");

    private static Geometry ChevronRightGeometry => s_chevronRightGeometry ??= Geometry.Parse(
        "M8.59 16.59L10 18L16 12L10 6L8.59 7.41L13.17 12L8.59 16.59Z");

    private static Geometry ChevronLeftGeometry => s_chevronLeftGeometry ??= Geometry.Parse(
        "M15.41 7.41L14 6L8 12L14 18L15.41 16.59L10.83 12L15.41 7.41Z");

    private const double ItemRowHeight = 34.0;
    private const double BackRowHeight = 32.0;
    private const double SeparatorHeight = 9.0;
    private const double RowSpacing = 2.0;
    private const double MenuPadding = 6.0;

    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<VelaGlassMenu, bool>(
            nameof(IsOpen),
            false,
            defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<Geometry?> TriggerIconProperty =
        AvaloniaProperty.Register<VelaGlassMenu, Geometry?>(nameof(TriggerIcon));

    public static readonly StyledProperty<string?> TriggerLabelProperty =
        AvaloniaProperty.Register<VelaGlassMenu, string?>(nameof(TriggerLabel));

    public static readonly StyledProperty<double> TriggerSizeProperty =
        AvaloniaProperty.Register<VelaGlassMenu, double>(nameof(TriggerSize), 38.0);

    public static readonly StyledProperty<double> MenuWidthProperty =
        AvaloniaProperty.Register<VelaGlassMenu, double>(nameof(MenuWidth), 228.0);

    public static readonly StyledProperty<double> MenuCornerRadiusProperty =
        AvaloniaProperty.Register<VelaGlassMenu, double>(nameof(MenuCornerRadius), 18.0);

    public static readonly StyledProperty<double> BlurRadiusProperty =
        AvaloniaProperty.Register<VelaGlassMenu, double>(nameof(BlurRadius), 44.0);

    public static readonly StyledProperty<bool> BlurBackgroundProperty =
        AvaloniaProperty.Register<VelaGlassMenu, bool>(nameof(BlurBackground), true);

    public static readonly StyledProperty<VelaGlassTintLevel> TintLevelProperty =
        AvaloniaProperty.Register<VelaGlassMenu, VelaGlassTintLevel>(
            nameof(TintLevel),
            VelaGlassTintLevel.Standard);

    public static readonly StyledProperty<Thickness> RimThicknessProperty =
        AvaloniaProperty.Register<VelaGlassMenu, Thickness>(nameof(RimThickness), new Thickness(1.2));

    public static readonly StyledProperty<BoxShadows> MenuShadowProperty =
        AvaloniaProperty.Register<VelaGlassMenu, BoxShadows>(nameof(MenuShadow));

    public static readonly StyledProperty<bool> CloseOnItemClickProperty =
        AvaloniaProperty.Register<VelaGlassMenu, bool>(nameof(CloseOnItemClick), true);

    public bool IsOpen { get => GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }
    public Geometry? TriggerIcon { get => GetValue(TriggerIconProperty); set => SetValue(TriggerIconProperty, value); }
    public string? TriggerLabel { get => GetValue(TriggerLabelProperty); set => SetValue(TriggerLabelProperty, value); }
    public double TriggerSize { get => GetValue(TriggerSizeProperty); set => SetValue(TriggerSizeProperty, value); }
    public double MenuWidth { get => GetValue(MenuWidthProperty); set => SetValue(MenuWidthProperty, value); }
    public double MenuCornerRadius { get => GetValue(MenuCornerRadiusProperty); set => SetValue(MenuCornerRadiusProperty, value); }
    public double BlurRadius { get => GetValue(BlurRadiusProperty); set => SetValue(BlurRadiusProperty, value); }
    public bool BlurBackground { get => GetValue(BlurBackgroundProperty); set => SetValue(BlurBackgroundProperty, value); }
    public VelaGlassTintLevel TintLevel { get => GetValue(TintLevelProperty); set => SetValue(TintLevelProperty, value); }
    public Thickness RimThickness { get => GetValue(RimThicknessProperty); set => SetValue(RimThicknessProperty, value); }
    public BoxShadows MenuShadow { get => GetValue(MenuShadowProperty); set => SetValue(MenuShadowProperty, value); }
    public bool CloseOnItemClick { get => GetValue(CloseOnItemClickProperty); set => SetValue(CloseOnItemClickProperty, value); }

    [Content]
    public ObservableCollection<VelaMenuItem> Items { get; } = new();

    public bool IsInSubmenu => _submenuStack.Count > 0;
    public VelaMenuItem? ActiveSubmenuParent => _submenuStack.Count > 0 ? _submenuStack.Peek() : null;

    public event EventHandler? Opened;
    public event EventHandler? Closed;
    public event EventHandler<VelaMenuItem>? ItemInvoked;

    protected override Type StyleKeyOverride => typeof(VelaGlassMenu);

    private sealed class MenuRowVisual
    {
        public required Border Row;
        public required VelaMenuItem? Item; // null for submenu Back header
        public required bool IsBackRow;
        public required double NominalY;
        public required double NominalHeight;
        public PathIcon? LeadingIcon;
        public PathIcon? CheckIcon;
        public TextBlock? HeaderText;
    }

    private Border? _shell;
    private Border? _blurBackdrop;
    private Panel? _triggerLayer;
    private PathIcon? _triggerIcon;
    private TextBlock? _triggerLabel;
    private Grid? _menuLayer;
    private Border? _highlight;
    private TranslateTransform? _hlTranslate;
    private TranslateTransform? _menuRevealTranslate;
    private ScaleTransform? _triggerScale;
    private StackPanel? _itemsHost;

    private TopLevel? _hookedTopLevel;
    private readonly Stack<VelaMenuItem> _submenuStack = new();
    private readonly List<MenuRowVisual> _rows = new();
    private readonly List<VelaMenuItem> _subscribedItems = new();
    private int _focusedRowIndex = -1;
    private bool _highlightPlaced;
    private bool _highlightIsDestructive;

    private readonly SpringRunner _runner;
    private readonly SpringChannel _morphW = new(38.0, VelaMotionTokens.Springs.Snappy);
    private readonly SpringChannel _morphH = new(38.0, VelaMotionTokens.Springs.Snappy);
    private readonly SpringChannel _morphRadius = new(19.0, VelaMotionTokens.Springs.Snappy);
    private readonly SpringChannel _reveal = new(0.0, VelaMotionTokens.Springs.Snappy);
    private readonly SpringChannel _hlY = new(0.0, VelaMotionTokens.Springs.Snappy);
    private readonly SpringChannel _hlH = new(ItemRowHeight, VelaMotionTokens.Springs.Snappy);
    private readonly SpringChannel _hlOpacity = new(0.0, VelaMotionTokens.Springs.Snappy);

    static VelaGlassMenu()
    {
        FocusableProperty.OverrideDefaultValue<VelaGlassMenu>(true);

        IsOpenProperty.Changed.AddClassHandler<VelaGlassMenu>((m, e) =>
            m.OnIsOpenChanged(e.NewValue is true));

        TriggerIconProperty.Changed.AddClassHandler<VelaGlassMenu>((m, _) => m.SyncTriggerVisual());
        TriggerLabelProperty.Changed.AddClassHandler<VelaGlassMenu>((m, _) =>
        {
            m.SyncTriggerVisual();
            m.UpdateMorphTargets(animate: false);
        });
        TriggerSizeProperty.Changed.AddClassHandler<VelaGlassMenu>((m, _) => m.UpdateMorphTargets(animate: false));
        MenuWidthProperty.Changed.AddClassHandler<VelaGlassMenu>((m, _) => m.UpdateMorphTargets(animate: m.IsOpen));
        MenuCornerRadiusProperty.Changed.AddClassHandler<VelaGlassMenu>((m, _) => m.UpdateMorphTargets(animate: m.IsOpen));
        BlurRadiusProperty.Changed.AddClassHandler<VelaGlassMenu>((m, _) => m.SyncBackdropBlur());
        BlurBackgroundProperty.Changed.AddClassHandler<VelaGlassMenu>((m, _) => m.SyncBackdropBlur());
    }

    public VelaGlassMenu()
    {
        _runner = new SpringRunner(this, OnTick);
        Items.CollectionChanged += OnItemsCollectionChanged;
    }

    public void Open() => SetCurrentValue(IsOpenProperty, true);

    public void Close() => SetCurrentValue(IsOpenProperty, false);

    public void Toggle() => SetCurrentValue(IsOpenProperty, !IsOpen);

    public void NavigateInto(VelaMenuItem submenuParent)
    {
        if (submenuParent is null || !submenuParent.HasSubItems)
            return;

        _submenuStack.Push(submenuParent);
        RebuildRows();
        UpdateMorphTargets(animate: true);
    }

    public bool NavigateBack()
    {
        if (_submenuStack.Count == 0)
            return false;

        _submenuStack.Pop();
        RebuildRows();
        UpdateMorphTargets(animate: true);
        return true;
    }

    /// <summary>
    /// Computes the natural expanded menu height for the currently active item list.
    /// Exposed internally for deterministic testing.
    /// </summary>
    internal double ComputeActiveMenuHeight()
    {
        var active = GetActiveItems();
        double h = MenuPadding * 2.0;
        var visibleCount = 0;

        if (_submenuStack.Count > 0)
        {
            h += BackRowHeight;
            visibleCount++;
            h += RowSpacing + SeparatorHeight;
            visibleCount++;
        }

        foreach (var item in active)
        {
            if (!item.IsVisible)
                continue;
            if (visibleCount > 0)
                h += RowSpacing;
            h += item.IsSeparator ? SeparatorHeight : ItemRowHeight;
            visibleCount++;
        }

        return Math.Max(TriggerSize, h);
    }

    internal (double Width, double Height, double Radius) ComputeTriggerMetrics()
    {
        var h = Math.Max(28.0, TriggerSize);
        var radius = h * 0.5;
        if (string.IsNullOrWhiteSpace(TriggerLabel))
            return (h, h, radius);

        var ft = new FormattedText(
            TriggerLabel,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface(FontFamily ?? FontFamily.Default, FontStyle.Normal, FontWeight.Medium),
            13.0,
            Brushes.Black);
        var w = Math.Max(h, ft.Width + 46.0);
        return (w, h, radius);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (_triggerLayer is not null)
            _triggerLayer.PointerPressed -= OnTriggerPointerPressed;
        if (_menuLayer is not null)
            _menuLayer.PointerExited -= OnMenuLayerPointerExited;

        _shell = e.NameScope.Find<Border>("PART_Shell");
        _blurBackdrop = e.NameScope.Find<Border>("PART_BlurBackdrop");
        _triggerLayer = e.NameScope.Find<Panel>("PART_TriggerLayer");
        _triggerIcon = e.NameScope.Find<PathIcon>("PART_TriggerIcon");
        _triggerLabel = e.NameScope.Find<TextBlock>("PART_TriggerLabel");
        _menuLayer = e.NameScope.Find<Grid>("PART_MenuLayer");
        _highlight = e.NameScope.Find<Border>("PART_Highlight");
        _itemsHost = e.NameScope.Find<StackPanel>("PART_ItemsHost");

        if (_triggerLayer is not null)
        {
            _triggerScale = new ScaleTransform(1.0, 1.0);
            _triggerLayer.RenderTransform = _triggerScale;
            _triggerLayer.RenderTransformOrigin = RelativePoint.Center;
            _triggerLayer.PointerPressed += OnTriggerPointerPressed;
        }

        if (_menuLayer is not null)
        {
            _menuRevealTranslate = new TranslateTransform(0.0, 0.0);
            _menuLayer.RenderTransform = _menuRevealTranslate;
            _menuLayer.PointerExited += OnMenuLayerPointerExited;
        }

        if (_highlight is not null)
        {
            _hlTranslate = new TranslateTransform(0.0, 0.0);
            _highlight.RenderTransform = _hlTranslate;
        }

        SyncBackdropBlur();
        SyncTriggerVisual();
        RebuildRows();
        UpdateMorphTargets(animate: false);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        VelaTheme.AccentChanged += OnThemeChanged;
        ActualThemeVariantChanged += OnThemeVariantChanged;
        if (IsOpen)
            AttachOutsideClickHook();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        VelaTheme.AccentChanged -= OnThemeChanged;
        ActualThemeVariantChanged -= OnThemeVariantChanged;
        DetachOutsideClickHook();
        UnsubscribeRowItems();
        _runner.Stop();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (!IsOpen)
        {
            if (e.Key is Key.Enter or Key.Space or Key.Down)
            {
                Open();
                e.Handled = true;
            }
            return;
        }

        switch (e.Key)
        {
            case Key.Escape:
                Close();
                e.Handled = true;
                break;
            case Key.Down:
                MoveFocus(1);
                e.Handled = true;
                break;
            case Key.Up:
                MoveFocus(-1);
                e.Handled = true;
                break;
            case Key.Home:
                FocusEdge(first: true);
                e.Handled = true;
                break;
            case Key.End:
                FocusEdge(first: false);
                e.Handled = true;
                break;
            case Key.Right:
                if (_focusedRowIndex >= 0 && _focusedRowIndex < _rows.Count)
                {
                    var entry = _rows[_focusedRowIndex];
                    if (entry.Item is { HasSubItems: true, IsEnabled: true })
                    {
                        NavigateInto(entry.Item);
                        e.Handled = true;
                    }
                }
                break;
            case Key.Left:
            case Key.Back:
                if (NavigateBack())
                    e.Handled = true;
                break;
            case Key.Enter:
            case Key.Space:
                if (_focusedRowIndex >= 0 && _focusedRowIndex < _rows.Count)
                {
                    ActivateRow(_rows[_focusedRowIndex]);
                    e.Handled = true;
                }
                break;
        }
    }

    private void OnItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (VelaMenuItem oldItem in e.OldItems)
                LogicalChildren.Remove(oldItem);
        }

        if (e.NewItems is not null)
        {
            foreach (VelaMenuItem newItem in e.NewItems)
            {
                if (!LogicalChildren.Contains(newItem))
                    LogicalChildren.Add(newItem);
            }
        }

        if (_itemsHost is not null)
        {
            RebuildRows();
            if (IsOpen)
                UpdateMorphTargets(animate: true);
        }
    }

    private void OnIsOpenChanged(bool open)
    {
        PseudoClasses.Set(":open", open);

        if (open)
        {
            _submenuStack.Clear();
            RebuildRows();
            AttachOutsideClickHook();
            Focus();
            UpdateMorphTargets(animate: true);
            Opened?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            DetachOutsideClickHook();
            HideHighlight();
            _focusedRowIndex = -1;
            UpdateMorphTargets(animate: true);
            Closed?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnTriggerPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            Open();
            e.Handled = true;
        }
    }

    private void OnMenuLayerPointerExited(object? sender, PointerEventArgs e)
    {
        if (IsOpen)
        {
            _focusedRowIndex = -1;
            HideHighlight();
        }
    }

    private void AttachOutsideClickHook()
    {
        DetachOutsideClickHook();
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null)
            return;

        _hookedTopLevel = topLevel;
        _hookedTopLevel.AddHandler(PointerPressedEvent, OnTopLevelPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    private void DetachOutsideClickHook()
    {
        if (_hookedTopLevel is null)
            return;

        _hookedTopLevel.RemoveHandler(PointerPressedEvent, OnTopLevelPointerPressed);
        _hookedTopLevel = null;
    }

    private void OnTopLevelPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!IsOpen)
            return;

        if (e.Source is Visual v && this.IsVisualAncestorOf(v))
            return;

        Close();
    }

    private void SyncBackdropBlur()
    {
        if (_blurBackdrop is null)
            return;

        _blurBackdrop.BackdropEffect = BlurBackground && BlurRadius > 0.1
            ? new BlurEffect { Radius = Math.Clamp(BlurRadius, 0.0, 120.0) }
            : null;
    }

    private void SyncTriggerVisual()
    {
        if (_triggerIcon is not null)
            _triggerIcon.Data = TriggerIcon ?? DefaultMoreIcon;

        if (_triggerLabel is not null)
        {
            var hasLabel = !string.IsNullOrWhiteSpace(TriggerLabel);
            _triggerLabel.Text = TriggerLabel ?? string.Empty;
            _triggerLabel.IsVisible = hasLabel;
        }
    }

    private IEnumerable<VelaMenuItem> GetActiveItems()
    {
        return _submenuStack.Count > 0 ? _submenuStack.Peek().Items : Items;
    }

    private void UnsubscribeRowItems()
    {
        foreach (var item in _subscribedItems)
            item.PropertyChanged -= OnMenuItemPropertyChanged;
        _subscribedItems.Clear();
    }

    private void OnMenuItemPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (sender is not VelaMenuItem item)
            return;

        if (e.Property == VelaMenuItem.IsCheckedProperty)
        {
            foreach (var row in _rows)
            {
                if (ReferenceEquals(row.Item, item))
                    ApplyItemCheckedVisual(row);
            }
        }
        else if (e.Property == VelaMenuItem.HeaderProperty ||
                 e.Property == VelaMenuItem.IsEnabledProperty ||
                 e.Property == VelaMenuItem.IsVisibleProperty)
        {
            RebuildRows();
            if (IsOpen)
                UpdateMorphTargets(animate: true);
        }
    }

    private void RebuildRows()
    {
        if (_itemsHost is null)
            return;

        UnsubscribeRowItems();
        _itemsHost.Children.Clear();
        _rows.Clear();
        _focusedRowIndex = -1;
        _highlightPlaced = false;
        HideHighlight();

        double y = 0.0;
        var childIndex = 0;

        if (_submenuStack.Count > 0)
        {
            var parent = _submenuStack.Peek();
            var backRow = BuildBackRow(parent.Header ?? "Back", y);
            _rows.Add(backRow);
            _itemsHost.Children.Add(backRow.Row);
            y += BackRowHeight + RowSpacing;
            childIndex++;

            _itemsHost.Children.Add(BuildSeparator());
            y += SeparatorHeight + RowSpacing;
            childIndex++;
        }

        foreach (var item in GetActiveItems())
        {
            if (!item.IsVisible)
                continue;

            item.PropertyChanged += OnMenuItemPropertyChanged;
            _subscribedItems.Add(item);

            if (item.IsSeparator)
            {
                _itemsHost.Children.Add(BuildSeparator());
                y += SeparatorHeight + RowSpacing;
                childIndex++;
                continue;
            }

            var visual = BuildItemRow(item, y);
            _rows.Add(visual);
            _itemsHost.Children.Add(visual.Row);
            y += ItemRowHeight + RowSpacing;
            childIndex++;
        }
    }

    private MenuRowVisual BuildBackRow(string title, double nominalY)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            ColumnSpacing = 8,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var chevron = new PathIcon
        {
            Width = 12,
            Height = 12,
            Data = ChevronLeftGeometry,
            VerticalAlignment = VerticalAlignment.Center,
        };
        chevron.Bind(PathIcon.ForegroundProperty, this.GetResourceObservable("VelaTextSecondaryBrush"));

        var label = new TextBlock
        {
            Text = title,
            FontSize = 12.5,
            FontWeight = FontWeight.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        label.Bind(TextBlock.ForegroundProperty, this.GetResourceObservable("VelaForegroundBrush"));
        Grid.SetColumn(label, 1);

        grid.Children.Add(chevron);
        grid.Children.Add(label);

        var row = new Border
        {
            Height = BackRowHeight,
            Padding = new Thickness(10, 0),
            CornerRadius = new CornerRadius(10),
            Background = Brushes.Transparent,
            Cursor = new Cursor(StandardCursorType.Hand),
            Child = grid,
        };

        var entry = new MenuRowVisual
        {
            Row = row,
            Item = null,
            IsBackRow = true,
            NominalY = nominalY,
            NominalHeight = BackRowHeight,
            HeaderText = label,
        };

        row.PointerEntered += (_, _) => HoverRow(entry);
        row.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(row).Properties.IsLeftButtonPressed)
            {
                ActivateRow(entry);
                e.Handled = true;
            }
        };

        return entry;
    }

    private MenuRowVisual BuildItemRow(VelaMenuItem item, double nominalY)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto"),
            ColumnSpacing = 9,
            VerticalAlignment = VerticalAlignment.Center,
        };

        PathIcon? leadingIcon = null;
        if (item.Icon is not null)
        {
            leadingIcon = new PathIcon
            {
                Width = 14,
                Height = 14,
                Data = item.Icon,
                VerticalAlignment = VerticalAlignment.Center,
            };
            grid.Children.Add(leadingIcon);
        }

        var header = new TextBlock
        {
            Text = item.Header ?? string.Empty,
            FontSize = 13,
            FontWeight = FontWeight.Normal,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        Grid.SetColumn(header, 1);
        grid.Children.Add(header);

        if (!string.IsNullOrEmpty(item.Shortcut))
        {
            var shortcut = new TextBlock
            {
                Text = item.Shortcut,
                FontSize = 11.5,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 0, 0),
            };
            shortcut.Bind(TextBlock.ForegroundProperty, this.GetResourceObservable("VelaTextMutedBrush"));
            Grid.SetColumn(shortcut, 2);
            grid.Children.Add(shortcut);
        }

        PathIcon? checkIcon = null;
        if (item.HasSubItems)
        {
            var chevron = new PathIcon
            {
                Width = 11,
                Height = 11,
                Data = ChevronRightGeometry,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 0, 0, 0),
            };
            chevron.Bind(PathIcon.ForegroundProperty, this.GetResourceObservable("VelaTextMutedBrush"));
            Grid.SetColumn(chevron, 3);
            grid.Children.Add(chevron);
        }
        else
        {
            checkIcon = new PathIcon
            {
                Width = 13,
                Height = 13,
                Data = CheckIconGeometry,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(2, 0, 0, 0),
                IsVisible = item.IsChecked,
            };
            checkIcon.Bind(PathIcon.ForegroundProperty, this.GetResourceObservable("VelaAccentBrush"));
            Grid.SetColumn(checkIcon, 3);
            grid.Children.Add(checkIcon);
        }

        var row = new Border
        {
            Height = ItemRowHeight,
            Padding = new Thickness(10, 0),
            CornerRadius = new CornerRadius(10),
            Background = Brushes.Transparent,
            Opacity = item.IsEnabled ? 1.0 : 0.42,
            IsHitTestVisible = item.IsEnabled,
            Cursor = item.IsEnabled ? new Cursor(StandardCursorType.Hand) : Cursor.Default,
            Child = grid,
        };

        var entry = new MenuRowVisual
        {
            Row = row,
            Item = item,
            IsBackRow = false,
            NominalY = nominalY,
            NominalHeight = ItemRowHeight,
            LeadingIcon = leadingIcon,
            CheckIcon = checkIcon,
            HeaderText = header,
        };

        ApplyItemCheckedVisual(entry);

        row.PointerEntered += (_, _) => HoverRow(entry);
        row.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(row).Properties.IsLeftButtonPressed)
            {
                ActivateRow(entry);
                e.Handled = true;
            }
        };

        return entry;
    }

    private void ApplyItemCheckedVisual(MenuRowVisual entry)
    {
        if (entry.Item is null)
            return;

        var item = entry.Item;
        if (entry.CheckIcon is not null)
            entry.CheckIcon.IsVisible = item.IsChecked;

        if (item.IsDestructive)
        {
            var danger = this.GetResourceObservable("VelaDangerBrush");
            entry.HeaderText?.Bind(TextBlock.ForegroundProperty, danger);
            entry.LeadingIcon?.Bind(PathIcon.ForegroundProperty, danger);
        }
        else
        {
            entry.HeaderText?.Bind(TextBlock.ForegroundProperty, this.GetResourceObservable("VelaForegroundBrush"));
            var iconKey = item.IsChecked ? "VelaAccentBrush" : "VelaTextSecondaryBrush";
            entry.LeadingIcon?.Bind(PathIcon.ForegroundProperty, this.GetResourceObservable(iconKey));
        }
    }

    private Control BuildSeparator()
    {
        var line = new Border
        {
            Height = 1,
            Margin = new Thickness(10, 4),
            IsHitTestVisible = false,
        };
        line.Bind(Border.BackgroundProperty, this.GetResourceObservable("VelaGlassDividerBrush"));
        return line;
    }

    private void HoverRow(MenuRowVisual entry)
    {
        var idx = _rows.IndexOf(entry);
        if (idx >= 0)
            _focusedRowIndex = idx;

        var y = entry.Row.Bounds.Height > 1.0 ? entry.Row.Bounds.Y : entry.NominalY;
        var h = entry.Row.Bounds.Height > 1.0 ? entry.Row.Bounds.Height : entry.NominalHeight;
        var isDestructive = entry.Item?.IsDestructive == true;

        ApplyHighlightTone(isDestructive);

        if (!_highlightPlaced || VelaMotionOptions.ReducedMotion)
        {
            _highlightPlaced = true;
            _hlY.Jump(y);
            _hlH.Jump(h);
            if (VelaMotionOptions.ReducedMotion)
                _hlOpacity.Jump(1.0);
            else
                _hlOpacity.SetTarget(1.0);
            ApplyHighlightVisual();
        }
        else
        {
            _hlY.SetTarget(y);
            _hlH.SetTarget(h);
            _hlOpacity.SetTarget(1.0);
        }

        if (!VelaMotionOptions.ReducedMotion)
            _runner.Wake();
    }

    private void HideHighlight()
    {
        _highlightPlaced = false;

        if (VelaMotionOptions.ReducedMotion)
        {
            _hlOpacity.Jump(0.0);
            ApplyHighlightVisual();
        }
        else
        {
            _hlOpacity.SetTarget(0.0);
            _runner.Wake();
        }
    }

    private void ApplyHighlightTone(bool isDestructive)
    {
        if (_highlight is null || _highlightIsDestructive == isDestructive)
            return;

        _highlightIsDestructive = isDestructive;
        var isDark = ActualThemeVariant == ThemeVariant.Dark;

        if (isDestructive)
        {
            _highlight.Background = new SolidColorBrush(Color.Parse(isDark ? "#38F43F5E" : "#22E11D48"));
            _highlight.BorderBrush = new SolidColorBrush(Color.Parse(isDark ? "#4CF43F5E" : "#33E11D48"));
        }
        else
        {
            _highlight.Bind(Border.BackgroundProperty, this.GetResourceObservable("VelaGlassMenuHighlightBrush"));
            _highlight.Bind(Border.BorderBrushProperty, this.GetResourceObservable("VelaGlassMenuHighlightBorderBrush"));
        }
    }

    private void ActivateRow(MenuRowVisual entry)
    {
        if (entry.IsBackRow)
        {
            NavigateBack();
            return;
        }

        var item = entry.Item;
        if (item is null || !item.IsEnabled)
            return;

        if (item.HasSubItems)
        {
            NavigateInto(item);
            return;
        }

        item.Invoke();
        ItemInvoked?.Invoke(this, item);
        ApplyItemCheckedVisual(entry);

        if (CloseOnItemClick)
            Close();
    }

    private void MoveFocus(int delta)
    {
        if (_rows.Count == 0)
            return;

        var start = _focusedRowIndex;
        for (var step = 0; step < _rows.Count; step++)
        {
            var next = start < 0
                ? (delta > 0 ? 0 : _rows.Count - 1)
                : (start + delta * (step + 1) + _rows.Count * 4) % _rows.Count;

            var candidate = _rows[next];
            if (candidate.IsBackRow || candidate.Item?.IsEnabled == true)
            {
                HoverRow(candidate);
                return;
            }
        }
    }

    private void FocusEdge(bool first)
    {
        if (_rows.Count == 0)
            return;

        if (first)
        {
            for (var i = 0; i < _rows.Count; i++)
            {
                if (_rows[i].IsBackRow || _rows[i].Item?.IsEnabled == true)
                {
                    HoverRow(_rows[i]);
                    return;
                }
            }
        }
        else
        {
            for (var i = _rows.Count - 1; i >= 0; i--)
            {
                if (_rows[i].IsBackRow || _rows[i].Item?.IsEnabled == true)
                {
                    HoverRow(_rows[i]);
                    return;
                }
            }
        }
    }

    private void UpdateMorphTargets(bool animate)
    {
        var (triggerW, triggerH, triggerR) = ComputeTriggerMetrics();
        double targetW, targetH, targetRadius, targetReveal;
        if (IsOpen)
        {
            targetW = Math.Max(triggerW, MenuWidth);
            targetH = ComputeActiveMenuHeight();
            targetRadius = Math.Clamp(MenuCornerRadius, 8.0, 28.0);
            targetReveal = 1.0;
        }
        else
        {
            targetW = triggerW;
            targetH = triggerH;
            targetRadius = triggerR;
            targetReveal = 0.0;
        }

        if (!animate || VelaMotionOptions.ReducedMotion || TopLevel.GetTopLevel(this) is null)
        {
            _morphW.Jump(targetW);
            _morphH.Jump(targetH);
            _morphRadius.Jump(targetRadius);
            _reveal.Jump(targetReveal);
            ApplyMorphVisual();
        }
        else
        {
            _morphW.SetTarget(targetW);
            _morphH.SetTarget(targetH);
            _morphRadius.SetTarget(targetRadius);
            _reveal.SetTarget(targetReveal);
            _runner.Wake();
        }
    }

    private bool OnTick(double dt)
    {
        var wMoving = _morphW.Step(dt);
        var hMoving = _morphH.Step(dt);
        var rMoving = _morphRadius.Step(dt);
        var revMoving = _reveal.Step(dt);

        var hlYMoving = _hlY.Step(dt);
        var hlHMoving = _hlH.Step(dt);
        var hlOMoving = _hlOpacity.Step(dt);

        ApplyMorphVisual();
        ApplyHighlightVisual();

        return wMoving || hMoving || rMoving || revMoving || hlYMoving || hlHMoving || hlOMoving;
    }

    private void ApplyMorphVisual()
    {
        if (_shell is not null)
        {
            _shell.Width = Math.Max(20.0, _morphW.Value);
            _shell.Height = Math.Max(20.0, _morphH.Value);
            _shell.CornerRadius = new CornerRadius(Math.Max(4.0, _morphRadius.Value));
        }

        var rev = Math.Clamp(_reveal.Value, 0.0, 1.0);

        if (_triggerLayer is not null)
        {
            var trigOpacity = Math.Clamp(1.0 - rev * 2.4, 0.0, 1.0);
            _triggerLayer.Opacity = trigOpacity;
            _triggerLayer.IsVisible = trigOpacity > 0.01;
            _triggerLayer.IsHitTestVisible = !IsOpen && trigOpacity > 0.2;

            if (_triggerScale is not null)
            {
                var s = 1.0 - rev * 0.14;
                _triggerScale.ScaleX = s;
                _triggerScale.ScaleY = s;
            }
        }

        if (_menuLayer is not null)
        {
            var menuOpacity = Math.Clamp((rev - 0.16) / 0.62, 0.0, 1.0);
            _menuLayer.Opacity = menuOpacity;
            _menuLayer.IsVisible = menuOpacity > 0.01 || IsOpen;
            _menuLayer.IsHitTestVisible = IsOpen && menuOpacity > 0.35;

            if (_menuRevealTranslate is not null)
                _menuRevealTranslate.Y = (1.0 - menuOpacity) * -4.0;
        }
    }

    private void ApplyHighlightVisual()
    {
        if (_highlight is null)
            return;

        _highlight.Height = Math.Max(16.0, _hlH.Value);
        _highlight.Opacity = Math.Clamp(_hlOpacity.Value, 0.0, 1.0);
        if (_hlTranslate is not null)
            _hlTranslate.Y = _hlY.Value;
    }

    private void OnThemeChanged(object? sender, Tokens.VelaAccent accent)
    {
        foreach (var row in _rows)
            ApplyItemCheckedVisual(row);
    }

    private void OnThemeVariantChanged(object? sender, EventArgs e)
    {
        var wasDestructive = _highlightIsDestructive;
        _highlightIsDestructive = !wasDestructive;
        ApplyHighlightTone(wasDestructive);
    }
}
