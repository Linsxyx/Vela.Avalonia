using System.Collections;
using System.Collections.Specialized;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using Vela.Avalonia.Motion;

namespace Vela.Avalonia.Controls;

/// <summary>
/// Attached properties for configuring drag behavior inside <see cref="VelaDragList"/>.
/// </summary>
public static class VelaDragDrop
{
    /// <summary>
    /// Marks a visual inside an item template as an explicit drag handle.
    /// When any descendant of an item has this set to <see langword="true"/>,
    /// dragging can only start from marked handles.
    /// </summary>
    public static readonly AttachedProperty<bool> IsDragHandleProperty =
        AvaloniaProperty.RegisterAttached<Visual, bool>("IsDragHandle", typeof(VelaDragDrop), false);

    /// <summary>
    /// Controls whether an individual item or container can be dragged.
    /// </summary>
    public static readonly AttachedProperty<bool> IsDraggableProperty =
        AvaloniaProperty.RegisterAttached<Visual, bool>("IsDraggable", typeof(VelaDragDrop), true);

    public static bool GetIsDragHandle(Visual element) => element.GetValue(IsDragHandleProperty);
    public static void SetIsDragHandle(Visual element, bool value) => element.SetValue(IsDragHandleProperty, value);

    public static bool GetIsDraggable(Visual element) => element.GetValue(IsDraggableProperty);
    public static void SetIsDraggable(Visual element, bool value) => element.SetValue(IsDraggableProperty, value);
}

/// <summary>
/// Describes an active drag operation for drop validation via <see cref="VelaDragList.CanAcceptDrop"/>.
/// </summary>
public sealed record VelaDragPayload(
    object Item,
    VelaDragList SourceList,
    int SourceIndex,
    string? DragGroup);

public sealed class VelaDragStartedEventArgs : EventArgs
{
    public object Item { get; }
    public VelaDragList SourceList { get; }
    public int SourceIndex { get; }

    public VelaDragStartedEventArgs(object item, VelaDragList sourceList, int sourceIndex)
    {
        Item = item;
        SourceList = sourceList;
        SourceIndex = sourceIndex;
    }
}

public sealed class VelaDragMovedEventArgs : EventArgs
{
    public object Item { get; }
    public VelaDragList SourceList { get; }
    public int SourceIndex { get; }
    public VelaDragList TargetList { get; }
    public int TargetIndex { get; }

    /// <summary>
    /// Set to <see langword="true"/> to prevent <see cref="VelaDragList.AutoMoveItems"/> from mutating the bound lists.
    /// </summary>
    public bool Handled { get; set; }

    public VelaDragMovedEventArgs(
        object item,
        VelaDragList sourceList,
        int sourceIndex,
        VelaDragList targetList,
        int targetIndex)
    {
        Item = item;
        SourceList = sourceList;
        SourceIndex = sourceIndex;
        TargetList = targetList;
        TargetIndex = targetIndex;
    }
}

public sealed class VelaDragEndedEventArgs : EventArgs
{
    public object Item { get; }
    public VelaDragList SourceList { get; }
    public int SourceIndex { get; }
    public VelaDragList? TargetList { get; }
    public int TargetIndex { get; }
    public bool Canceled { get; }

    public VelaDragEndedEventArgs(
        object item,
        VelaDragList sourceList,
        int sourceIndex,
        VelaDragList? targetList,
        int targetIndex,
        bool canceled)
    {
        Item = item;
        SourceList = sourceList;
        SourceIndex = sourceIndex;
        TargetList = targetList;
        TargetIndex = targetIndex;
        Canceled = canceled;
    }
}

public sealed class VelaDragItemClickedEventArgs : EventArgs
{
    public object Item { get; }
    public int Index { get; }

    public VelaDragItemClickedEventArgs(object item, int index)
    {
        Item = item;
        Index = index;
    }
}

/// <summary>
/// Container for an individual draggable item inside <see cref="VelaDragList"/>.
/// Supports FLIP-style analytical spring displacement when neighbors reorder.
/// </summary>
public class VelaDragItem : ContentControl
{
    private readonly TranslateTransform _flipTransform = new();
    private readonly SpringChannel _offsetX = new(0.0, VelaMotionTokens.Springs.Snappy);
    private readonly SpringChannel _offsetY = new(0.0, VelaMotionTokens.Springs.Snappy);
    private readonly SpringRunner _springRunner;

    internal VelaDragList? Owner { get; set; }
    internal object? ItemData { get; set; }

    internal double CurrentOffsetX => _offsetX.Value;
    internal double CurrentOffsetY => _offsetY.Value;

    protected override Type StyleKeyOverride => typeof(VelaDragItem);

    public VelaDragItem()
    {
        RenderTransform = _flipTransform;
        _springRunner = new SpringRunner(this, OnSpringTick);
    }

    internal void SetDraggingVisualState(bool isDragging)
    {
        PseudoClasses.Set(":dragging", isDragging);
    }

    internal void AnimateFromOffset(double dx, double dy)
    {
        if (VelaMotionOptions.ReducedMotion || (Math.Abs(dx) < 0.5 && Math.Abs(dy) < 0.5))
        {
            ResetFlipOffset();
            return;
        }

        _offsetX.Jump(dx);
        _offsetY.Jump(dy);
        _offsetX.SetTarget(0.0, VelaMotionTokens.Springs.Snappy);
        _offsetY.SetTarget(0.0, VelaMotionTokens.Springs.Snappy);
        _flipTransform.X = dx;
        _flipTransform.Y = dy;
        _springRunner.Wake();
    }

    internal void ResetFlipOffset()
    {
        _springRunner.Stop();
        _offsetX.Jump(0.0);
        _offsetY.Jump(0.0);
        _flipTransform.X = 0.0;
        _flipTransform.Y = 0.0;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        ResetFlipOffset();
    }

    private bool OnSpringTick(double dt)
    {
        var ax = _offsetX.Step(dt);
        var ay = _offsetY.Step(dt);
        _flipTransform.X = _offsetX.Value;
        _flipTransform.Y = _offsetY.Value;
        return ax || ay;
    }
}

/// <summary>
/// Motion-first reorderable and cross-container drag-and-drop list control.
/// Uses pointer capture + <see cref="OverlayLayer"/> floating preview + FLIP spring neighbor displacement,
/// making it suitable for Kanban columns, swimlanes, and sortable lists across Desktop and Browser targets.
/// </summary>
public class VelaDragList : TemplatedControl
{
    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty =
        AvaloniaProperty.Register<VelaDragList, IEnumerable?>(nameof(ItemsSource));

