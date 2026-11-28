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
/// Flattened hierarchical row projection inside <see cref="VelaTreeTable"/>.
/// </summary>
public sealed class VelaTreeTableRowState
{
    internal VelaTreeTableRowState(
        VelaTreeTable owner,
        object item,
        object? parent,
        int rowIndex,
        int depth,
        bool hasChildren,
        bool isExpanded,
        bool isSelected,
        bool? isChecked,
        Geometry? icon)
    {
        Owner = owner;
        Item = item;
        Parent = parent;
        RowIndex = rowIndex;
        Depth = depth;
        HasChildren = hasChildren;
        IsExpanded = isExpanded;
        IsSelected = isSelected;
        IsChecked = isChecked;
        Icon = icon;
    }

    internal VelaTreeTable Owner { get; }
    public object Item { get; }
    public object? Parent { get; }
    public int RowIndex { get; internal set; }
    public int Depth { get; }
    public bool HasChildren { get; }
    public bool IsExpanded { get; internal set; }
    public bool IsSelected { get; internal set; }
    public bool? IsChecked { get; internal set; }
    public Geometry? Icon { get; }
}

/// <summary>
/// Virtualized hierarchical tree table combining multi-column sorting, interactive column resizing,
/// and sticky headers with tree indent guide lines, spring-rotating disclosure chevrons,
/// level-preserving hierarchical sorting, ancestor-preserving search, and tri-state checkboxes.
/// </summary>
public class VelaTreeTable : TemplatedControl
{
    internal const double CheckBoxColumnWidth = 42.0;

    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty =
        AvaloniaProperty.Register<VelaTreeTable, IEnumerable?>(nameof(ItemsSource));

