using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Primitives.PopupPositioning;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Metadata;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Vela.Avalonia.Motion;

namespace Vela.Avalonia.Controls;

/// <summary>
/// Port of Arc UI's ContextMenu for Vela.Avalonia:
/// - Attachable declaratively to any <see cref="Control"/> via <see cref="MenuProperty"/> or opened imperatively via <see cref="ShowAt"/>
/// - Floating <see cref="VelaPopupSurface"/> with snappy spring entrance anchored to the pointer or target element
/// - Gliding spring hover highlight pill tracking pointer and keyboard focus, transitioning softly to danger tint on destructive items
/// - Full keyboard navigation (ArrowUp/Down, Home/End, Enter/Space, Escape) and single-active-menu coordination
/// </summary>
public class VelaContextMenu : StyledElement
{
    private static VelaContextMenu? s_activeMenu;

    /// <summary>
    /// Attached property that binds a <see cref="VelaContextMenu"/> to any <see cref="Control"/>'s context-menu gesture
    /// (right-click, Shift+F10, ContextMenu key).
    /// </summary>
    public static readonly AttachedProperty<VelaContextMenu?> MenuProperty =
        AvaloniaProperty.RegisterAttached<VelaContextMenu, Control, VelaContextMenu?>("Menu");

    public static VelaContextMenu? GetMenu(Control control) => control.GetValue(MenuProperty);
    public static void SetMenu(Control control, VelaContextMenu? value) => control.SetValue(MenuProperty, value);

    public static readonly StyledProperty<double> MenuMinWidthProperty =
        AvaloniaProperty.Register<VelaContextMenu, double>(nameof(MenuMinWidth), 204.0);

    public static readonly StyledProperty<double> MenuMaxHeightProperty =
        AvaloniaProperty.Register<VelaContextMenu, double>(nameof(MenuMaxHeight), 320.0);

    public static readonly StyledProperty<PlacementMode> PlacementProperty =
        AvaloniaProperty.Register<VelaContextMenu, PlacementMode>(nameof(Placement), PlacementMode.Pointer);

    public static readonly StyledProperty<double> HorizontalOffsetProperty =
        AvaloniaProperty.Register<VelaContextMenu, double>(nameof(HorizontalOffset), 0.0);

    public static readonly StyledProperty<double> VerticalOffsetProperty =
        AvaloniaProperty.Register<VelaContextMenu, double>(nameof(VerticalOffset), 0.0);

    public static readonly DirectProperty<VelaContextMenu, bool> IsOpenProperty =
        AvaloniaProperty.RegisterDirect<VelaContextMenu, bool>(nameof(IsOpen), o => o.IsOpen);

    public double MenuMinWidth { get => GetValue(MenuMinWidthProperty); set => SetValue(MenuMinWidthProperty, value); }
    public double MenuMaxHeight { get => GetValue(MenuMaxHeightProperty); set => SetValue(MenuMaxHeightProperty, value); }
    public PlacementMode Placement { get => GetValue(PlacementProperty); set => SetValue(PlacementProperty, value); }
    public double HorizontalOffset { get => GetValue(HorizontalOffsetProperty); set => SetValue(HorizontalOffsetProperty, value); }
    public double VerticalOffset { get => GetValue(VerticalOffsetProperty); set => SetValue(VerticalOffsetProperty, value); }

    private bool _isOpen;
    public bool IsOpen
    {
        get => _isOpen;
        private set => SetAndRaise(IsOpenProperty, ref _isOpen, value);
    }

    [Content]
    public ObservableCollection<VelaMenuItem> Items { get; } = new();

    public event EventHandler<CancelEventArgs>? Opening;
    public event EventHandler? Opened;
    public event EventHandler? Closed;

    private Popup? _popup;
    private VelaPopupSurface? _surface;
    private Grid? _menuTrack;
    private StackPanel? _itemsHost;
    private Border? _highlight;
    private TranslateTransform? _hlTranslate;
    private Control? _target;
    private bool _ownsInheritedDataContext;
    private bool _highlightPlaced;
    private int _focusedRowIndex = -1;
    private readonly List<MenuRowEntry> _rows = new();
    private VelaContextMenu? _parentMenu;
    private VelaContextMenu? _activeSubmenu;
    private Border? _activeSubmenuAnchor;