    public static readonly StyledProperty<IDataTemplate?> ItemTemplateProperty =
        AvaloniaProperty.Register<VelaDragList, IDataTemplate?>(nameof(ItemTemplate));

    public static readonly StyledProperty<IDataTemplate?> DragPreviewTemplateProperty =
        AvaloniaProperty.Register<VelaDragList, IDataTemplate?>(nameof(DragPreviewTemplate));

    public static readonly StyledProperty<Orientation> OrientationProperty =
        AvaloniaProperty.Register<VelaDragList, Orientation>(nameof(Orientation), Orientation.Vertical);

    public static readonly StyledProperty<double> SpacingProperty =
        AvaloniaProperty.Register<VelaDragList, double>(nameof(Spacing), 8.0);

    public static readonly StyledProperty<string?> DragGroupProperty =
        AvaloniaProperty.Register<VelaDragList, string?>(nameof(DragGroup));

    public static readonly StyledProperty<bool> IsDragEnabledProperty =
        AvaloniaProperty.Register<VelaDragList, bool>(nameof(IsDragEnabled), true);

    public static readonly StyledProperty<bool> AutoMoveItemsProperty =
        AvaloniaProperty.Register<VelaDragList, bool>(nameof(AutoMoveItems), true);

    public static readonly StyledProperty<double> DragThresholdProperty =
        AvaloniaProperty.Register<VelaDragList, double>(nameof(DragThreshold), 5.0);

    public static readonly StyledProperty<CornerRadius> PlaceholderCornerRadiusProperty =
        AvaloniaProperty.Register<VelaDragList, CornerRadius>(nameof(PlaceholderCornerRadius), new CornerRadius(12));

    public static readonly StyledProperty<string?> EmptyPlaceholderTextProperty =
        AvaloniaProperty.Register<VelaDragList, string?>(nameof(EmptyPlaceholderText));

    public static readonly StyledProperty<Func<VelaDragPayload, bool>?> CanAcceptDropProperty =
        AvaloniaProperty.Register<VelaDragList, Func<VelaDragPayload, bool>?>(nameof(CanAcceptDrop));

    public static readonly DirectProperty<VelaDragList, bool> IsDropTargetActiveProperty =
        AvaloniaProperty.RegisterDirect<VelaDragList, bool>(
            nameof(IsDropTargetActive),
            o => o.IsDropTargetActive);

    public static readonly DirectProperty<VelaDragList, bool> IsDraggingProperty =
        AvaloniaProperty.RegisterDirect<VelaDragList, bool>(
            nameof(IsDragging),
            o => o.IsDragging);

    public static readonly DirectProperty<VelaDragList, bool> HasItemsProperty =
        AvaloniaProperty.RegisterDirect<VelaDragList, bool>(
            nameof(HasItems),
            o => o.HasItems);

    public IEnumerable? ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public IDataTemplate? ItemTemplate
    {
        get => GetValue(ItemTemplateProperty);
        set => SetValue(ItemTemplateProperty, value);
    }

    public IDataTemplate? DragPreviewTemplate
    {
        get => GetValue(DragPreviewTemplateProperty);
        set => SetValue(DragPreviewTemplateProperty, value);
    }

