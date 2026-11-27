using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Metadata;
using Avalonia.VisualTree;

namespace Vela.Avalonia.Controls;

/// <summary>
/// Projected row state inside <see cref="VelaDataGrid"/>.
/// </summary>
public sealed class VelaDataGridRowState
{
    internal VelaDataGridRowState(VelaDataGrid owner, object item, int rowIndex, bool isSelected)
    {
        Owner = owner;
        Item = item;
        RowIndex = rowIndex;
        IsSelected = isSelected;
    }

    internal VelaDataGrid Owner { get; }
    public object Item { get; }
    public int RowIndex { get; internal set; }
    public bool IsSelected { get; internal set; }
}

/// <summary>
/// Reusable interactive column header cell with click-to-sort, sort indicator glyph,
/// and smooth pointer-drag column resizing (double-click divider to reset width).
/// Shared by <see cref="VelaDataGrid"/> and <see cref="VelaTreeTable"/>.
/// </summary>
[PseudoClasses(":sortable", ":sorted", ":resizing")]
internal sealed class VelaTableHeaderCell : Border
{
    private readonly Control _owner;
    private readonly VelaDataGridColumn _column;
    private readonly Action<VelaDataGridColumn> _onSortClicked;
    private readonly Action<VelaDataGridColumn, double> _onResized;
    private readonly Action<VelaDataGridColumn> _onResetWidth;
    private readonly Func<bool> _canSortGlobal;
    private readonly Func<bool> _canResizeGlobal;

    private readonly PathIcon _sortIcon;
    private readonly Border _resizeGrip;
    private readonly Border _resizeLine;
    private readonly Border _verticalRule;

    private bool _isDraggingResize;
    private Point _dragStartPoint;
    private double _dragStartWidth;

    private bool _showVerticalGridLines;

    public VelaDataGridColumn Column => _column;

    public VelaTableHeaderCell(
        Control owner,
        VelaDataGridColumn column,
        Action<VelaDataGridColumn> onSortClicked,
        Action<VelaDataGridColumn, double> onResized,
        Action<VelaDataGridColumn> onResetWidth,
        Func<bool> canSortGlobal,
        Func<bool> canResizeGlobal,
        bool showVerticalGridLines)
    {
        _owner = owner;
        _column = column;
        _onSortClicked = onSortClicked;
        _onResized = onResized;
        _onResetWidth = onResetWidth;
        _canSortGlobal = canSortGlobal;
        _canResizeGlobal = canResizeGlobal;

        Classes.Add("vela-table-header-cell");
        Background = Brushes.Transparent;
        Height = 38;
        ClipToBounds = true;

        var contentGrid = new Grid
        {
            ColumnDefinitions = ColumnDefinitions.Parse("*,Auto"),
            Margin = new Thickness(12, 0, 10, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };

        var headerPresenter = new ContentPresenter
        {
            Content = column.Header,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = column.HorizontalAlignment,
        };
        if (column.Header is string headerStr)
        {
            headerPresenter.Content = new TextBlock
            {
                Text = headerStr,
                FontSize = 12,
                FontWeight = FontWeight.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = column.HorizontalAlignment,
                [!TextBlock.ForegroundProperty] = this.GetResourceObservable("VelaTextSecondaryBrush").ToBinding(),
            };
        }

        _sortIcon = new PathIcon
        {
            Width = 12,
            Height = 12,
            Margin = new Thickness(6, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            IsVisible = false,
            [!PathIcon.ForegroundProperty] = this.GetResourceObservable("VelaAccentBrush").ToBinding(),
        };

        Grid.SetColumn(headerPresenter, 0);
        Grid.SetColumn(_sortIcon, 1);
        contentGrid.Children.Add(headerPresenter);
        contentGrid.Children.Add(_sortIcon);

        _verticalRule = new Border
        {
            Width = 1,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Stretch,
            Margin = new Thickness(0, showVerticalGridLines ? 0 : 9, 0, showVerticalGridLines ? 0 : 9),
            IsHitTestVisible = false,
            [!Border.BackgroundProperty] = this.GetResourceObservable("VelaBorderSubtleBrush").ToBinding(),
        };

        _resizeLine = new Border
        {
            Width = 2,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Stretch,
            Opacity = 0,
            IsHitTestVisible = false,
            [!Border.BackgroundProperty] = this.GetResourceObservable("VelaAccentBrush").ToBinding(),
        };

        _resizeGrip = new Border
        {
            Width = 8,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Stretch,
            Background = Brushes.Transparent,
            Cursor = new Cursor(StandardCursorType.SizeWestEast),
        };
        _resizeGrip.PointerEntered += (_, _) =>
        {
            if (CanResizeNow)
                _resizeLine.Opacity = 0.7;
        };
        _resizeGrip.PointerExited += (_, _) =>
        {
            if (!_isDraggingResize)
                _resizeLine.Opacity = 0;
        };
        _resizeGrip.PointerPressed += OnResizePointerPressed;
        _resizeGrip.PointerMoved += OnResizePointerMoved;
        _resizeGrip.PointerReleased += OnResizePointerReleased;
        _resizeGrip.DoubleTapped += OnResizeDoubleTapped;

        var root = new Panel();
        root.Children.Add(contentGrid);
        root.Children.Add(_verticalRule);
        root.Children.Add(_resizeLine);
        root.Children.Add(_resizeGrip);

        Child = root;
        SyncState(showVerticalGridLines);
    }

    private bool CanSortNow => _canSortGlobal() && _column.CanSort;
    private bool CanResizeNow => _canResizeGlobal() && _column.CanResize;

    public void SyncState(bool showVerticalGridLines)
    {
        _showVerticalGridLines = showVerticalGridLines;
        Width = Math.Max(24.0, _column.ActualWidth);
        _resizeGrip.IsVisible = CanResizeNow;
        _verticalRule.Margin = new Thickness(0, showVerticalGridLines ? 0 : 9, 0, showVerticalGridLines ? 0 : 9);

        var sortable = CanSortNow;
        PseudoClasses.Set(":sortable", sortable);
        PseudoClasses.Set(":sorted", _column.SortDirection != VelaSortDirection.None);
        Cursor = sortable ? new Cursor(StandardCursorType.Hand) : Cursor.Default;

        if (_column.SortDirection == VelaSortDirection.Ascending)
        {
            _sortIcon.IsVisible = true;
            if (this.TryFindResource("VelaIconChevronUp", ActualThemeVariant, out var up) && up is Geometry upGeom)
                _sortIcon.Data = upGeom;
        }
        else if (_column.SortDirection == VelaSortDirection.Descending)
        {
            _sortIcon.IsVisible = true;
            if (this.TryFindResource("VelaIconChevronDown", ActualThemeVariant, out var down) && down is Geometry downGeom)
                _sortIcon.Data = downGeom;
        }
        else
        {
            _sortIcon.IsVisible = false;
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        SyncState(_showVerticalGridLines);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.Handled || !CanSortNow)
            return;

        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _onSortClicked(_column);
            e.Handled = true;
        }
    }

    private void OnResizePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!CanResizeNow || !e.GetCurrentPoint(_resizeGrip).Properties.IsLeftButtonPressed)
            return;

