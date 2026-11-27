using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Metadata;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Threading;
using SkiaSharp;
using Vela.Avalonia.Tokens;

namespace Vela.Avalonia.Controls;

/// <summary>
/// Declarative, theme-aware system tray icon for Vela.Avalonia:
/// - Opt-in attachment to <see cref="Application"/> (via <see cref="InstanceProperty"/>) or <see cref="VelaWindow.TrayIcon"/>
/// - Pure Skia HiDPI icon synthesis supporting vector <see cref="Geometry"/> / SVG path data, status dots, numeric badges, and progress rings
/// - Automatic macOS template-icon switching and live Light/Dark + <see cref="VelaTheme.Accent"/> synchronization
/// - Dual presentation modes: OS-native <see cref="NativeMenu"/> or frameless spring-animated Vela floating menu / custom popover
/// - Optional <see cref="CloseToTray"/> and <see cref="MinimizeToTray"/> window lifecycle integration
/// - Safe no-op degradation on Browser (WASM) and headless test environments
/// </summary>
public class VelaTrayIcon : StyledElement, IDisposable
{
    /// <summary>
    /// Attached property allowing declarative registration of a <see cref="VelaTrayIcon"/> directly on <see cref="Application"/> in XAML.
    /// </summary>
    public static readonly AttachedProperty<VelaTrayIcon?> InstanceProperty =
        AvaloniaProperty.RegisterAttached<VelaTrayIcon, Application, VelaTrayIcon?>("Instance");

    public static VelaTrayIcon? GetInstance(Application app) => app.GetValue(InstanceProperty);
    public static void SetInstance(Application app, VelaTrayIcon? value) => app.SetValue(InstanceProperty, value);

    public static readonly StyledProperty<string?> ToolTipTextProperty =
        AvaloniaProperty.Register<VelaTrayIcon, string?>(nameof(ToolTipText));

    public static readonly StyledProperty<bool> IsVisibleProperty =
        AvaloniaProperty.Register<VelaTrayIcon, bool>(nameof(IsVisible), true);

    public static readonly StyledProperty<WindowIcon?> IconProperty =
        AvaloniaProperty.Register<VelaTrayIcon, WindowIcon?>(nameof(Icon));

    public static readonly StyledProperty<Bitmap?> IconBitmapProperty =
        AvaloniaProperty.Register<VelaTrayIcon, Bitmap?>(nameof(IconBitmap));

    public static readonly StyledProperty<Geometry?> IconGeometryProperty =
        AvaloniaProperty.Register<VelaTrayIcon, Geometry?>(nameof(IconGeometry));

    public static readonly StyledProperty<string?> IconPathDataProperty =
        AvaloniaProperty.Register<VelaTrayIcon, string?>(nameof(IconPathData));

    public static readonly StyledProperty<IBrush?> IconBrushProperty =
        AvaloniaProperty.Register<VelaTrayIcon, IBrush?>(nameof(IconBrush));

    public static readonly StyledProperty<IBrush?> IconBackgroundProperty =
        AvaloniaProperty.Register<VelaTrayIcon, IBrush?>(nameof(IconBackground));

    public static readonly StyledProperty<VelaTrayIconThemeMode> IconThemeModeProperty =
        AvaloniaProperty.Register<VelaTrayIcon, VelaTrayIconThemeMode>(nameof(IconThemeMode), VelaTrayIconThemeMode.Auto);

    public static readonly StyledProperty<int> IconPixelSizeProperty =
        AvaloniaProperty.Register<VelaTrayIcon, int>(nameof(IconPixelSize), 64);

    public static readonly StyledProperty<VelaTrayBadgeVariant> BadgeVariantProperty =
        AvaloniaProperty.Register<VelaTrayIcon, VelaTrayBadgeVariant>(nameof(BadgeVariant), VelaTrayBadgeVariant.None);

    public static readonly StyledProperty<VelaTrayBadgeTone> BadgeToneProperty =
        AvaloniaProperty.Register<VelaTrayIcon, VelaTrayBadgeTone>(nameof(BadgeTone), VelaTrayBadgeTone.Accent);

    public static readonly StyledProperty<int> BadgeCountProperty =
        AvaloniaProperty.Register<VelaTrayIcon, int>(nameof(BadgeCount), 0);

    public static readonly StyledProperty<IBrush?> BadgeBrushProperty =
        AvaloniaProperty.Register<VelaTrayIcon, IBrush?>(nameof(BadgeBrush));

    public static readonly StyledProperty<double?> ProgressProperty =
        AvaloniaProperty.Register<VelaTrayIcon, double?>(nameof(Progress));

    public static readonly StyledProperty<VelaTrayPresentation> PresentationProperty =
        AvaloniaProperty.Register<VelaTrayIcon, VelaTrayPresentation>(nameof(Presentation), VelaTrayPresentation.VelaMenu);

    public static readonly StyledProperty<VelaTrayClickBehavior> ClickBehaviorProperty =
        AvaloniaProperty.Register<VelaTrayIcon, VelaTrayClickBehavior>(nameof(ClickBehavior), VelaTrayClickBehavior.Default);