    public Orientation Orientation
    {
        get => GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    public double Spacing
    {
        get => GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    public string? DragGroup
    {
        get => GetValue(DragGroupProperty);
        set => SetValue(DragGroupProperty, value);
    }

    public bool IsDragEnabled
    {
        get => GetValue(IsDragEnabledProperty);
        set => SetValue(IsDragEnabledProperty, value);
    }

    public bool AutoMoveItems
    {
        get => GetValue(AutoMoveItemsProperty);
        set => SetValue(AutoMoveItemsProperty, value);
    }

    public double DragThreshold
    {
        get => GetValue(DragThresholdProperty);
        set => SetValue(DragThresholdProperty, value);
    }

    public CornerRadius PlaceholderCornerRadius
    {
        get => GetValue(PlaceholderCornerRadiusProperty);
        set => SetValue(PlaceholderCornerRadiusProperty, value);
    }

    public string? EmptyPlaceholderText
    {
        get => GetValue(EmptyPlaceholderTextProperty);
        set => SetValue(EmptyPlaceholderTextProperty, value);
    }

    public Func<VelaDragPayload, bool>? CanAcceptDrop
    {
        get => GetValue(CanAcceptDropProperty);
        set => SetValue(CanAcceptDropProperty, value);
    }

    private bool _isDropTargetActive;
    public bool IsDropTargetActive
    {
        get => _isDropTargetActive;
        private set
        {
            if (SetAndRaise(IsDropTargetActiveProperty, ref _isDropTargetActive, value))
                PseudoClasses.Set(":drop-active", value);
        }
    }

    private bool _isDragging;
    public bool IsDragging
    {
        get => _isDragging;
        private set
        {
            if (SetAndRaise(IsDraggingProperty, ref _isDragging, value))
                PseudoClasses.Set(":dragging", value);
        }
    }

    private bool _hasItems;
    public bool HasItems
    {
        get => _hasItems;
        private set
        {
            if (SetAndRaise(HasItemsProperty, ref _hasItems, value))
                PseudoClasses.Set(":empty", !value);
        }
    }

    public event EventHandler<VelaDragStartedEventArgs>? DragStarted;
    public event EventHandler<VelaDragMovedEventArgs>? ItemReordered;
    public event EventHandler<VelaDragMovedEventArgs>? ItemTransferred;
    public event EventHandler<VelaDragEndedEventArgs>? DragEnded;
    public event EventHandler<VelaDragItemClickedEventArgs>? ItemClicked;

    protected override Type StyleKeyOverride => typeof(VelaDragList);

    private StackPanel? _itemsPanel;
    private INotifyCollectionChanged? _subscribedCollection;
    private readonly List<VelaDragItem> _containers = new();
    private VelaDragCoordinator? _coordinator;
    private Border? _activePlaceholder;
    private Dictionary<VelaDragItem, Point>? _flipSnapshot;

    // Pending press state (before exceeding DragThreshold)
    private VelaDragItem? _pressedContainer;
    private IPointer? _pressedPointer;
    private Point _pressPointInTopLevel;
    private Point _pressOffsetInItem;

    internal StackPanel? ItemsPanel => _itemsPanel;
    internal IReadOnlyList<VelaDragItem> Containers => _containers;

    static VelaDragList()
    {
        ItemsSourceProperty.Changed.AddClassHandler<VelaDragList>((list, e) => list.OnItemsSourceChanged(e));
        ItemTemplateProperty.Changed.AddClassHandler<VelaDragList>((list, _) => list.UpdateContainerTemplates());
        OrientationProperty.Changed.AddClassHandler<VelaDragList>((list, _) => list.SyncPanelProperties());
        SpacingProperty.Changed.AddClassHandler<VelaDragList>((list, _) => list.SyncPanelProperties());
    }

    public VelaDragList()
    {
        PseudoClasses.Set(":empty", true);
        AddHandler(PointerPressedEvent, OnItemPointerPressed, RoutingStrategies.Tunnel);
        AddHandler(PointerMovedEvent, OnItemPointerMoved, RoutingStrategies.Tunnel);
        AddHandler(PointerReleasedEvent, OnItemPointerReleased, RoutingStrategies.Tunnel);
        AddHandler(PointerCaptureLostEvent, OnItemPointerCaptureLost, RoutingStrategies.Bubble);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_itemsPanel is not null)
            _itemsPanel.LayoutUpdated -= OnItemsPanelLayoutUpdated;

        _itemsPanel = e.NameScope.Find<StackPanel>("PART_ItemsPanel");
        if (_itemsPanel is not null)
        {
            _itemsPanel.LayoutUpdated += OnItemsPanelLayoutUpdated;
            SyncPanelProperties();
            RebuildContainers();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (TopLevel.GetTopLevel(this) is { } topLevel)
        {
            _coordinator = VelaDragCoordinator.GetFor(topLevel);
            _coordinator.Register(this);
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        ClearPendingPress();
        _coordinator?.Unregister(this);
        _coordinator = null;
        _flipSnapshot = null;
        IsDropTargetActive = false;
        IsDragging = false;
    }

    internal void SetDropTargetActive(bool active) => IsDropTargetActive = active;
    internal void SetDraggingState(bool dragging) => IsDragging = dragging;

    internal bool AcceptsPayload(VelaDragPayload payload)
    {
        if (!IsDragEnabled || !IsVisible || !IsEffectivelyVisible)
            return false;

        if (payload.SourceList != this)
        {
            if (string.IsNullOrEmpty(DragGroup) || !string.Equals(DragGroup, payload.DragGroup, StringComparison.Ordinal))
                return false;
        }

        if (CanAcceptDrop is { } predicate && !predicate(payload))
            return false;

        return true;
    }

    internal int IndexOfContainer(VelaDragItem container) => _containers.IndexOf(container);

    internal void CaptureFlipPositions()
    {
        if (_itemsPanel is null || VelaMotionOptions.ReducedMotion)
            return;

        _flipSnapshot ??= new Dictionary<VelaDragItem, Point>(_containers.Count);
        _flipSnapshot.Clear();

        foreach (var item in _containers)
        {
            if (!item.IsVisible)
                continue;

            _flipSnapshot[item] = new Point(
                item.Bounds.X + item.CurrentOffsetX,
                item.Bounds.Y + item.CurrentOffsetY);
        }
    }

    internal void InsertOrMovePlaceholder(Border placeholder, int logicalVisibleIndex)
    {
        if (_itemsPanel is null)
            return;

        CaptureFlipPositions();

        var panelChildren = _itemsPanel.Children;
        if (_activePlaceholder == placeholder && panelChildren.Contains(placeholder))
            panelChildren.Remove(placeholder);

        // Map logical visible index (among non-hidden VelaDragItems) to panel child index.
        var visibleSeen = 0;
        var insertChildIndex = panelChildren.Count;
        for (var i = 0; i < panelChildren.Count; i++)
        {
            if (panelChildren[i] is VelaDragItem item && item.IsVisible)
            {
                if (visibleSeen == logicalVisibleIndex)
                {
                    insertChildIndex = i;
                    break;
                }
                visibleSeen++;
            }
        }

        insertChildIndex = Math.Clamp(insertChildIndex, 0, panelChildren.Count);
        panelChildren.Insert(insertChildIndex, placeholder);
        _activePlaceholder = placeholder;
        UpdateHasItems();
    }

    internal void RemovePlaceholder(Border placeholder)
    {
        if (_itemsPanel is null)
            return;

        if (_itemsPanel.Children.Contains(placeholder))
        {
            CaptureFlipPositions();
            _itemsPanel.Children.Remove(placeholder);
        }

        if (_activePlaceholder == placeholder)
            _activePlaceholder = null;

        UpdateHasItems();
    }

    internal int ComputeInsertionIndex(Point pointerInTopLevel, TopLevel topLevel)
    {
        if (_itemsPanel is null)
            return 0;

        var isVertical = Orientation == Orientation.Vertical;
        var logicalIndex = 0;

        foreach (var item in _containers)
        {
            if (!item.IsVisible)
                continue;

            var origin = _itemsPanel.TranslatePoint(new Point(item.Bounds.X, item.Bounds.Y), topLevel);
            if (!origin.HasValue)
            {
                logicalIndex++;
                continue;
            }

            var midpoint = isVertical
                ? origin.Value.Y + item.Bounds.Height * 0.5
                : origin.Value.X + item.Bounds.Width * 0.5;

            var pointerCoord = isVertical ? pointerInTopLevel.Y : pointerInTopLevel.X;
            if (pointerCoord < midpoint)
                return logicalIndex;

            logicalIndex++;
        }

        return logicalIndex;
    }

    internal void RaiseDragStarted(VelaDragStartedEventArgs args) => DragStarted?.Invoke(this, args);
    internal void RaiseItemReordered(VelaDragMovedEventArgs args) => ItemReordered?.Invoke(this, args);
    internal void RaiseItemTransferred(VelaDragMovedEventArgs args) => ItemTransferred?.Invoke(this, args);
    internal void RaiseDragEnded(VelaDragEndedEventArgs args) => DragEnded?.Invoke(this, args);

    /// <summary>
    /// Programmatically reorders or transfers an item between two <see cref="VelaDragList"/> instances.
    /// Useful for keyboard accessibility and unit testing.
    /// </summary>
    public static bool CommitMove(
        VelaDragList sourceList,
        int sourceIndex,
        VelaDragList targetList,
        int targetIndex)
    {
        if (sourceIndex < 0 || sourceIndex >= sourceList._containers.Count)
            return false;

        var item = sourceList._containers[sourceIndex].ItemData;
        if (item is null)
            return false;

        if (sourceList == targetList && sourceIndex == targetIndex)
            return false;

        var args = new VelaDragMovedEventArgs(item, sourceList, sourceIndex, targetList, targetIndex);
        if (sourceList == targetList)
        {
            sourceList.RaiseItemReordered(args);
        }
        else
        {
            sourceList.RaiseItemTransferred(args);
            targetList.RaiseItemTransferred(args);
        }

        if (!args.Handled && sourceList.AutoMoveItems && targetList.AutoMoveItems)
        {
            if (sourceList == targetList)
            {
                if (sourceList.ItemsSource is IList list && sourceIndex < list.Count)
                {
                    list.RemoveAt(sourceIndex);
                    var clamped = Math.Clamp(targetIndex, 0, list.Count);
                    list.Insert(clamped, item);
                    if (list is not INotifyCollectionChanged)
                        sourceList.RebuildContainers();
                }
            }
            else if (sourceList.ItemsSource is IList srcList &&
                     targetList.ItemsSource is IList dstList &&
                     sourceIndex < srcList.Count)
            {
                srcList.RemoveAt(sourceIndex);
                var clamped = Math.Clamp(targetIndex, 0, dstList.Count);
                dstList.Insert(clamped, item);
                if (srcList is not INotifyCollectionChanged)
                    sourceList.RebuildContainers();
                if (dstList is not INotifyCollectionChanged)
                    targetList.RebuildContainers();
            }
        }

        return true;
    }

    private void OnItemsPanelLayoutUpdated(object? sender, EventArgs e)
    {
        if (_flipSnapshot is null || _flipSnapshot.Count == 0)
            return;

        foreach (var (item, oldVisualPos) in _flipSnapshot)
        {
            if (!item.IsVisible)
                continue;

            var dx = oldVisualPos.X - item.Bounds.X;
            var dy = oldVisualPos.Y - item.Bounds.Y;
            if (Math.Abs(dx) >= 0.5 || Math.Abs(dy) >= 0.5)
                item.AnimateFromOffset(dx, dy);
        }

        _flipSnapshot.Clear();
    }

    private void SyncPanelProperties()
    {
        if (_itemsPanel is null)
            return;

        _itemsPanel.Orientation = Orientation;
        _itemsPanel.Spacing = Spacing;
    }

    private void OnItemsSourceChanged(AvaloniaPropertyChangedEventArgs e)
    {
        if (_subscribedCollection is not null)
        {
            _subscribedCollection.CollectionChanged -= OnCollectionChanged;
            _subscribedCollection = null;
        }

        if (e.NewValue is INotifyCollectionChanged notify)
        {
            _subscribedCollection = notify;
            _subscribedCollection.CollectionChanged += OnCollectionChanged;
        }

        RebuildContainers();
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_itemsPanel is null)
            return;

        // If a drag is actively in flight involving this list, cancel gracefully or rebuild cleanly.
        if (_coordinator?.IsListInActiveDrag(this) == true)
        {
            RebuildContainers();
            return;
        }

        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add when e.NewItems is not null && e.NewStartingIndex >= 0:
            {
                CaptureFlipPositions();
                var insertAt = Math.Clamp(e.NewStartingIndex, 0, _containers.Count);
                foreach (var item in e.NewItems)
                {
                    if (item is null) continue;
                    var container = CreateContainer(item);
                    _containers.Insert(insertAt, container);
                    _itemsPanel.Children.Insert(insertAt, container);
                    insertAt++;
                }
                UpdateHasItems();
                break;
            }
            case NotifyCollectionChangedAction.Remove when e.OldItems is not null && e.OldStartingIndex >= 0:
            {
                CaptureFlipPositions();
                var removeAt = e.OldStartingIndex;
                for (var i = 0; i < e.OldItems.Count && removeAt < _containers.Count; i++)
                {
                    var container = _containers[removeAt];
                    container.ResetFlipOffset();
                    container.Owner = null;
                    _containers.RemoveAt(removeAt);
                    _itemsPanel.Children.Remove(container);
                }
                UpdateHasItems();
                break;
            }
            case NotifyCollectionChangedAction.Move when e.OldStartingIndex >= 0 && e.NewStartingIndex >= 0 &&
                                                         e.OldStartingIndex < _containers.Count:
            {
                CaptureFlipPositions();
                var container = _containers[e.OldStartingIndex];
                _containers.RemoveAt(e.OldStartingIndex);
                _itemsPanel.Children.Remove(container);

                var newIndex = Math.Clamp(e.NewStartingIndex, 0, _containers.Count);
                _containers.Insert(newIndex, container);
                _itemsPanel.Children.Insert(newIndex, container);
                break;
            }
            default:
                RebuildContainers();
                break;
        }
    }

    private void RebuildContainers()
    {
        if (_itemsPanel is null)
            return;

        foreach (var existing in _containers)
        {
            existing.ResetFlipOffset();
            existing.Owner = null;
        }
        _containers.Clear();
        _itemsPanel.Children.Clear();

        if (ItemsSource is not null)
        {
            foreach (var item in ItemsSource)
            {
                if (item is null) continue;
                var container = CreateContainer(item);
                _containers.Add(container);
                _itemsPanel.Children.Add(container);
            }
        }

        if (_activePlaceholder is not null)
            _itemsPanel.Children.Add(_activePlaceholder);

        UpdateHasItems();
    }

    private VelaDragItem CreateContainer(object item)
    {
        if (item is VelaDragItem existing)
        {
            existing.Owner = this;
            existing.ItemData = item;
            return existing;
        }

        var container = new VelaDragItem
        {
            Owner = this,
            ItemData = item,
            Content = item,
            ContentTemplate = ItemTemplate,
        };
        return container;
    }

    private void UpdateContainerTemplates()
    {
        var template = ItemTemplate;
        foreach (var container in _containers)
        {
            if (container.ItemData is not VelaDragItem)
                container.ContentTemplate = template;
        }
    }

    private void UpdateHasItems()
    {
        HasItems = _containers.Count > 0 || _activePlaceholder is not null;
    }

    private void OnItemPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!IsDragEnabled || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        if (_coordinator?.IsDragging == true)
            return;

        var sourceVisual = e.Source as Visual;
        var container = FindOwningContainer(sourceVisual);
        if (container is null || !VelaDragDrop.GetIsDraggable(container))
            return;

        if (!CanInitiateDragFrom(container, sourceVisual))
            return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null)
            return;

