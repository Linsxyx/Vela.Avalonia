using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Vela.Avalonia.Motion;

namespace Vela.Avalonia.Controls;

public class VelaSegmentedItem : ListBoxItem
{
    protected override Type StyleKeyOverride => typeof(VelaSegmentedItem);
}

public class VelaSegmentedControl : ListBox
{
    private readonly SpringChannel _xSpring = new(0.0, VelaMotionTokens.Springs.Morph);
    private readonly SpringChannel _widthSpring = new(0.0, VelaMotionTokens.Springs.Morph);
    private readonly SpringChannel _scrollSpring = new(0.0, VelaMotionTokens.Springs.Morph);
    private readonly SpringRunner _runner;

    private ScrollViewer? _scroller;
    private Panel? _track;
    private Border? _indicator;
    private TranslateTransform? _indicatorTranslate;
    private bool _hasPositioned;
    private int _positionedIndex = -1;
    private bool _isPointerSliding;

    protected override Type StyleKeyOverride => typeof(VelaSegmentedControl);

    static VelaSegmentedControl()
    {
        SelectionModeProperty.OverrideDefaultValue<VelaSegmentedControl>(SelectionMode.Single | SelectionMode.AlwaysSelected);
        SelectedIndexProperty.Changed.AddClassHandler<VelaSegmentedControl>((c, _) => c.OnSelectedIndexChanged());
    }

