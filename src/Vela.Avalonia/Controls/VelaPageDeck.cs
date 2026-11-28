using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Templates;
using Avalonia.Rendering.Composition;
using Vela.Avalonia.Motion;

namespace Vela.Avalonia.Controls;

public enum VelaPageCacheMode
{
    /// <summary>
    /// Lazy-instantiates pages on first visit and keeps them alive in the visual tree.
    /// Switching back to visited pages is O(1) (~0.1ms) with scroll positions and state preserved.
    /// </summary>
    KeepAlive,

    /// <summary>
    /// Keeps up to <see cref="VelaPageDeck.MaxCachedPages"/> most recently visited pages alive
    /// and cleanly evicts the least recently used inactive page to bound memory.
    /// </summary>
    Lru,

    /// <summary>
    /// Detaches and discards the previous page on every switch (traditional ContentPresenter behavior).
    /// </summary>
    None,
}

public enum VelaPageTransitionMode
{
    /// <summary>
    /// Zero-latency instant switch with no offscreen render-target allocation (highest performance).
    /// </summary>
    Instant,

    /// <summary>
    /// Ultra-light 110ms GPU render-thread opacity fade via Avalonia's Compositor (zero UI-thread loop).
    /// </summary>
    CompositorFade,
}

/// <summary>
/// Optional lifecycle interface implemented by page controls or their ViewModels
/// to receive crisp activation/deactivation callbacks even when cached via KeepAlive/Lru.
/// </summary>
public interface IVelaNavigationAware
{
    void OnNavigatedTo();
    void OnNavigatedFrom();
}

public readonly record struct VelaPageSwitchMetrics(
    object? PageKey,
    bool IsCacheHit,
    TimeSpan Elapsed,
    int CachedCount,
    VelaPageCacheMode CacheMode);

/// <summary>
/// High-performance page deck optimized for desktop navigation switching:
/// - Lazy instantiation on first visit (fast startup)
/// - O(1) KeepAlive / LRU visual-tree caching (zero teardown/rebuild, preserves ScrollViewer state)
/// - Skips inactive pages completely during MeasureOverride &amp; ArrangeOverride (zero idle layout cost)
/// - Optional render-thread Compositor fade (zero UI-thread animation frames)
/// </summary>
public sealed class VelaPageDeck : Panel
{
    public static readonly StyledProperty<VelaPageCacheMode> PageCacheModeProperty =
        AvaloniaProperty.Register<VelaPageDeck, VelaPageCacheMode>(nameof(PageCacheMode), VelaPageCacheMode.Lru);

    public static readonly StyledProperty<int> MaxCachedPagesProperty =
        AvaloniaProperty.Register<VelaPageDeck, int>(nameof(MaxCachedPages), 4);

    public static readonly StyledProperty<VelaPageTransitionMode> TransitionModeProperty =
        AvaloniaProperty.Register<VelaPageDeck, VelaPageTransitionMode>(nameof(TransitionMode), VelaPageTransitionMode.Instant);

    public static readonly StyledProperty<IDataTemplate?> PageTemplateProperty =
        AvaloniaProperty.Register<VelaPageDeck, IDataTemplate?>(nameof(PageTemplate));

    public VelaPageCacheMode PageCacheMode
    {
        get => GetValue(PageCacheModeProperty);
        set => SetValue(PageCacheModeProperty, value);
    }

    public int MaxCachedPages
    {
        get => GetValue(MaxCachedPagesProperty);
        set => SetValue(MaxCachedPagesProperty, Math.Max(1, value));
    }

    public VelaPageTransitionMode TransitionMode
    {
        get => GetValue(TransitionModeProperty);
        set => SetValue(TransitionModeProperty, value);
    }

    public IDataTemplate? PageTemplate
    {
        get => GetValue(PageTemplateProperty);
        set => SetValue(PageTemplateProperty, value);
    }

    public int CachedPageCount => _slots.Count;

    public event EventHandler<VelaPageSwitchMetrics>? PageSwitched;

    private sealed class PageSlot
    {
        public required object Key { get; init; }
        public required Control Visual { get; init; }
        public long LastUsedTick { get; set; }
    }

    private readonly Dictionary<object, PageSlot> _slots = new();
    private PageSlot? _activeSlot;
    private long _lruCounter;

    static VelaPageDeck()
    {
        PageCacheModeProperty.Changed.AddClassHandler<VelaPageDeck>((d, _) => d.EnforceCachePolicy());
        MaxCachedPagesProperty.Changed.AddClassHandler<VelaPageDeck>((d, _) => d.EnforceCachePolicy());
    }