        _pressedContainer = container;
        _pressedPointer = e.Pointer;
        _pressPointInTopLevel = e.GetPosition(topLevel);
        _pressOffsetInItem = e.GetPosition(container);
    }

    private void OnItemPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_coordinator is null)
            return;

        if (_coordinator.IsActiveSource(this))
        {
            _coordinator.HandlePointerMoved(e);
            e.Handled = true;
            return;
        }

        if (_pressedContainer is null || _pressedPointer != e.Pointer)
            return;

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            ClearPendingPress();
            return;
        }

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null)
        {
            ClearPendingPress();
            return;
        }

        var current = e.GetPosition(topLevel);
        var dx = current.X - _pressPointInTopLevel.X;
        var dy = current.Y - _pressPointInTopLevel.Y;
        var threshold = Math.Max(1.0, DragThreshold);
        if (dx * dx + dy * dy < threshold * threshold)
            return;

        var container = _pressedContainer;
        var offsetInItem = _pressOffsetInItem;
        ClearPendingPress();

        if (_coordinator.TryStartDrag(this, container, e.Pointer, current, offsetInItem))
            e.Handled = true;
    }

    private void OnItemPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_coordinator?.IsActiveSource(this) == true)
        {
            _coordinator.HandlePointerReleased(e);
            e.Handled = true;
            return;
        }

        if (_pressedContainer is { } container && _pressedPointer == e.Pointer)
        {
            var index = _containers.IndexOf(container);
            var itemData = container.ItemData;
            ClearPendingPress();

            if (index >= 0 && itemData is not null)
                ItemClicked?.Invoke(this, new VelaDragItemClickedEventArgs(itemData, index));
        }
    }

    private void OnItemPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        ClearPendingPress();
        if (_coordinator?.IsActiveSource(this) == true)
            _coordinator.CancelDrag();
    }

    private void ClearPendingPress()
    {
        _pressedContainer = null;
        _pressedPointer = null;
    }

    private VelaDragItem? FindOwningContainer(Visual? visual)
    {
        var current = visual;
        while (current is not null && current != this)
        {
            if (current is VelaDragItem item && item.Owner == this)
                return item;
            current = current.GetVisualParent();
        }
        return null;
    }

    private static bool CanInitiateDragFrom(VelaDragItem container, Visual? sourceVisual)
    {
        if (sourceVisual is null)
            return true;

        var hasExplicitHandles = HasDescendantDragHandle(container);
        var current = sourceVisual;
        while (current is not null)
        {
            if (VelaDragDrop.GetIsDragHandle(current))
                return true;

            if (!hasExplicitHandles && current != container && IsInteractiveControl(current))
                return false;

            if (current == container)
                break;

            current = current.GetVisualParent();
        }

        return !hasExplicitHandles;
    }

    private static bool HasDescendantDragHandle(Visual root)
    {
        foreach (var child in root.GetVisualChildren())
        {
            if (VelaDragDrop.GetIsDragHandle(child) || HasDescendantDragHandle(child))
                return true;
        }
        return false;
    }

    private static bool IsInteractiveControl(Visual visual) =>
        visual is TextBox
            or Button
            or RepeatButton
            or ToggleButton
            or Slider
            or ScrollBar
            or ComboBox
            or NumericUpDown
            or VelaInput
            or VelaSwitch
            or VelaCheckBox
            or VelaSelect
            or VelaComboBox
            or VelaElasticSlider
            or VelaRulerSlider
            or VelaScrubInput;
}

