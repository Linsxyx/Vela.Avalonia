using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Metadata;
using Vela.Avalonia.Motion;

namespace Vela.Avalonia.Controls;

/// <summary>
/// Declarative pane inside <see cref="VelaResizablePanels"/>.
/// </summary>
public class VelaResizablePane : AvaloniaObject
{
    public static readonly StyledProperty<string?> HeaderProperty =
        AvaloniaProperty.Register<VelaResizablePane, string?>(nameof(Header));

    public static readonly StyledProperty<object?> ContentProperty =
        AvaloniaProperty.Register<VelaResizablePane, object?>(nameof(Content));

    public static readonly StyledProperty<double> DefaultShareProperty =
        AvaloniaProperty.Register<VelaResizablePane, double>(nameof(DefaultShare), 1.0);

    public static readonly StyledProperty<double> MinSizeProperty =
        AvaloniaProperty.Register<VelaResizablePane, double>(nameof(MinSize), 96.0);

    public static readonly StyledProperty<double> MaxSizeProperty =
        AvaloniaProperty.Register<VelaResizablePane, double>(nameof(MaxSize), double.PositiveInfinity);

    public static readonly StyledProperty<bool> IsCollapsibleProperty =
        AvaloniaProperty.Register<VelaResizablePane, bool>(nameof(IsCollapsible));

    public static readonly StyledProperty<bool> IsCollapsedProperty =
        AvaloniaProperty.Register<VelaResizablePane, bool>(
            nameof(IsCollapsed),
            false,
            defaultBindingMode: BindingMode.TwoWay);

    public string? Header { get => GetValue(HeaderProperty); set => SetValue(HeaderProperty, value); }

    [Content]
    public object? Content { get => GetValue(ContentProperty); set => SetValue(ContentProperty, value); }

    public double DefaultShare { get => GetValue(DefaultShareProperty); set => SetValue(DefaultShareProperty, value); }
    public double MinSize { get => GetValue(MinSizeProperty); set => SetValue(MinSizeProperty, value); }
    public double MaxSize { get => GetValue(MaxSizeProperty); set => SetValue(MaxSizeProperty, value); }
    public bool IsCollapsible { get => GetValue(IsCollapsibleProperty); set => SetValue(IsCollapsibleProperty, value); }
    public bool IsCollapsed { get => GetValue(IsCollapsedProperty); set => SetValue(IsCollapsedProperty, value); }
}

/// <summary>
/// Port of Arc UI's ResizablePanels:
/// - Horizontal or vertical split pane container with 1:1 divider dragging
/// - Elastic rubber-band resistance past MinSize / MaxSize limits
/// - Optional collapsible panes that snap smoothly closed with a clickable restore pill on the divider
/// - Double-clicking any divider smoothly springs all panes back to their DefaultShare proportions
/// </summary>
public class VelaResizablePanels : TemplatedControl
{
    public static readonly StyledProperty<Orientation> OrientationProperty =
        AvaloniaProperty.Register<VelaResizablePanels, Orientation>(nameof(Orientation), Orientation.Horizontal);

    public Orientation Orientation { get => GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }

    [Content]
    public ObservableCollection<VelaResizablePane> Panes { get; } = new();

    public event EventHandler<IReadOnlyList<double>>? LayoutChanged;

    protected override Type StyleKeyOverride => typeof(VelaResizablePanels);

    private VelaResizableLayoutPanel? _layoutPanel;

    static VelaResizablePanels()
    {
        OrientationProperty.Changed.AddClassHandler<VelaResizablePanels>((p, _) => p.Rebuild());
    }

    public VelaResizablePanels()
    {
        Panes.CollectionChanged += OnPanesChanged;
    }

    public void ResetLayout() => _layoutPanel?.ResetLayout();

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _layoutPanel = e.NameScope.Find<VelaResizableLayoutPanel>("PART_LayoutPanel");
        Rebuild();
    }

    private void OnPanesChanged(object? sender, NotifyCollectionChangedEventArgs e) => Rebuild();

    private void Rebuild()
    {
        if (_layoutPanel is null)
            return;

        _layoutPanel.Configure(this, Orientation, Panes);
    }

    internal void RaiseLayoutChanged(IReadOnlyList<double> shares) => LayoutChanged?.Invoke(this, shares);
}