        _isDraggingResize = true;
        _dragStartPoint = e.GetPosition(_owner);
        _dragStartWidth = _column.ActualWidth;
        _resizeLine.Opacity = 1.0;
        PseudoClasses.Set(":resizing", true);
        e.Pointer.Capture(_resizeGrip);
        e.Handled = true;
    }

    private void OnResizePointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isDraggingResize)
            return;

        var current = e.GetPosition(_owner);
        var deltaX = current.X - _dragStartPoint.X;
        _onResized(_column, _dragStartWidth + deltaX);
        e.Handled = true;
    }

    private void OnResizePointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isDraggingResize)
            return;

        _isDraggingResize = false;
        _resizeLine.Opacity = 0;
        PseudoClasses.Set(":resizing", false);
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    private void OnResizeDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (!CanResizeNow)
            return;

        _onResetWidth(_column);
        e.Handled = true;
    }
}

/// <summary>
/// High-performance, Native AOT-safe virtualized data grid with sticky column headers,
/// click-to-sort, interactive column resizing, single/multi selection with optional tri-state
/// header checkbox, built-in search/filter, density presets, and footer slot (e.g. pagination).
/// </summary>
public class VelaDataGrid : TemplatedControl
{
    internal const double CheckBoxColumnWidth = 42.0;

    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty =
        AvaloniaProperty.Register<VelaDataGrid, IEnumerable?>(nameof(ItemsSource));

