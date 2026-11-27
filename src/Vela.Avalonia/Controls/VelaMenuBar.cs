using System.Collections.ObjectModel;
using System.Collections.Specialized;
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
using Vela.Avalonia.Tokens;

namespace Vela.Avalonia.Controls;

/// <summary>
/// Top-level menu item inside <see cref="VelaMenuBar"/> (e.g. File, Edit, View, Help).
/// Opens a <see cref="VelaPopupSurface"/> menu hosting declarative <see cref="VelaMenuItem"/> entries.
/// </summary>
[PseudoClasses(":open", ":compact")]
public class VelaMenuBarItem : TemplatedControl
{
    public static readonly StyledProperty<string?> HeaderProperty =
        AvaloniaProperty.Register<VelaMenuBarItem, string?>(nameof(Header));

    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<VelaMenuBarItem, bool>(nameof(IsOpen), false, defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<bool> IsCompactProperty =
        AvaloniaProperty.Register<VelaMenuBarItem, bool>(nameof(IsCompact), false);

    public static readonly StyledProperty<double> MinMenuWidthProperty =
        AvaloniaProperty.Register<VelaMenuBarItem, double>(nameof(MinMenuWidth), 204.0);

    public static readonly StyledProperty<VelaSurfaceMaterial> SurfaceMaterialProperty =
        AvaloniaProperty.Register<VelaMenuBarItem, VelaSurfaceMaterial>(nameof(SurfaceMaterial), VelaSurfaceMaterial.Solid);

    public string? Header { get => GetValue(HeaderProperty); set => SetValue(HeaderProperty, value); }
    public bool IsOpen { get => GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }
    public bool IsCompact { get => GetValue(IsCompactProperty); set => SetValue(IsCompactProperty, value); }
    public double MinMenuWidth { get => GetValue(MinMenuWidthProperty); set => SetValue(MinMenuWidthProperty, value); }
    public VelaSurfaceMaterial SurfaceMaterial { get => GetValue(SurfaceMaterialProperty); set => SetValue(SurfaceMaterialProperty, value); }

    [Content]
    public ObservableCollection<VelaMenuItem> Items { get; } = new();

    internal VelaMenuBar? OwnerBar { get; set; }

    protected override Type StyleKeyOverride => typeof(VelaMenuBarItem);

    private Button? _triggerButton;
    private Popup? _popup;
    private VelaPopupSurface? _popupSurface;
    private StackPanel? _menuHost;

    static VelaMenuBarItem()
    {
        IsOpenProperty.Changed.AddClassHandler<VelaMenuBarItem>((item, _) => item.OnIsOpenChanged());
        IsCompactProperty.Changed.AddClassHandler<VelaMenuBarItem>((item, _) => item.PseudoClasses.Set(":compact", item.IsCompact));
    }

    public VelaMenuBarItem()
    {
        Items.CollectionChanged += OnItemsChanged;
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (_triggerButton is not null)
        {
            _triggerButton.Click -= OnTriggerClick;
            _triggerButton.PointerEntered -= OnTriggerPointerEntered;
        }
        if (_popup is not null)
        {
            _popup.Closed -= OnPopupClosed;
        }

        _triggerButton = e.NameScope.Find<Button>("PART_Trigger");
        _popup = e.NameScope.Find<Popup>("PART_Popup");
        _popupSurface = e.NameScope.Find<VelaPopupSurface>("PART_PopupSurface");
        _menuHost = e.NameScope.Find<StackPanel>("PART_MenuHost");

        if (_triggerButton is not null)
        {
            _triggerButton.Click += OnTriggerClick;
            _triggerButton.PointerEntered += OnTriggerPointerEntered;
        }
        if (_popup is not null)
        {
            _popup.Closed += OnPopupClosed;
        }

        RebuildMenuVisuals();
        OnIsOpenChanged();
    }

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (IsOpen)
            RebuildMenuVisuals();
    }