    public static readonly StyledProperty<VelaSurfaceMaterial> SurfaceMaterialProperty =
        AvaloniaProperty.Register<VelaTrayIcon, VelaSurfaceMaterial>(nameof(SurfaceMaterial), VelaSurfaceMaterial.Solid);

    public static readonly StyledProperty<VelaTrayFlyoutPlacement> FlyoutPlacementProperty =
        AvaloniaProperty.Register<VelaTrayIcon, VelaTrayFlyoutPlacement>(nameof(FlyoutPlacement), VelaTrayFlyoutPlacement.Auto);

    public static readonly StyledProperty<double> FlyoutMinWidthProperty =
        AvaloniaProperty.Register<VelaTrayIcon, double>(nameof(FlyoutMinWidth), 224.0);

    public static readonly StyledProperty<double> FlyoutMaxWidthProperty =
        AvaloniaProperty.Register<VelaTrayIcon, double>(nameof(FlyoutMaxWidth), 360.0);

    public static readonly StyledProperty<Thickness> FlyoutPaddingProperty =
        AvaloniaProperty.Register<VelaTrayIcon, Thickness>(nameof(FlyoutPadding), new Thickness(6));

    public static readonly StyledProperty<object?> FlyoutHeaderProperty =
        AvaloniaProperty.Register<VelaTrayIcon, object?>(nameof(FlyoutHeader));

    public static readonly StyledProperty<object?> FlyoutFooterProperty =
        AvaloniaProperty.Register<VelaTrayIcon, object?>(nameof(FlyoutFooter));

    public static readonly StyledProperty<object?> PopoverContentProperty =
        AvaloniaProperty.Register<VelaTrayIcon, object?>(nameof(PopoverContent));

    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<VelaTrayIcon, ICommand?>(nameof(Command));

    public static readonly StyledProperty<object?> CommandParameterProperty =
        AvaloniaProperty.Register<VelaTrayIcon, object?>(nameof(CommandParameter));

    public static readonly StyledProperty<Window?> TargetWindowProperty =
        AvaloniaProperty.Register<VelaTrayIcon, Window?>(nameof(TargetWindow));

    public static readonly StyledProperty<bool> CloseToTrayProperty =
        AvaloniaProperty.Register<VelaTrayIcon, bool>(nameof(CloseToTray), false);

    public static readonly StyledProperty<bool> MinimizeToTrayProperty =
        AvaloniaProperty.Register<VelaTrayIcon, bool>(nameof(MinimizeToTray), false);

