using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Metadata;
using Avalonia.VisualTree;

namespace Vela.Avalonia.Controls;

/// <summary>
/// Flattened visible row projection inside <see cref="VelaTreeView"/>.
/// </summary>
public sealed class VelaTreeRowState
{
    internal VelaTreeRowState(
        VelaTreeView owner,
        object item,
        object? parent,
        int depth,
        bool hasChildren,
        bool isExpanded,
        bool isSelected,
        bool? isChecked,
        bool isEnabled,
        string label,
        string? description,
        Geometry? icon,
        string? badge,
        VelaBadgeTone badgeTone)
    {
        Owner = owner;
        Item = item;
        Parent = parent;
        Depth = depth;
        HasChildren = hasChildren;
        IsExpanded = isExpanded;
        IsSelected = isSelected;
        IsChecked = isChecked;
        IsEnabled = isEnabled;
        Label = label;
        Description = description;
        Icon = icon;
        Badge = badge;
        BadgeTone = badgeTone;
    }

    internal VelaTreeView Owner { get; }
    public object Item { get; }
    public object? Parent { get; }
    public int Depth { get; }
    public bool HasChildren { get; }
    public bool IsExpanded { get; internal set; }
    public bool IsSelected { get; internal set; }
    public bool? IsChecked { get; internal set; }
    public bool IsEnabled { get; }
    public string Label { get; }
    public string? Description { get; }
    public Geometry? Icon { get; }
    public string? Badge { get; }
    public VelaBadgeTone BadgeTone { get; }
}

/// <summary>
/// Virtualized, motion-first hierarchical tree view with 1px vertical indent guide lines,
/// spring-rotating disclosure chevrons, tri-state cascading checkboxes, built-in ancestor-preserving
/// filtering, single/multi selection, and full keyboard navigation.
/// </summary>
public class VelaTreeView : TemplatedControl
{
    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty =
        AvaloniaProperty.Register<VelaTreeView, IEnumerable?>(nameof(ItemsSource));

    public static readonly StyledProperty<IDataTemplate?> ItemTemplateProperty =
        AvaloniaProperty.Register<VelaTreeView, IDataTemplate?>(nameof(ItemTemplate));