    private readonly SpringChannel _hlY = new(0.0, VelaMotionTokens.Springs.Snappy);
    private readonly SpringChannel _hlH = new(34.0, VelaMotionTokens.Springs.Snappy);
    private readonly SpringChannel _hlOpacity = new(0.0, VelaMotionTokens.Springs.Snappy);
    private SpringRunner? _runner;

    private sealed record MenuRowEntry(VelaMenuItem Item, Border Row);

    static VelaContextMenu()
    {
        MenuProperty.Changed.AddClassHandler<Control>((control, e) =>
        {
            if (e.OldValue is VelaContextMenu oldMenu)
            {
                control.ContextRequested -= OnControlContextRequested;
                control.DetachedFromVisualTree -= OnControlDetached;
                if (ReferenceEquals(oldMenu._target, control))
                    oldMenu.Close();
            }

            if (e.NewValue is VelaContextMenu)
            {
                control.ContextRequested += OnControlContextRequested;
                control.DetachedFromVisualTree += OnControlDetached;
            }
        });
    }

    public VelaContextMenu()
    {
        Items.CollectionChanged += OnItemsCollectionChanged;
    }

    private static void OnControlContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        if (sender is not Control control)
            return;

        var menu = GetMenu(control);
        if (menu is null)
            return;