    public static readonly StyledProperty<object?> SelectedItemProperty =
        AvaloniaProperty.Register<VelaTreeTable, object?>(nameof(SelectedItem), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<VelaSelectionMode> SelectionModeProperty =
        AvaloniaProperty.Register<VelaTreeTable, VelaSelectionMode>(nameof(SelectionMode), VelaSelectionMode.Single);

    public static readonly StyledProperty<VelaDataDensity> DensityProperty =
        AvaloniaProperty.Register<VelaTreeTable, VelaDataDensity>(nameof(Density), VelaDataDensity.Standard);

    public static readonly StyledProperty<int> TreeColumnIndexProperty =
        AvaloniaProperty.Register<VelaTreeTable, int>(nameof(TreeColumnIndex), 0);

    public static readonly StyledProperty<double> IndentSizeProperty =
        AvaloniaProperty.Register<VelaTreeTable, double>(nameof(IndentSize), 20.0);

    public static readonly StyledProperty<bool> ShowGuideLinesProperty =
        AvaloniaProperty.Register<VelaTreeTable, bool>(nameof(ShowGuideLines), true);

    public static readonly StyledProperty<bool> ShowCheckBoxesProperty =
        AvaloniaProperty.Register<VelaTreeTable, bool>(nameof(ShowCheckBoxes));

    public static readonly StyledProperty<bool> CascadeCheckStateProperty =
        AvaloniaProperty.Register<VelaTreeTable, bool>(nameof(CascadeCheckState), true);

    public static readonly StyledProperty<bool> ShowAlternatingRowsProperty =
        AvaloniaProperty.Register<VelaTreeTable, bool>(nameof(ShowAlternatingRows));

    public static readonly StyledProperty<bool> ShowHorizontalGridLinesProperty =
        AvaloniaProperty.Register<VelaTreeTable, bool>(nameof(ShowHorizontalGridLines), true);

    public static readonly StyledProperty<bool> ShowVerticalGridLinesProperty =
        AvaloniaProperty.Register<VelaTreeTable, bool>(nameof(ShowVerticalGridLines));

    public static readonly StyledProperty<bool> CanUserSortColumnsProperty =
        AvaloniaProperty.Register<VelaTreeTable, bool>(nameof(CanUserSortColumns), true);

    public static readonly StyledProperty<bool> CanUserResizeColumnsProperty =
        AvaloniaProperty.Register<VelaTreeTable, bool>(nameof(CanUserResizeColumns), true);

    public static readonly StyledProperty<bool> ExpandOnDoubleClickProperty =
        AvaloniaProperty.Register<VelaTreeTable, bool>(nameof(ExpandOnDoubleClick), true);

    public static readonly StyledProperty<string?> SearchTextProperty =
        AvaloniaProperty.Register<VelaTreeTable, string?>(nameof(SearchText));

    public static readonly StyledProperty<object?> EmptyContentProperty =
        AvaloniaProperty.Register<VelaTreeTable, object?>(nameof(EmptyContent));

    public static readonly StyledProperty<object?> FooterContentProperty =
        AvaloniaProperty.Register<VelaTreeTable, object?>(nameof(FooterContent));

    public static readonly DirectProperty<VelaTreeTable, int> VisibleCountProperty =
        AvaloniaProperty.RegisterDirect<VelaTreeTable, int>(nameof(VisibleCount), o => o.VisibleCount);

    public IEnumerable? ItemsSource { get => GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }
    public object? SelectedItem { get => GetValue(SelectedItemProperty); set => SetValue(SelectedItemProperty, value); }
    public VelaSelectionMode SelectionMode { get => GetValue(SelectionModeProperty); set => SetValue(SelectionModeProperty, value); }
    public VelaDataDensity Density { get => GetValue(DensityProperty); set => SetValue(DensityProperty, value); }
    public int TreeColumnIndex { get => GetValue(TreeColumnIndexProperty); set => SetValue(TreeColumnIndexProperty, value); }
    public double IndentSize { get => GetValue(IndentSizeProperty); set => SetValue(IndentSizeProperty, value); }
    public bool ShowGuideLines { get => GetValue(ShowGuideLinesProperty); set => SetValue(ShowGuideLinesProperty, value); }
    public bool ShowCheckBoxes { get => GetValue(ShowCheckBoxesProperty); set => SetValue(ShowCheckBoxesProperty, value); }
    public bool CascadeCheckState { get => GetValue(CascadeCheckStateProperty); set => SetValue(CascadeCheckStateProperty, value); }
    public bool ShowAlternatingRows { get => GetValue(ShowAlternatingRowsProperty); set => SetValue(ShowAlternatingRowsProperty, value); }
    public bool ShowHorizontalGridLines { get => GetValue(ShowHorizontalGridLinesProperty); set => SetValue(ShowHorizontalGridLinesProperty, value); }
    public bool ShowVerticalGridLines { get => GetValue(ShowVerticalGridLinesProperty); set => SetValue(ShowVerticalGridLinesProperty, value); }
    public bool CanUserSortColumns { get => GetValue(CanUserSortColumnsProperty); set => SetValue(CanUserSortColumnsProperty, value); }
    public bool CanUserResizeColumns { get => GetValue(CanUserResizeColumnsProperty); set => SetValue(CanUserResizeColumnsProperty, value); }
    public bool ExpandOnDoubleClick { get => GetValue(ExpandOnDoubleClickProperty); set => SetValue(ExpandOnDoubleClickProperty, value); }
    public string? SearchText { get => GetValue(SearchTextProperty); set => SetValue(SearchTextProperty, value); }
    public object? EmptyContent { get => GetValue(EmptyContentProperty); set => SetValue(EmptyContentProperty, value); }
    public object? FooterContent { get => GetValue(FooterContentProperty); set => SetValue(FooterContentProperty, value); }

    [Content]
    public ObservableCollection<VelaDataGridColumn> Columns { get; } = new();

    private Func<object, IEnumerable?>? _childrenSelector;
    private Func<object, bool>? _hasChildrenSelector;
    private Func<object, Geometry?>? _iconSelector;
    private Func<object, bool>? _initialExpandedSelector;
    private Func<object, bool>? _filterPredicate;
    private Func<object, string, bool>? _searchPredicate;

    /// <summary>AOT-safe delegate returning child items for hierarchical rows.</summary>
    public Func<object, IEnumerable?>? ChildrenSelector
    {
        get => _childrenSelector;
        set { _childrenSelector = value; RebuildProjection(); }
    }

    /// <summary>Optional explicit override indicating whether an item has children (for lazy loading).</summary>
    public Func<object, bool>? HasChildrenSelector
    {
        get => _hasChildrenSelector;
        set { _hasChildrenSelector = value; RebuildProjection(); }
    }

    /// <summary>Optional delegate supplying an icon rendered inside the tree column before the cell content.</summary>
    public Func<object, Geometry?>? IconSelector
    {
        get => _iconSelector;
        set { _iconSelector = value; RebuildProjection(); }
    }

    /// <summary>Optional delegate returning whether an item should start expanded when first observed.</summary>
    public Func<object, bool>? InitialExpandedSelector
    {
        get => _initialExpandedSelector;
        set { _initialExpandedSelector = value; _initializedItems.Clear(); RebuildProjection(); }
    }

    /// <summary>Optional custom filter predicate evaluated for every node.</summary>
    public Func<object, bool>? FilterPredicate
    {
        get => _filterPredicate;
        set { _filterPredicate = value; RebuildProjection(); }
    }

    /// <summary>Optional custom search predicate evaluated when <see cref="SearchText"/> is non-empty.</summary>
    public Func<object, string, bool>? SearchPredicate
    {
        get => _searchPredicate;
        set { _searchPredicate = value; RebuildProjection(); }
    }

    public VelaDataGridColumn? SortColumn { get; private set; }
    public VelaSortDirection SortDirection { get; private set; }

    public IReadOnlyList<VelaTreeTableRowState> VisibleRows => _visibleRows;
    public IReadOnlyList<object> SelectedItems => _selectedItemsList;
    public IReadOnlyList<object> CheckedItems => _checkedItemsList;

    private int _visibleCount;
    public int VisibleCount
    {
        get => _visibleCount;
        private set => SetAndRaise(VisibleCountProperty, ref _visibleCount, value);
    }

    public event EventHandler? SelectionChanged;
    public event EventHandler? CheckedItemsChanged;
    public event EventHandler<VelaDataItemEventArgs>? RowDoubleTapped;
    public event EventHandler<VelaDataItemEventArgs>? NodeExpanded;
    public event EventHandler<VelaDataItemEventArgs>? NodeCollapsed;
    public event EventHandler<VelaSortChangedEventArgs>? SortChanged;
    public event EventHandler<VelaColumnResizedEventArgs>? ColumnResized;

    protected override Type StyleKeyOverride => typeof(VelaTreeTable);

    internal readonly VelaObservableList<VelaTreeTableRowState> _visibleRows = new();
    internal readonly HashSet<VelaTreeTableRowControl> _realizedRows = new();
    internal readonly List<VelaDataGridColumn> _visibleColumns = new();
    private readonly HashSet<object> _expandedItems = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<object> _initializedItems = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<object> _selectedItems = new(ReferenceEqualityComparer.Instance);
    private readonly List<object> _selectedItemsList = new();
    private readonly HashSet<object> _checkedItems = new(ReferenceEqualityComparer.Instance);
    private readonly List<object> _checkedItemsList = new();
    private readonly HashSet<INotifyCollectionChanged> _subscribedCollections = new(ReferenceEqualityComparer.Instance);
    private readonly List<VelaTableHeaderCell> _headerCells = new();

    private ScrollViewer? _headerScroll;
    private StackPanel? _headerPanel;
    private ListBox? _listBox;
    private ScrollViewer? _bodyScroll;
    private ContentPresenter? _emptyHost;
    private VelaCheckBox? _selectAllCheckBox;

    private object? _anchorItem;
    private object? _lastAnimatedToggleItem;
    private bool _syncingSelection;
    private bool _suppressSelectAllEvent;
    private double _lastViewportWidth = 680.0;

    static VelaTreeTable()
    {
        FocusableProperty.OverrideDefaultValue<VelaTreeTable>(true);
        ItemsSourceProperty.Changed.AddClassHandler<VelaTreeTable>((t, _) => t.OnItemsSourceChanged());
        SelectedItemProperty.Changed.AddClassHandler<VelaTreeTable>((t, e) => t.OnSelectedItemPropertyChanged(e.NewValue));
        SelectionModeProperty.Changed.AddClassHandler<VelaTreeTable>((t, _) => t.OnSelectionModeChanged());
        DensityProperty.Changed.AddClassHandler<VelaTreeTable>((t, _) => t.RefreshVisibleRowsVisual());
        TreeColumnIndexProperty.Changed.AddClassHandler<VelaTreeTable>((t, _) => t.RefreshVisibleRowsVisual(rebuildCells: true));
        IndentSizeProperty.Changed.AddClassHandler<VelaTreeTable>((t, _) => t.RefreshVisibleRowsVisual());
        ShowGuideLinesProperty.Changed.AddClassHandler<VelaTreeTable>((t, _) => t.RefreshVisibleRowsVisual());
        ShowCheckBoxesProperty.Changed.AddClassHandler<VelaTreeTable>((t, _) => { t.RebuildHeaderAndLayout(); t.RebuildProjection(); });
        CascadeCheckStateProperty.Changed.AddClassHandler<VelaTreeTable>((t, _) => t.RebuildProjection());
        ShowAlternatingRowsProperty.Changed.AddClassHandler<VelaTreeTable>((t, _) => t.RefreshVisibleRowsVisual());
        ShowHorizontalGridLinesProperty.Changed.AddClassHandler<VelaTreeTable>((t, _) => t.RefreshVisibleRowsVisual());
        ShowVerticalGridLinesProperty.Changed.AddClassHandler<VelaTreeTable>((t, _) => t.RebuildHeaderAndLayout());
        CanUserSortColumnsProperty.Changed.AddClassHandler<VelaTreeTable>((t, _) => t.SyncHeaderCells());
        CanUserResizeColumnsProperty.Changed.AddClassHandler<VelaTreeTable>((t, _) => t.SyncHeaderCells());
        SearchTextProperty.Changed.AddClassHandler<VelaTreeTable>((t, _) => t.RebuildProjection());
    }

    public VelaTreeTable()
    {
        Columns.CollectionChanged += OnColumnsCollectionChanged;
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        DetachBodyScroll();
        if (_listBox is not null)
        {
            _listBox.TemplateApplied -= OnListBoxTemplateApplied;
            _listBox.PropertyChanged -= OnListBoxPropertyChanged;
        }

        _headerScroll = e.NameScope.Find<ScrollViewer>("PART_HeaderScroll");
        _headerPanel = e.NameScope.Find<StackPanel>("PART_HeaderPanel");
        _listBox = e.NameScope.Find<ListBox>("PART_List");
        _emptyHost = e.NameScope.Find<ContentPresenter>("PART_EmptyHost");

        if (_listBox is not null)
        {
            _listBox.ItemsSource = _visibleRows;
            _listBox.ItemTemplate = new FuncDataTemplate<VelaTreeTableRowState>((_, _) => new VelaTreeTableRowControl(), supportsRecycling: true);
            _listBox.TemplateApplied += OnListBoxTemplateApplied;
            _listBox.PropertyChanged += OnListBoxPropertyChanged;
        }

        RebuildHeaderAndLayout();
        RebuildProjection();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        DetachBodyScroll();
        UnsubscribeAllCollections();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        RebuildProjection();
    }

    /// <summary>Re-evaluates columns, hierarchy projection, filtering, and sorting.</summary>
    public void Refresh()
    {
        RecalculateColumnWidths();
        RebuildProjection();
    }

    /// <summary>Returns whether the specified item is currently expanded.</summary>
    public bool IsItemExpanded(object item)
    {
        if (!_initializedItems.Contains(item))
        {
            if (InitialExpandedSelector is not null)
                return InitialExpandedSelector(item);
            if (item is VelaTreeNode node)
                return node.IsExpanded;
        }
        return _expandedItems.Contains(item);
    }

    /// <summary>Expands a hierarchical row if it has children.</summary>
    public void Expand(object item)
    {
        if (item is null || !ResolveHasChildren(item))
            return;
        if (IsItemExpanded(item))
            return;

        _initializedItems.Add(item);
        _expandedItems.Add(item);
        if (item is VelaTreeNode node)
            node.IsExpanded = true;

        _lastAnimatedToggleItem = item;
        RebuildProjection();
        _lastAnimatedToggleItem = null;
        NodeExpanded?.Invoke(this, new VelaDataItemEventArgs(item));
    }

    /// <summary>Collapses a hierarchical row.</summary>
    public void Collapse(object item)
    {
        if (item is null || !IsItemExpanded(item))
            return;

        _initializedItems.Add(item);
        _expandedItems.Remove(item);
        if (item is VelaTreeNode node)
            node.IsExpanded = false;

        _lastAnimatedToggleItem = item;
        RebuildProjection();
        _lastAnimatedToggleItem = null;
        NodeCollapsed?.Invoke(this, new VelaDataItemEventArgs(item));
    }

    /// <summary>Toggles expanded/collapsed state for a hierarchical row.</summary>
    public void ToggleExpand(object item)
    {
        if (IsItemExpanded(item))
            Collapse(item);
        else
            Expand(item);
    }

    /// <summary>Expands all parent rows across the entire hierarchy.</summary>
    public void ExpandAll()
    {
        foreach (var root in GetRootItems())
            ExpandRecursive(root);
        RebuildProjection();
    }

    /// <summary>Collapses all rows across the entire hierarchy.</summary>
    public void CollapseAll()
    {
        foreach (var root in GetRootItems())
            CollapseRecursive(root);
        _expandedItems.Clear();
        RebuildProjection();
    }

    /// <summary>Sorts rows hierarchically (preserving parent-child grouping at every depth level).</summary>
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
        RebuildProjection();
        SortChanged?.Invoke(this, new VelaSortChangedEventArgs(SortColumn, SortDirection));
    }