    public static readonly StyledProperty<object?> SelectedItemProperty =
        AvaloniaProperty.Register<VelaTreeView, object?>(nameof(SelectedItem), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<VelaSelectionMode> SelectionModeProperty =
        AvaloniaProperty.Register<VelaTreeView, VelaSelectionMode>(nameof(SelectionMode), VelaSelectionMode.Single);

    public static readonly StyledProperty<VelaDataDensity> DensityProperty =
        AvaloniaProperty.Register<VelaTreeView, VelaDataDensity>(nameof(Density), VelaDataDensity.Standard);

    public static readonly StyledProperty<double> IndentSizeProperty =
        AvaloniaProperty.Register<VelaTreeView, double>(nameof(IndentSize), 20.0);

    public static readonly StyledProperty<bool> ShowGuideLinesProperty =
        AvaloniaProperty.Register<VelaTreeView, bool>(nameof(ShowGuideLines), true);

    public static readonly StyledProperty<bool> ShowCheckBoxesProperty =
        AvaloniaProperty.Register<VelaTreeView, bool>(nameof(ShowCheckBoxes), false);

    public static readonly StyledProperty<bool> CascadeCheckStateProperty =
        AvaloniaProperty.Register<VelaTreeView, bool>(nameof(CascadeCheckState), true);

    public static readonly StyledProperty<bool> ExpandOnDoubleClickProperty =
        AvaloniaProperty.Register<VelaTreeView, bool>(nameof(ExpandOnDoubleClick), true);

    public static readonly StyledProperty<string?> FilterTextProperty =
        AvaloniaProperty.Register<VelaTreeView, string?>(nameof(FilterText));

    public static readonly StyledProperty<object?> EmptyContentProperty =
        AvaloniaProperty.Register<VelaTreeView, object?>(nameof(EmptyContent));

    public static readonly DirectProperty<VelaTreeView, int> VisibleCountProperty =
        AvaloniaProperty.RegisterDirect<VelaTreeView, int>(nameof(VisibleCount), o => o.VisibleCount);

    public IEnumerable? ItemsSource { get => GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }
    public IDataTemplate? ItemTemplate { get => GetValue(ItemTemplateProperty); set => SetValue(ItemTemplateProperty, value); }
    public object? SelectedItem { get => GetValue(SelectedItemProperty); set => SetValue(SelectedItemProperty, value); }
    public VelaSelectionMode SelectionMode { get => GetValue(SelectionModeProperty); set => SetValue(SelectionModeProperty, value); }
    public VelaDataDensity Density { get => GetValue(DensityProperty); set => SetValue(DensityProperty, value); }
    public double IndentSize { get => GetValue(IndentSizeProperty); set => SetValue(IndentSizeProperty, value); }
    public bool ShowGuideLines { get => GetValue(ShowGuideLinesProperty); set => SetValue(ShowGuideLinesProperty, value); }
    public bool ShowCheckBoxes { get => GetValue(ShowCheckBoxesProperty); set => SetValue(ShowCheckBoxesProperty, value); }
    public bool CascadeCheckState { get => GetValue(CascadeCheckStateProperty); set => SetValue(CascadeCheckStateProperty, value); }
    public bool ExpandOnDoubleClick { get => GetValue(ExpandOnDoubleClickProperty); set => SetValue(ExpandOnDoubleClickProperty, value); }
    public string? FilterText { get => GetValue(FilterTextProperty); set => SetValue(FilterTextProperty, value); }
    public object? EmptyContent { get => GetValue(EmptyContentProperty); set => SetValue(EmptyContentProperty, value); }

    /// <summary>Direct XAML/code node collection used when <see cref="ItemsSource"/> is not set.</summary>
    [Content]
    public ObservableCollection<VelaTreeNode> Nodes { get; } = new();

    private Func<object, IEnumerable?>? _childrenSelector;
    private Func<object, bool>? _hasChildrenSelector;
    private Func<object, string?>? _labelSelector;
    private Func<object, Geometry?>? _iconSelector;
    private Func<object, string, bool>? _filterPredicate;

    /// <summary>AOT-safe delegate returning children for arbitrary MVVM item models.</summary>
    public Func<object, IEnumerable?>? ChildrenSelector
    {
        get => _childrenSelector;
        set { _childrenSelector = value; RebuildProjection(); }
    }

    /// <summary>Optional explicit override indicating whether an item has children (useful for lazy loading).</summary>
    public Func<object, bool>? HasChildrenSelector
    {
        get => _hasChildrenSelector;
        set { _hasChildrenSelector = value; RebuildProjection(); }
    }

    /// <summary>Optional label extractor for arbitrary MVVM item models.</summary>
    public Func<object, string?>? LabelSelector
    {
        get => _labelSelector;
        set { _labelSelector = value; RebuildProjection(); }
    }

    /// <summary>Optional icon extractor for arbitrary MVVM item models.</summary>
    public Func<object, Geometry?>? IconSelector
    {
        get => _iconSelector;
        set { _iconSelector = value; RebuildProjection(); }
    }

    /// <summary>Optional custom filter predicate evaluated against each item.</summary>
    public Func<object, string, bool>? FilterPredicate
    {
        get => _filterPredicate;
        set { _filterPredicate = value; RebuildProjection(); }
    }

    public IReadOnlyList<VelaTreeRowState> VisibleRows => _visibleRows;
    public IReadOnlyList<object> SelectedItems => _selectedItemsList;
    public IReadOnlyList<object> CheckedItems => _checkedItemsList;

    private int _visibleCount;
    public int VisibleCount
    {
        get => _visibleCount;
        private set => SetAndRaise(VisibleCountProperty, ref _visibleCount, value);
    }

    public event EventHandler? SelectionChanged;
    public event EventHandler<VelaDataItemEventArgs>? ItemInvoked;
    public event EventHandler<VelaDataItemEventArgs>? NodeExpanded;
    public event EventHandler<VelaDataItemEventArgs>? NodeCollapsed;
    public event EventHandler? CheckedItemsChanged;

    protected override Type StyleKeyOverride => typeof(VelaTreeView);

    internal readonly ObservableCollection<VelaTreeRowState> _visibleRows = new();
    private readonly HashSet<object> _expandedItems = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<object> _selectedItems = new(ReferenceEqualityComparer.Instance);
    private readonly List<object> _selectedItemsList = new();
    private readonly HashSet<object> _checkedItems = new(ReferenceEqualityComparer.Instance);
    private readonly List<object> _checkedItemsList = new();
    private readonly HashSet<object> _initializedNodes = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<INotifyCollectionChanged> _subscribedCollections = new(ReferenceEqualityComparer.Instance);

    private ListBox? _listBox;
    private ContentPresenter? _emptyHost;
    private object? _anchorItem;
    private bool _syncingSelection;
    private object? _lastAnimatedToggleItem;

    static VelaTreeView()
    {
        FocusableProperty.OverrideDefaultValue<VelaTreeView>(true);
        ItemsSourceProperty.Changed.AddClassHandler<VelaTreeView>((v, _) => v.OnItemsSourceChanged());
        SelectedItemProperty.Changed.AddClassHandler<VelaTreeView>((v, e) => v.OnSelectedItemPropertyChanged(e.NewValue));
        SelectionModeProperty.Changed.AddClassHandler<VelaTreeView>((v, _) => v.OnSelectionModeChanged());
        DensityProperty.Changed.AddClassHandler<VelaTreeView>((v, _) => v.RefreshRowsVisual());
        IndentSizeProperty.Changed.AddClassHandler<VelaTreeView>((v, _) => v.RefreshRowsVisual());
        ShowGuideLinesProperty.Changed.AddClassHandler<VelaTreeView>((v, _) => v.RefreshRowsVisual());
        ShowCheckBoxesProperty.Changed.AddClassHandler<VelaTreeView>((v, _) => v.RebuildProjection());
        CascadeCheckStateProperty.Changed.AddClassHandler<VelaTreeView>((v, _) => v.RebuildProjection());
        FilterTextProperty.Changed.AddClassHandler<VelaTreeView>((v, _) => v.RebuildProjection());
        ItemTemplateProperty.Changed.AddClassHandler<VelaTreeView>((v, _) => v.RefreshRowsVisual());
    }

    public VelaTreeView()
    {
        Nodes.CollectionChanged += OnRootCollectionChanged;
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _listBox = e.NameScope.Find<ListBox>("PART_List");
        _emptyHost = e.NameScope.Find<ContentPresenter>("PART_EmptyHost");

        if (_listBox is not null)
        {
            _listBox.ItemsSource = _visibleRows;
            _listBox.ItemTemplate = new FuncDataTemplate<VelaTreeRowState>((_, _) => new VelaTreeRowControl(), supportsRecycling: true);
        }

        RebuildProjection();
        UpdateEmptyState();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        UnsubscribeAllCollections();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        RebuildProjection();
    }

    /// <summary>Rebuilds the flattened tree projection from the current data source.</summary>
    public void Refresh() => RebuildProjection();

    /// <summary>Returns whether the specified item is currently expanded.</summary>
    public bool IsItemExpanded(object item)
    {
        if (item is VelaTreeNode node && !_initializedNodes.Contains(node))
            return node.IsExpanded;
        return _expandedItems.Contains(item);
    }

    /// <summary>Expands the specified item if it has children.</summary>
    public void Expand(object item)
    {
        if (item is null || !ResolveHasChildren(item))
            return;
        if (IsItemExpanded(item))
            return;

        _initializedNodes.Add(item);
        _expandedItems.Add(item);
        if (item is VelaTreeNode node)
            node.IsExpanded = true;

        _lastAnimatedToggleItem = item;
        RebuildProjection();
        _lastAnimatedToggleItem = null;
        NodeExpanded?.Invoke(this, new VelaDataItemEventArgs(item));
    }

    /// <summary>Collapses the specified item.</summary>
    public void Collapse(object item)
    {
        if (item is null || !IsItemExpanded(item))
            return;

        _initializedNodes.Add(item);
        _expandedItems.Remove(item);
        if (item is VelaTreeNode node)
            node.IsExpanded = false;

        _lastAnimatedToggleItem = item;
        RebuildProjection();
        _lastAnimatedToggleItem = null;
        NodeCollapsed?.Invoke(this, new VelaDataItemEventArgs(item));
    }

    /// <summary>Toggles expanded/collapsed state for the specified item.</summary>
    public void ToggleExpand(object item)
    {
        if (IsItemExpanded(item))
            Collapse(item);
        else
            Expand(item);
    }

    /// <summary>Expands all parent nodes in the tree.</summary>
    public void ExpandAll()
    {
        foreach (var root in GetRootItems())
            ExpandRecursive(root);
        RebuildProjection();
    }

    /// <summary>Collapses all nodes in the tree.</summary>
    public void CollapseAll()
    {
        foreach (var root in GetRootItems())
            CollapseRecursive(root);
        _expandedItems.Clear();
        RebuildProjection();
    }

    /// <summary>Returns the computed tri-state check status (<c>true</c>, <c>false</c>, or <c>null</c>) for an item.</summary>
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

    /// <summary>Sets the check state of an item, optionally cascading to descendants when <see cref="CascadeCheckState"/> is enabled.</summary>
    public void SetChecked(object item, bool isChecked)
    {
        if (item is null)
            return;

        if (CascadeCheckState)
            SetCheckedRecursive(item, isChecked);
        else
            ApplySingleCheck(item, isChecked);

        SyncNodeCheckProperties();
        SyncCheckedItemsList();
        RebuildProjection();
        CheckedItemsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Clears all row selections.</summary>
    public void ClearSelection()
    {
        if (_selectedItems.Count == 0 && SelectedItem is null)
            return;

        _selectedItems.Clear();
        _selectedItemsList.Clear();
        _syncingSelection = true;
        SelectedItem = null;
        _syncingSelection = false;
        UpdateVisibleRowSelectionFlags();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    internal bool ConsumeShouldAnimateToggle(object item)
        => ReferenceEquals(_lastAnimatedToggleItem, item);

    internal void HandleRowClicked(VelaTreeRowState row, KeyModifiers modifiers)
    {
        if (!row.IsEnabled)
            return;

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

        // Multiple selection
        if (isShift && _anchorItem is not null)
        {
            var anchorIdx = FindVisibleRowIndex(_anchorItem);
            var targetIdx = FindVisibleRowIndex(row.Item);
            if (anchorIdx >= 0 && targetIdx >= 0)
            {
                if (!isCtrl)
                    _selectedItems.Clear();

                var start = Math.Min(anchorIdx, targetIdx);
                var end = Math.Max(anchorIdx, targetIdx);
                for (var i = start; i <= end; i++)
                {
                    if (_visibleRows[i].IsEnabled)
                        _selectedItems.Add(_visibleRows[i].Item);
                }

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

    internal void HandleRowDoubleTapped(VelaTreeRowState row)
    {
        if (!row.IsEnabled)
            return;

        if (ExpandOnDoubleClick && row.HasChildren)
            ToggleExpand(row.Item);

        ItemInvoked?.Invoke(this, new VelaDataItemEventArgs(row.Item));
    }

    internal void HandleRowCheckToggled(VelaTreeRowState row)
    {
        if (!row.IsEnabled)
            return;

        var current = GetCheckState(row.Item);
        var next = current != true;
        SetChecked(row.Item, next);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (_visibleRows.Count == 0)
            return;

        var currentIdx = SelectedItem is not null ? FindVisibleRowIndex(SelectedItem) : -1;

        switch (e.Key)
        {
            case Key.Down:
                SelectVisibleIndex(currentIdx < 0 ? 0 : Math.Min(_visibleRows.Count - 1, currentIdx + 1), e.KeyModifiers);
                e.Handled = true;
                break;

            case Key.Up:
                SelectVisibleIndex(currentIdx < 0 ? _visibleRows.Count - 1 : Math.Max(0, currentIdx - 1), e.KeyModifiers);
                e.Handled = true;
                break;

            case Key.Home:
                SelectVisibleIndex(0, e.KeyModifiers);
                e.Handled = true;
                break;

            case Key.End:
                SelectVisibleIndex(_visibleRows.Count - 1, e.KeyModifiers);
                e.Handled = true;
                break;

            case Key.Right:
                if (currentIdx >= 0)
                {
                    var row = _visibleRows[currentIdx];
                    if (row.HasChildren && !row.IsExpanded)
                    {
                        Expand(row.Item);
                    }
                    else if (row.HasChildren && currentIdx + 1 < _visibleRows.Count && _visibleRows[currentIdx + 1].Depth > row.Depth)
                    {
                        SelectVisibleIndex(currentIdx + 1, KeyModifiers.None);
                    }
                    e.Handled = true;
                }
                break;

            case Key.Left:
                if (currentIdx >= 0)
                {
                    var row = _visibleRows[currentIdx];
                    if (row.HasChildren && row.IsExpanded)
                    {
                        Collapse(row.Item);
                    }
                    else if (row.Parent is not null)
                    {
                        var parentIdx = FindVisibleRowIndex(row.Parent);
                        if (parentIdx >= 0)
                            SelectVisibleIndex(parentIdx, KeyModifiers.None);
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
                    var row = _visibleRows[currentIdx];
                    ItemInvoked?.Invoke(this, new VelaDataItemEventArgs(row.Item));
                    e.Handled = true;
                }
                break;
        }
    }

    private void SelectVisibleIndex(int index, KeyModifiers modifiers)
    {
        if (index < 0 || index >= _visibleRows.Count)
            return;

        var row = _visibleRows[index];
        HandleRowClicked(row, modifiers);
        _listBox?.ScrollIntoView(row);
    }

    private void OnItemsSourceChanged()
    {
        _initializedNodes.Clear();
        RebuildProjection();
    }

    private void OnRootCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RebuildProjection();
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
        UpdateVisibleRowSelectionFlags();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnSelectionModeChanged()
    {
        if (SelectionMode == VelaSelectionMode.None)
        {
            ClearSelection();
        }
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
        UpdateVisibleRowSelectionFlags();
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

    private void UpdateVisibleRowSelectionFlags()
    {
        foreach (var row in _visibleRows)
            row.IsSelected = _selectedItems.Contains(row.Item);

        RefreshRowsVisual();
    }

    private void RefreshRowsVisual()
    {
        if (_listBox is null)
            return;

        foreach (var rowCtrl in _listBox.GetVisualDescendants().OfType<VelaTreeRowControl>())
            rowCtrl.SyncFromBoundRow(animateChevron: false);
    }

    private void RebuildProjection()
    {
        UnsubscribeAllCollections();

        var roots = GetRootItems();
        if (ItemsSource is INotifyCollectionChanged incc)
            SubscribeCollection(incc);

        var filter = FilterText?.Trim();
        var hasFilter = !string.IsNullOrEmpty(filter);

        var nextRows = new List<VelaTreeRowState>();
        var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);

        foreach (var root in roots)
            ProjectNode(root, parent: null, depth: 0, filter, hasFilter, nextRows, visited);

        _visibleRows.Clear();
        foreach (var row in nextRows)
            _visibleRows.Add(row);

        VisibleCount = _visibleRows.Count;
        UpdateEmptyState();
    }

    private bool ProjectNode(
        object item,
        object? parent,
        int depth,
        string? filter,
        bool hasFilter,
        List<VelaTreeRowState> output,
        HashSet<object> visited)
    {
        if (item is null || !visited.Add(item))
            return false;

        InitializeNodeStateIfNeeded(item);

        var children = GetChildItemsList(item);
        var hasChildren = ResolveHasChildren(item, children.Count);
        var selfMatch = !hasFilter || MatchesFilter(item, filter!);

        if (!hasFilter)
        {
            var isExpanded = hasChildren && _expandedItems.Contains(item);
            output.Add(CreateRowState(item, parent, depth, hasChildren, isExpanded));

            if (isExpanded)
            {
                foreach (var child in children)
                    ProjectNode(child, item, depth + 1, filter, hasFilter: false, output, visited);
            }

            return true;
        }

        // Filtered mode: evaluate children first so ancestors of matching descendants are retained & expanded.
        var childRows = new List<VelaTreeRowState>();
        var anyChildMatched = false;
        foreach (var child in children)
        {
            if (ProjectNode(child, item, depth + 1, filter, hasFilter: true, childRows, visited))
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
            // Node itself matched and is already expanded: also include its visible subtree.
            foreach (var child in children)
                ProjectNode(child, item, depth + 1, filter: null, hasFilter: false, output, visited);
        }

        return true;
    }

    private VelaTreeRowState CreateRowState(object item, object? parent, int depth, bool hasChildren, bool isExpanded)
    {
        var node = item as VelaTreeNode;
        var label = LabelSelector?.Invoke(item) ?? node?.Header ?? item.ToString() ?? string.Empty;
        var description = node?.Description;
        var icon = IconSelector?.Invoke(item) ?? node?.EffectiveIcon;
        var badge = node?.Badge;
        var badgeTone = node?.BadgeTone ?? VelaBadgeTone.Neutral;
        var isEnabled = node?.IsEnabled ?? true;
        var checkState = ShowCheckBoxes ? GetCheckState(item) : (bool?)false;

        return new VelaTreeRowState(
            owner: this,
            item: item,
            parent: parent,
            depth: depth,
            hasChildren: hasChildren,
            isExpanded: isExpanded,
            isSelected: _selectedItems.Contains(item),
            isChecked: checkState,
            isEnabled: isEnabled,
            label: label,
            description: description,
            icon: icon,
            badge: badge,
            badgeTone: badgeTone);
    }

    private void InitializeNodeStateIfNeeded(object item)
    {
        if (!_initializedNodes.Add(item))
            return;

        if (item is VelaTreeNode node)
        {
            if (node.IsExpanded)
                _expandedItems.Add(node);
            if (node.IsChecked == true)
                _checkedItems.Add(node);
        }
    }

    private bool MatchesFilter(object item, string query)
    {
        if (FilterPredicate is not null)
            return FilterPredicate(item, query);

        var label = LabelSelector?.Invoke(item) ?? (item as VelaTreeNode)?.Header ?? item.ToString();
        if (!string.IsNullOrEmpty(label) && label.Contains(query, StringComparison.OrdinalIgnoreCase))
            return true;

        var desc = (item as VelaTreeNode)?.Description;
        if (!string.IsNullOrEmpty(desc) && desc.Contains(query, StringComparison.OrdinalIgnoreCase))
            return true;

        var badge = (item as VelaTreeNode)?.Badge;
        if (!string.IsNullOrEmpty(badge) && badge.Contains(query, StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    private IEnumerable<object> GetRootItems()
    {
        if (ItemsSource is not null)
        {
            foreach (var item in ItemsSource)
            {
                if (item is not null)
                    yield return item;
            }
            yield break;
        }

        foreach (var node in Nodes)
            yield return node;
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
        _initializedNodes.Add(item);
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
        _initializedNodes.Add(item);
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
        _initializedNodes.Add(item);
        if (isChecked)
            _checkedItems.Add(item);
        else
            _checkedItems.Remove(item);
    }

    private void SyncNodeCheckProperties()
    {
        foreach (var root in GetRootItems())
            SyncNodeCheckRecursive(root);
    }

    private void SyncNodeCheckRecursive(object item)
    {
        if (item is VelaTreeNode node)
            node.IsChecked = GetCheckState(node);

        foreach (var child in GetChildItemsList(item))
            SyncNodeCheckRecursive(child);
    }

    private int FindVisibleRowIndex(object item)
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
/// Recycled visual row container inside <see cref="VelaTreeView"/>.
/// Renders indent guide lines, spring-rotating disclosure chevron, optional tri-state checkbox,
/// icon, label/description or custom <see cref="IDataTemplate"/>, and trailing badge.
/// </summary>
[PseudoClasses(":selected", ":disabled")]
internal sealed class VelaTreeRowControl : Border
{
    private readonly Grid _grid;
    private readonly VelaTreeIndentGuides _guides;
    private readonly VelaTreeChevron _chevron;
    private readonly VelaCheckBox _checkBox;
    private readonly PathIcon _icon;
    private readonly ContentControl _contentHost;
    private readonly VelaBadge _badge;
    private readonly Border _accentBar;

    private VelaTreeRowState? _row;
    private object? _boundItem;
    private IDataTemplate? _lastTemplate;
    private bool _suppressCheckEvent;

    public VelaTreeRowControl()
    {
        Classes.Add("vela-tree-row");
        CornerRadius = new CornerRadius(8);
        Margin = new Thickness(4, 1);
        Padding = new Thickness(6, 0, 10, 0);
        Background = Brushes.Transparent;
        Cursor = new Cursor(StandardCursorType.Hand);

        _accentBar = new Border
        {
            Width = 3,
            Height = 16,
            CornerRadius = new CornerRadius(2),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(-4, 0, 0, 0),
            IsVisible = false,
            [!Border.BackgroundProperty] = this.GetResourceObservable("VelaAccentBrush").ToBinding(),
        };

        _guides = new VelaTreeIndentGuides
        {
            VerticalAlignment = VerticalAlignment.Stretch,
            [!VelaTreeIndentGuides.LineBrushProperty] = this.GetResourceObservable("VelaBorderBrush").ToBinding(),
        };

        _chevron = new VelaTreeChevron
        {
            [!VelaTreeChevron.ForegroundProperty] = this.GetResourceObservable("VelaTextMutedBrush").ToBinding(),
            [!VelaTreeChevron.HoverBackgroundProperty] = this.GetResourceObservable("VelaSurfaceMutedBrush").ToBinding(),
        };
        _chevron.Toggled += (_, _) =>
        {
            if (_row is not null)
                _row.Owner.ToggleExpand(_row.Item);
        };

        _checkBox = new VelaCheckBox
        {
            IsThreeState = true,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(2, 0, 4, 0),
            IsVisible = false,
        };
        _checkBox.IsCheckedChanged += (_, _) =>
        {
            if (_suppressCheckEvent || _row is null)
                return;
            _row.Owner.HandleRowCheckToggled(_row);
        };

        _icon = new PathIcon
        {
            Width = 15,
            Height = 15,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(2, 0, 6, 0),
            IsVisible = false,
            [!PathIcon.ForegroundProperty] = this.GetResourceObservable("VelaTextSecondaryBrush").ToBinding(),
        };

        _contentHost = new ContentControl
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
        };

        _badge = new VelaBadge
        {
            Size = VelaControlSize.Small,
            IsPill = true,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0),
            IsVisible = false,
        };

        _grid = new Grid
        {
            ColumnDefinitions = ColumnDefinitions.Parse("Auto,Auto,Auto,Auto,*,Auto"),
        };

        Grid.SetColumn(_guides, 0);
        Grid.SetColumn(_chevron, 1);
        Grid.SetColumn(_checkBox, 2);
        Grid.SetColumn(_icon, 3);
        Grid.SetColumn(_contentHost, 4);
        Grid.SetColumn(_badge, 5);

        _grid.Children.Add(_guides);
        _grid.Children.Add(_chevron);
        _grid.Children.Add(_checkBox);
        _grid.Children.Add(_icon);
        _grid.Children.Add(_contentHost);
        _grid.Children.Add(_badge);

        var rootPanel = new Panel();
        rootPanel.Children.Add(_accentBar);
        rootPanel.Children.Add(_grid);
        Child = rootPanel;
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is VelaTreeRowState next)
        {
            var sameItem = ReferenceEquals(_boundItem, next.Item);
            var animateChevron = sameItem && next.Owner.ConsumeShouldAnimateToggle(next.Item);
            _row = next;
            _boundItem = next.Item;
            SyncFromBoundRow(animateChevron);
        }
        else
        {
            _row = null;
            _boundItem = null;
        }
    }

    internal void SyncFromBoundRow(bool animateChevron)
    {
        if (_row is null)
            return;

        var owner = _row.Owner;
        Height = VelaColumnLayoutEngine.GetRowHeight(owner.Density, isTreeView: true);

        _guides.Depth = _row.Depth;
        _guides.IndentSize = owner.IndentSize;
        _guides.ShowGuides = owner.ShowGuideLines;

        _chevron.HasChildren = _row.HasChildren;
        _chevron.SetExpanded(_row.IsExpanded, animateChevron);

        _checkBox.IsVisible = owner.ShowCheckBoxes;
        if (owner.ShowCheckBoxes)
        {
            _suppressCheckEvent = true;
            _checkBox.IsChecked = _row.IsChecked;
            _checkBox.IsEnabled = _row.IsEnabled;
            _suppressCheckEvent = false;
        }

        PseudoClasses.Set(":selected", _row.IsSelected);
        PseudoClasses.Set(":disabled", !_row.IsEnabled);
        _accentBar.IsVisible = _row.IsSelected;

        var template = owner.ItemTemplate;
        if (template is not null)
        {
            _icon.IsVisible = false;
            _badge.IsVisible = false;
            if (!ReferenceEquals(_lastTemplate, template) || _contentHost.Content is not Control existing || !ReferenceEquals(existing.DataContext, _row.Item))
            {
                _lastTemplate = template;
                var built = template.Build(_row.Item);
                if (built is not null)
                    built.DataContext = _row.Item;
                _contentHost.Content = built;
            }
            return;
        }

        _lastTemplate = null;

        _icon.Data = _row.Icon;
        _icon.IsVisible = _row.Icon is not null;

        if (!string.IsNullOrEmpty(_row.Description))
        {
            var stack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                VerticalAlignment = VerticalAlignment.Center,
            };
            var title = new TextBlock
            {
                Text = _row.Label,
                FontSize = 13,
                FontWeight = _row.IsSelected ? FontWeight.SemiBold : FontWeight.Normal,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center,
                [!TextBlock.ForegroundProperty] = this.GetResourceObservable("VelaForegroundBrush").ToBinding(),
            };
            var desc = new TextBlock
            {
                Text = _row.Description,
                FontSize = 11.5,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center,
                [!TextBlock.ForegroundProperty] = this.GetResourceObservable("VelaTextMutedBrush").ToBinding(),
            };
            stack.Children.Add(title);
            stack.Children.Add(desc);
            _contentHost.Content = stack;
        }
        else
        {
            _contentHost.Content = new TextBlock
            {
                Text = _row.Label,
                FontSize = 13,
                FontWeight = _row.IsSelected ? FontWeight.SemiBold : FontWeight.Normal,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center,
                [!TextBlock.ForegroundProperty] = this.GetResourceObservable("VelaForegroundBrush").ToBinding(),
            };
        }

        _badge.Content = _row.Badge;
        _badge.Tone = _row.BadgeTone;
        _badge.IsVisible = !string.IsNullOrEmpty(_row.Badge);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (_row is null || e.Handled)
            return;

        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            // Ignore clicks inside the checkbox itself so it handles check state independently.
            if (e.Source is Visual v && (ReferenceEquals(v, _checkBox) || v.FindAncestorOfType<VelaCheckBox>() is not null))
                return;

            _row.Owner.HandleRowClicked(_row, e.KeyModifiers);
        }
    }

    protected override void OnDoubleTapped(TappedEventArgs e)
    {
        base.OnDoubleTapped(e);
        if (_row is null || e.Handled)
            return;

        if (e.Source is Visual v && (ReferenceEquals(v, _checkBox) || v.FindAncestorOfType<VelaCheckBox>() is not null))
            return;

        _row.Owner.HandleRowDoubleTapped(_row);
        e.Handled = true;
    }
}