    public static readonly StyledProperty<object?> SelectedItemProperty =
        AvaloniaProperty.Register<VelaDataGrid, object?>(nameof(SelectedItem), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<VelaSelectionMode> SelectionModeProperty =
        AvaloniaProperty.Register<VelaDataGrid, VelaSelectionMode>(nameof(SelectionMode), VelaSelectionMode.Single);

    public static readonly StyledProperty<VelaDataDensity> DensityProperty =
        AvaloniaProperty.Register<VelaDataGrid, VelaDataDensity>(nameof(Density), VelaDataDensity.Standard);

    public static readonly StyledProperty<bool> ShowCheckBoxesProperty =
        AvaloniaProperty.Register<VelaDataGrid, bool>(nameof(ShowCheckBoxes));

    public static readonly StyledProperty<bool> ShowAlternatingRowsProperty =
        AvaloniaProperty.Register<VelaDataGrid, bool>(nameof(ShowAlternatingRows));

    public static readonly StyledProperty<bool> ShowHorizontalGridLinesProperty =
        AvaloniaProperty.Register<VelaDataGrid, bool>(nameof(ShowHorizontalGridLines), true);

    public static readonly StyledProperty<bool> ShowVerticalGridLinesProperty =
        AvaloniaProperty.Register<VelaDataGrid, bool>(nameof(ShowVerticalGridLines));

    public static readonly StyledProperty<bool> CanUserSortColumnsProperty =
        AvaloniaProperty.Register<VelaDataGrid, bool>(nameof(CanUserSortColumns), true);

    public static readonly StyledProperty<bool> CanUserResizeColumnsProperty =
        AvaloniaProperty.Register<VelaDataGrid, bool>(nameof(CanUserResizeColumns), true);

    public static readonly StyledProperty<string?> SearchTextProperty =
        AvaloniaProperty.Register<VelaDataGrid, string?>(nameof(SearchText));

    public static readonly StyledProperty<object?> EmptyContentProperty =
        AvaloniaProperty.Register<VelaDataGrid, object?>(nameof(EmptyContent));

    public static readonly StyledProperty<object?> FooterContentProperty =
        AvaloniaProperty.Register<VelaDataGrid, object?>(nameof(FooterContent));

    public static readonly DirectProperty<VelaDataGrid, int> VisibleCountProperty =
        AvaloniaProperty.RegisterDirect<VelaDataGrid, int>(nameof(VisibleCount), o => o.VisibleCount);

    public IEnumerable? ItemsSource { get => GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }
    public object? SelectedItem { get => GetValue(SelectedItemProperty); set => SetValue(SelectedItemProperty, value); }
    public VelaSelectionMode SelectionMode { get => GetValue(SelectionModeProperty); set => SetValue(SelectionModeProperty, value); }
    public VelaDataDensity Density { get => GetValue(DensityProperty); set => SetValue(DensityProperty, value); }
    public bool ShowCheckBoxes { get => GetValue(ShowCheckBoxesProperty); set => SetValue(ShowCheckBoxesProperty, value); }
    public bool ShowAlternatingRows { get => GetValue(ShowAlternatingRowsProperty); set => SetValue(ShowAlternatingRowsProperty, value); }
    public bool ShowHorizontalGridLines { get => GetValue(ShowHorizontalGridLinesProperty); set => SetValue(ShowHorizontalGridLinesProperty, value); }
    public bool ShowVerticalGridLines { get => GetValue(ShowVerticalGridLinesProperty); set => SetValue(ShowVerticalGridLinesProperty, value); }
    public bool CanUserSortColumns { get => GetValue(CanUserSortColumnsProperty); set => SetValue(CanUserSortColumnsProperty, value); }
    public bool CanUserResizeColumns { get => GetValue(CanUserResizeColumnsProperty); set => SetValue(CanUserResizeColumnsProperty, value); }
    public string? SearchText { get => GetValue(SearchTextProperty); set => SetValue(SearchTextProperty, value); }
    public object? EmptyContent { get => GetValue(EmptyContentProperty); set => SetValue(EmptyContentProperty, value); }
    public object? FooterContent { get => GetValue(FooterContentProperty); set => SetValue(FooterContentProperty, value); }

    [Content]
    public ObservableCollection<VelaDataGridColumn> Columns { get; } = new();

    private Func<object, bool>? _filterPredicate;
    private Func<object, string, bool>? _searchPredicate;

    /// <summary>Optional custom filter predicate evaluated for every source item.</summary>
    public Func<object, bool>? FilterPredicate
    {
        get => _filterPredicate;
        set { _filterPredicate = value; RebuildRows(); }
    }

    /// <summary>Optional custom search predicate evaluated when <see cref="SearchText"/> is non-empty.</summary>
    public Func<object, string, bool>? SearchPredicate
    {
        get => _searchPredicate;
        set { _searchPredicate = value; RebuildRows(); }
    }

    public VelaDataGridColumn? SortColumn { get; private set; }
    public VelaSortDirection SortDirection { get; private set; }

    public IReadOnlyList<VelaDataGridRowState> VisibleRows => _visibleRows;
    public IReadOnlyList<object> SelectedItems => _selectedItemsList;

    private int _visibleCount;
    public int VisibleCount
    {
        get => _visibleCount;
        private set => SetAndRaise(VisibleCountProperty, ref _visibleCount, value);
    }

    public event EventHandler? SelectionChanged;
    public event EventHandler<VelaDataItemEventArgs>? RowDoubleTapped;
    public event EventHandler<VelaSortChangedEventArgs>? SortChanged;
    public event EventHandler<VelaColumnResizedEventArgs>? ColumnResized;

    protected override Type StyleKeyOverride => typeof(VelaDataGrid);

    internal readonly ObservableCollection<VelaDataGridRowState> _visibleRows = new();
    internal readonly List<VelaDataGridColumn> _visibleColumns = new();
    private readonly HashSet<object> _selectedItems = new(ReferenceEqualityComparer.Instance);
    private readonly List<object> _selectedItemsList = new();
    private readonly List<VelaTableHeaderCell> _headerCells = new();

    private INotifyCollectionChanged? _subscribedSource;
    private ScrollViewer? _headerScroll;
    private StackPanel? _headerPanel;
    private ListBox? _listBox;
    private ScrollViewer? _bodyScroll;
    private ContentPresenter? _emptyHost;
    private VelaCheckBox? _selectAllCheckBox;

    private object? _anchorItem;
    private bool _syncingSelection;
    private bool _suppressSelectAllEvent;
    private double _lastViewportWidth = 640.0;

    static VelaDataGrid()
    {
        FocusableProperty.OverrideDefaultValue<VelaDataGrid>(true);
        ItemsSourceProperty.Changed.AddClassHandler<VelaDataGrid>((g, e) => g.OnItemsSourceChanged(e.NewValue as IEnumerable));
        SelectedItemProperty.Changed.AddClassHandler<VelaDataGrid>((g, e) => g.OnSelectedItemPropertyChanged(e.NewValue));
        SelectionModeProperty.Changed.AddClassHandler<VelaDataGrid>((g, _) => g.OnSelectionModeChanged());
        DensityProperty.Changed.AddClassHandler<VelaDataGrid>((g, _) => g.RefreshVisibleRowsVisual());
        ShowCheckBoxesProperty.Changed.AddClassHandler<VelaDataGrid>((g, _) => g.RebuildHeaderAndLayout());
        ShowAlternatingRowsProperty.Changed.AddClassHandler<VelaDataGrid>((g, _) => g.RefreshVisibleRowsVisual());
        ShowHorizontalGridLinesProperty.Changed.AddClassHandler<VelaDataGrid>((g, _) => g.RefreshVisibleRowsVisual());
        ShowVerticalGridLinesProperty.Changed.AddClassHandler<VelaDataGrid>((g, _) => g.RebuildHeaderAndLayout());
        CanUserSortColumnsProperty.Changed.AddClassHandler<VelaDataGrid>((g, _) => g.SyncHeaderCells());
        CanUserResizeColumnsProperty.Changed.AddClassHandler<VelaDataGrid>((g, _) => g.SyncHeaderCells());
        SearchTextProperty.Changed.AddClassHandler<VelaDataGrid>((g, _) => g.RebuildRows());
    }

    public VelaDataGrid()
    {
        Columns.CollectionChanged += OnColumnsCollectionChanged;
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        DetachBodyScroll();

        _headerScroll = e.NameScope.Find<ScrollViewer>("PART_HeaderScroll");
        _headerPanel = e.NameScope.Find<StackPanel>("PART_HeaderPanel");
        _listBox = e.NameScope.Find<ListBox>("PART_List");
        _emptyHost = e.NameScope.Find<ContentPresenter>("PART_EmptyHost");

        if (_listBox is not null)
        {
            _listBox.ItemsSource = _visibleRows;
            _listBox.ItemTemplate = new FuncDataTemplate<VelaDataGridRowState>((_, _) => new VelaDataGridRowControl(), supportsRecycling: true);
            _listBox.TemplateApplied += OnListBoxTemplateApplied;
            _listBox.PropertyChanged += OnListBoxPropertyChanged;
        }

        RebuildHeaderAndLayout();
        RebuildRows();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        DetachBodyScroll();
        if (_subscribedSource is not null)
        {
            _subscribedSource.CollectionChanged -= OnSourceCollectionChanged;
            _subscribedSource = null;
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (ItemsSource is INotifyCollectionChanged incc && _subscribedSource is null)
        {
            _subscribedSource = incc;
            _subscribedSource.CollectionChanged += OnSourceCollectionChanged;
        }
        RebuildRows();
    }

    /// <summary>Re-evaluates filtering, sorting, and column layout.</summary>
    public void Refresh()
    {
        RecalculateColumnWidths();
        RebuildRows();
    }

    /// <summary>Sorts rows by the specified column and direction.</summary>
    public void SortBy(VelaDataGridColumn? column, VelaSortDirection direction)
    {
        foreach (var col in Columns)
            col.SortDirection = VelaSortDirection.None;

        if (column is not null && direction != VelaSortDirection.None)
        {
            SortColumn = column;
            SortDirection = direction;
            column.SortDirection = direction;
        }
        else
        {
            SortColumn = null;
            SortDirection = VelaSortDirection.None;
        }

        SyncHeaderCells();
        RebuildRows();
        SortChanged?.Invoke(this, new VelaSortChangedEventArgs(SortColumn, SortDirection));
    }

    /// <summary>Cycles sort state on a column: None -> Ascending -> Descending -> None.</summary>
    public void ToggleSort(VelaDataGridColumn column)
    {
        if (!CanUserSortColumns || !column.CanSort)
            return;

        var next = ReferenceEquals(SortColumn, column)
            ? SortDirection switch
            {
                VelaSortDirection.None => VelaSortDirection.Ascending,
                VelaSortDirection.Ascending => VelaSortDirection.Descending,
                _ => VelaSortDirection.None,
            }
            : VelaSortDirection.Ascending;

        SortBy(next == VelaSortDirection.None ? null : column, next);
    }

    /// <summary>Clears any active sort direction.</summary>
    public void ClearSort() => SortBy(null, VelaSortDirection.None);

    /// <summary>Resizes a column to an explicit clamped pixel width.</summary>
    public void ResizeColumn(VelaDataGridColumn column, double requestedWidth)
    {
        var oldWidth = column.ActualWidth;
        var minW = Math.Max(24.0, double.IsFinite(column.MinWidth) ? column.MinWidth : 24.0);
        var maxW = double.IsFinite(column.MaxWidth) && column.MaxWidth >= minW ? column.MaxWidth : double.PositiveInfinity;
        var clamped = Math.Clamp(requestedWidth, minW, maxW);

        column.ResizedWidth = clamped;
        RecalculateColumnWidths();
        if (Math.Abs(column.ActualWidth - oldWidth) > 0.1)
            ColumnResized?.Invoke(this, new VelaColumnResizedEventArgs(column, oldWidth, column.ActualWidth));
    }

    /// <summary>Clears explicit user resize width and restores the column's declarative width.</summary>
    public void ResetColumnWidth(VelaDataGridColumn column)
    {
        var oldWidth = column.ActualWidth;
        column.ResizedWidth = null;
        RecalculateColumnWidths();
        if (Math.Abs(column.ActualWidth - oldWidth) > 0.1)
            ColumnResized?.Invoke(this, new VelaColumnResizedEventArgs(column, oldWidth, column.ActualWidth));
    }

    /// <summary>Selects all currently visible rows (in Multiple selection mode).</summary>
    public void SelectAll()
    {
        if (SelectionMode != VelaSelectionMode.Multiple || _visibleRows.Count == 0)
            return;

        _selectedItems.Clear();
        foreach (var row in _visibleRows)
            _selectedItems.Add(row.Item);

        CommitSelection(_visibleRows[0].Item);
    }

    /// <summary>Clears all row selections.</summary>
    public void ClearSelection()
    {
        if (_selectedItems.Count == 0 && SelectedItem is null)
            return;

        _selectedItems.Clear();
        CommitSelection(null);
    }

    /// <summary>Computes tri-state select-all checkbox value across currently visible rows.</summary>
    public bool? GetSelectAllState()
    {
        if (_visibleRows.Count == 0)
            return false;

        var selectedVisible = 0;
        foreach (var row in _visibleRows)
        {
            if (_selectedItems.Contains(row.Item))
                selectedVisible++;
        }

        if (selectedVisible == 0) return false;
        if (selectedVisible == _visibleRows.Count) return true;
        return null;
    }

    internal void HandleRowClicked(VelaDataGridRowState row, KeyModifiers modifiers, bool fromCheckBox)
    {
        Focus();
        if (SelectionMode == VelaSelectionMode.None)
            return;

        var isCtrl = fromCheckBox || modifiers.HasFlag(KeyModifiers.Control) || modifiers.HasFlag(KeyModifiers.Meta);
        var isShift = !fromCheckBox && modifiers.HasFlag(KeyModifiers.Shift);

        if (SelectionMode == VelaSelectionMode.Single)
        {
            if (fromCheckBox && _selectedItems.Contains(row.Item))
            {
                _selectedItems.Clear();
                CommitSelection(null);
                return;
            }

            _selectedItems.Clear();
            _selectedItems.Add(row.Item);
            _anchorItem = row.Item;
            CommitSelection(row.Item);
            return;
        }

        if (isShift && _anchorItem is not null)
        {
            var anchorIdx = FindVisibleIndex(_anchorItem);
            var targetIdx = FindVisibleIndex(row.Item);
            if (anchorIdx >= 0 && targetIdx >= 0)
            {
                if (!isCtrl)
                    _selectedItems.Clear();

                var start = Math.Min(anchorIdx, targetIdx);
                var end = Math.Max(anchorIdx, targetIdx);
                for (var i = start; i <= end; i++)
                    _selectedItems.Add(_visibleRows[i].Item);

                CommitSelection(row.Item);
                return;
            }
        }

        if (isCtrl)
        {
            if (!_selectedItems.Add(row.Item))
                _selectedItems.Remove(row.Item);
            _anchorItem = row.Item;
            CommitSelection(_selectedItems.Contains(row.Item) ? row.Item : _selectedItems.FirstOrDefault());
        }
        else
        {
            _selectedItems.Clear();
            _selectedItems.Add(row.Item);
            _anchorItem = row.Item;
            CommitSelection(row.Item);
        }
    }

    internal void HandleRowDoubleTapped(VelaDataGridRowState row)
    {
        RowDoubleTapped?.Invoke(this, new VelaDataItemEventArgs(row.Item));
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (_visibleRows.Count == 0)
            return;

        var currentIdx = SelectedItem is not null ? FindVisibleIndex(SelectedItem) : -1;

        switch (e.Key)
        {
            case Key.Down:
                SelectIndex(currentIdx < 0 ? 0 : Math.Min(_visibleRows.Count - 1, currentIdx + 1), e.KeyModifiers);
                e.Handled = true;
                break;

            case Key.Up:
                SelectIndex(currentIdx < 0 ? _visibleRows.Count - 1 : Math.Max(0, currentIdx - 1), e.KeyModifiers);
                e.Handled = true;
                break;

            case Key.Home:
                SelectIndex(0, e.KeyModifiers);
                e.Handled = true;
                break;

            case Key.End:
                SelectIndex(_visibleRows.Count - 1, e.KeyModifiers);
                e.Handled = true;
                break;

            case Key.Space:
                if (currentIdx >= 0 && ShowCheckBoxes)
                {
                    HandleRowClicked(_visibleRows[currentIdx], KeyModifiers.None, fromCheckBox: true);
                    e.Handled = true;
                }
                break;

            case Key.Enter:
                if (currentIdx >= 0)
                {
                    RowDoubleTapped?.Invoke(this, new VelaDataItemEventArgs(_visibleRows[currentIdx].Item));
                    e.Handled = true;
                }
                break;
        }
    }

    private void SelectIndex(int index, KeyModifiers modifiers)
    {
        if (index < 0 || index >= _visibleRows.Count)
            return;

        var row = _visibleRows[index];
        HandleRowClicked(row, modifiers, fromCheckBox: false);
        _listBox?.ScrollIntoView(row);
    }

    private void OnListBoxTemplateApplied(object? sender, TemplateAppliedEventArgs e)
    {
        DetachBodyScroll();
        _bodyScroll = e.NameScope.Find<ScrollViewer>("PART_ScrollViewer");
        if (_bodyScroll is not null)
        {
            _bodyScroll.ScrollChanged += OnBodyScrollChanged;
            if (_bodyScroll.Viewport.Width > 0)
            {
                _lastViewportWidth = _bodyScroll.Viewport.Width;
                RecalculateColumnWidths();
            }
        }
    }

    private void OnListBoxPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == BoundsProperty && _listBox is not null && _listBox.Bounds.Width > 0)
        {
            var nextW = _bodyScroll is { Viewport.Width: > 0 } ? _bodyScroll.Viewport.Width : _listBox.Bounds.Width;
            if (Math.Abs(nextW - _lastViewportWidth) > 0.5)
            {
                _lastViewportWidth = nextW;
                RecalculateColumnWidths();
            }
        }
    }