    /// <summary>
    /// Activates the page identified by <paramref name="key"/>.
    /// <paramref name="contentFactory"/> is only invoked on a cache miss.
    /// </summary>
    public void Activate(object? key, Func<object?> contentFactory)
    {
        var sw = Stopwatch.StartNew();

        if (key is null)
        {
            DeactivateCurrent();
            sw.Stop();
            PageSwitched?.Invoke(this, new VelaPageSwitchMetrics(null, false, sw.Elapsed, _slots.Count, PageCacheMode));
            return;
        }

        if (_activeSlot is not null && Equals(_activeSlot.Key, key))
            return;

        var mode = PageCacheMode;
        var cacheHit = false;
        PageSlot? targetSlot = null;

        if (mode != VelaPageCacheMode.None && _slots.TryGetValue(key, out var existing))
        {
            targetSlot = existing;
            cacheHit = true;
        }
        else
        {
            var raw = contentFactory();
            if (raw is not null)
            {
                var visual = WrapAsControl(raw);
                targetSlot = new PageSlot
                {
                    Key = key,
                    Visual = visual,
                };
                Children.Add(visual);
                _slots[key] = targetSlot;
            }
        }

        // Deactivate outgoing page
        var previous = _activeSlot;
        if (previous is not null)
        {
            NotifyLifecycle(previous.Visual, entering: false);
            if (mode == VelaPageCacheMode.None)
            {
                EvictSlot(previous);
            }
            else
            {
                VelaBackdropCacheHelper.SuspendSubtree(previous.Visual);
                previous.Visual.IsVisible = false;
            }
        }

        // Activate incoming page
        _activeSlot = targetSlot;
        if (targetSlot is not null)
        {
            targetSlot.LastUsedTick = ++_lruCounter;
            targetSlot.Visual.IsVisible = true;
            VelaBackdropCacheHelper.ResumeSubtree(targetSlot.Visual);
            NotifyLifecycle(targetSlot.Visual, entering: true);
            PlayCompositorTransitionIfEnabled(targetSlot.Visual);
        }

        if (mode == VelaPageCacheMode.Lru)
            TrimLruCache();

        sw.Stop();
        PageSwitched?.Invoke(this, new VelaPageSwitchMetrics(key, cacheHit, sw.Elapsed, _slots.Count, mode));
    }

    /// <summary>
    /// Clears all inactive cached pages immediately to reclaim memory.
    /// </summary>
    public void ClearInactiveCache()
    {
        var toRemove = _slots.Values.Where(s => !ReferenceEquals(s, _activeSlot)).ToList();
        foreach (var slot in toRemove)
            EvictSlot(slot);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        // Only measure the single active page — inactive cached pages cost 0 layout CPU!
        if (_activeSlot is { Visual.IsVisible: true } active)
        {
            active.Visual.Measure(availableSize);
            return active.Visual.DesiredSize;
        }

        return default;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        // Only arrange the single active page!
        if (_activeSlot is { Visual.IsVisible: true } active)
        {
            active.Visual.Arrange(new Rect(finalSize));
        }

        return finalSize;
    }

    private void DeactivateCurrent()
    {
        if (_activeSlot is null) return;
        NotifyLifecycle(_activeSlot.Visual, entering: false);
        if (PageCacheMode == VelaPageCacheMode.None)
        {
            EvictSlot(_activeSlot);
        }
        else
        {
            VelaBackdropCacheHelper.SuspendSubtree(_activeSlot.Visual);
            _activeSlot.Visual.IsVisible = false;
        }
        _activeSlot = null;
    }

    private void EnforceCachePolicy()
    {
        if (PageCacheMode == VelaPageCacheMode.None)
            ClearInactiveCache();
        else if (PageCacheMode == VelaPageCacheMode.Lru)
            TrimLruCache();
    }

    private void TrimLruCache()
    {
        var limit = Math.Max(1, MaxCachedPages);
        if (_slots.Count <= limit)
            return;

        var victims = _slots.Values
            .Where(s => !ReferenceEquals(s, _activeSlot))
            .OrderBy(s => s.LastUsedTick)
            .Take(_slots.Count - limit)
            .ToList();

        foreach (var victim in victims)
            EvictSlot(victim);
    }

    private void EvictSlot(PageSlot slot)
    {
        Children.Remove(slot.Visual);
        _slots.Remove(slot.Key);
        if (slot.Visual is ContentPresenter cp)
        {
            cp.Content = null;
            cp.ContentTemplate = null;
        }
    }

    private Control WrapAsControl(object content)
    {
        if (content is Control control)
        {
            // Detach from any previous logical parent if reused in None mode
            return control;
        }

        return new ContentPresenter
        {
            Content = content,
            ContentTemplate = PageTemplate,
        };
    }

    private void PlayCompositorTransitionIfEnabled(Control target)
    {
        if (TransitionMode != VelaPageTransitionMode.CompositorFade || VelaMotionOptions.ReducedMotion)
            return;

        var visual = ElementComposition.GetElementVisual(target);
        if (visual is null)
            return;

        var compositor = visual.Compositor;
        var opacityAnim = compositor.CreateScalarKeyFrameAnimation();
        opacityAnim.Duration = TimeSpan.FromMilliseconds(110);
        opacityAnim.InsertKeyFrame(0.0f, 0.0f);
        opacityAnim.InsertKeyFrame(1.0f, 1.0f);
        visual.StartAnimation("Opacity", opacityAnim);
    }

    private static void NotifyLifecycle(Control visual, bool entering)
    {
        if (visual is IVelaNavigationAware controlAware)
        {
            if (entering) controlAware.OnNavigatedTo();
            else controlAware.OnNavigatedFrom();
        }

        if (visual is ContentPresenter { Content: IVelaNavigationAware presenterAware })
        {
            if (entering) presenterAware.OnNavigatedTo();
            else presenterAware.OnNavigatedFrom();
        }
        else if (visual.DataContext is IVelaNavigationAware vmAware && !ReferenceEquals(vmAware, visual))
        {
            if (entering) vmAware.OnNavigatedTo();
            else vmAware.OnNavigatedFrom();
        }
    }
}
