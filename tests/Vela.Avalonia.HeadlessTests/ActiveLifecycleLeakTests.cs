using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Vela.Avalonia.Controls;
using Vela.Avalonia.HeadlessTests.Infrastructure;
using Vela.Avalonia.Motion;
using Vela.Avalonia.Tokens;
using Xunit;

namespace Vela.Avalonia.HeadlessTests;

/// <summary>
/// High-risk leak tests targeting controls with active DispatcherTimers,
/// TopLevel/Application event hooks, RequestAnimationFrame loops, custom Skia/Composition
/// handlers, and modal/navigation hosts.
/// </summary>
public class ActiveLifecycleLeakTests
{
    [AvaloniaFact]
    public void VelaToastHost_DetachedWhileAutoDismissTimersActive_IsCollected()
    {
        HeadlessLeakVerifier.AssertControlCollected(
            () => new VelaToastHost { Width = 400, Height = 300 },
            exercise: (window, ctrl) =>
            {
                var host = (VelaToastHost)ctrl;
                host.Show(new VelaToastOptions
                {
                    Title = "Saved",
                    Description = "Document saved",
                    Duration = TimeSpan.FromSeconds(30),
                });
                host.Show(new VelaToastOptions
                {
                    Title = "Syncing",
                    Description = "Background sync in progress",
                    Duration = TimeSpan.FromSeconds(45),
                });
                HeadlessLeakVerifier.FlushLayoutAndRender(window);
            },
            label: "VelaToastHost (active DispatcherTimers)");
    }

    [AvaloniaFact]
    public void VelaBusyOverlay_DetachedWhileSpinnerAnimating_IsCollected()
    {
        HeadlessLeakVerifier.AssertControlCollected(
            () => new VelaBusyOverlay
            {
                Width = 300,
                Height = 200,
                IsBusy = true,
                Content = new TextBlock { Text = "Loading..." },
            },
            exercise: (window, _) =>
            {
                for (var i = 0; i < 6; i++)
                {
                    AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                    Dispatcher.UIThread.RunJobs();
                }
            },
            label: "VelaBusyOverlay (active RequestAnimationFrame)");
    }

    [AvaloniaFact]
    public void SpringRunner_DetachedWhileAnimationActive_DoesNotPinOwnerControl()
    {
        HeadlessLeakVerifier.AssertControlCollected(
            () => new SpringAnimatedProbeControl { Width = 120, Height = 40 },
            exercise: (window, ctrl) =>
            {
                var probe = (SpringAnimatedProbeControl)ctrl;
                probe.StartSpring(target: 100.0);
                for (var i = 0; i < 5; i++)
                {
                    AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                    Dispatcher.UIThread.RunJobs();
                }
            },
            label: "SpringRunner active animation owner");
    }

    [AvaloniaFact]
    public void VelaCommandPalette_DetachedAfterKeyRouting_IsCollected()
    {
        HeadlessLeakVerifier.AssertControlCollected(
            () => new VelaCommandPalette { Width = 500, Height = 400 },
            exercise: (window, _) =>
            {
                window.KeyPressQwerty(PhysicalKey.K, RawInputModifiers.Meta);
                Dispatcher.UIThread.RunJobs();
                window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
                Dispatcher.UIThread.RunJobs();
            },
            label: "VelaCommandPalette (TopLevel KeyDown tunnel)");
    }