    private void OnBodyScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (_bodyScroll is null)
            return;

        if (_headerScroll is not null && Math.Abs(_headerScroll.Offset.X - _bodyScroll.Offset.X) > 0.1)
            _headerScroll.Offset = new Vector(_bodyScroll.Offset.X, 0);

        if (_bodyScroll.Viewport.Width > 0 && Math.Abs(_bodyScroll.Viewport.Width - _lastViewportWidth) > 0.5)
        {
            _lastViewportWidth = _bodyScroll.Viewport.Width;
            RecalculateColumnWidths();
        }
    }

    private void DetachBodyScroll()
    {
        if (_bodyScroll is not null)
        {
            _bodyScroll.ScrollChanged -= OnBodyScrollChanged;
            _bodyScroll = null;
        }
    }

    private void OnItemsSourceChanged(IEnumerable? next)
    {
        if (_subscribedSource is not null)
        {
            _subscribedSource.CollectionChanged -= OnSourceCollectionChanged;
            _subscribedSource = null;
        }

        if (next is INotifyCollectionChanged incc)
        {
            _subscribedSource = incc;
            _subscribedSource.CollectionChanged += OnSourceCollectionChanged;
        }

        RebuildRows();
    }

    private void OnSourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RebuildRows();
    }

    private void OnColumnsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (VelaDataGridColumn col in e.OldItems)
                col.LayoutInvalidated -= OnColumnLayoutInvalidated;
        }

        if (e.NewItems is not null)
        {
            foreach (VelaDataGridColumn col in e.NewItems)
                col.LayoutInvalidated += OnColumnLayoutInvalidated;
        }

        RebuildHeaderAndLayout();
        RebuildRows();
    }

    private void OnColumnLayoutInvalidated(object? sender, EventArgs e)
    {
        RebuildHeaderAndLayout();
        RefreshVisibleRowsVisual(rebuildCells: true);
    }

    private void OnSelectedItemPropertyChanged(object? newValue)
    {
        if (_syncingSelection)
            return;

        _selectedItems.Clear();
        if (newValue is not null && SelectionMode != VelaSelectionMode.None)
            _selectedItems.Add(newValue);
        _anchorItem = newValue;
        SyncSelectedItemsList();
        UpdateSelectionVisuals();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnSelectionModeChanged()
    {
        if (SelectionMode == VelaSelectionMode.None)
            ClearSelection();
        else if (SelectionMode == VelaSelectionMode.Single && _selectedItems.Count > 1)
        {
            var keep = SelectedItem ?? _selectedItems.FirstOrDefault();
            _selectedItems.Clear();
            if (keep is not null)
                _selectedItems.Add(keep);
            CommitSelection(keep);
        }

        RebuildHeaderAndLayout();
    }

    private void CommitSelection(object? primary)
    {
        SyncSelectedItemsList();
        _syncingSelection = true;
        SelectedItem = primary;
        _syncingSelection = false;
        UpdateSelectionVisuals();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private void SyncSelectedItemsList()
    {
        _selectedItemsList.Clear();
        foreach (var item in _selectedItems)
            _selectedItemsList.Add(item);
    }

    private void UpdateSelectionVisuals()
    {
        foreach (var row in _visibleRows)
            row.IsSelected = _selectedItems.Contains(row.Item);

        SyncSelectAllCheckBox();
        RefreshVisibleRowsVisual();
    }

    private void SyncSelectAllCheckBox()
    {
        if (_selectAllCheckBox is null)
            return;

        _suppressSelectAllEvent = true;
        _selectAllCheckBox.IsChecked = GetSelectAllState();
        _selectAllCheckBox.IsEnabled = SelectionMode == VelaSelectionMode.Multiple && _visibleRows.Count > 0;
        _suppressSelectAllEvent = false;
    }

    private void RebuildHeaderAndLayout()
    {
        _visibleColumns.Clear();
        foreach (var col in Columns)
        {
            if (col.IsVisible)
                _visibleColumns.Add(col);
        }

        RecalculateColumnWidths(syncVisuals: false);

        if (_headerPanel is null)
            return;

        _headerPanel.Children.Clear();
        _headerCells.Clear();
        _selectAllCheckBox = null;

        if (ShowCheckBoxes)
        {
            _selectAllCheckBox = new VelaCheckBox
            {
                IsThreeState = true,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            _selectAllCheckBox.IsCheckedChanged += (_, _) =>
            {
                if (_suppressSelectAllEvent)
                    return;

                var state = GetSelectAllState();
                if (state == true)
                    ClearSelection();
                else
                    SelectAll();
            };

            var checkHost = new Border
            {
                Width = CheckBoxColumnWidth,
                Height = 38,
                Child = _selectAllCheckBox,
            };
            _headerPanel.Children.Add(checkHost);
            SyncSelectAllCheckBox();
        }

        foreach (var col in _visibleColumns)
        {
            var cell = new VelaTableHeaderCell(
                owner: this,
                column: col,
                onSortClicked: ToggleSort,
                onResized: ResizeColumn,
                onResetWidth: ResetColumnWidth,
                canSortGlobal: () => CanUserSortColumns,
                canResizeGlobal: () => CanUserResizeColumns,
                showVerticalGridLines: ShowVerticalGridLines);
            _headerCells.Add(cell);
            _headerPanel.Children.Add(cell);
        }

        RefreshVisibleRowsVisual(rebuildCells: true);
    }

    private void RecalculateColumnWidths(bool syncVisuals = true)
    {
        _visibleColumns.Clear();
        foreach (var col in Columns)
        {
            if (col.IsVisible)
                _visibleColumns.Add(col);
        }

        if (_visibleColumns.Count == 0)
            return;

        var specs = new VelaColumnSpec[_visibleColumns.Count];
        for (var i = 0; i < _visibleColumns.Count; i++)
            specs[i] = _visibleColumns[i].ToSpec();

        var gutter = ShowCheckBoxes ? CheckBoxColumnWidth : 0.0;
        var available = Math.Max(0.0, _lastViewportWidth - gutter);
        var resolved = VelaColumnLayoutEngine.ResolveWidths(specs, available);

        for (var i = 0; i < _visibleColumns.Count; i++)
            _visibleColumns[i].ActualWidth = resolved[i];

        if (syncVisuals)
        {
            SyncHeaderCells();
            SyncRealizedRowWidths();
        }
    }

    private void SyncHeaderCells()
    {
        foreach (var cell in _headerCells)
            cell.SyncState(ShowVerticalGridLines);
    }

    private void SyncRealizedRowWidths()
    {
        if (_listBox is null)
            return;

        foreach (var rowCtrl in _listBox.GetVisualDescendants().OfType<VelaDataGridRowControl>())
            rowCtrl.SyncColumnWidths();
    }

    private void RefreshVisibleRowsVisual(bool rebuildCells = false)
    {
        if (_listBox is null)
            return;

        foreach (var rowCtrl in _listBox.GetVisualDescendants().OfType<VelaDataGridRowControl>())
            rowCtrl.SyncFromBoundRow(rebuildCells);
    }

    private void RebuildRows()
    {
        var items = new List<object>();
        if (ItemsSource is not null)
        {
            var search = SearchText?.Trim();
            var hasSearch = !string.IsNullOrEmpty(search);

            foreach (var item in ItemsSource)
            {
                if (item is null)
                    continue;
                if (FilterPredicate is not null && !FilterPredicate(item))
                    continue;
                if (hasSearch && !MatchesSearch(item, search!))
                    continue;
                items.Add(item);
            }
        }

        if (SortColumn is not null && SortDirection != VelaSortDirection.None)
        {
            var col = SortColumn;
            var sign = SortDirection == VelaSortDirection.Descending ? -1 : 1;
            items = items
                .Select((item, idx) => (item, idx))
                .OrderBy(pair => pair, Comparer<(object item, int idx)>.Create((a, b) =>
                {
                    var cmp = col.CompareItems(a.item, b.item) * sign;
                    return cmp != 0 ? cmp : a.idx.CompareTo(b.idx);
                }))
                .Select(pair => pair.item)
                .ToList();
        }

        _visibleRows.Clear();
        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            _visibleRows.Add(new VelaDataGridRowState(this, item, i, _selectedItems.Contains(item)));
        }

        VisibleCount = _visibleRows.Count;
        SyncSelectAllCheckBox();
        UpdateEmptyState();
    }

    private bool MatchesSearch(object item, string query)
    {
        if (SearchPredicate is not null)
            return SearchPredicate(item, query);

        foreach (var col in Columns)
        {
            if (!col.IsVisible)
                continue;

            var text = col.GetSearchableText(item);
            if (!string.IsNullOrEmpty(text) && text.Contains(query, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        var fallback = item.ToString();
        return !string.IsNullOrEmpty(fallback) && fallback.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private int FindVisibleIndex(object item)
    {
        for (var i = 0; i < _visibleRows.Count; i++)
        {
            if (ReferenceEquals(_visibleRows[i].Item, item))
                return i;
        }
        return -1;
    }

    private void UpdateEmptyState()
    {
        if (_emptyHost is not null)
            _emptyHost.IsVisible = _visibleRows.Count == 0;
        if (_listBox is not null)
            _listBox.IsVisible = _visibleRows.Count > 0;
    }
}

/// <summary>
/// Recycled virtualized row container inside <see cref="VelaDataGrid"/>.
/// </summary>
[PseudoClasses(":selected", ":alternate")]
internal sealed class VelaDataGridRowControl : Border
{
    private readonly StackPanel _cellsPanel;
    private readonly List<(Border CellHost, VelaDataGridColumn Column)> _cellHosts = new();
    private Border? _checkBoxHost;
    private VelaCheckBox? _checkBox;

    private VelaDataGridRowState? _row;
    private bool _suppressCheckEvent;

    public VelaDataGridRowControl()
    {
        Classes.Add("vela-table-row");
        Background = Brushes.Transparent;
        Cursor = new Cursor(StandardCursorType.Hand);

        _cellsPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
        };

        Child = _cellsPanel;
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is VelaDataGridRowState next)
        {
            _row = next;
            SyncFromBoundRow(rebuildCells: true);
        }
        else
        {
            _row = null;
        }
    }

    internal void SyncFromBoundRow(bool rebuildCells)
    {
        if (_row is null)
            return;

        var owner = _row.Owner;
        Height = VelaColumnLayoutEngine.GetRowHeight(owner.Density, isTreeView: false);
        BorderThickness = owner.ShowHorizontalGridLines ? new Thickness(0, 0, 0, 1) : new Thickness(0);
        this[!BorderBrushProperty] = this.GetResourceObservable("VelaBorderSubtleBrush").ToBinding();

        PseudoClasses.Set(":selected", _row.IsSelected);
        PseudoClasses.Set(":alternate", owner.ShowAlternatingRows && (_row.RowIndex % 2 == 1));

        if (rebuildCells)
            RebuildCells();
        else
            SyncStateOnly();
    }

    internal void SyncColumnWidths()
    {
        foreach (var (host, col) in _cellHosts)
            host.Width = Math.Max(24.0, col.ActualWidth);
    }

    private void SyncStateOnly()
    {
        if (_row is null)
            return;

        if (_checkBox is not null)
        {
            _suppressCheckEvent = true;
            _checkBox.IsChecked = _row.IsSelected;
            _suppressCheckEvent = false;
        }

        SyncColumnWidths();
    }

    private void RebuildCells()
    {
        if (_row is null)
            return;

        var owner = _row.Owner;
        _cellsPanel.Children.Clear();
        _cellHosts.Clear();
        _checkBoxHost = null;
        _checkBox = null;

        if (owner.ShowCheckBoxes)
        {
            _checkBox = new VelaCheckBox
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            _suppressCheckEvent = true;
            _checkBox.IsChecked = _row.IsSelected;
            _suppressCheckEvent = false;
            _checkBox.IsCheckedChanged += (_, _) =>
            {
                if (_suppressCheckEvent || _row is null)
                    return;
                _row.Owner.HandleRowClicked(_row, KeyModifiers.None, fromCheckBox: true);
            };

            _checkBoxHost = new Border
            {
                Width = VelaDataGrid.CheckBoxColumnWidth,
                Child = _checkBox,
            };
            _cellsPanel.Children.Add(_checkBoxHost);
        }

        var cols = owner._visibleColumns;
        for (var i = 0; i < cols.Count; i++)
        {
            var col = cols[i];
            var cellContent = col.CreateCellContent(_row.Item);
            var cellHost = new Border
            {
                Width = Math.Max(24.0, col.ActualWidth),
                Padding = new Thickness(12, 0),
                ClipToBounds = true,
                BorderThickness = owner.ShowVerticalGridLines && i < cols.Count - 1 ? new Thickness(0, 0, 1, 0) : new Thickness(0),
                Child = cellContent,
            };
            if (owner.ShowVerticalGridLines)
                cellHost[!Border.BorderBrushProperty] = this.GetResourceObservable("VelaBorderSubtleBrush").ToBinding();

            _cellHosts.Add((cellHost, col));
            _cellsPanel.Children.Add(cellHost);
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (_row is null || e.Handled)
            return;

        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            if (e.Source is Visual v && v.FindAncestorOfType<VelaCheckBox>() is not null)
                return;

            _row.Owner.HandleRowClicked(_row, e.KeyModifiers, fromCheckBox: false);
        }
    }

    protected override void OnDoubleTapped(TappedEventArgs e)
    {
        base.OnDoubleTapped(e);
        if (_row is null || e.Handled)
            return;

        if (e.Source is Visual v && v.FindAncestorOfType<VelaCheckBox>() is not null)
            return;

        _row.Owner.HandleRowDoubleTapped(_row);
        e.Handled = true;
    }
}