    private void OnTriggerClick(object? sender, RoutedEventArgs e)
    {
        IsOpen = !IsOpen;
    }

    private void OnTriggerPointerEntered(object? sender, PointerEventArgs e)
    {
        // Classic desktop menu bar behavior: hovering another top-level menu while one is open switches immediately
        if (OwnerBar is { ActiveItem: not null } bar && !ReferenceEquals(bar.ActiveItem, this))
        {
            bar.OpenItem(this);
        }
    }

    private void OnPopupClosed(object? sender, EventArgs e)
    {
        if (IsOpen)
            SetCurrentValue(IsOpenProperty, false);
    }

    private void OnIsOpenChanged()
    {
        PseudoClasses.Set(":open", IsOpen);
        OwnerBar?.NotifyItemOpenStateChanged(this, IsOpen);

        if (IsOpen)
        {
            RebuildMenuVisuals();
            if (_popup is not null)
                _popup.IsOpen = true;
            _popupSurface?.PlayEnter();
        }
        else if (_popup is not null)
        {
            _popup.IsOpen = false;
        }
    }

    private void RebuildMenuVisuals()
    {
        if (_menuHost is null)
            return;

        _menuHost.Children.Clear();

        foreach (var item in Items)
        {
            if (!item.IsVisible)
                continue;

            if (item.IsSeparator)
            {
                _menuHost.Children.Add(new Border
                {
                    Height = 1,
                    Margin = new Thickness(6, 4),
                    Background = ResolveBrush("VelaBorderSubtleBrush", Brushes.Gray),
                });
                continue;
            }

            _menuHost.Children.Add(BuildMenuRow(item));
        }
    }

    private Button BuildMenuRow(VelaMenuItem item)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("18,*,Auto"),
            ColumnSpacing = 8,
            VerticalAlignment = VerticalAlignment.Center,
        };

        if (item.Icon is not null)
        {
            var icon = new PathIcon
            {
                Width = 13.5,
                Height = 13.5,
                Data = item.Icon,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = item.IsDestructive
                    ? ResolveBrush("VelaDangerBrush", Brushes.Crimson)
                    : ResolveBrush("VelaTextSecondaryBrush", Brushes.Gray),
            };
            Grid.SetColumn(icon, 0);
            grid.Children.Add(icon);
        }
        else if (item.IsChecked)
        {
            var check = new Border
            {
                Width = 6,
                Height = 6,
                CornerRadius = new CornerRadius(99),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Background = ResolveBrush("VelaAccentBrush", Brushes.DodgerBlue),
            };
            Grid.SetColumn(check, 0);
            grid.Children.Add(check);
        }

        var header = new TextBlock
        {
            Text = item.Header,
            FontSize = 12.5,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = item.IsDestructive
                ? ResolveBrush("VelaDangerBrush", Brushes.Crimson)
                : ResolveBrush("VelaForegroundBrush", Brushes.White),
        };
        Grid.SetColumn(header, 1);
        grid.Children.Add(header);

        if (!string.IsNullOrEmpty(item.Shortcut))
        {
            var shortcut = new TextBlock
            {
                Text = item.Shortcut,
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(16, 0, 0, 0),
                Foreground = ResolveBrush("VelaTextMutedBrush", Brushes.Gray),
            };
            Grid.SetColumn(shortcut, 2);
            grid.Children.Add(shortcut);
        }

        var row = new Button
        {
            Content = grid,
            IsEnabled = item.IsEnabled,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Height = 30,
            Padding = new Thickness(8, 0, 10, 0),
            CornerRadius = new CornerRadius(7),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Cursor = new Cursor(StandardCursorType.Hand),
        };

        if (this.TryFindResource("VelaIconButtonTheme", ActualThemeVariant, out var themeObj) && themeObj is ControlTheme ct)
            row.Theme = ct;

        row.Click += (_, _) =>
        {
            IsOpen = false;
            item.Invoke();
        };

        return row;
    }

    private IBrush ResolveBrush(string key, IBrush fallback) =>
        this.TryFindResource(key, ActualThemeVariant, out var res) && res is IBrush b ? b : fallback;
}