/// <summary>
/// Per-<see cref="TopLevel"/> drag coordinator implementing Plan A:
/// Pointer Capture + <see cref="OverlayLayer"/> floating ghost + spring physics + auto-scroll.
/// </summary>
internal sealed class VelaDragCoordinator
{
    private static readonly ConditionalWeakTable<TopLevel, VelaDragCoordinator> Coordinators = new();

    private readonly WeakReference<TopLevel> _topLevelRef;
    private readonly List<VelaDragList> _lists = new();

    private VelaDragList? _sourceList;
    private VelaDragItem? _sourceContainer;
    private object? _draggedItem;
    private int _sourceIndex = -1;
    private IPointer? _capturedPointer;

    private VelaDragList? _targetList;
    private int _targetIndex = -1;

    private Panel? _overlayLayer;
    private Border? _ghostHost;
    private Border? _placeholder;

    private ScaleTransform? _ghostScaleTransform;
    private RotateTransform? _ghostRotateTransform;
    private TranslateTransform? _ghostTranslateTransform;

    private readonly SpringChannel _ghostX = new(0.0, VelaMotionTokens.Springs.Responsive);
    private readonly SpringChannel _ghostY = new(0.0, VelaMotionTokens.Springs.Responsive);
    private readonly SpringChannel _ghostScale = new(1.0, VelaMotionTokens.Springs.Snappy);
    private readonly SpringChannel _ghostTilt = new(0.0, VelaMotionTokens.Springs.Gentle);
    private SpringRunner? _springRunner;

    private Point _pointerOffsetInItem;
    private Point _lastPointerInTopLevel;
    private double _smoothedVelocityX;
    private bool _isDragging;
    private bool _isSettling;
    private bool _pendingCancel;

    public bool IsDragging => _isDragging;

    private VelaDragCoordinator(TopLevel topLevel)
    {
        _topLevelRef = new WeakReference<TopLevel>(topLevel);
    }

    public static VelaDragCoordinator GetFor(TopLevel topLevel) =>
        Coordinators.GetValue(topLevel, static tl => new VelaDragCoordinator(tl));

    public void Register(VelaDragList list)
    {
        if (!_lists.Contains(list))
            _lists.Add(list);
    }

    public void Unregister(VelaDragList list)
    {
        _lists.Remove(list);
        if (_sourceList == list || _targetList == list)
            ForceCleanup(canceled: true);
    }

    public bool IsActiveSource(VelaDragList list) => _isDragging && !_isSettling && _sourceList == list;
    public bool IsListInActiveDrag(VelaDragList list) => _isDragging && (_sourceList == list || _targetList == list);