    /// <summary>Cycles column sort: None -> Ascending -> Descending -> None.</summary>
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

    /// <summary>Clears any active sort state.</summary>
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

    /// <summary>Resets a column back to its declared width.</summary>
    public void ResetColumnWidth(VelaDataGridColumn column)
    {
        var oldWidth = column.ActualWidth;
        column.ResizedWidth = null;
        RecalculateColumnWidths();
        if (Math.Abs(column.ActualWidth - oldWidth) > 0.1)
            ColumnResized?.Invoke(this, new VelaColumnResizedEventArgs(column, oldWidth, column.ActualWidth));
    }

    /// <summary>Returns the tri-state checkbox value for a given item.</summary>
    public bool? GetCheckState(object item)
    {
        if (CascadeCheckState && ResolveHasChildren(item))
        {
            var children = GetChildItemsList(item);
            if (children.Count > 0)
            {
                var allChecked = true;
                var anyChecked = false;
                foreach (var child in children)
                {
                    var state = GetCheckState(child);
                    if (state == true)
                    {
                        anyChecked = true;
                    }
                    else if (state is null)
                    {
                        anyChecked = true;
                        allChecked = false;
                    }
                    else
                    {
                        allChecked = false;
                    }
                }

                if (allChecked) return true;
                if (anyChecked) return null;
                return false;
            }
        }

        return _checkedItems.Contains(item);
    }