    public VelaSegmentedControl()
    {
        _runner = new SpringRunner(this, OnMotionTick);
    }

    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey)
        => new VelaSegmentedItem();

    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey)
        => NeedsContainer<VelaSegmentedItem>(item, out recycleKey);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (_track is not null)
        {
            _track.LayoutUpdated -= OnTrackLayoutUpdated;
            _track.RemoveHandler(PointerPressedEvent, OnTrackPointerPressed);
            _track.RemoveHandler(PointerMovedEvent, OnTrackPointerMoved);
            _track.RemoveHandler(PointerReleasedEvent, OnTrackPointerReleased);
        }

        _scroller = e.NameScope.Find<ScrollViewer>("PART_Scroller");
        _track = e.NameScope.Find<Panel>("PART_Track");
        _indicator = e.NameScope.Find<Border>("PART_Indicator");

        if (_indicator is not null)
        {
            _indicatorTranslate = new TranslateTransform();
            _indicator.RenderTransform = _indicatorTranslate;
            _indicator.Opacity = 0;
        }

        if (_track is not null)
        {
            _track.LayoutUpdated += OnTrackLayoutUpdated;
            _track.AddHandler(PointerPressedEvent, OnTrackPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
            _track.AddHandler(PointerMovedEvent, OnTrackPointerMoved, RoutingStrategies.Tunnel, handledEventsToo: true);
            _track.AddHandler(PointerReleasedEvent, OnTrackPointerReleased, RoutingStrategies.Tunnel, handledEventsToo: true);
        }

        _hasPositioned = false;
        _positionedIndex = -1;
        Dispatcher.UIThread.Post(() => UpdateIndicator(animate: false), DispatcherPriority.Loaded);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _runner.Stop();
        if (_track is not null)
        {
            _track.LayoutUpdated -= OnTrackLayoutUpdated;
            _track.RemoveHandler(PointerPressedEvent, OnTrackPointerPressed);
            _track.RemoveHandler(PointerMovedEvent, OnTrackPointerMoved);
            _track.RemoveHandler(PointerReleasedEvent, OnTrackPointerReleased);
        }
    }

    private void OnTrackPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (IsEffectivelyEnabled && e.GetCurrentPoint(_track).Properties.IsLeftButtonPressed)
            _isPointerSliding = true;
    }

    private void OnTrackPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isPointerSliding || _track is null || !IsEffectivelyEnabled)
            return;

        if (!e.GetCurrentPoint(_track).Properties.IsLeftButtonPressed)
        {
            _isPointerSliding = false;
            return;
        }

        var pos = e.GetPosition(_track);
        for (var i = 0; i < ItemCount; i++)
        {
            var container = ContainerFromIndex(i);
            if (container is null || container.Bounds.Width <= 0)
                continue;

            var origin = container.TranslatePoint(new Point(0, 0), _track);
            if (!origin.HasValue)
                continue;

            if (pos.X >= origin.Value.X && pos.X <= origin.Value.X + container.Bounds.Width)
            {
                if (SelectedIndex != i)
                    SelectedIndex = i;
                break;
            }
        }
    }

    private void OnTrackPointerReleased(object? sender, PointerReleasedEventArgs e) => _isPointerSliding = false;

    protected override void OnKeyDown(KeyEventArgs e)
    {
        var count = ItemCount;
        if (count > 0)
        {
            var idx = Math.Max(0, SelectedIndex);
            var target = e.Key switch
            {
                Key.Right or Key.Down => idx == count - 1 ? 0 : idx + 1,
                Key.Left or Key.Up => idx == 0 ? count - 1 : idx - 1,
                Key.Home => 0,
                Key.End => count - 1,
                _ => -1,
            };

            if (target >= 0)
            {
                SelectedIndex = target;
                ContainerFromIndex(target)?.Focus(NavigationMethod.Directional);
                e.Handled = true;
                return;
            }
        }

        base.OnKeyDown(e);
    }

    private void OnSelectedIndexChanged()
    {
        if (!UpdateIndicator(animate: true))
            Dispatcher.UIThread.Post(() => UpdateIndicator(animate: _hasPositioned), DispatcherPriority.Loaded);
    }

    private void OnTrackLayoutUpdated(object? sender, EventArgs e)
    {
        if (!_hasPositioned)
        {
            UpdateIndicator(animate: false);
            return;
        }

        if (SelectedIndex == _positionedIndex && !_xSpring.IsAnimating && !_widthSpring.IsAnimating)
            UpdateIndicator(animate: false);
    }

    private bool UpdateIndicator(bool animate)
    {
        if (_track is null || _indicator is null)
            return false;

        if (SelectedIndex < 0 || SelectedIndex >= ItemCount)
        {
            _indicator.Opacity = 0;
            return true;
        }

        var container = ContainerFromIndex(SelectedIndex);
        if (container is null || container.Bounds.Width <= 0)
            return false;

        var origin = container.TranslatePoint(new Point(0, 0), _track);
        if (!origin.HasValue)
            return false;

        var targetX = origin.Value.X;
        var targetWidth = container.Bounds.Width;
        _indicator.Height = container.Bounds.Height;
        _indicator.Opacity = 1;
        _positionedIndex = SelectedIndex;

        var shouldAnimate = _hasPositioned && animate && !VelaMotionOptions.ReducedMotion;
        _hasPositioned = true;

        if (!shouldAnimate)
        {
            _xSpring.Jump(targetX);
            _widthSpring.Jump(targetWidth);
            ApplyVisuals();
            ScrollSelectedIntoView(targetX, targetWidth, animate: false);
            return true;
        }

        _xSpring.SetTarget(targetX, VelaMotionTokens.Springs.Morph);
        _widthSpring.SetTarget(targetWidth, VelaMotionTokens.Springs.Morph);
        ScrollSelectedIntoView(targetX, targetWidth, animate: true);
        _runner.Wake();
        return true;
    }

    private void ScrollSelectedIntoView(double itemX, double itemWidth, bool animate)
    {
        if (_scroller is null)
            return;

        var viewportW = _scroller.Viewport.Width;
        var extentW = _scroller.Extent.Width;
        if (extentW <= viewportW + 1.0)
            return;

        const double room = 20.0;
        var current = _scroller.Offset.X;
        var start = itemX - room;
        var end = itemX + itemWidth + room - viewportW;
        var next = current > start ? start : (current < end ? end : current);
        next = Math.Clamp(next, 0.0, Math.Max(0.0, extentW - viewportW));

        if (Math.Abs(next - current) < 0.5)
            return;

        if (!animate || VelaMotionOptions.ReducedMotion)
        {
            _scrollSpring.Jump(next);
            _scroller.Offset = new Vector(next, _scroller.Offset.Y);
        }
        else
        {
            _scrollSpring.Jump(current);
            _scrollSpring.SetTarget(next, VelaMotionTokens.Springs.Morph);
            _runner.Wake();
        }
    }

    private bool OnMotionTick(double dt)
    {
        var active = _xSpring.Step(dt);
        active |= _widthSpring.Step(dt);

        if (_scrollSpring.IsAnimating)
        {
            active |= _scrollSpring.Step(dt);
            if (_scroller is not null)
                _scroller.Offset = new Vector(_scrollSpring.Value, _scroller.Offset.Y);
        }

        ApplyVisuals();
        return active;
    }

    private void ApplyVisuals()
    {
        if (_indicatorTranslate is not null)
            _indicatorTranslate.X = _xSpring.Value;
        if (_indicator is not null)
            _indicator.Width = Math.Max(0, _widthSpring.Value);
    }
}