    public bool TryStartDrag(
        VelaDragList sourceList,
        VelaDragItem container,
        IPointer pointer,
        Point pointerInTopLevel,
        Point offsetInItem)
    {
        if (_isDragging || !_topLevelRef.TryGetTarget(out var topLevel))
            return false;

        var sourceIndex = sourceList.IndexOfContainer(container);
        if (sourceIndex < 0 || container.ItemData is null)
            return false;

        var overlay = ResolveFloatingLayer(sourceList, topLevel);
        if (overlay is null)
            return false;

        var itemTopLeftInOverlay = container.TranslatePoint(new Point(0, 0), overlay);
        if (!itemTopLeftInOverlay.HasValue)
            return false;

        _isDragging = true;
        _isSettling = false;
        _pendingCancel = false;
        _sourceList = sourceList;
        _sourceContainer = container;
        _draggedItem = container.ItemData;
        _sourceIndex = sourceIndex;
        _targetList = sourceList;
        _targetIndex = sourceIndex;
        _capturedPointer = pointer;
        _overlayLayer = overlay;
        _pointerOffsetInItem = offsetInItem;
        _lastPointerInTopLevel = pointerInTopLevel;
        _smoothedVelocityX = 0.0;

        // Capture pointer on sourceList so moves/releases stay routed even outside window bounds.
        pointer.Capture(sourceList);
        topLevel.AddHandler(InputElement.KeyDownEvent, OnTopLevelKeyDown, RoutingStrategies.Tunnel);

        var width = Math.Max(1.0, container.Bounds.Width);
        var height = Math.Max(1.0, container.Bounds.Height);

        // Build floating ghost in OverlayLayer.
        _ghostScaleTransform = new ScaleTransform(1.0, 1.0);
        _ghostRotateTransform = new RotateTransform(0.0);
        _ghostTranslateTransform = new TranslateTransform(itemTopLeftInOverlay.Value.X, itemTopLeftInOverlay.Value.Y);

        var transformGroup = new TransformGroup();
        transformGroup.Children.Add(_ghostScaleTransform);
        transformGroup.Children.Add(_ghostRotateTransform);
        transformGroup.Children.Add(_ghostTranslateTransform);

        _ghostHost = CreateGhostHost(sourceList, container, width, height, transformGroup);
        if (overlay is AdornerLayer)
            AdornerLayer.SetIsClipEnabled(_ghostHost, false);
        overlay.Children.Add(_ghostHost);

        // Build matching placeholder slot in sourceList and hide source container.
        _placeholder = CreatePlaceholder(sourceList, width, height);
        container.ResetFlipOffset();
        container.SetDraggingVisualState(true);
        container.IsVisible = false;

        sourceList.SetDraggingState(true);
        sourceList.SetDropTargetActive(true);
        sourceList.InsertOrMovePlaceholder(_placeholder, sourceIndex);

        // Initialize springs and lift effect.
        _ghostX.Jump(itemTopLeftInOverlay.Value.X);
        _ghostY.Jump(itemTopLeftInOverlay.Value.Y);
        _ghostScale.Jump(1.0);
        _ghostTilt.Jump(0.0);

        if (topLevel.TranslatePoint(pointerInTopLevel, overlay) is { } pointerInOverlay)
        {
            var startX = pointerInOverlay.X - offsetInItem.X;
            var startY = pointerInOverlay.Y - offsetInItem.Y;
            _ghostX.Jump(startX);
            _ghostY.Jump(startY);
            _ghostTranslateTransform.X = startX;
            _ghostTranslateTransform.Y = startY;
        }

        _ghostScale.SetTarget(VelaMotionOptions.ReducedMotion ? 1.0 : 1.03, VelaMotionTokens.Springs.Snappy);

        _springRunner = new SpringRunner(overlay, OnFrameTick);
        _springRunner.Wake();

        sourceList.RaiseDragStarted(new VelaDragStartedEventArgs(_draggedItem, sourceList, sourceIndex));
        return true;
    }

    public void HandlePointerMoved(PointerEventArgs e)
    {
        if (!_isDragging || _isSettling || _overlayLayer is null || !_topLevelRef.TryGetTarget(out var topLevel))
            return;

        var pointerInTopLevel = e.GetPosition(topLevel);
        var pointerInOverlay = e.GetPosition(_overlayLayer);

        var dx = pointerInTopLevel.X - _lastPointerInTopLevel.X;
        _smoothedVelocityX = _smoothedVelocityX * 0.72 + dx * 0.28;
        _lastPointerInTopLevel = pointerInTopLevel;

        var desiredX = pointerInOverlay.X - _pointerOffsetInItem.X;
        var desiredY = pointerInOverlay.Y - _pointerOffsetInItem.Y;

        // 1:1 responsive cursor tracking on X/Y, with subtle velocity-driven tilt.
        _ghostX.Jump(desiredX);
        _ghostY.Jump(desiredY);
        if (_ghostTranslateTransform is not null)
        {
            _ghostTranslateTransform.X = desiredX;
            _ghostTranslateTransform.Y = desiredY;
        }

        if (!VelaMotionOptions.ReducedMotion)
        {
            var tilt = Math.Clamp(_smoothedVelocityX * 0.28, -3.5, 3.5);
            _ghostTilt.SetTarget(tilt, VelaMotionTokens.Springs.Gentle);
        }

        UpdateDropTarget(pointerInTopLevel, topLevel);
        _springRunner?.Wake();
    }

    public void HandlePointerReleased(PointerReleasedEventArgs e)
    {
        if (!_isDragging || _isSettling)
            return;

        BeginSettle(canceled: false);
    }

    public void CancelDrag()
    {
        if (!_isDragging || _isSettling)
            return;

        if (_sourceList is not null && _placeholder is not null)
        {
            if (_targetList is not null && _targetList != _sourceList)
            {
                _targetList.SetDropTargetActive(false);
                _targetList.RemovePlaceholder(_placeholder);
            }

            _targetList = _sourceList;
            _targetIndex = _sourceIndex;
            _sourceList.SetDropTargetActive(true);
            _sourceList.InsertOrMovePlaceholder(_placeholder, _sourceIndex);
        }

        BeginSettle(canceled: true);
    }

    private void BeginSettle(bool canceled)
    {
        _isSettling = true;
        _pendingCancel = canceled;

        if (_capturedPointer is not null)
        {
            _capturedPointer.Capture(null);
            _capturedPointer = null;
        }

        if (_topLevelRef.TryGetTarget(out var topLevel))
            topLevel.RemoveHandler(InputElement.KeyDownEvent, OnTopLevelKeyDown);

        if (VelaMotionOptions.ReducedMotion ||
            _overlayLayer is null ||
            _placeholder is null ||
            _placeholder.TranslatePoint(new Point(0, 0), _overlayLayer) is not { } targetPos)
        {
            CompleteAndCleanup(canceled);
            return;
        }

        _ghostX.SetTarget(targetPos.X, VelaMotionTokens.Springs.Snappy);
        _ghostY.SetTarget(targetPos.Y, VelaMotionTokens.Springs.Snappy);
        _ghostScale.SetTarget(1.0, VelaMotionTokens.Springs.Snappy);
        _ghostTilt.SetTarget(0.0, VelaMotionTokens.Springs.Snappy);
        _springRunner?.Wake();
    }

