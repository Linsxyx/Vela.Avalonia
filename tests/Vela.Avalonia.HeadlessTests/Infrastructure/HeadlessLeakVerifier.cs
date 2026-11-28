using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Styling;
using Avalonia.Threading;
using Vela.Avalonia.Tokens;
using Xunit;

namespace Vela.Avalonia.HeadlessTests.Infrastructure;

/// <summary>
/// Helpers for deterministic Avalonia.Headless GC leak detection, steady-state heap
/// tracking, and per-operation allocation measurements.
/// </summary>
internal static class HeadlessLeakVerifier
{
    public static HeadlessTestApp CurrentApp =>
        Assert.IsType<HeadlessTestApp>(Application.Current);

    /// <summary>
    /// Flushes pending dispatcher jobs and forces a full render frame pass on a window.
    /// </summary>
    public static void FlushLayoutAndRender(Window window, int renderTicks = 2)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        for (var i = 0; i < renderTicks; i++)
        {
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
        }
        using var frame = window.CaptureRenderedFrame();
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>
    /// Mounts a control created by <paramref name="factory"/> inside a headless Window,
    /// exercises layout/rendering/theme switching, detaches and closes the Window, then
    /// verifies that the control instance is eligible for garbage collection.
    /// </summary>
    public static void AssertControlCollected(
        Func<Control> factory,
        Action<Window, Control>? exercise = null,
        bool exerciseThemeAndAccentSwitch = true,
        string? label = null)
    {
        var weakRef = MountExerciseAndDetach(factory, exercise, exerciseThemeAndAccentSwitch);
        ForceFullGcAndFlush();

        Assert.False(
            weakRef.TryGetTarget(out var survivor),
            $"Memory leak detected for {label ?? survivor?.GetType().Name ?? "Control"}: instance survived full GC after detaching from visual tree.");
    }

    /// <summary>
    /// Mounts a standalone Window (e.g. VelaWindow), renders it, exercises it, closes it,
    /// and verifies the Window itself is garbage collected.
    /// </summary>
    public static void AssertWindowCollected(
        Func<Window> factory,
        Action<Window>? exercise = null,
        string? label = null)
    {
        var weakRef = ShowExerciseAndCloseWindow(factory, exercise);
        ForceFullGcAndFlush();

        Assert.False(
            weakRef.TryGetTarget(out var survivor),
            $"Memory leak detected for {label ?? survivor?.GetType().Name ?? "Window"}: window instance survived full GC after Close().");
    }

    /// <summary>
    /// Verifies that an arbitrary object created inside <paramref name="producer"/> is collected
    /// after the scope exits and full GC runs.
    /// </summary>
    public static void AssertCollected(Func<object> producer, string label)
    {
        var weakRef = CreateWeakRef(producer);
        ForceFullGcAndFlush();

        Assert.False(
            weakRef.TryGetTarget(out _),
            $"Memory leak detected for {label}: object survived full GC.");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<object> CreateWeakRef(Func<object> producer)
    {
        var target = producer();
        return new WeakReference<object>(target);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<Control> MountExerciseAndDetach(
        Func<Control> factory,
        Action<Window, Control>? exercise,
        bool exerciseThemeAndAccentSwitch)
    {
        var window = new Window
        {
            Width = 960,
            Height = 640,
        };

        var control = factory();
        var weakRef = new WeakReference<Control>(control);

        window.Content = control;
        window.Show();
        FlushLayoutAndRender(window);

        exercise?.Invoke(window, control);
        FlushLayoutAndRender(window);

        if (exerciseThemeAndAccentSwitch && Application.Current is HeadlessTestApp app)
        {
            var originalVariant = app.RequestedThemeVariant;
            var originalAccent = app.Theme.Accent;
            try
            {
                app.RequestedThemeVariant = ThemeVariant.Dark;
                app.Theme.Accent = VelaAccent.Coral;
                FlushLayoutAndRender(window, renderTicks: 1);

                app.RequestedThemeVariant = ThemeVariant.Light;
                app.Theme.Accent = VelaAccent.Green;
                FlushLayoutAndRender(window, renderTicks: 1);
            }
            finally
            {
                app.RequestedThemeVariant = originalVariant;
                app.Theme.Accent = originalAccent;
                FlushLayoutAndRender(window, renderTicks: 1);
            }
        }

        window.Content = null;
        FlushLayoutAndRender(window, renderTicks: 1);
        window.Close();
        Dispatcher.UIThread.RunJobs();

        return weakRef;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<Window> ShowExerciseAndCloseWindow(
        Func<Window> factory,
        Action<Window>? exercise)
    {
        var window = factory();
        var weakRef = new WeakReference<Window>(window);

        window.Show();
        FlushLayoutAndRender(window);

        exercise?.Invoke(window);
        FlushLayoutAndRender(window);

        window.Content = null;
        window.Close();
        Dispatcher.UIThread.RunJobs();

        return weakRef;
    }

    public static void ForceFullGcAndFlush(int passes = 5)
    {
        for (var i = 0; i < passes; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();

            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
            GC.WaitForPendingFinalizers();
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
        }
    }

    /// <summary>
    /// Runs <paramref name="warmupCycles"/> to stabilize caches/pools, then measures the net
    /// managed heap delta across <paramref name="measuredCycles"/> after full GC.
    /// </summary>
    public static long MeasureSteadyStateHeapDelta(Action cycle, int warmupCycles, int measuredCycles)
    {
        for (var i = 0; i < warmupCycles; i++)
            cycle();

        ForceFullGcAndFlush();
        var before = GC.GetTotalMemory(forceFullCollection: true);

        for (var i = 0; i < measuredCycles; i++)
            cycle();

        ForceFullGcAndFlush();
        var after = GC.GetTotalMemory(forceFullCollection: true);

        return after - before;
    }

    /// <summary>
    /// Measures bytes allocated on the current thread while executing <paramref name="action"/>.
    /// </summary>
    public static long MeasureAllocatedBytes(Action action)
    {
        var before = GC.GetAllocatedBytesForCurrentThread();
        action();
        var after = GC.GetAllocatedBytesForCurrentThread();
        return after - before;
    }
}