/// <summary>
/// Desktop application menu bar hosting <see cref="VelaMenuBarItem"/> entries (File, Edit, View, Help).
/// Designed to fit naturally inside <see cref="VelaWindow.TitleBarLeftContent"/>, <see cref="VelaWindow.TitleBarContent"/>,
/// or above workspace content.
/// </summary>
[PseudoClasses(":compact")]
public class VelaMenuBar : TemplatedControl
{
    public static readonly StyledProperty<bool> IsCompactProperty =
        AvaloniaProperty.Register<VelaMenuBar, bool>(nameof(IsCompact), false);

    public static readonly StyledProperty<VelaSurfaceMaterial> SurfaceMaterialProperty =
        AvaloniaProperty.Register<VelaMenuBar, VelaSurfaceMaterial>(nameof(SurfaceMaterial), VelaSurfaceMaterial.Solid);

    public bool IsCompact { get => GetValue(IsCompactProperty); set => SetValue(IsCompactProperty, value); }
    public VelaSurfaceMaterial SurfaceMaterial { get => GetValue(SurfaceMaterialProperty); set => SetValue(SurfaceMaterialProperty, value); }

    [Content]
    public ObservableCollection<VelaMenuBarItem> Items { get; } = new();

    public VelaMenuBarItem? ActiveItem { get; private set; }

    protected override Type StyleKeyOverride => typeof(VelaMenuBar);

    private StackPanel? _itemsHost;

    static VelaMenuBar()
    {
        IsCompactProperty.Changed.AddClassHandler<VelaMenuBar>((b, _) => b.SyncItemsState());
        SurfaceMaterialProperty.Changed.AddClassHandler<VelaMenuBar>((b, _) => b.SyncItemsState());
    }

    public VelaMenuBar()
    {
        Items.CollectionChanged += OnItemsChanged;
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _itemsHost = e.NameScope.Find<StackPanel>("PART_ItemsHost");
        RebuildItems();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape && ActiveItem is not null)
        {
            CloseAll();
            e.Handled = true;
        }
    }

    public void OpenItem(VelaMenuBarItem item)
    {
        if (!Items.Contains(item))
            return;

        foreach (var other in Items)
        {
            if (!ReferenceEquals(other, item) && other.IsOpen)
                other.IsOpen = false;
        }

        item.IsOpen = true;
        ActiveItem = item;
    }

    public void CloseAll()
    {
        foreach (var item in Items)
        {
            if (item.IsOpen)
                item.IsOpen = false;
        }
        ActiveItem = null;
    }

    internal void NotifyItemOpenStateChanged(VelaMenuBarItem item, bool isOpen)
    {
        if (isOpen)
        {
            foreach (var other in Items)
            {
                if (!ReferenceEquals(other, item) && other.IsOpen)
                    other.IsOpen = false;
            }
            ActiveItem = item;
        }
        else if (ReferenceEquals(ActiveItem, item))
        {
            ActiveItem = null;
        }
    }

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RebuildItems();
    }

    private void RebuildItems()
    {
        foreach (var item in Items)
        {
            item.OwnerBar = this;
            item.IsCompact = IsCompact;
            item.SurfaceMaterial = SurfaceMaterial;
        }

        if (_itemsHost is null)
            return;

        _itemsHost.Children.Clear();
        foreach (var item in Items)
        {
            if (item.Parent is Panel oldPanel)
                oldPanel.Children.Remove(item);
            _itemsHost.Children.Add(item);
        }
    }

    private void SyncItemsState()
    {
        PseudoClasses.Set(":compact", IsCompact);
        foreach (var item in Items)
        {
            item.IsCompact = IsCompact;
            item.SurfaceMaterial = SurfaceMaterial;
        }
    }
}