    /// <summary>Sets the check state of an item, cascading to descendants when <see cref="CascadeCheckState"/> is enabled.</summary>
    public void SetChecked(object item, bool isChecked)
    {
        if (item is null)
            return;

        if (CascadeCheckState)
            SetCheckedRecursive(item, isChecked);
        else
            ApplySingleCheck(item, isChecked);

        SyncCheckedItemsList();
        RebuildProjection();
        CheckedItemsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Checks or unchecks all items across the entire hierarchy.</summary>
    public void SetAllChecked(bool isChecked)
    {
        if (!isChecked)
        {
            _checkedItems.Clear();
        }
        else
        {
            foreach (var root in GetRootItems())
                SetCheckedRecursive(root, true);
        }

        SyncCheckedItemsList();
        RebuildProjection();
        CheckedItemsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Computes the tri-state header checkbox state across visible rows.</summary>
    public bool? GetHeaderCheckState()
    {
        if (_visibleRows.Count == 0)
            return false;

        var allChecked = true;
        var anyChecked = false;
        foreach (var row in _visibleRows)
        {
            var st = row.IsChecked;
            if (st == true)
            {
                anyChecked = true;
            }
            else if (st is null)
            {
                anyChecked = true;
                allChecked = false;
            }
            else
            {
                allChecked = false;
            }
        }

        if (allChecked) return true;
        if (anyChecked) return null;
        return false;
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

    internal bool ConsumeShouldAnimateToggle(object item)
        => ReferenceEquals(_lastAnimatedToggleItem, item);

    internal void HandleRowClicked(VelaTreeTableRowState row, KeyModifiers modifiers)
    {
        Focus();
        if (SelectionMode == VelaSelectionMode.None)
            return;

        var isCtrl = modifiers.HasFlag(KeyModifiers.Control) || modifiers.HasFlag(KeyModifiers.Meta);
        var isShift = modifiers.HasFlag(KeyModifiers.Shift);

        if (SelectionMode == VelaSelectionMode.Single)
        {
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

    internal void HandleRowDoubleTapped(VelaTreeTableRowState row)
    {
        if (ExpandOnDoubleClick && row.HasChildren)
            ToggleExpand(row.Item);

        RowDoubleTapped?.Invoke(this, new VelaDataItemEventArgs(row.Item));
    }

    internal void HandleRowCheckToggled(VelaTreeTableRowState row)
    {
        var current = GetCheckState(row.Item);
        SetChecked(row.Item, current != true);
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

            case Key.Right:
                if (currentIdx >= 0)
                {
                    var row = _visibleRows[currentIdx];
                    if (row.HasChildren && !row.IsExpanded)
                        Expand(row.Item);
                    else if (row.HasChildren && currentIdx + 1 < _visibleRows.Count && _visibleRows[currentIdx + 1].Depth > row.Depth)
                        SelectIndex(currentIdx + 1, KeyModifiers.None);
                    e.Handled = true;
                }
                break;

            case Key.Left:
                if (currentIdx >= 0)
                {
                    var row = _visibleRows[currentIdx];
                    if (row.HasChildren && row.IsExpanded)
                        Collapse(row.Item);
                    else if (row.Parent is not null)
                    {
                        var parentIdx = FindVisibleIndex(row.Parent);
                        if (parentIdx >= 0)
                            SelectIndex(parentIdx, KeyModifiers.None);
                    }
                    e.Handled = true;
                }
                break;

            case Key.Space:
                if (currentIdx >= 0)
                {
                    var row = _visibleRows[currentIdx];
                    if (ShowCheckBoxes)
                        HandleRowCheckToggled(row);
                    else if (row.HasChildren)
                        ToggleExpand(row.Item);
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
        HandleRowClicked(row, modifiers);
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

    private void OnItemsSourceChanged()
    {
        _initializedItems.Clear();
        RebuildProjection();
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
        RebuildProjection();
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

    private void SyncCheckedItemsList()
    {
        _checkedItemsList.Clear();
        foreach (var item in _checkedItems)
            _checkedItemsList.Add(item);
    }

    private void UpdateSelectionVisuals()
    {
        foreach (var row in _visibleRows)
            row.IsSelected = _selectedItems.Contains(row.Item);

        RefreshVisibleRowsVisual();
    }

    private void SyncSelectAllCheckBox()
    {
        if (_selectAllCheckBox is null)
            return;

        _suppressSelectAllEvent = true;
        _selectAllCheckBox.IsChecked = GetHeaderCheckState();
        _selectAllCheckBox.IsEnabled = _visibleRows.Count > 0;
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

                var state = GetHeaderCheckState();
                SetAllChecked(state != true);
            };

            _headerPanel.Children.Add(new Border
            {
                Width = CheckBoxColumnWidth,
                Height = 38,
                Child = _selectAllCheckBox,
            });
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
        foreach (var rowCtrl in _realizedRows)
            rowCtrl.SyncColumnWidths();
    }

    private void RefreshVisibleRowsVisual(bool rebuildCells = false)
    {
        foreach (var rowCtrl in _realizedRows)
            rowCtrl.SyncFromBoundRow(rebuildCells, animateChevron: false);
    }

    private void RebuildProjection()
    {
        UnsubscribeAllCollections();

        if (ItemsSource is INotifyCollectionChanged incc)
            SubscribeCollection(incc);

        var roots = SortLevel(GetRootItems());
        var search = SearchText?.Trim();
        var hasSearch = !string.IsNullOrEmpty(search);
        var hasFilter = hasSearch || FilterPredicate is not null;

        var nextRows = new List<VelaTreeTableRowState>();
        var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);

        foreach (var root in roots)
            ProjectNode(root, parent: null, depth: 0, search, hasSearch, hasFilter, nextRows, visited);

        for (var i = 0; i < nextRows.Count; i++)
            nextRows[i].RowIndex = i;

        _visibleRows.ReplaceAll(nextRows);

        VisibleCount = _visibleRows.Count;
        SyncSelectAllCheckBox();
        UpdateEmptyState();
    }

    private bool ProjectNode(
        object item,
        object? parent,
        int depth,
        string? search,
        bool hasSearch,
        bool hasFilter,
        List<VelaTreeTableRowState> output,
        HashSet<object> visited)
    {
        if (item is null || !visited.Add(item))
            return false;

        InitializeItemIfNeeded(item);

        var children = SortLevel(GetChildItemsList(item));
        var hasChildren = ResolveHasChildren(item, children.Count);

        if (!hasFilter)
        {
            var isExpanded = hasChildren && _expandedItems.Contains(item);
            output.Add(CreateRowState(item, parent, depth, hasChildren, isExpanded));

            if (isExpanded)
            {
                foreach (var child in children)
                    ProjectNode(child, item, depth + 1, search: null, hasSearch: false, hasFilter: false, output, visited);
            }

            return true;
        }

        var selfMatch = MatchesFilters(item, search, hasSearch);
        var childRows = new List<VelaTreeTableRowState>();
        var anyChildMatched = false;

        foreach (var child in children)
        {
            if (ProjectNode(child, item, depth + 1, search, hasSearch, hasFilter: true, childRows, visited))
                anyChildMatched = true;
        }

        if (!selfMatch && !anyChildMatched)
            return false;

        var expandedInFilter = hasChildren && (anyChildMatched || _expandedItems.Contains(item));
        output.Add(CreateRowState(item, parent, depth, hasChildren, expandedInFilter));

        if (anyChildMatched)
        {
            output.AddRange(childRows);
        }
        else if (expandedInFilter)
        {
            foreach (var child in children)
                ProjectNode(child, item, depth + 1, search: null, hasSearch: false, hasFilter: false, output, visited);
        }

        return true;
    }

    private VelaTreeTableRowState CreateRowState(object item, object? parent, int depth, bool hasChildren, bool isExpanded)
    {
        var icon = IconSelector?.Invoke(item) ?? (item as VelaTreeNode)?.EffectiveIcon;
        var checkState = ShowCheckBoxes ? GetCheckState(item) : (bool?)false;

        return new VelaTreeTableRowState(
            owner: this,
            item: item,
            parent: parent,
            rowIndex: 0,
            depth: depth,
            hasChildren: hasChildren,
            isExpanded: isExpanded,
            isSelected: _selectedItems.Contains(item),
            isChecked: checkState,
            icon: icon);
    }

    private void InitializeItemIfNeeded(object item)
    {
        if (!_initializedItems.Add(item))
            return;

        if (InitialExpandedSelector is not null)
        {
            if (InitialExpandedSelector(item))
                _expandedItems.Add(item);
        }
        else if (item is VelaTreeNode node)
        {
            if (node.IsExpanded)
                _expandedItems.Add(node);
            if (node.IsChecked == true)
                _checkedItems.Add(node);
        }
    }

    private List<object> SortLevel(List<object> levelItems)
    {
        if (SortColumn is null || SortDirection == VelaSortDirection.None || levelItems.Count <= 1)
            return levelItems;

        var col = SortColumn;
        var sign = SortDirection == VelaSortDirection.Descending ? -1 : 1;

        return levelItems
            .Select((item, idx) => (item, idx))
            .OrderBy(pair => pair, Comparer<(object item, int idx)>.Create((a, b) =>
            {
                var cmp = col.CompareItems(a.item, b.item) * sign;
                return cmp != 0 ? cmp : a.idx.CompareTo(b.idx);
            }))
            .Select(pair => pair.item)
            .ToList();
    }

    private bool MatchesFilters(object item, string? search, bool hasSearch)
    {
        if (FilterPredicate is not null && !FilterPredicate(item))
            return false;

        if (!hasSearch)
            return true;

        if (SearchPredicate is not null)
            return SearchPredicate(item, search!);

        foreach (var col in Columns)
        {
            if (!col.IsVisible)
                continue;

            var text = col.GetSearchableText(item);
            if (!string.IsNullOrEmpty(text) && text.Contains(search!, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        var fallback = item.ToString();
        return !string.IsNullOrEmpty(fallback) && fallback.Contains(search!, StringComparison.OrdinalIgnoreCase);
    }

    private List<object> GetRootItems()
    {
        var list = new List<object>();
        if (ItemsSource is not null)
        {
            foreach (var item in ItemsSource)
            {
                if (item is not null)
                    list.Add(item);
            }
        }
        return list;
    }

    private List<object> GetChildItemsList(object item)
    {
        IEnumerable? raw = null;
        if (ChildrenSelector is not null)
            raw = ChildrenSelector(item);
        else if (item is VelaTreeNode node)
            raw = node.Children;

        if (raw is INotifyCollectionChanged incc)
            SubscribeCollection(incc);

        var list = new List<object>();
        if (raw is not null)
        {
            foreach (var child in raw)
            {
                if (child is not null)
                    list.Add(child);
            }
        }
        return list;
    }

    private bool ResolveHasChildren(object item, int? knownChildCount = null)
    {
        if (HasChildrenSelector is not null)
            return HasChildrenSelector(item);

        if (item is VelaTreeNode node && node.HasChildren.HasValue)
            return node.HasChildren.Value;

        if (knownChildCount.HasValue)
            return knownChildCount.Value > 0;

        return GetChildItemsList(item).Count > 0;
    }

    private void ExpandRecursive(object item)
    {
        _initializedItems.Add(item);
        var children = GetChildItemsList(item);
        if (ResolveHasChildren(item, children.Count))
        {
            _expandedItems.Add(item);
            if (item is VelaTreeNode node)
                node.IsExpanded = true;
            foreach (var child in children)
                ExpandRecursive(child);
        }
    }

    private void CollapseRecursive(object item)
    {
        _initializedItems.Add(item);
        _expandedItems.Remove(item);
        if (item is VelaTreeNode node)
            node.IsExpanded = false;
        foreach (var child in GetChildItemsList(item))
            CollapseRecursive(child);
    }

    private void SetCheckedRecursive(object item, bool isChecked)
    {
        ApplySingleCheck(item, isChecked);
        foreach (var child in GetChildItemsList(item))
            SetCheckedRecursive(child, isChecked);
    }

    private void ApplySingleCheck(object item, bool isChecked)
    {
        _initializedItems.Add(item);
        if (isChecked)
            _checkedItems.Add(item);
        else
            _checkedItems.Remove(item);
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

    private void SubscribeCollection(INotifyCollectionChanged collection)
    {
        if (_subscribedCollections.Add(collection))
            collection.CollectionChanged += OnChildCollectionChanged;
    }

    private void UnsubscribeAllCollections()
    {
        foreach (var c in _subscribedCollections)
            c.CollectionChanged -= OnChildCollectionChanged;
        _subscribedCollections.Clear();
    }

    private void OnChildCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RebuildProjection();
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
/// Recycled virtualized row container inside <see cref="VelaTreeTable"/>.
/// Injects indent guide lines, spring-rotating disclosure chevron, and optional icon into the tree column.
/// </summary>
[PseudoClasses(":selected", ":alternate")]
internal sealed class VelaTreeTableRowControl : Border
{
    private readonly StackPanel _cellsPanel;
    private readonly List<(Border CellHost, VelaDataGridColumn Column)> _cellHosts = new();
    private VelaCheckBox? _checkBox;
    private VelaTreeIndentGuides? _guides;
    private VelaTreeChevron? _chevron;

    private VelaTreeTableRowState? _row;
    private object? _boundItem;
    private bool _suppressCheckEvent;

    public VelaTreeTableRowControl()
    {
        Classes.Add("vela-table-row");
        Background = Brushes.Transparent;

        _cellsPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
        };

        Child = _cellsPanel;
        this[!BorderBrushProperty] = VelaResourceBinding.Create("VelaBorderSubtleBrush");
    }

    private static Cursor? s_handCursor;
    private VelaTreeTable? _registeredOwner;
    private PathIcon? _treeIcon;
    private Grid? _treeGrid;
    private int _builtTreeColIndex = -1;
    private bool _builtShowCheckBoxes;
    private bool _builtShowVerticalGridLines;
    private bool _hasBuiltCells;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Cursor ??= s_handCursor ??= new Cursor(StandardCursorType.Hand);
        RegisterWithOwner(_row?.Owner);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        RegisterWithOwner(null);
    }

    private void RegisterWithOwner(VelaTreeTable? owner)
    {
        if (ReferenceEquals(_registeredOwner, owner))
            return;
        _registeredOwner?._realizedRows.Remove(this);
        _registeredOwner = owner;
        _registeredOwner?._realizedRows.Add(this);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is VelaTreeTableRowState next)
        {
            var sameItem = ReferenceEquals(_boundItem, next.Item);
            var animateChevron = sameItem && next.Owner.ConsumeShouldAnimateToggle(next.Item);
            _row = next;
            _boundItem = next.Item;
            if (VisualRoot is not null)
                RegisterWithOwner(next.Owner);
            SyncFromBoundRow(rebuildCells: false, animateChevron, updateCellValues: !sameItem);
        }
        else
        {
            _row = null;
            _boundItem = null;
        }
    }

    internal void SyncFromBoundRow(bool rebuildCells, bool animateChevron, bool updateCellValues = false)
    {
        if (_row is null)
            return;

        var owner = _row.Owner;
        Height = VelaColumnLayoutEngine.GetRowHeight(owner.Density, isTreeView: false);
        BorderThickness = owner.ShowHorizontalGridLines ? new Thickness(0, 0, 0, 1) : new Thickness(0);

        PseudoClasses.Set(":selected", _row.IsSelected);
        PseudoClasses.Set(":alternate", owner.ShowAlternatingRows && (_row.RowIndex % 2 == 1));

        if (rebuildCells || !CanReuseBuiltCells(owner))
            RebuildCells(animateChevron);
        else
            SyncStateOnly(animateChevron, updateCellValues);
    }

    private bool CanReuseBuiltCells(VelaTreeTable owner)
    {
        var cols = owner._visibleColumns;
        var treeColIdx = Math.Clamp(owner.TreeColumnIndex, 0, Math.Max(0, cols.Count - 1));
        if (!_hasBuiltCells
            || _builtShowCheckBoxes != owner.ShowCheckBoxes
            || _builtShowVerticalGridLines != owner.ShowVerticalGridLines
            || _builtTreeColIndex != treeColIdx
            || _cellHosts.Count != cols.Count)
        {
            return false;
        }

        for (var i = 0; i < cols.Count; i++)
        {
            if (!ReferenceEquals(_cellHosts[i].Column, cols[i]))
                return false;
        }

        return true;
    }

    internal void SyncColumnWidths()
    {
        foreach (var (host, col) in _cellHosts)
            host.Width = Math.Max(24.0, col.ActualWidth);
    }

    private void SyncStateOnly(bool animateChevron, bool updateCellValues)
    {
        if (_row is null)
            return;

        var owner = _row.Owner;

        if (_checkBox is not null)
        {
            _suppressCheckEvent = true;
            _checkBox.IsChecked = _row.IsChecked;
            _suppressCheckEvent = false;
        }

        if (_guides is not null)
        {
            _guides.Depth = _row.Depth;
            _guides.IndentSize = owner.IndentSize;
            _guides.ShowGuides = owner.ShowGuideLines;
        }

        if (_chevron is not null)
        {
            _chevron.HasChildren = _row.HasChildren;
            _chevron.SetExpanded(_row.IsExpanded, animateChevron);
        }

        if (_treeIcon is not null)
        {
            _treeIcon.Data = _row.Icon;
            _treeIcon.IsVisible = _row.Icon is not null;
        }

        if (updateCellValues)
        {
            for (var i = 0; i < _cellHosts.Count; i++)
            {
                var (host, col) = _cellHosts[i];
                host.Width = Math.Max(24.0, col.ActualWidth);
                if (i == _builtTreeColIndex && _treeGrid is not null)
                {
                    Control? existing = null;
                    for (var c = 0; c < _treeGrid.Children.Count; c++)
                    {
                        if (Grid.GetColumn(_treeGrid.Children[c]) == 3)
                        {
                            existing = _treeGrid.Children[c];
                            break;
                        }
                    }

                    if (existing is null || !col.TryUpdateCellContent(existing, _row.Item))
                    {
                        if (existing is not null)
                            _treeGrid.Children.Remove(existing);
                        var rawCell = col.CreateCellContent(_row.Item);
                        rawCell.Margin = new Thickness(2, 0, 0, 0);
                        Grid.SetColumn(rawCell, 3);
                        _treeGrid.Children.Add(rawCell);
                    }
                }
                else
                {
                    if (host.Child is not Control existing || !col.TryUpdateCellContent(existing, _row.Item))
                        host.Child = col.CreateCellContent(_row.Item);
                }
            }
        }
        else
        {
            SyncColumnWidths();
        }
    }

    private void RebuildCells(bool animateChevron)
    {
        if (_row is null)
            return;

        var owner = _row.Owner;
        var colsForSig = owner._visibleColumns;
        _builtShowCheckBoxes = owner.ShowCheckBoxes;
        _builtShowVerticalGridLines = owner.ShowVerticalGridLines;
        _builtTreeColIndex = Math.Clamp(owner.TreeColumnIndex, 0, Math.Max(0, colsForSig.Count - 1));
        _hasBuiltCells = true;
        _cellsPanel.Children.Clear();
        _cellHosts.Clear();
        _checkBox = null;
        _guides = null;
        _chevron = null;
        _treeIcon = null;
        _treeGrid = null;

        if (owner.ShowCheckBoxes)
        {
            _checkBox = new VelaCheckBox
            {
                IsThreeState = true,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            _suppressCheckEvent = true;
            _checkBox.IsChecked = _row.IsChecked;
            _suppressCheckEvent = false;
            _checkBox.IsCheckedChanged += (_, _) =>
            {
                if (_suppressCheckEvent || _row is null)
                    return;
                _row.Owner.HandleRowCheckToggled(_row);
            };

            _cellsPanel.Children.Add(new Border
            {
                Width = VelaTreeTable.CheckBoxColumnWidth,
                Child = _checkBox,
            });
        }

        var cols = owner._visibleColumns;
        var treeColIdx = Math.Clamp(owner.TreeColumnIndex, 0, Math.Max(0, cols.Count - 1));

        for (var i = 0; i < cols.Count; i++)
        {
            var col = cols[i];
            var rawCell = col.CreateCellContent(_row.Item);

            Control cellInner;
            if (i == treeColIdx)
            {
                _guides = new VelaTreeIndentGuides
                {
                    Depth = _row.Depth,
                    IndentSize = owner.IndentSize,
                    ShowGuides = owner.ShowGuideLines,
                    VerticalAlignment = VerticalAlignment.Stretch,
                    [!VelaTreeIndentGuides.LineBrushProperty] = VelaResourceBinding.Create("VelaBorderBrush"),
                };

                _chevron = new VelaTreeChevron
                {
                    HasChildren = _row.HasChildren,
                    [!VelaTreeChevron.ForegroundProperty] = VelaResourceBinding.Create("VelaTextMutedBrush"),
                    [!VelaTreeChevron.HoverBackgroundProperty] = VelaResourceBinding.Create("VelaSurfaceMutedBrush"),
                };
                _chevron.SetExpanded(_row.IsExpanded, animateChevron);
                _chevron.Toggled += (_, _) =>
                {
                    if (_row is not null)
                        _row.Owner.ToggleExpand(_row.Item);
                };

                _treeGrid = new Grid
                {
                    ColumnDefinitions = ColumnDefinitions.Parse("Auto,Auto,Auto,*"),
                };
                Grid.SetColumn(_guides, 0);
                Grid.SetColumn(_chevron, 1);
                _treeGrid.Children.Add(_guides);
                _treeGrid.Children.Add(_chevron);

                _treeIcon = new PathIcon
                {
                    Width = 15,
                    Height = 15,
                    Data = _row.Icon,
                    IsVisible = _row.Icon is not null,
                    Margin = new Thickness(2, 0, 7, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    [!PathIcon.ForegroundProperty] = VelaResourceBinding.Create("VelaTextSecondaryBrush"),
                };
                Grid.SetColumn(_treeIcon, 2);
                _treeGrid.Children.Add(_treeIcon);

                rawCell.Margin = new Thickness(2, 0, 0, 0);
                Grid.SetColumn(rawCell, 3);
                _treeGrid.Children.Add(rawCell);
                cellInner = _treeGrid;
            }
            else
            {
                cellInner = rawCell;
            }

            var cellHost = new Border
            {
                Width = Math.Max(24.0, col.ActualWidth),
                Padding = i == treeColIdx ? new Thickness(8, 0, 12, 0) : new Thickness(12, 0),
                ClipToBounds = true,
                BorderThickness = owner.ShowVerticalGridLines && i < cols.Count - 1 ? new Thickness(0, 0, 1, 0) : new Thickness(0),
                Child = cellInner,
            };
            if (owner.ShowVerticalGridLines)
                cellHost[!Border.BorderBrushProperty] = VelaResourceBinding.Create("VelaBorderSubtleBrush");

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

            _row.Owner.HandleRowClicked(_row, e.KeyModifiers);
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