    private bool OnFrameTick(double dt)
    {
        if (!_isDragging)
            return false;

        if (!_isSettling)
        {
            // Decay velocity tilt back toward 0 when pointer pauses.
            _smoothedVelocityX *= Math.Pow(0.08, dt);
            if (!VelaMotionOptions.ReducedMotion && Math.Abs(_smoothedVelocityX) < 0.08)
                _ghostTilt.SetTarget(0.0, VelaMotionTokens.Springs.Gentle);

            var scrolled = TryAutoScroll(dt);
            if (scrolled && _topLevelRef.TryGetTarget(out var topLevel))
                UpdateDropTarget(_lastPointerInTopLevel, topLevel);

            var sAnim = _ghostScale.Step(dt);
            var tAnim = _ghostTilt.Step(dt);

            if (_ghostScaleTransform is not null)
            {
                _ghostScaleTransform.ScaleX = _ghostScale.Value;
                _ghostScaleTransform.ScaleY = _ghostScale.Value;
            }
            if (_ghostRotateTransform is not null)
                _ghostRotateTransform.Angle = _ghostTilt.Value;

            return sAnim || tAnim || scrolled;
        }
        else
        {
            var xAnim = _ghostX.Step(dt);
            var yAnim = _ghostY.Step(dt);
            var sAnim = _ghostScale.Step(dt);
            var tAnim = _ghostTilt.Step(dt);

            if (_ghostTranslateTransform is not null)
            {
                _ghostTranslateTransform.X = _ghostX.Value;
                _ghostTranslateTransform.Y = _ghostY.Value;
            }
            if (_ghostScaleTransform is not null)
            {
                _ghostScaleTransform.ScaleX = _ghostScale.Value;
                _ghostScaleTransform.ScaleY = _ghostScale.Value;
            }
            if (_ghostRotateTransform is not null)
                _ghostRotateTransform.Angle = _ghostTilt.Value;

            if (!xAnim && !yAnim && !sAnim && !tAnim)
            {
                CompleteAndCleanup(_pendingCancel);
                return false;
            }

            return true;
        }
    }

    private void UpdateDropTarget(Point pointerInTopLevel, TopLevel topLevel)
    {
        if (_sourceList is null || _draggedItem is null || _placeholder is null)
            return;

        var payload = new VelaDragPayload(_draggedItem, _sourceList, _sourceIndex, _sourceList.DragGroup);
        var matchedList = FindTargetList(pointerInTopLevel, topLevel, payload);
        if (matchedList is null)
            return;

        var newIndex = matchedList.ComputeInsertionIndex(pointerInTopLevel, topLevel);
        if (matchedList == _targetList && newIndex == _targetIndex)
            return;

        if (_targetList is not null && _targetList != matchedList)
        {
            _targetList.SetDropTargetActive(false);
            _targetList.RemovePlaceholder(_placeholder);
        }

        _targetList = matchedList;
        _targetIndex = newIndex;
        _targetList.SetDropTargetActive(true);
        _targetList.InsertOrMovePlaceholder(_placeholder, newIndex);
    }

    private VelaDragList? FindTargetList(Point pointerInTopLevel, TopLevel topLevel, VelaDragPayload payload)
    {
        const double hitPad = 6.0;
        for (var i = _lists.Count - 1; i >= 0; i--)
        {
            var candidate = _lists[i];
            if (!candidate.AcceptsPayload(payload))
                continue;

            var bounds = candidate.Bounds;
            if (bounds.Width <= 0 || bounds.Height <= 0)
                continue;

            var topLeft = candidate.TranslatePoint(new Point(0, 0), topLevel);
            if (!topLeft.HasValue)
                continue;

            var rect = new Rect(
                topLeft.Value.X - hitPad,
                topLeft.Value.Y - hitPad,
                bounds.Width + hitPad * 2.0,
                bounds.Height + hitPad * 2.0);

            if (rect.Contains(pointerInTopLevel))
                return candidate;
        }

        return _targetList;
    }

    private bool TryAutoScroll(double dt)
    {
        if (_targetList is null || !_topLevelRef.TryGetTarget(out var topLevel))
            return false;

        const double edgeZone = 44.0;
        const double maxSpeedPxPerSec = 520.0;
        var scrolledAny = false;

        Visual? current = _targetList;
        while (current is not null)
        {
            if (current is ScrollViewer sv && sv.Bounds.Width > 0 && sv.Bounds.Height > 0)
            {
                var svOrigin = sv.TranslatePoint(new Point(0, 0), topLevel);
                if (svOrigin.HasValue)
                {
                    var rect = new Rect(svOrigin.Value, sv.Bounds.Size);
                    if (rect.Contains(_lastPointerInTopLevel))
                    {
                        var dx = 0.0;
                        var dy = 0.0;

                        if (sv.Extent.Height > sv.Viewport.Height + 1.0)
                        {
                            var distTop = _lastPointerInTopLevel.Y - rect.Top;
                            var distBottom = rect.Bottom - _lastPointerInTopLevel.Y;
                            if (distTop < edgeZone)
                                dy = -((edgeZone - distTop) / edgeZone) * maxSpeedPxPerSec * dt;
                            else if (distBottom < edgeZone)
                                dy = ((edgeZone - distBottom) / edgeZone) * maxSpeedPxPerSec * dt;
                        }

                        if (sv.Extent.Width > sv.Viewport.Width + 1.0)
                        {
                            var distLeft = _lastPointerInTopLevel.X - rect.Left;
                            var distRight = rect.Right - _lastPointerInTopLevel.X;
                            if (distLeft < edgeZone)
                                dx = -((edgeZone - distLeft) / edgeZone) * maxSpeedPxPerSec * dt;
                            else if (distRight < edgeZone)
                                dx = ((edgeZone - distRight) / edgeZone) * maxSpeedPxPerSec * dt;
                        }

                        if (Math.Abs(dx) > 0.01 || Math.Abs(dy) > 0.01)
                        {
                            var nextX = Math.Clamp(sv.Offset.X + dx, 0.0, Math.Max(0.0, sv.Extent.Width - sv.Viewport.Width));
                            var nextY = Math.Clamp(sv.Offset.Y + dy, 0.0, Math.Max(0.0, sv.Extent.Height - sv.Viewport.Height));
                            if (Math.Abs(nextX - sv.Offset.X) > 0.01 || Math.Abs(nextY - sv.Offset.Y) > 0.01)
                            {
                                sv.Offset = new Vector(nextX, nextY);
                                scrolledAny = true;
                            }
                        }
                    }
                }
            }

            current = current.GetVisualParent();
        }

        return scrolledAny;
    }