/// <summary>
/// Custom physics-driven split layout panel hosted inside <see cref="VelaResizablePanels"/>.
/// </summary>
public sealed class VelaResizableLayoutPanel : Panel
{
    private const double HandleHitSize = 10.0;
    private const double RubberStretch = 40.0;

    private sealed class PaneHost
    {
        public required VelaResizablePane Model;
        public required Border Container;
        public required ContentPresenter Presenter;
        public readonly SpringChannel SizeSpring = new(0.0, VelaMotionTokens.Springs.Smooth);
        public double RestoreSize;
        public bool Collapsed;
    }

    private sealed class DividerHost
    {
        public required int HandleIndex;
        public required Border HitArea;
        public required Border Hairline;
        public required Border GripPill;
        public required Border RestoreBadge;
        public required PathIcon RestoreChevron;
    }

    private VelaResizablePanels? _owner;
    private Orientation _orientation = Orientation.Horizontal;
    private readonly List<PaneHost> _panes = new();
    private readonly List<DividerHost> _dividers = new();
    private readonly SpringRunner _runner;

    private double _lastAvailableMain;
    private bool _sizesInitialized;

    private int _dragHandle = -1;
    private double _dragStartCoord;
    private double _dragA0;
    private double _dragB0;
    private bool _dragMoved;
    private long _lastHandleClickTicks;
    private int _lastHandleClickIndex = -1;

    public VelaResizableLayoutPanel()
    {
        ClipToBounds = true;
        _runner = new SpringRunner(this, OnTick);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _runner.Stop();
    }

    internal void Configure(VelaResizablePanels owner, Orientation orientation, IList<VelaResizablePane> panes)
    {
        _owner = owner;
        _orientation = orientation;
        _sizesInitialized = false;
        _lastAvailableMain = 0;
        _dragHandle = -1;

        foreach (var p in _panes)
            p.Presenter.Content = null;
        _panes.Clear();
        _dividers.Clear();
        Children.Clear();

        var isHoriz = orientation == Orientation.Horizontal;

        for (var i = 0; i < panes.Count; i++)
        {
            var model = panes[i];
            var presenter = new ContentPresenter
            {
                Content = model.Content,
            };
            var container = new Border
            {
                ClipToBounds = true,
                Child = presenter,
            };

            var host = new PaneHost
            {
                Model = model,
                Container = container,
                Presenter = presenter,
                Collapsed = model.IsCollapsible && model.IsCollapsed,
            };
            _panes.Add(host);
            Children.Add(container);

            if (i < panes.Count - 1)
            {
                var divider = CreateDivider(i, isHoriz);
                _dividers.Add(divider);
                Children.Add(divider.HitArea);
            }
        }

        InvalidateMeasure();
        InvalidateArrange();
    }