    public string? ToolTipText { get => GetValue(ToolTipTextProperty); set => SetValue(ToolTipTextProperty, value); }
    public bool IsVisible { get => GetValue(IsVisibleProperty); set => SetValue(IsVisibleProperty, value); }
    public WindowIcon? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }
    public Bitmap? IconBitmap { get => GetValue(IconBitmapProperty); set => SetValue(IconBitmapProperty, value); }
    public Geometry? IconGeometry { get => GetValue(IconGeometryProperty); set => SetValue(IconGeometryProperty, value); }
    public string? IconPathData { get => GetValue(IconPathDataProperty); set => SetValue(IconPathDataProperty, value); }
    public IBrush? IconBrush { get => GetValue(IconBrushProperty); set => SetValue(IconBrushProperty, value); }
    public IBrush? IconBackground { get => GetValue(IconBackgroundProperty); set => SetValue(IconBackgroundProperty, value); }
    public VelaTrayIconThemeMode IconThemeMode { get => GetValue(IconThemeModeProperty); set => SetValue(IconThemeModeProperty, value); }
    public int IconPixelSize { get => GetValue(IconPixelSizeProperty); set => SetValue(IconPixelSizeProperty, value); }
    public VelaTrayBadgeVariant BadgeVariant { get => GetValue(BadgeVariantProperty); set => SetValue(BadgeVariantProperty, value); }
    public VelaTrayBadgeTone BadgeTone { get => GetValue(BadgeToneProperty); set => SetValue(BadgeToneProperty, value); }
    public int BadgeCount { get => GetValue(BadgeCountProperty); set => SetValue(BadgeCountProperty, value); }
    public IBrush? BadgeBrush { get => GetValue(BadgeBrushProperty); set => SetValue(BadgeBrushProperty, value); }
    public double? Progress { get => GetValue(ProgressProperty); set => SetValue(ProgressProperty, value); }
    public VelaTrayPresentation Presentation { get => GetValue(PresentationProperty); set => SetValue(PresentationProperty, value); }
    public VelaTrayClickBehavior ClickBehavior { get => GetValue(ClickBehaviorProperty); set => SetValue(ClickBehaviorProperty, value); }
    public VelaSurfaceMaterial SurfaceMaterial { get => GetValue(SurfaceMaterialProperty); set => SetValue(SurfaceMaterialProperty, value); }
    public VelaTrayFlyoutPlacement FlyoutPlacement { get => GetValue(FlyoutPlacementProperty); set => SetValue(FlyoutPlacementProperty, value); }
    public double FlyoutMinWidth { get => GetValue(FlyoutMinWidthProperty); set => SetValue(FlyoutMinWidthProperty, value); }
    public double FlyoutMaxWidth { get => GetValue(FlyoutMaxWidthProperty); set => SetValue(FlyoutMaxWidthProperty, value); }
    public Thickness FlyoutPadding { get => GetValue(FlyoutPaddingProperty); set => SetValue(FlyoutPaddingProperty, value); }
    public object? FlyoutHeader { get => GetValue(FlyoutHeaderProperty); set => SetValue(FlyoutHeaderProperty, value); }
    public object? FlyoutFooter { get => GetValue(FlyoutFooterProperty); set => SetValue(FlyoutFooterProperty, value); }
    public object? PopoverContent { get => GetValue(PopoverContentProperty); set => SetValue(PopoverContentProperty, value); }
    public ICommand? Command { get => GetValue(CommandProperty); set => SetValue(CommandProperty, value); }
    public object? CommandParameter { get => GetValue(CommandParameterProperty); set => SetValue(CommandParameterProperty, value); }
    public Window? TargetWindow { get => GetValue(TargetWindowProperty); set => SetValue(TargetWindowProperty, value); }
    public bool CloseToTray { get => GetValue(CloseToTrayProperty); set => SetValue(CloseToTrayProperty, value); }
    public bool MinimizeToTray { get => GetValue(MinimizeToTrayProperty); set => SetValue(MinimizeToTrayProperty, value); }

    /// <summary>Declarative menu items rendered either in <see cref="NativeMenu"/> or the Vela floating menu.</summary>
    [Content]
    public ObservableCollection<VelaMenuItem> MenuItems { get; } = new();

    /// <summary>Whether the native OS tray icon is currently attached on a desktop platform.</summary>
    public bool IsAttached => _attachedApp is not null
        && (_nativeTray is not null || _macStatusItem is { IsCreated: true });

    /// <summary>Whether the Vela floating flyout window is currently visible.</summary>
    public bool IsFlyoutOpen => _flyoutWindow is { IsVisible: true };

    /// <summary>
    /// Effective presentation mode after applying platform policy.
    /// On Linux, <see cref="VelaTrayPresentation.VelaMenu"/> and <see cref="VelaTrayPresentation.VelaPopover"/>
    /// automatically resolve to <see cref="VelaTrayPresentation.Native"/> (<c>StatusNotifierItem</c> / <c>DBusMenu</c>).
    /// </summary>
    public VelaTrayPresentation EffectivePresentation =>
        ResolveEffectivePresentation(Presentation, OperatingSystem.IsLinux());

    /// <summary>Whether the current runtime supports desktop system tray icons.</summary>
    public static bool IsPlatformSupported =>
        Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime;

    public event EventHandler? Clicked;
    public event EventHandler? FlyoutOpening;
    public event EventHandler? FlyoutOpened;
    public event EventHandler? FlyoutClosed;
    public event EventHandler? RenderedIconChanged;

    private Application? _attachedApp;
    private Window? _linkedWindow;
    private TrayIcon? _nativeTray;
    private NativeMenu? _nativeMenu;
    private VelaMacOsStatusItem? _macStatusItem;
    private VelaTrayFlyoutWindow? _flyoutWindow;
    private readonly List<Bitmap> _nativeMenuIconBitmaps = new();
    private long _lastFlyoutDismissTicks;
    private bool _allowRealClose;
    private bool _isDisposed;
    private bool _iconRefreshQueued;

    static VelaTrayIcon()
    {
        InstanceProperty.Changed.AddClassHandler<Application>((app, e) =>
        {
            if (e.OldValue is VelaTrayIcon oldTray)
                oldTray.Detach();
            if (e.NewValue is VelaTrayIcon newTray)
                newTray.Attach(app);
        });

        ToolTipTextProperty.Changed.AddClassHandler<VelaTrayIcon>((t, _) => t.SyncNativeTrayState());
        IsVisibleProperty.Changed.AddClassHandler<VelaTrayIcon>((t, _) => t.SyncNativeTrayState());

        IconProperty.Changed.AddClassHandler<VelaTrayIcon>((t, _) => t.ScheduleIconRefresh());
        IconBitmapProperty.Changed.AddClassHandler<VelaTrayIcon>((t, _) => t.ScheduleIconRefresh());
        IconGeometryProperty.Changed.AddClassHandler<VelaTrayIcon>((t, _) => t.ScheduleIconRefresh());
        IconPathDataProperty.Changed.AddClassHandler<VelaTrayIcon>((t, _) => t.ScheduleIconRefresh());
        IconBrushProperty.Changed.AddClassHandler<VelaTrayIcon>((t, _) => t.ScheduleIconRefresh());
        IconBackgroundProperty.Changed.AddClassHandler<VelaTrayIcon>((t, _) => t.ScheduleIconRefresh());
        IconThemeModeProperty.Changed.AddClassHandler<VelaTrayIcon>((t, _) => t.ScheduleIconRefresh());
        IconPixelSizeProperty.Changed.AddClassHandler<VelaTrayIcon>((t, _) => t.ScheduleIconRefresh());
        BadgeVariantProperty.Changed.AddClassHandler<VelaTrayIcon>((t, _) => t.ScheduleIconRefresh());
        BadgeToneProperty.Changed.AddClassHandler<VelaTrayIcon>((t, _) => t.ScheduleIconRefresh());
        BadgeCountProperty.Changed.AddClassHandler<VelaTrayIcon>((t, _) => t.ScheduleIconRefresh());
        BadgeBrushProperty.Changed.AddClassHandler<VelaTrayIcon>((t, _) => t.ScheduleIconRefresh());
        ProgressProperty.Changed.AddClassHandler<VelaTrayIcon>((t, _) => t.ScheduleIconRefresh());

        PresentationProperty.Changed.AddClassHandler<VelaTrayIcon>((t, _) => t.SyncPresentation());
        TargetWindowProperty.Changed.AddClassHandler<VelaTrayIcon>((t, e) =>
        {
            t.BindWindowEvents(e.NewValue as Window);
        });
    }

    public VelaTrayIcon()
    {
        MenuItems.CollectionChanged += OnMenuItemsCollectionChanged;
    }

    /// <summary>Attaches this tray icon to the given <see cref="Application"/> (safe no-op on non-desktop runtimes).</summary>
    public void Attach(Application? application = null)
    {
        if (_isDisposed)
            return;

        var app = application ?? Application.Current;
        if (app is null)
            return;

        if (_attachedApp == app && IsAttached)
        {
            EnsurePlatformTrayBackend(app);
            SyncNativeTrayState();
            SyncPresentation();
            RefreshRenderedIcon();
            return;
        }

        Detach();
        _attachedApp = app;

        VelaTheme.AccentChanged += OnThemeOrAccentChanged;
        app.ActualThemeVariantChanged += OnAppThemeChanged;

        if (app.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownRequested += OnDesktopShutdownRequested;
            EnsurePlatformTrayBackend(app);
        }

        SyncNativeTrayState();
        SyncPresentation();
        RefreshRenderedIcon();
    }

    /// <summary>Detaches the tray icon from the application and closes any active flyout window.</summary>
    public void Detach()
    {
        HideFlyout();
        DisposeFlyoutWindow();
        ClearNativeMenuIconBitmaps();
        DisposeMacStatusItem();

        VelaTheme.AccentChanged -= OnThemeOrAccentChanged;

        if (_attachedApp is not null)
        {
            _attachedApp.ActualThemeVariantChanged -= OnAppThemeChanged;
            if (_attachedApp.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                desktop.ShutdownRequested -= OnDesktopShutdownRequested;

            DisposeNativeAvaloniaTray(_attachedApp);
            _attachedApp = null;
        }
    }

    /// <summary>
    /// Links this tray icon to a <see cref="Window"/> for <see cref="CloseToTray"/>, <see cref="MinimizeToTray"/>,
    /// and window restore actions, and ensures attachment to <see cref="Application.Current"/>.
    /// </summary>
    public void AttachToWindow(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        TargetWindow = window;
        Attach(Application.Current);
    }

    /// <summary>Unlinks window lifecycle handlers without disposing this tray icon.</summary>
    public void DetachFromWindow()
    {
        TargetWindow = null;
    }

    /// <summary>Shows the Vela floating menu or popover flyout window (macOS &amp; Windows).</summary>
    public void ShowFlyout(PixelPoint? anchorPoint = null)
    {
        if (_isDisposed || OperatingSystem.IsLinux())
            return;

        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime)
            return;

        FlyoutOpening?.Invoke(this, EventArgs.Empty);

        if (_flyoutWindow is null)
        {
            _flyoutWindow = new VelaTrayFlyoutWindow();
            _flyoutWindow.Dismissed += OnFlyoutDismissed;
        }

        _flyoutWindow.Present(this, anchorPoint);
        FlyoutOpened?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Hides the Vela floating flyout window if open.</summary>
    public void HideFlyout()
    {
        if (_flyoutWindow is { IsVisible: true })
            _flyoutWindow.RequestDismiss();
    }

    /// <summary>Toggles the Vela floating flyout window open or closed.</summary>
    public void ToggleFlyout(PixelPoint? anchorPoint = null)
    {
        if (IsFlyoutOpen)
            HideFlyout();
        else
            ShowFlyout(anchorPoint);
    }

    /// <summary>Restores and activates the target window (or main desktop window).</summary>
    public void RestoreWindow()
    {
        var window = ResolveTargetWindow();
        if (window is null)
            return;

        window.ShowInTaskbar = true;
        if (!window.IsVisible)
            window.Show();
        if (window.WindowState == WindowState.Minimized)
            window.WindowState = WindowState.Normal;
        window.Activate();
    }

    /// <summary>Hides the target window to the system tray.</summary>
    public void HideWindowToTray()
    {
        var window = ResolveTargetWindow();
        if (window is null)
            return;

        window.Hide();
    }

    /// <summary>Toggles visibility of the target window between visible/active and hidden to tray.</summary>
    public void ToggleWindowVisibility()
    {
        var window = ResolveTargetWindow();
        if (window is null)
            return;

        if (window.IsVisible && window.WindowState != WindowState.Minimized)
            HideWindowToTray();
        else
            RestoreWindow();
    }

    /// <summary>
    /// Cleanly closes the target window (or shuts down the desktop application), bypassing <see cref="CloseToTray"/>.
    /// Ideal for wiring an "Exit" tray menu item.
    /// </summary>
    public void RequestRealClose()
    {
        HideFlyout();
        _allowRealClose = true;

        var window = ResolveTargetWindow();
        if (window is not null)
        {
            window.Close();
        }
        else if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }

    /// <summary>
    /// Computes the current Skia rendering specification for this tray icon.
    /// Returns the spec plus any temporary <see cref="SKBitmap"/> that the caller must dispose.
    /// </summary>
    public VelaTrayIconRenderSpec BuildRenderSpec(out SKBitmap? ownedSourceBitmap, int? pixelSizeOverride = null, bool? isDarkOverride = null)
    {
        var size = Math.Clamp(pixelSizeOverride ?? IconPixelSize, 16, 256);
        var isDark = isDarkOverride ?? IsCurrentThemeDark();
        var useTemplate = ShouldUseMacOsTemplateIcon();

        var fgColor = ResolveForegroundColor(isDark, useTemplate);
        var (accentColor, accentFg) = ResolveAccentColors(isDark);
        var (badgeBg, badgeFg) = VelaTrayIconRenderer.ResolveBadgeColors(BadgeTone, accentColor, accentFg, isDark);

        if (BadgeBrush is ISolidColorBrush solidBadge)
            badgeBg = VelaTrayIconRenderer.ToSKColor(solidBadge.Color);

        SKColor? bgFill = IconBackground is ISolidColorBrush solidBg
            ? VelaTrayIconRenderer.ToSKColor(solidBg.Color)
            : null;

        ownedSourceBitmap = null;
        var tintSource = false;

        if (IconBitmap is not null)
        {
            ownedSourceBitmap = VelaTrayIconRenderer.DecodeAvaloniaBitmap(IconBitmap);
            tintSource = IconThemeMode != VelaTrayIconThemeMode.Original;
        }
        else if (IconGeometry is not null)
        {
            ownedSourceBitmap = VelaTrayIconRenderer.RasterizeGeometry(IconGeometry, size, fgColor);
            tintSource = false;
        }

        var progressTrack = isDark
            ? new SKColor(0xFF, 0xFF, 0xFF, 0x38)
            : new SKColor(0x00, 0x00, 0x00, 0x2C);

        return new VelaTrayIconRenderSpec
        {
            PixelSize = size,
            PathData = IconPathData,
            SourceBitmap = ownedSourceBitmap,
            TintSourceBitmap = tintSource,
            ForegroundColor = VelaTrayIconRenderer.ToSKColor(fgColor),
            BackgroundFillColor = bgFill,
            CornerRadiusFraction = 0.26f,
            PaddingFraction = 0.12f,
            BadgeVariant = BadgeVariant,
            BadgeCount = BadgeCount,
            BadgeBackgroundColor = badgeBg,
            BadgeForegroundColor = badgeFg,
            Progress = Progress.HasValue ? Math.Clamp(Progress.Value, 0.0, 1.0) : null,
            ProgressColor = VelaTrayIconRenderer.ToSKColor(accentColor),
            ProgressTrackColor = progressTrack,
        };
    }

    /// <summary>Renders the current tray icon configuration into PNG bytes (works in headless tests and WASM).</summary>
    public byte[] RenderIconPng(int? pixelSizeOverride = null, bool? isDarkOverride = null)
    {
        var spec = BuildRenderSpec(out var owned, pixelSizeOverride, isDarkOverride);
        try
        {
            return VelaTrayIconRenderer.RenderPng(in spec);
        }
        finally
        {
            owned?.Dispose();
        }
    }

    /// <summary>Renders the current tray icon configuration into an Avalonia <see cref="Bitmap"/> for live previews.</summary>
    public Bitmap? RenderPreviewBitmap(int? pixelSizeOverride = null, bool? isDarkOverride = null)
    {
        var spec = BuildRenderSpec(out var owned, pixelSizeOverride, isDarkOverride);
        try
        {
            return VelaTrayIconRenderer.RenderAvaloniaBitmap(in spec);
        }
        finally
        {
            owned?.Dispose();
        }
    }

    /// <summary>Determines whether macOS template-icon rendering should be enabled for the current visual configuration.</summary>
    public bool ShouldUseMacOsTemplateIcon(bool? isMacOsOverride = null)
    {
        var isMac = isMacOsOverride ?? OperatingSystem.IsMacOS();
        if (!isMac)
            return false;

        if (IconThemeMode == VelaTrayIconThemeMode.Template)
            return true;

        if (IconThemeMode != VelaTrayIconThemeMode.Auto)
            return false;

        var hasColoredBadge = BadgeVariant == VelaTrayBadgeVariant.Dot
            || (BadgeVariant == VelaTrayBadgeVariant.Count && BadgeCount > 0);
        var hasColoredOverlay = hasColoredBadge
            || Progress.HasValue
            || IconBackground is not null
            || IconBrush is not null
            || IconBitmap is not null
            || Icon is not null;

        return !hasColoredOverlay;
    }

    internal static VelaTrayPresentation ResolveEffectivePresentation(
        VelaTrayPresentation requested,
        bool isLinux)
    {
        if (isLinux && requested is VelaTrayPresentation.VelaMenu or VelaTrayPresentation.VelaPopover)
            return VelaTrayPresentation.Native;
        return requested;
    }

    internal static bool ShouldInterceptWindowClose(
        bool closeToTray,
        bool allowRealClose,
        bool isProgrammatic,
        WindowCloseReason reason)
    {
        if (!closeToTray || allowRealClose || isProgrammatic)
            return false;

        return reason is not WindowCloseReason.ApplicationShutdown
            and not WindowCloseReason.OSShutdown;
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        MenuItems.CollectionChanged -= OnMenuItemsCollectionChanged;
        foreach (var item in MenuItems)
            item.PropertyChanged -= OnMenuItemPropertyChanged;

        BindWindowEvents(null);
        Detach();
        GC.SuppressFinalize(this);
    }

    private void OnNativeTrayClicked(object? sender, EventArgs e)
    {
        Clicked?.Invoke(this, EventArgs.Empty);

        // Guard against clicking the tray icon while flyout is closing via Deactivated
        var recentlyDismissed = Environment.TickCount64 - _lastFlyoutDismissTicks < 220;

        if (ClickBehavior == VelaTrayClickBehavior.ToggleMainWindow)
        {
            ToggleWindowVisibility();
            InvokeCommand();
            return;
        }

        if (ClickBehavior == VelaTrayClickBehavior.RestoreMainWindow)
        {
            RestoreWindow();
            InvokeCommand();
            return;
        }

        switch (EffectivePresentation)
        {
            case VelaTrayPresentation.VelaMenu:
            case VelaTrayPresentation.VelaPopover:
                if (IsFlyoutOpen)
                    HideFlyout();
                else if (!recentlyDismissed)
                    ShowFlyout();
                break;

            case VelaTrayPresentation.CommandOnly:
                InvokeCommand();
                break;

            case VelaTrayPresentation.Native:
                InvokeCommand();
                break;
        }
    }

    private void InvokeCommand()
    {
        if (Command?.CanExecute(CommandParameter) == true)
            Command.Execute(CommandParameter);
    }

    private void OnFlyoutDismissed(object? sender, EventArgs e)
    {
        _lastFlyoutDismissTicks = Environment.TickCount64;
        FlyoutClosed?.Invoke(this, EventArgs.Empty);
    }

    private void DisposeFlyoutWindow()
    {
        if (_flyoutWindow is null)
            return;

        _flyoutWindow.Dismissed -= OnFlyoutDismissed;
        _flyoutWindow.DetachHostedContent();
        _flyoutWindow.Close();
        _flyoutWindow = null;
    }

    private void EnsurePlatformTrayBackend(Application app)
    {
        if (app.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime)
            return;

        // On macOS, Avalonia's TrayIcon.Clicked is never raised (only NativeMenu works).
        // Use the native AppKit NSStatusItem bridge for interactive modes so clicks respond immediately.
        var effective = EffectivePresentation;
        var useMacStatusItem = OperatingSystem.IsMacOS() && effective != VelaTrayPresentation.Native;
        if (useMacStatusItem)
        {
            DisposeNativeAvaloniaTray(app);
            if (_macStatusItem is null)
            {
                var statusItem = new VelaMacOsStatusItem();
                if (statusItem.IsCreated)
                {
                    statusItem.Clicked += OnNativeTrayClicked;
                    _macStatusItem = statusItem;
                }
                else
                {
                    statusItem.Dispose();
                }
            }
            return;
        }

        DisposeMacStatusItem();
        if (_nativeTray is null)
        {
            try
            {
                _nativeTray = new TrayIcon
                {
                    IsVisible = IsVisible,
                    ToolTipText = ToolTipText,
                };
                _nativeTray.Clicked += OnNativeTrayClicked;

                // Assign Menu BEFORE adding to TrayIcon.Icons: Avalonia.Native caches the initial
                // NativeMenu reference on attach and throws if replaced with a different instance later.
                if (effective == VelaTrayPresentation.Native)
                {
                    _nativeMenu = new NativeMenu();
                    PopulateNativeMenu(_nativeMenu);
                    _nativeTray.Menu = _nativeMenu;
                }

                var icons = TrayIcon.GetIcons(app);
                if (icons is null)
                {
                    icons = new TrayIcons();
                    TrayIcon.SetIcons(app, icons);
                }
                icons.Add(_nativeTray);
            }
            catch
            {
                _nativeTray?.Dispose();
                _nativeTray = null;
                _nativeMenu = null;
            }
        }
    }

    private void DisposeNativeAvaloniaTray(Application app)
    {
        if (_nativeTray is null)
            return;

        _nativeTray.Clicked -= OnNativeTrayClicked;
        var icons = TrayIcon.GetIcons(app);
        icons?.Remove(_nativeTray);
        _nativeTray.Dispose();
        _nativeTray = null;
        _nativeMenu = null;
    }

    private void DisposeMacStatusItem()
    {
        if (_macStatusItem is null)
            return;

        _macStatusItem.Clicked -= OnNativeTrayClicked;
        _macStatusItem.Dispose();
        _macStatusItem = null;
    }

    private void SyncNativeTrayState()
    {
        if (_nativeTray is not null)
        {
            _nativeTray.ToolTipText = ToolTipText;
            _nativeTray.IsVisible = IsVisible;
        }

        if (_macStatusItem is not null)
        {
            _macStatusItem.SetToolTip(ToolTipText);
            _macStatusItem.SetIsVisible(IsVisible);
        }
    }

    private void SyncPresentation()
    {
        var createdNativeNow = false;
        if (_attachedApp is not null)
        {
            var prevMac = _macStatusItem is not null;
            var prevNative = _nativeTray is not null;
            EnsurePlatformTrayBackend(_attachedApp);

            createdNativeNow = !prevNative && _nativeTray is not null;

            // If we swapped backends (e.g. macOS Native <-> VelaMenu), re-push icon & tooltip state
            if (prevMac != (_macStatusItem is not null) || prevNative != (_nativeTray is not null))
            {
                SyncNativeTrayState();
                RefreshRenderedIcon();
            }
        }

        if (_nativeTray is null)
            return;

        if (EffectivePresentation == VelaTrayPresentation.Native)
        {
            HideFlyout();
            if (createdNativeNow)
                return;

            if (_nativeMenu is null)
            {
                _nativeMenu = new NativeMenu();
                PopulateNativeMenu(_nativeMenu);
                _nativeTray.Menu = _nativeMenu;
            }
            else
            {
                // Mutate the existing NativeMenu instance in-place so Avalonia.Native updates safely.
                PopulateNativeMenu(_nativeMenu);
            }
        }
        else
        {
            _nativeTray.Menu = null;
            _nativeMenu = null;
            ClearNativeMenuIconBitmaps();
        }
    }

    private void PopulateNativeMenu(NativeMenu menu)
    {
        menu.Items.Clear();
        ClearNativeMenuIconBitmaps();

        var isDark = IsCurrentThemeDark();
        var defaultIconColor = isDark ? Color.FromRgb(0xE8, 0xE8, 0xE8) : Color.FromRgb(0x22, 0x22, 0x22);
        var dangerIconColor = isDark ? Color.FromRgb(0xF0, 0x73, 0x8F) : Color.FromRgb(0xBB, 0x2C, 0x54);

        foreach (var item in MenuItems)
        {
            if (!item.IsVisible)
                continue;

            if (item.IsSeparator)
            {
                menu.Items.Add(new NativeMenuItemSeparator());
                continue;
            }

            var nativeItem = new NativeMenuItem
            {
                Header = item.Header,
                IsEnabled = item.IsEnabled,
                ToggleType = item.IsChecked ? MenuItemToggleType.CheckBox : MenuItemToggleType.None,
                IsChecked = item.IsChecked,
                Gesture = TryParseKeyGesture(item.Shortcut),
            };

            if (item.Icon is not null)
            {
                var bmp = VelaTrayIconRenderer.RenderMenuItemBitmap(
                    item.Icon,
                    item.IsDestructive ? dangerIconColor : defaultIconColor,
                    32);
                if (bmp is not null)
                {
                    _nativeMenuIconBitmaps.Add(bmp);
                    nativeItem.Icon = bmp;
                }
            }

            var captured = item;
            nativeItem.Click += (_, _) => captured.Invoke();
            menu.Items.Add(nativeItem);
        }
    }

    private void ClearNativeMenuIconBitmaps()
    {
        foreach (var bmp in _nativeMenuIconBitmaps)
            bmp.Dispose();
        _nativeMenuIconBitmaps.Clear();
    }

    private void ScheduleIconRefresh()
    {
        if (_isDisposed)
            return;

        if (_attachedApp is null && Application.Current is null)
        {
            RefreshRenderedIcon();
            return;
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            if (_iconRefreshQueued)
                return;

            _iconRefreshQueued = true;
            Dispatcher.UIThread.Post(() =>
            {
                _iconRefreshQueued = false;
                if (!_isDisposed)
                    RefreshRenderedIcon();
            }, DispatcherPriority.Background);
        }
        else
        {
            RefreshRenderedIcon();
        }
    }

    private void RefreshRenderedIcon()
    {
        if (_isDisposed)
            return;

        var useTemplate = ShouldUseMacOsTemplateIcon();

        if (_macStatusItem is { IsCreated: true })
        {
            byte[]? pngBytes = null;
            if (Icon is not null
                && BadgeVariant == VelaTrayBadgeVariant.None
                && !Progress.HasValue
                && IconBackground is null)
            {
                using var ms = new MemoryStream();
                Icon.Save(ms);
                pngBytes = ms.ToArray();
            }
            else
            {
                pngBytes = RenderIconPng();
            }

            if (pngBytes is { Length: > 0 })
                _macStatusItem.SetIconPng(pngBytes, useTemplate);
        }

        if (_nativeTray is not null)
        {
            MacOSProperties.SetIsTemplateIcon(_nativeTray, useTemplate);

            if (Icon is not null
                && BadgeVariant == VelaTrayBadgeVariant.None
                && !Progress.HasValue
                && IconBackground is null)
            {
                _nativeTray.Icon = Icon;
            }
            else
            {
                var spec = BuildRenderSpec(out var owned);
                try
                {
                    var synthesized = VelaTrayIconRenderer.RenderWindowIcon(in spec);
                    if (synthesized is not null)
                        _nativeTray.Icon = synthesized;
                }
                finally
                {
                    owned?.Dispose();
                }
            }
        }

        RenderedIconChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnThemeOrAccentChanged(object? sender, VelaAccent accent)
    {
        ScheduleIconRefresh();
    }

    private void OnAppThemeChanged(object? sender, EventArgs e)
    {
        ScheduleIconRefresh();
        if (EffectivePresentation == VelaTrayPresentation.Native)
            SyncPresentation();
    }

    private void OnDesktopShutdownRequested(object? sender, ShutdownRequestedEventArgs e)
    {
        _allowRealClose = true;
        HideFlyout();
    }

    private void OnMenuItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (VelaMenuItem oldItem in e.OldItems)
            {
                oldItem.PropertyChanged -= OnMenuItemPropertyChanged;
                LogicalChildren.Remove(oldItem);
            }
        }
        if (e.NewItems is not null)
        {
            foreach (VelaMenuItem newItem in e.NewItems)
            {
                newItem.PropertyChanged += OnMenuItemPropertyChanged;
                if (!LogicalChildren.Contains(newItem))
                    LogicalChildren.Add(newItem);
            }
        }

        if (EffectivePresentation == VelaTrayPresentation.Native)
            SyncPresentation();
    }

    private void OnMenuItemPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (EffectivePresentation == VelaTrayPresentation.Native)
            SyncPresentation();
    }

    private void BindWindowEvents(Window? nextWindow)
    {
        if (_linkedWindow is not null)
        {
            _linkedWindow.Closing -= OnLinkedWindowClosing;
            _linkedWindow.PropertyChanged -= OnLinkedWindowPropertyChanged;
            _linkedWindow.Closed -= OnLinkedWindowClosed;
        }

        _linkedWindow = nextWindow;
        _allowRealClose = false;

        if (_linkedWindow is not null)
        {
            _linkedWindow.Closing += OnLinkedWindowClosing;
            _linkedWindow.PropertyChanged += OnLinkedWindowPropertyChanged;
            _linkedWindow.Closed += OnLinkedWindowClosed;
        }
    }

    private void OnLinkedWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        if (ShouldInterceptWindowClose(CloseToTray, _allowRealClose, e.IsProgrammatic, e.CloseReason))
        {
            e.Cancel = true;
            HideWindowToTray();
        }
    }

    private void OnLinkedWindowPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (MinimizeToTray
            && e.Property == Window.WindowStateProperty
            && sender is Window window
            && window.WindowState == WindowState.Minimized)
        {
            HideWindowToTray();
        }
    }

    private void OnLinkedWindowClosed(object? sender, EventArgs e)
    {
        HideFlyout();
        if (_allowRealClose)
            Detach();
    }

    private Window? ResolveTargetWindow()
    {
        if (TargetWindow is not null)
            return TargetWindow;

        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            return desktop.MainWindow;

        return null;
    }

    private bool IsCurrentThemeDark()
    {
        var variant = _attachedApp?.ActualThemeVariant ?? Application.Current?.ActualThemeVariant;
        return variant == ThemeVariant.Dark;
    }

    private Color ResolveForegroundColor(bool isDark, bool useTemplate)
    {
        if (useTemplate)
            return Colors.Black;

        if (IconBrush is ISolidColorBrush solid)
            return solid.Color;

        return IconThemeMode switch
        {
            VelaTrayIconThemeMode.MonochromeLight => Color.FromRgb(0xF5, 0xF5, 0xF5),
            VelaTrayIconThemeMode.MonochromeDark => Color.FromRgb(0x11, 0x11, 0x11),
            VelaTrayIconThemeMode.Template => Colors.Black,
            _ => isDark ? Color.FromRgb(0xF5, 0xF5, 0xF5) : Color.FromRgb(0x11, 0x11, 0x11),
        };
    }

    private (Color Accent, Color Foreground) ResolveAccentColors(bool isDark)
    {
        var app = _attachedApp ?? Application.Current;
        var theme = app?.Styles.OfType<VelaTheme>().FirstOrDefault();
        var palette = VelaAccentPalettes.Get(theme?.Accent ?? VelaAccent.Violet, isDark);
        return (palette.Accent, palette.AccentForeground);
    }

    internal static KeyGesture? TryParseKeyGesture(string? shortcut)
    {
        if (string.IsNullOrWhiteSpace(shortcut))
            return null;

        try
        {
            return KeyGesture.Parse(shortcut);
        }
        catch
        {
            return null;
        }
    }
}