    private void CompleteAndCleanup(bool canceled)
    {
        var sourceList = _sourceList;
        var targetList = _targetList;
        var sourceIndex = _sourceIndex;
        var targetIndex = _targetIndex;
        var draggedItem = _draggedItem;

        ForceCleanup(canceled);

        if (sourceList is null || draggedItem is null)
            return;

        if (!canceled && targetList is not null)
            VelaDragList.CommitMove(sourceList, sourceIndex, targetList, targetIndex);

        sourceList.RaiseDragEnded(new VelaDragEndedEventArgs(
            draggedItem,
            sourceList,
            sourceIndex,
            canceled ? null : targetList,
            canceled ? sourceIndex : targetIndex,
            canceled));
    }

    private void ForceCleanup(bool canceled)
    {
        _springRunner?.Stop();
        _springRunner = null;

        if (_capturedPointer is not null)
        {
            _capturedPointer.Capture(null);
            _capturedPointer = null;
        }

        if (_topLevelRef.TryGetTarget(out var topLevel))
            topLevel.RemoveHandler(InputElement.KeyDownEvent, OnTopLevelKeyDown);

        if (_overlayLayer is not null && _ghostHost is not null)
            _overlayLayer.Children.Remove(_ghostHost);

        if (_placeholder is not null)
        {
            _targetList?.RemovePlaceholder(_placeholder);
            _sourceList?.RemovePlaceholder(_placeholder);
        }

        if (_sourceContainer is not null)
        {
            _sourceContainer.SetDraggingVisualState(false);
            _sourceContainer.IsVisible = true;
            _sourceContainer.ResetFlipOffset();
        }

        _sourceList?.SetDraggingState(false);
        _sourceList?.SetDropTargetActive(false);
        _targetList?.SetDropTargetActive(false);

        _overlayLayer = null;
        _ghostHost = null;
        _placeholder = null;
        _ghostScaleTransform = null;
        _ghostRotateTransform = null;
        _ghostTranslateTransform = null;
        _sourceList = null;
        _sourceContainer = null;
        _draggedItem = null;
        _targetList = null;
        _sourceIndex = -1;
        _targetIndex = -1;
        _isDragging = false;
        _isSettling = false;
        _pendingCancel = false;
    }

    private void OnTopLevelKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _isDragging && !_isSettling)
        {
            e.Handled = true;
            CancelDrag();
        }
    }

    private static Panel? ResolveFloatingLayer(Visual anchor, TopLevel topLevel) =>
        (Panel?)OverlayLayer.GetOverlayLayer(anchor)
        ?? AdornerLayer.GetAdornerLayer(anchor)
        ?? (Panel?)OverlayLayer.GetOverlayLayer(topLevel)
        ?? AdornerLayer.GetAdornerLayer(topLevel);

    private static Border CreateGhostHost(
        VelaDragList sourceList,
        VelaDragItem container,
        double width,
        double height,
        Transform transform)
    {
        Control ghostChild;
        var previewTemplate = sourceList.DragPreviewTemplate ?? sourceList.ItemTemplate;

        if (container.ItemData is Visual && previewTemplate is null)
        {
            ghostChild = new Border
            {
                Width = width,
                Height = height,
                CornerRadius = container.CornerRadius,
                Background = new VisualBrush(container)
                {
                    Stretch = Stretch.None,
                    AlignmentX = AlignmentX.Left,
                    AlignmentY = AlignmentY.Top,
                },
            };
        }
        else
        {
            ghostChild = new ContentPresenter
            {
                Content = container.ItemData,
                ContentTemplate = previewTemplate,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
            };
        }

        var host = new Border
        {
            Width = width,
            Height = height,
            CornerRadius = sourceList.PlaceholderCornerRadius,
            IsHitTestVisible = false,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            RenderTransformOrigin = RelativePoint.Center,
            RenderTransform = transform,
            Opacity = 0.98,
            Child = ghostChild,
        };

        host.Classes.Add("velaDragGhost");
        if (sourceList.TryFindResource("VelaShadowFloating", sourceList.ActualThemeVariant, out var shadowObj) &&
            shadowObj is BoxShadows shadows)
        {
            host.BoxShadow = shadows;
        }

        return host;
    }

    private static Border CreatePlaceholder(VelaDragList sourceList, double width, double height)
    {
        var placeholder = new Border
        {
            Width = sourceList.Orientation == Orientation.Horizontal ? width : double.NaN,
            Height = height,
            MinHeight = Math.Max(32.0, height),
            CornerRadius = sourceList.PlaceholderCornerRadius,
            BorderThickness = new Thickness(1.5),
            IsHitTestVisible = false,
        };

        placeholder.Classes.Add("velaDragPlaceholder");

        if (sourceList.TryFindResource("VelaAccentSubtleBrush", sourceList.ActualThemeVariant, out var bg) &&
            bg is IBrush bgBrush)
        {
            placeholder.Background = bgBrush;
        }
        else if (sourceList.TryFindResource("VelaSurfaceMutedBrush", sourceList.ActualThemeVariant, out var fallbackBg) &&
                 fallbackBg is IBrush fallbackBgBrush)
        {
            placeholder.Background = fallbackBgBrush;
        }

        if (sourceList.TryFindResource("VelaAccentBrush", sourceList.ActualThemeVariant, out var border) &&
            border is IBrush borderBrush)
        {
            placeholder.BorderBrush = borderBrush;
        }
        else if (sourceList.TryFindResource("VelaBorderStrongBrush", sourceList.ActualThemeVariant, out var fallbackBorder) &&
                 fallbackBorder is IBrush fallbackBorderBrush)
        {
            placeholder.BorderBrush = fallbackBorderBrush;
        }

        return placeholder;
    }
}