    private DividerHost CreateDivider(int handleIndex, bool isHoriz)
    {
        var hairline = new Border
        {
            Width = isHoriz ? 1 : double.NaN,
            Height = isHoriz ? double.NaN : 1,
            HorizontalAlignment = isHoriz ? HorizontalAlignment.Center : HorizontalAlignment.Stretch,
            VerticalAlignment = isHoriz ? VerticalAlignment.Stretch : VerticalAlignment.Center,
            [!Border.BackgroundProperty] = this.GetResourceObservable("VelaBorderBrush").ToBinding(),
        };

        var grip = new Border
        {
            Width = isHoriz ? 4 : 28,
            Height = isHoriz ? 28 : 4,
            CornerRadius = new CornerRadius(999),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = 0.55,
            [!Border.BackgroundProperty] = this.GetResourceObservable("VelaBorderStrongBrush").ToBinding(),
        };

        var chevron = new PathIcon
        {
            Width = 11,
            Height = 11,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            [!PathIcon.ForegroundProperty] = this.GetResourceObservable("VelaForegroundBrush").ToBinding(),
        };

        var restoreBadge = new Border
        {
            Width = 20,
            Height = 20,
            CornerRadius = new CornerRadius(999),
            BorderThickness = new Thickness(1),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            IsVisible = false,
            [!Border.BackgroundProperty] = this.GetResourceObservable("VelaSurfaceRaisedBrush").ToBinding(),
            [!Border.BorderBrushProperty] = this.GetResourceObservable("VelaBorderStrongBrush").ToBinding(),
            Child = chevron,
        };

        var stack = new Panel
        {
            Children = { hairline, grip, restoreBadge },
        };

        var hitArea = new Border
        {
            Background = Brushes.Transparent,
            Cursor = new Cursor(isHoriz ? StandardCursorType.SizeWestEast : StandardCursorType.SizeNorthSouth),
            ZIndex = 10,
            Child = stack,
        };

        var divider = new DividerHost
        {
            HandleIndex = handleIndex,
            HitArea = hitArea,
            Hairline = hairline,
            GripPill = grip,
            RestoreBadge = restoreBadge,
            RestoreChevron = chevron,
        };

        hitArea.PointerEntered += (_, _) => SetDividerVisualState(divider, hover: true, active: _dragHandle == handleIndex);
        hitArea.PointerExited += (_, _) => SetDividerVisualState(divider, hover: false, active: _dragHandle == handleIndex);
        hitArea.PointerPressed += (_, ev) => OnDividerPressed(divider, ev);
        hitArea.PointerMoved += (_, ev) => OnDividerMoved(divider, ev);
        hitArea.PointerReleased += (_, ev) => OnDividerReleased(divider, ev);
        hitArea.PointerCaptureLost += (_, _) => CancelDrag();

        return divider;
    }

    private void SetDividerVisualState(DividerHost d, bool hover, bool active)
    {
        var key = active ? "VelaAccentBrush" : hover ? "VelaTextSecondaryBrush" : "VelaBorderStrongBrush";
        if (this.TryFindResource(key, ActualThemeVariant, out var res) && res is IBrush b)
            d.GripPill.Background = b;
        d.GripPill.Opacity = active ? 1.0 : hover ? 0.92 : 0.55;
    }

    private void OnDividerPressed(DividerHost d, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(d.HitArea).Properties.IsLeftButtonPressed)
            return;

        var now = Environment.TickCount64;
        if (_lastHandleClickIndex == d.HandleIndex && now - _lastHandleClickTicks < 340)
        {
            _lastHandleClickTicks = 0;
            ResetLayout();
            e.Handled = true;
            return;
        }
        _lastHandleClickTicks = now;
        _lastHandleClickIndex = d.HandleIndex;

        _dragHandle = d.HandleIndex;
        _dragMoved = false;
        var pos = e.GetPosition(this);
        _dragStartCoord = _orientation == Orientation.Horizontal ? pos.X : pos.Y;
        _dragA0 = _panes[d.HandleIndex].SizeSpring.Value;
        _dragB0 = _panes[d.HandleIndex + 1].SizeSpring.Value;