    [AvaloniaFact]
    public void VelaDialogHost_ClosedDialogContent_IsCollected()
    {
        var window = new Window { Width = 640, Height = 480 };
        var host = new VelaDialogHost { Content = new TextBlock { Text = "Base" } };
        window.Content = host;
        window.Show();
        HeadlessLeakVerifier.FlushLayoutAndRender(window);

        var contentRef = OpenAndCloseDialog(window, host);
        HeadlessLeakVerifier.ForceFullGcAndFlush();

        window.Content = null;
        window.Close();
        Dispatcher.UIThread.RunJobs();

        Assert.False(
            contentRef.TryGetTarget(out _),
            "Memory leak detected: dialog content survived full GC after VelaDialogHost.Close().");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<Control> OpenAndCloseDialog(Window window, VelaDialogHost host)
    {
        var previousReducedMotion = VelaMotionOptions.ReducedMotion;
        VelaMotionOptions.ReducedMotion = true;
        try
        {
            var body = new Border
            {
                Width = 280,
                Height = 140,
                Child = new TextBlock { Text = "Transient Dialog Body" },
            };
            var weakRef = new WeakReference<Control>(body);

            _ = host.ShowAsync<object?>(body);
            HeadlessLeakVerifier.FlushLayoutAndRender(window);

            host.Close();
            HeadlessLeakVerifier.FlushLayoutAndRender(window);

            return weakRef;
        }
        finally
        {
            VelaMotionOptions.ReducedMotion = previousReducedMotion;
        }
    }

    [AvaloniaFact]
    public void VelaDrawerHost_ClosedDrawerBody_IsCollected()
    {
        var window = new Window { Width = 640, Height = 480 };
        var host = new VelaDrawerHost { Content = new TextBlock { Text = "Base" } };
        window.Content = host;
        window.Show();
        HeadlessLeakVerifier.FlushLayoutAndRender(window);

        var bodyRef = OpenAndCloseDrawer(window, host);
        HeadlessLeakVerifier.ForceFullGcAndFlush();

        window.Content = null;
        window.Close();
        Dispatcher.UIThread.RunJobs();

        Assert.False(
            bodyRef.TryGetTarget(out _),
            "Memory leak detected: drawer body survived full GC after VelaDrawerHost.Close().");
    }

    [AvaloniaFact]
    public void VelaDialogHost_DetachedMidCloseAnimation_CollectsHostAndDialogContent()
    {
        var (hostRef, contentRef) = OpenCloseAndDetachMidAnimation();
        HeadlessLeakVerifier.ForceFullGcAndFlush();

        Assert.False(
            hostRef.TryGetTarget(out _),
            "Memory leak detected: VelaDialogHost survived full GC when detached mid-close animation.");
        Assert.False(
            contentRef.TryGetTarget(out _),
            "Memory leak detected: dialog content survived full GC when VelaDialogHost was detached mid-close animation.");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (WeakReference<VelaDialogHost> HostRef, WeakReference<Control> ContentRef) OpenCloseAndDetachMidAnimation()
    {
        var previousReducedMotion = VelaMotionOptions.ReducedMotion;
        VelaMotionOptions.ReducedMotion = false;
        try
        {
            var window = new Window { Width = 640, Height = 480 };
            var host = new VelaDialogHost { Content = new TextBlock { Text = "Base" } };
            window.Content = host;
            window.Show();
            HeadlessLeakVerifier.FlushLayoutAndRender(window);

            var body = new Border
            {
                Width = 280,
                Height = 140,
                Child = new TextBlock { Text = "Mid-Exit Dialog Body" },
            };
            var hostRef = new WeakReference<VelaDialogHost>(host);
            var weakRef = new WeakReference<Control>(body);

            _ = host.ShowAsync<object?>(body);
            HeadlessLeakVerifier.FlushLayoutAndRender(window, renderTicks: 1);

            host.Close();
            // Detach immediately while close spring animation is still running
            window.Content = null;
            HeadlessLeakVerifier.FlushLayoutAndRender(window, renderTicks: 1);
            window.Close();
            Dispatcher.UIThread.RunJobs();

            return (hostRef, weakRef);
        }
        finally
        {
            VelaMotionOptions.ReducedMotion = previousReducedMotion;
        }
    }

    [AvaloniaFact]
    public void VelaDrawerHost_DetachedMidCloseAnimation_CollectsHostAndDrawerBody()
    {
        var (hostRef, bodyRef) = OpenCloseAndDetachDrawerMidAnimation();
        HeadlessLeakVerifier.ForceFullGcAndFlush();

        Assert.False(
            hostRef.TryGetTarget(out _),
            "Memory leak detected: VelaDrawerHost survived full GC when detached mid-close animation.");
        Assert.False(
            bodyRef.TryGetTarget(out _),
            "Memory leak detected: drawer body survived full GC when VelaDrawerHost was detached mid-close animation.");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (WeakReference<VelaDrawerHost> HostRef, WeakReference<Control> BodyRef) OpenCloseAndDetachDrawerMidAnimation()
    {
        var previousReducedMotion = VelaMotionOptions.ReducedMotion;
        VelaMotionOptions.ReducedMotion = false;
        try
        {
            var window = new Window { Width = 640, Height = 480 };
            var host = new VelaDrawerHost { Content = new TextBlock { Text = "Base" } };
            window.Content = host;
            window.Show();
            HeadlessLeakVerifier.FlushLayoutAndRender(window);

            var body = new Border
            {
                Width = 260,
                Child = new TextBlock { Text = "Mid-Exit Drawer Body" },
            };
            var hostRef = new WeakReference<VelaDrawerHost>(host);
            var weakRef = new WeakReference<Control>(body);

            _ = host.ShowAsync(body);
            HeadlessLeakVerifier.FlushLayoutAndRender(window, renderTicks: 1);

            host.Close();
            // Detach immediately while close spring animation is still running
            window.Content = null;
            HeadlessLeakVerifier.FlushLayoutAndRender(window, renderTicks: 1);
            window.Close();
            Dispatcher.UIThread.RunJobs();

            return (hostRef, weakRef);
        }
        finally
        {
            VelaMotionOptions.ReducedMotion = previousReducedMotion;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<Control> OpenAndCloseDrawer(Window window, VelaDrawerHost host)
    {
        var previousReducedMotion = VelaMotionOptions.ReducedMotion;
        VelaMotionOptions.ReducedMotion = true;
        try
        {
            var body = new Border
            {
                Width = 260,
                Child = new TextBlock { Text = "Transient Drawer Body" },
            };
            var weakRef = new WeakReference<Control>(body);

            _ = host.ShowAsync(body);
            HeadlessLeakVerifier.FlushLayoutAndRender(window);

            host.Close();
            HeadlessLeakVerifier.FlushLayoutAndRender(window);

            return weakRef;
        }
        finally
        {
            VelaMotionOptions.ReducedMotion = previousReducedMotion;
        }
    }

    [AvaloniaFact]
    public void VelaPageDeck_ClearedInactivePages_AreCollected()
    {
        var window = new Window { Width = 640, Height = 480 };
        var deck = new VelaPageDeck();
        window.Content = deck;
        window.Show();
        HeadlessLeakVerifier.FlushLayoutAndRender(window);

        var firstPageRef = SwitchPagesAndClearCache(window, deck);
        HeadlessLeakVerifier.ForceFullGcAndFlush();

        window.Content = null;
        window.Close();
        Dispatcher.UIThread.RunJobs();

        Assert.False(
            firstPageRef.TryGetTarget(out _),
            "Memory leak detected: inactive page survived full GC after VelaPageDeck.ClearInactiveCache().");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<Control> SwitchPagesAndClearCache(Window window, VelaPageDeck deck)
    {
        WeakReference<Control>? pageARef = null;
        deck.Activate("page-a", () =>
        {
            var page = new Border { Child = new TextBlock { Text = "Page A" } };
            pageARef = new WeakReference<Control>(page);
            return page;
        });
        HeadlessLeakVerifier.FlushLayoutAndRender(window);

        deck.Activate("page-b", () => new Border { Child = new TextBlock { Text = "Page B" } });
        HeadlessLeakVerifier.FlushLayoutAndRender(window);

        deck.ClearInactiveCache();
        HeadlessLeakVerifier.FlushLayoutAndRender(window);

        return pageARef!;
    }

    [AvaloniaFact]
    public void VelaLiquidGlass_And_VelaShaderBackground_CollectedAfterMultipleSkiaFrames()
    {
        HeadlessLeakVerifier.AssertControlCollected(
            () => new Grid
            {
                Width = 400,
                Height = 260,
                Children =
                {
                    new VelaShaderBackground(),
                    new VelaLiquidGlass
                    {
                        Width = 240,
                        Height = 120,
                        Content = new TextBlock { Text = "Liquid Glass" },
                    },
                },
            },
            exercise: (window, _) =>
            {
                for (var i = 0; i < 6; i++)
                {
                    HeadlessLeakVerifier.FlushLayoutAndRender(window, renderTicks: 1);
                }
            },
            label: "VelaShaderBackground + VelaLiquidGlass");
    }

    [AvaloniaFact]
    public void VelaWindow_CollectedAfterShowRenderAndClose()
    {
        HeadlessLeakVerifier.AssertWindowCollected(
            () => new VelaWindow
            {
                Width = 800,
                Height = 500,
                Title = "Headless Leak Probe",
                Content = new StackPanel
                {
                    Children =
                    {
                        new VelaButton { Content = "Action" },
                        new VelaInput { Text = "Probe" },
                    },
                },
            },
            exercise: window =>
            {
                var app = HeadlessLeakVerifier.CurrentApp;
                var origAccent = app.Theme.Accent;
                var origVariant = app.RequestedThemeVariant;
                try
                {
                    app.RequestedThemeVariant = ThemeVariant.Dark;
                    app.Theme.Accent = VelaAccent.Blue;
                    HeadlessLeakVerifier.FlushLayoutAndRender(window, renderTicks: 2);
                }
                finally
                {
                    app.RequestedThemeVariant = origVariant;
                    app.Theme.Accent = origAccent;
                }
            },
            label: nameof(VelaWindow));
    }

    private sealed class SpringAnimatedProbeControl : Control
    {
        private readonly SpringRunner _runner;
        private readonly SpringChannel _spring = new(0.0, SpringSpec.FromVisualDuration(0.35, bounce: 0.15));

        public SpringAnimatedProbeControl()
        {
            _runner = new SpringRunner(this, dt =>
            {
                _spring.Step(dt);
                InvalidateVisual();
                return _spring.IsAnimating;
            });
        }

        public void StartSpring(double target)
        {
            _spring.SetTarget(target);
            _runner.Wake();
        }
    }
}