        var showAtPointer = e.TryGetPosition(control, out _);
        menu.ShowAt(control, showAtPointer);
        e.Handled = true;
    }

    private static void OnControlDetached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (sender is not Control control)
            return;

        var menu = GetMenu(control);
        if (menu is not null && ReferenceEquals(menu._target, control))
            menu.Close();
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
    }

    /// <summary>
    /// Opens this context menu anchored to <paramref name="target"/>.
    /// When <paramref name="showAtPointer"/> is <see langword="true"/>, the menu opens at the current pointer location;
    /// otherwise it opens below <paramref name="target"/> using <see cref="Placement"/>.
    /// </summary>
    public void ShowAt(Control target, bool showAtPointer = true)
    {
        ArgumentNullException.ThrowIfNull(target);

        if (s_activeMenu is not null && !ReferenceEquals(s_activeMenu, this))
            s_activeMenu.Close();

        if (IsOpen)
            Close();

        _target = target;
        SyncDataContextFromTarget(target);

        var cancelArgs = new CancelEventArgs();
        Opening?.Invoke(this, cancelArgs);
        if (cancelArgs.Cancel)
            return;

        if (!HasVisibleRows())
            return;

        EnsurePopup();
        RebuildRows();

        if (_popup is null || _surface is null)
            return;

        _popup.PlacementTarget = target;
        _popup.Placement = showAtPointer
            ? PlacementMode.Pointer
            : (Placement == PlacementMode.Pointer ? PlacementMode.BottomEdgeAlignedLeft : Placement);
        _popup.PlacementGravity = showAtPointer ? PopupGravity.BottomRight : PopupGravity.BottomRight;
        _popup.HorizontalOffset = HorizontalOffset;
        _popup.VerticalOffset = showAtPointer ? VerticalOffset : (VerticalOffset == 0.0 ? 4.0 : VerticalOffset);

        _surface.EnterOffsetX = 0.0;
        _surface.EnterOffsetY = showAtPointer ? -3.0 : -5.0;
        _surface.RenderTransformOrigin = showAtPointer
            ? new RelativePoint(0.0, 0.0, RelativeUnit.Relative)
            : new RelativePoint(0.5, 0.0, RelativeUnit.Relative);

        if (_popup.Parent is null)
            ((ISetLogicalParent)_popup).SetParent(target);
        else if (!ReferenceEquals(_popup.Parent, target))
        {
            ((ISetLogicalParent)_popup).SetParent(null);
            ((ISetLogicalParent)_popup).SetParent(target);
        }

        _highlightPlaced = false;
        _focusedRowIndex = -1;
        _hlOpacity.Jump(0.0);
        if (_highlight is not null)
            _highlight.Opacity = 0.0;

        s_activeMenu = this;
        IsOpen = true;
        _popup.IsOpen = true;
        _surface.PlayEnter();

        Dispatcher.UIThread.Post(() =>
        {
            if (IsOpen)
                _surface?.Focus();
        }, DispatcherPriority.Input);

        Opened?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Closes this context menu if it is currently open.</summary>
    public void Close()
    {
        CloseSubmenu();

        _runner?.Stop();
        _highlightPlaced = false;
        _focusedRowIndex = -1;
        _hlOpacity.Jump(0.0);
        if (_highlight is not null)
            _highlight.Opacity = 0.0;

        var wasOpen = IsOpen;
        IsOpen = false;

        if (_popup is not null)
        {
            if (_popup.IsOpen)
                _popup.IsOpen = false;
            if (_popup.Parent is not null)
                ((ISetLogicalParent)_popup).SetParent(null);
        }

        if (ReferenceEquals(s_activeMenu, this))
            s_activeMenu = null;

        if (wasOpen)
            Closed?.Invoke(this, EventArgs.Empty);
    }

    private void CloseHierarchy()
    {
        var root = this;
        while (root._parentMenu is not null)
            root = root._parentMenu;
        root.Close();
    }

    private void CloseSubmenu()
    {
        if (_activeSubmenu is null)
            return;

        var submenu = _activeSubmenu;
        _activeSubmenu = null;
        _activeSubmenuAnchor = null;
        submenu._parentMenu = null;
        submenu.Close();
    }

    private void OpenSubmenu(VelaMenuItem parentItem, Border anchorRow)
    {
        if (!parentItem.HasSubItems)
            return;

        if (_activeSubmenu is not null && ReferenceEquals(_activeSubmenuAnchor, anchorRow))
            return;

        CloseSubmenu();

        var submenu = new VelaContextMenu
        {
            MenuMinWidth = Math.Max(168.0, MenuMinWidth - 16.0),
            MenuMaxHeight = MenuMaxHeight,
            _parentMenu = this,
        };
        foreach (var child in parentItem.Items)
            submenu.Items.Add(child);

        _activeSubmenu = submenu;
        _activeSubmenuAnchor = anchorRow;
        submenu.ShowAsSubmenu(anchorRow);
    }

    private void ShowAsSubmenu(Control anchorRow)
    {
        _target = anchorRow;
        SyncDataContextFromTarget(anchorRow);

        if (!HasVisibleRows())
            return;

        EnsurePopup();
        RebuildRows();

        if (_popup is null || _surface is null)
            return;

        _popup.IsLightDismissEnabled = false;
        _popup.PlacementTarget = anchorRow;
        _popup.Placement = PlacementMode.RightEdgeAlignedTop;
        _popup.HorizontalOffset = 4.0;
        _popup.VerticalOffset = -5.0;

        _surface.EnterOffsetX = -4.0;
        _surface.EnterOffsetY = 0.0;
        _surface.RenderTransformOrigin = new RelativePoint(0.0, 0.0, RelativeUnit.Relative);

        if (_popup.Parent is not null)
            ((ISetLogicalParent)_popup).SetParent(null);
        ((ISetLogicalParent)_popup).SetParent(anchorRow);

        _highlightPlaced = false;
        _focusedRowIndex = -1;
        _hlOpacity.Jump(0.0);
        if (_highlight is not null)
            _highlight.Opacity = 0.0;

        IsOpen = true;
        _popup.IsOpen = true;
        _surface.PlayEnter();
    }

    internal static int ComputeNextFocusableIndex(IReadOnlyList<bool> enabledFlags, int currentIndex, Key key)
    {
        var count = enabledFlags.Count;
        if (count == 0)
            return -1;

        var hasAnyEnabled = false;
        for (var i = 0; i < count; i++)
        {
            if (enabledFlags[i])
            {
                hasAnyEnabled = true;
                break;
            }
        }

        if (!hasAnyEnabled)
            return -1;

        switch (key)
        {
            case Key.Home:
                for (var i = 0; i < count; i++)
                {
                    if (enabledFlags[i]) return i;
                }
                return -1;

            case Key.End:
                for (var i = count - 1; i >= 0; i--)
                {
                    if (enabledFlags[i]) return i;
                }
                return -1;

            case Key.Down:
                for (var step = 1; step <= count; step++)
                {
                    var idx = currentIndex < 0 ? (step - 1) % count : (currentIndex + step) % count;
                    if (enabledFlags[idx]) return idx;
                }
                return -1;

            case Key.Up:
                for (var step = 1; step <= count; step++)
                {
                    var idx = currentIndex < 0
                        ? (count - step + count) % count
                        : (currentIndex - step + count * 2) % count;
                    if (enabledFlags[idx]) return idx;
                }
                return -1;

            default:
                return currentIndex;
        }
    }

    private void SyncDataContextFromTarget(Control target)
    {
        if (!IsSet(DataContextProperty) || _ownsInheritedDataContext)
        {
            _ownsInheritedDataContext = true;
            DataContext = target.DataContext;
        }
    }

    private bool HasVisibleRows()
    {
        foreach (var item in Items)
        {
            if (item.IsVisible && !item.IsSeparator)
                return true;
        }
        return false;
    }

    private void EnsurePopup()
    {
        if (_popup is not null)
            return;

        _hlTranslate = new TranslateTransform();
        _highlight = new Border
        {
            CornerRadius = new CornerRadius(9),
            Opacity = 0,
            IsHitTestVisible = false,
            RenderTransform = _hlTranslate,
        };

        var highlightCanvas = new Canvas
        {
            IsHitTestVisible = false,
            Children = { _highlight },
        };

        _itemsHost = new StackPanel
        {
            Spacing = 1,
        };

        _menuTrack = new Grid
        {
            Children = { highlightCanvas, _itemsHost },
        };
        _menuTrack.PointerExited += OnMenuPointerExited;

        var scrollViewer = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MaxHeight = MenuMaxHeight,
            Content = _menuTrack,
        };

        _surface = new VelaPopupSurface
        {
            Margin = new Thickness(6, 4, 6, 14),
            MinWidth = MenuMinWidth,
            Padding = new Thickness(5),
            CornerRadius = new CornerRadius(14),
            BorderThickness = new Thickness(1),
            Focusable = true,
            Child = scrollViewer,
        };
        _surface[!Border.BackgroundProperty] = VelaResourceBinding.Create("VelaSurfaceRaisedBrush");
        _surface[!Border.BorderBrushProperty] = VelaResourceBinding.Create("VelaBorderBrush");
        _surface[!Border.BoxShadowProperty] = VelaResourceBinding.Create("VelaShadowFloating");
        _surface.KeyDown += OnSurfaceKeyDown;

        _highlight[!Border.BackgroundProperty] = VelaResourceBinding.Create("VelaSurfaceMutedBrush");
        _runner = new SpringRunner(_surface, OnTick);

        _popup = new Popup
        {
            WindowManagerAddShadowHint = false,
            IsLightDismissEnabled = true,
            PlacementConstraintAdjustment = PopupPositionerConstraintAdjustment.FlipX
                | PopupPositionerConstraintAdjustment.FlipY
                | PopupPositionerConstraintAdjustment.SlideX
                | PopupPositionerConstraintAdjustment.SlideY,
            Child = _surface,
        };
        _popup.Closed += OnPopupClosed;
    }

    private void RebuildRows()
    {
        if (_itemsHost is null || _surface is null)
            return;

        _surface.MinWidth = MenuMinWidth;
        if (_surface.Child is ScrollViewer sv)
            sv.MaxHeight = MenuMaxHeight;

        _itemsHost.Children.Clear();
        _rows.Clear();

        var hasAnyLeadingVisual = false;
        foreach (var item in Items)
        {
            if (item.IsVisible && !item.IsSeparator && (item.Icon is not null || item.IsChecked))
            {
                hasAnyLeadingVisual = true;
                break;
            }
        }

        var lastWasSeparator = true;
        foreach (var item in Items)
        {
            if (!item.IsVisible)
                continue;

            if (item.IsSeparator)
            {
                if (lastWasSeparator)
                    continue;

                var sep = new Border
                {
                    Height = 1,
                    Margin = new Thickness(6, 4),
                };
                sep[!Border.BackgroundProperty] = VelaResourceBinding.Create("VelaBorderSubtleBrush");
                _itemsHost.Children.Add(sep);
                lastWasSeparator = true;
                continue;
            }

            var rowIndex = _rows.Count;
            var row = BuildRow(item, rowIndex, hasAnyLeadingVisual);
            _rows.Add(new MenuRowEntry(item, row));
            _itemsHost.Children.Add(row);
            lastWasSeparator = false;
        }

        // Trim trailing separator if present
        while (_itemsHost.Children.Count > 0
               && _itemsHost.Children[^1] is Border trailing
               && Math.Abs(trailing.Height - 1.0) < 0.01)
        {
            _itemsHost.Children.RemoveAt(_itemsHost.Children.Count - 1);
        }
    }

    private Border BuildRow(VelaMenuItem item, int rowIndex, bool reserveIconColumn)
    {
        var surface = _surface!;
        var grid = new Grid
        {
            ColumnDefinitions = reserveIconColumn
                ? new ColumnDefinitions("18,*,Auto")
                : new ColumnDefinitions("Auto,*,Auto"),
            ColumnSpacing = reserveIconColumn ? 9 : 0,
            MinHeight = 34,
        };

        var fgKey = item.IsDestructive ? "VelaDangerBrush" : "VelaForegroundBrush";
        var iconKey = item.IsDestructive ? "VelaDangerBrush" : "VelaTextSecondaryBrush";

        if (item.Icon is not null)
        {
            var icon = new PathIcon
            {
                Width = 15,
                Height = 15,
                Data = item.Icon,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            icon[!PathIcon.ForegroundProperty] = VelaResourceBinding.Create(iconKey);
            grid.Children.Add(icon);
        }
        else if (item.IsChecked)
        {
            var checkIcon = new PathIcon
            {
                Width = 14,
                Height = 14,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            checkIcon[!PathIcon.DataProperty] = VelaResourceBinding.Create("VelaIconCheck");
            checkIcon[!PathIcon.ForegroundProperty] = VelaResourceBinding.Create(iconKey);
            grid.Children.Add(checkIcon);
        }

        var label = new TextBlock
        {
            Text = item.Header,
            FontSize = 13.5,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
        };
        label[!TextBlock.ForegroundProperty] = VelaResourceBinding.Create(fgKey);
        Grid.SetColumn(label, 1);
        grid.Children.Add(label);

        if (item.HasSubItems)
        {
            var chevron = new PathIcon
            {
                Width = 12,
                Height = 12,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 0, 0, 0),
            };
            chevron[!PathIcon.DataProperty] = VelaResourceBinding.Create("VelaIconChevronRight");
            chevron[!PathIcon.ForegroundProperty] = VelaResourceBinding.Create("VelaTextMutedBrush");
            Grid.SetColumn(chevron, 2);
            grid.Children.Add(chevron);
        }
        else if (!string.IsNullOrEmpty(item.Shortcut))
        {
            var shortcut = new TextBlock
            {
                Text = item.Shortcut,
                FontSize = 11.5,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(14, 0, 0, 0),
            };
            shortcut[!TextBlock.ForegroundProperty] = VelaResourceBinding.Create("VelaTextMutedBrush");
            Grid.SetColumn(shortcut, 2);
            grid.Children.Add(shortcut);
        }

        var rowBorder = new Border
        {
            Padding = new Thickness(10, 0),
            CornerRadius = new CornerRadius(9),
            Background = Brushes.Transparent,
            Cursor = item.IsEnabled ? new Cursor(StandardCursorType.Hand) : Cursor.Default,
            Opacity = item.IsEnabled ? 1.0 : 0.45,
            Child = grid,
        };

        if (item.IsEnabled)
        {
            rowBorder.PointerEntered += (_, _) =>
            {
                _focusedRowIndex = rowIndex;
                HighlightRow(rowBorder, item.IsDestructive, glide: true);
                if (item.HasSubItems)
                    OpenSubmenu(item, rowBorder);
                else
                    CloseSubmenu();
            };
            rowBorder.PointerPressed += (_, ev) =>
            {
                if (!ev.GetCurrentPoint(rowBorder).Properties.IsLeftButtonPressed)
                    return;

                if (item.HasSubItems)
                {
                    OpenSubmenu(item, rowBorder);
                    ev.Handled = true;
                    return;
                }

                var isDark = surface.ActualThemeVariant == ThemeVariant.Dark;
                rowBorder.Background = isDark
                    ? new SolidColorBrush(Color.FromArgb(0x16, 0xFF, 0xFF, 0xFF))
                    : new SolidColorBrush(Color.FromArgb(0x0D, 0x00, 0x00, 0x00));
                ev.Pointer.Capture(rowBorder);
                ev.Handled = true;
            };
            rowBorder.PointerReleased += (_, ev) =>
            {
                if (ReferenceEquals(ev.Pointer.Captured, rowBorder))
                    ev.Pointer.Capture(null);

                rowBorder.Background = Brushes.Transparent;
                if (item.HasSubItems || ev.InitialPressMouseButton != MouseButton.Left)
                    return;

                var pt = ev.GetPosition(rowBorder);
                if (pt.X >= 0 && pt.Y >= 0 && pt.X <= rowBorder.Bounds.Width && pt.Y <= rowBorder.Bounds.Height)
                {
                    CloseHierarchy();
                    item.Invoke();
                    ev.Handled = true;
                }
            };
            rowBorder.PointerExited += (_, _) =>
            {
                rowBorder.Background = Brushes.Transparent;
            };
        }

        return rowBorder;
    }

    private void OnMenuPointerExited(object? sender, PointerEventArgs e)
    {
        if (_activeSubmenu is { IsOpen: true })
            return;

        _focusedRowIndex = -1;
        _hlOpacity.SetTarget(0.0, VelaMotionTokens.Springs.Snappy);
        _runner?.Wake();
    }

    private void OnSurfaceKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            if (_parentMenu is not null)
            {
                var parent = _parentMenu;
                parent.CloseSubmenu();
                parent._surface?.Focus();
            }
            else
            {
                var anchor = _target;
                Close();
                anchor?.Focus();
            }
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Left && _parentMenu is not null)
        {
            var parent = _parentMenu;
            parent.CloseSubmenu();
            parent._surface?.Focus();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Right && _focusedRowIndex >= 0 && _focusedRowIndex < _rows.Count)
        {
            var entry = _rows[_focusedRowIndex];
            if (entry.Item.IsEnabled && entry.Item.HasSubItems)
            {
                OpenSubmenu(entry.Item, entry.Row);
                Dispatcher.UIThread.Post(() => _activeSubmenu?._surface?.Focus(), DispatcherPriority.Input);
                e.Handled = true;
                return;
            }
        }

        if (e.Key is Key.Enter or Key.Space)
        {
            if (_focusedRowIndex >= 0 && _focusedRowIndex < _rows.Count)
            {
                var entry = _rows[_focusedRowIndex];
                if (entry.Item.IsEnabled)
                {
                    if (entry.Item.HasSubItems)
                    {
                        OpenSubmenu(entry.Item, entry.Row);
                        Dispatcher.UIThread.Post(() => _activeSubmenu?._surface?.Focus(), DispatcherPriority.Input);
                    }
                    else
                    {
                        CloseHierarchy();
                        entry.Item.Invoke();
                    }
                    e.Handled = true;
                }
            }
            return;
        }

        if (e.Key is Key.Down or Key.Up or Key.Home or Key.End)
        {
            var flags = new bool[_rows.Count];
            for (var i = 0; i < _rows.Count; i++)
                flags[i] = _rows[i].Item.IsEnabled;

            var next = ComputeNextFocusableIndex(flags, _focusedRowIndex, e.Key);
            if (next >= 0)
            {
                _focusedRowIndex = next;
                var entry = _rows[next];
                HighlightRow(entry.Row, entry.Item.IsDestructive, glide: false);
                e.Handled = true;
            }
        }
    }

    private void OnPopupClosed(object? sender, EventArgs e)
    {
        if (IsOpen)
            Close();
    }

    private void HighlightRow(Control row, bool destructive, bool glide)
    {
        if (_menuTrack is null || _highlight is null || _surface is null)
            return;

        var pos = row.TranslatePoint(new Point(0, 0), _menuTrack);
        if (!pos.HasValue)
            return;

        var bgKey = destructive ? "VelaDangerSubtleBrush" : "VelaSurfaceMutedBrush";
        if (_surface.TryFindResource(bgKey, _surface.ActualThemeVariant, out var res) && res is IBrush brush)
            _highlight.Background = brush;

        _highlight.Width = _menuTrack.Bounds.Width;

        if (!_highlightPlaced || !glide || VelaMotionOptions.ReducedMotion)
        {
            _highlightPlaced = true;
            _hlY.Jump(pos.Value.Y);
            _hlH.Jump(row.Bounds.Height);
            _hlOpacity.SetTarget(1.0, VelaMotionTokens.Springs.Snappy);
            ApplyHighlight();
            _runner?.Wake();
            return;
        }

        _hlY.SetTarget(pos.Value.Y, VelaMotionTokens.Springs.Snappy);
        _hlH.SetTarget(row.Bounds.Height, VelaMotionTokens.Springs.Snappy);
        _hlOpacity.SetTarget(1.0, VelaMotionTokens.Springs.Snappy);
        _runner?.Wake();
    }

    private bool OnTick(double dt)
    {
        var active = _hlY.Step(dt);
        active |= _hlH.Step(dt);
        active |= _hlOpacity.Step(dt);
        ApplyHighlight();
        return active;
    }

    private void ApplyHighlight()
    {
        if (_highlight is null || _hlTranslate is null)
            return;

        _hlTranslate.Y = _hlY.Value;
        _highlight.Height = Math.Max(0, _hlH.Value);
        _highlight.Opacity = Math.Clamp(_hlOpacity.Value, 0.0, 1.0);
    }
}