        e.Pointer.Capture(d.HitArea);
        SetDividerVisualState(d, hover: true, active: true);
        e.Handled = true;
    }

    private void OnDividerMoved(DividerHost d, PointerEventArgs e)
    {
        if (_dragHandle != d.HandleIndex)
            return;

        var pos = e.GetPosition(this);
        var coord = _orientation == Orientation.Horizontal ? pos.X : pos.Y;
        var delta = coord - _dragStartCoord;

        if (!_dragMoved)
        {
            if (Math.Abs(delta) < 2.0) return;
            _dragMoved = true;
        }

        var paneA = _panes[d.HandleIndex];
        var paneB = _panes[d.HandleIndex + 1];
        var pair = _dragA0 + _dragB0;
        var minA = paneA.Model.MinSize;
        var minB = paneB.Model.MinSize;
        var lo = Math.Clamp(Math.Max(minA, pair - paneB.Model.MaxSize), 0, pair);
        var hi = Math.Clamp(Math.Min(paneA.Model.MaxSize, pair - minB), 0, pair);
        if (lo > hi) lo = hi = pair * 0.5;

        var rawA = _dragA0 + delta;
        double nextA;

        var collapseThreshA = minA - Math.Max(minA * 0.45, 34.0);
        var collapseThreshB = minB - Math.Max(minB * 0.45, 34.0);

        if (paneA.Model.IsCollapsible && rawA < collapseThreshA)
        {
            nextA = 0;
        }
        else if (paneB.Model.IsCollapsible && (pair - rawA) < collapseThreshB)
        {
            nextA = pair;
        }
        else
        {
            nextA = ElasticClamp(rawA, lo, hi);
        }

        paneA.SizeSpring.Jump(Math.Clamp(nextA, 0, pair));
        paneB.SizeSpring.Jump(Math.Clamp(pair - nextA, 0, pair));
        UpdatePaneOpacities();
        InvalidateArrange();
        e.Handled = true;
    }

    private void OnDividerReleased(DividerHost d, PointerReleasedEventArgs e)
    {
        if (_dragHandle != d.HandleIndex)
            return;

        e.Pointer.Capture(null);
        _dragHandle = -1;
        SetDividerVisualState(d, hover: d.HitArea.IsPointerOver, active: false);

        var paneA = _panes[d.HandleIndex];
        var paneB = _panes[d.HandleIndex + 1];
        var pair = _dragA0 + _dragB0;

        if (!_dragMoved)
        {
            // Tap on a collapsed divider restores the hidden pane!
            if (paneA.Collapsed)
            {
                var targetA = Math.Clamp(paneA.RestoreSize > 1 ? paneA.RestoreSize : paneA.Model.MinSize, paneA.Model.MinSize, Math.Max(paneA.Model.MinSize, pair - paneB.Model.MinSize));
                SetPaneCollapsed(paneA, false);
                AnimatePair(paneA, paneB, targetA, pair - targetA);
                UpdateDividerBadges();
            }
            else if (paneB.Collapsed)
            {
                var targetB = Math.Clamp(paneB.RestoreSize > 1 ? paneB.RestoreSize : paneB.Model.MinSize, paneB.Model.MinSize, Math.Max(paneB.Model.MinSize, pair - paneA.Model.MinSize));
                SetPaneCollapsed(paneB, false);
                AnimatePair(paneA, paneB, pair - targetB, targetB);
                UpdateDividerBadges();
            }
            e.Handled = true;
            return;
        }

        var pos = e.GetPosition(this);
        var coord = _orientation == Orientation.Horizontal ? pos.X : pos.Y;
        var rawA = _dragA0 + (coord - _dragStartCoord);
        var minA = paneA.Model.MinSize;
        var minB = paneB.Model.MinSize;
        var lo = Math.Clamp(Math.Max(minA, pair - paneB.Model.MaxSize), 0, pair);
        var hi = Math.Clamp(Math.Min(paneA.Model.MaxSize, pair - minB), 0, pair);
        if (lo > hi) lo = hi = pair * 0.5;

        var collapseThreshA = minA - Math.Max(minA * 0.45, 34.0);
        var collapseThreshB = minB - Math.Max(minB * 0.45, 34.0);

        double finalA;
        if (paneA.Model.IsCollapsible && rawA < collapseThreshA)
        {
            if (_dragA0 > 1) paneA.RestoreSize = _dragA0;
            SetPaneCollapsed(paneA, true);
            SetPaneCollapsed(paneB, false);
            finalA = 0;
        }
        else if (paneB.Model.IsCollapsible && (pair - rawA) < collapseThreshB)
        {
            if (_dragB0 > 1) paneB.RestoreSize = _dragB0;
            SetPaneCollapsed(paneA, false);
            SetPaneCollapsed(paneB, true);
            finalA = pair;
        }
        else
        {
            SetPaneCollapsed(paneA, false);
            SetPaneCollapsed(paneB, false);
            finalA = Math.Clamp(rawA, lo, hi);
        }

        AnimatePair(paneA, paneB, finalA, pair - finalA);
        UpdateDividerBadges();
        NotifyLayoutChanged();
        e.Handled = true;
    }

    private void CancelDrag()
    {
        if (_dragHandle < 0) return;
        var idx = _dragHandle;
        _dragHandle = -1;
        if (idx < _dividers.Count)
            SetDividerVisualState(_dividers[idx], hover: false, active: false);
        AnimatePair(_panes[idx], _panes[idx + 1], _dragA0, _dragB0);
    }

    private void SetPaneCollapsed(PaneHost pane, bool collapsed)
    {
        pane.Collapsed = collapsed;
        pane.Model.IsCollapsed = collapsed;
    }

    private void AnimatePair(PaneHost a, PaneHost b, double targetA, double targetB)
    {
        if (VelaMotionOptions.ReducedMotion)
        {
            a.SizeSpring.Jump(targetA);
            b.SizeSpring.Jump(targetB);
            UpdatePaneOpacities();
            InvalidateArrange();
            return;
        }

        a.SizeSpring.SetTarget(targetA, VelaMotionTokens.Springs.Smooth);
        b.SizeSpring.SetTarget(targetB, VelaMotionTokens.Springs.Smooth);
        _runner.Wake();
    }

    internal void ResetLayout()
    {
        if (_panes.Count == 0 || _lastAvailableMain <= 1)
            return;

        var totalShares = _panes.Sum(p => p.Model.IsCollapsible && p.Model.IsCollapsed ? 0.0 : Math.Max(0.05, p.Model.DefaultShare));
        if (totalShares <= 0) totalShares = _panes.Count;

        foreach (var pane in _panes)
        {
            SetPaneCollapsed(pane, false);
            var target = _lastAvailableMain * (Math.Max(0.05, pane.Model.DefaultShare) / totalShares);
            if (VelaMotionOptions.ReducedMotion)
                pane.SizeSpring.Jump(target);
            else
                pane.SizeSpring.SetTarget(target, VelaMotionTokens.Springs.Smooth);
        }

        UpdateDividerBadges();
        if (VelaMotionOptions.ReducedMotion)
        {
            UpdatePaneOpacities();
            InvalidateArrange();
        }
        else
        {
            _runner.Wake();
        }
        NotifyLayoutChanged();
    }

    private void UpdateDividerBadges()
    {
        var isHoriz = _orientation == Orientation.Horizontal;
        var rightGeom = ResolveGeometry("VelaIconChevronRight");
        var leftGeom = ResolveGeometry("VelaIconChevronLeft");
        var downGeom = ResolveGeometry("VelaIconChevronDown");

        foreach (var d in _dividers)
        {
            var leftCollapsed = _panes[d.HandleIndex].Collapsed;
            var rightCollapsed = _panes[d.HandleIndex + 1].Collapsed;
            var anyCollapsed = leftCollapsed || rightCollapsed;
            d.RestoreBadge.IsVisible = anyCollapsed;
            d.GripPill.IsVisible = !anyCollapsed;
            if (anyCollapsed)
            {
                d.RestoreChevron.Data = isHoriz
                    ? (leftCollapsed ? rightGeom : leftGeom)
                    : downGeom;
            }
        }
    }

    private void UpdatePaneOpacities()
    {
        foreach (var p in _panes)
        {
            var min = Math.Max(1.0, p.Model.MinSize);
            p.Presenter.Opacity = Math.Clamp(p.SizeSpring.Value / min, 0.0, 1.0);
        }
    }

    private void NotifyLayoutChanged()
    {
        if (_owner is null || _panes.Count == 0) return;
        var total = _panes.Sum(p => Math.Max(0, p.SizeSpring.Target));
        if (total <= 0.1) total = 1.0;
        var shares = _panes.Select(p => Math.Round(Math.Max(0, p.SizeSpring.Target) / total * 100.0, 1)).ToList();
        _owner.RaiseLayoutChanged(shares);
    }

    private static double ElasticClamp(double raw, double lo, double hi)
    {
        if (raw < lo)
        {
            var over = lo - raw;
            return lo - over * RubberStretch * 0.55 / (RubberStretch + 0.55 * over);
        }
        if (raw > hi)
        {
            var over = raw - hi;
            return hi + over * RubberStretch * 0.55 / (RubberStretch + 0.55 * over);
        }
        return raw;
    }

    private void EnsureSizesForAvailable(double availableMain)
    {
        if (_panes.Count == 0 || availableMain <= 1)
            return;

        if (!_sizesInitialized)
        {
            _sizesInitialized = true;
            _lastAvailableMain = availableMain;
            var sumShare = _panes.Sum(p => p.Collapsed ? 0.0 : Math.Max(0.05, p.Model.DefaultShare));
            if (sumShare <= 0) sumShare = _panes.Count;
            foreach (var p in _panes)
            {
                var s = p.Collapsed ? 0.0 : availableMain * (Math.Max(0.05, p.Model.DefaultShare) / sumShare);
                p.SizeSpring.Jump(s);
            }
            UpdatePaneOpacities();
            UpdateDividerBadges();
            return;
        }

        if (Math.Abs(availableMain - _lastAvailableMain) > 0.5 && _dragHandle < 0)
        {
            var curTotal = _panes.Sum(p => Math.Max(0.0, p.SizeSpring.Value));
            var scale = curTotal > 1.0 ? availableMain / curTotal : 1.0;
            _lastAvailableMain = availableMain;
            foreach (var p in _panes)
                p.SizeSpring.Jump(Math.Max(0.0, p.SizeSpring.Value * scale));
            UpdatePaneOpacities();
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var w = double.IsInfinity(availableSize.Width) ? 600 : availableSize.Width;
        var h = double.IsInfinity(availableSize.Height) ? 260 : availableSize.Height;
        var isHoriz = _orientation == Orientation.Horizontal;
        var main = isHoriz ? w : h;

        EnsureSizesForAvailable(main);

        for (var i = 0; i < _panes.Count; i++)
        {
            var paneMain = Math.Max(0.0, _panes[i].SizeSpring.Value);
            var childSize = isHoriz ? new Size(paneMain, h) : new Size(w, paneMain);
            _panes[i].Container.Measure(childSize);
        }

        foreach (var d in _dividers)
        {
            var divSize = isHoriz ? new Size(HandleHitSize, h) : new Size(w, HandleHitSize);
            d.HitArea.Measure(divSize);
        }

        return new Size(w, h);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var isHoriz = _orientation == Orientation.Horizontal;
        var main = isHoriz ? finalSize.Width : finalSize.Height;
        EnsureSizesForAvailable(main);

        double offset = 0;
        for (var i = 0; i < _panes.Count; i++)
        {
            var paneMain = Math.Max(0.0, _panes[i].SizeSpring.Value);
            var rect = isHoriz
                ? new Rect(offset, 0, paneMain, finalSize.Height)
                : new Rect(0, offset, finalSize.Width, paneMain);
            _panes[i].Container.Arrange(rect);

            offset += paneMain;

            if (i < _dividers.Count)
            {
                var divRect = isHoriz
                    ? new Rect(offset - HandleHitSize * 0.5, 0, HandleHitSize, finalSize.Height)
                    : new Rect(0, offset - HandleHitSize * 0.5, finalSize.Width, HandleHitSize);
                _dividers[i].HitArea.Arrange(divRect);
            }
        }

        return finalSize;
    }

    private bool OnTick(double dt)
    {
        var active = false;
        foreach (var p in _panes)
            active |= p.SizeSpring.Step(dt);

        UpdatePaneOpacities();
        InvalidateArrange();
        return active;
    }

    private Geometry? ResolveGeometry(string key) =>
        this.TryFindResource(key, ActualThemeVariant, out var res) && res is Geometry g ? g : null;
}
