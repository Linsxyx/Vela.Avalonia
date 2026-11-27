using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Chrome;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Threading;

namespace Vela.Avalonia.Controls;

/// <summary>
/// Built-in window background material mode for <see cref="VelaWindow"/>.
/// </summary>
public enum VelaWindowMaterial
{
    /// <summary>Clean opaque theme background (default).</summary>
    Solid,

    /// <summary>Built-in SkSL ambient shader background powered by <see cref="VelaShaderBackground"/>.</summary>
    Shader,

    /// <summary>OS-native Mica transparency with graceful fallback.</summary>
    Mica,

    /// <summary>OS-native Acrylic / Blur transparency with graceful fallback.</summary>
    AcrylicBlur,
}

/// <summary>
/// Controls which platform style the window caption buttons follow.
/// </summary>
public enum VelaWindowChromeMode
{
    /// <summary>Follow the host OS (native traffic lights on macOS, right-aligned monochrome caption buttons elsewhere).</summary>
    Auto,

    /// <summary>Right-aligned Windows/Linux monochrome caption buttons.</summary>
    Windows,

    /// <summary>Left-aligned macOS traffic-light style caption buttons.</summary>
    MacOS,
}

/// <summary>
/// Controls title bar visibility behavior when <see cref="Window.WindowState"/> is <see cref="WindowState.FullScreen"/>.
/// </summary>
public enum VelaTitleBarVisibilityMode
{
    /// <summary>Keep <see cref="VelaWindow.IsTitleBarVisible"/> unchanged in fullscreen.</summary>
    Unchanged,

    /// <summary>Always show the title bar in fullscreen.</summary>
    Visible,

    /// <summary>Always hide the title bar in fullscreen.</summary>
    Hidden,

    /// <summary>Automatically hide the title bar in fullscreen and reveal it when the pointer moves to the top edge.</summary>
    AutoHidden,
}

/// <summary>
/// Optional modern frameless desktop window shell for Vela.Avalonia:
/// - Opt-in replacement for <see cref="Window"/> with zero forced coupling
/// - Seamless native macOS traffic-light integration via <see cref="Window.ExtendClientAreaToDecorationsHint"/>
/// - Restrained custom monochrome Minimize / Maximize / FullScreen / Pin / Close caption buttons
/// - Customizable top bar (left/center/right slots or full <see cref="TitleBarContent"/> + overlay mode)
/// - Customizable bottom bar (left/center/right slots or full <see cref="BottomBarContent"/>)
/// - Built-in material background (<see cref="BackgroundMaterial"/>) and replaceable full-window <see cref="BackgroundContent"/> layer
/// - Top-level <see cref="Hosts"/> layer for global dialogs, drawers, and toasts above chrome
/// </summary>
[PseudoClasses(
    ":macos",
    ":macos-chrome",
    ":maximized",
    ":fullscreen",
    ":topmost",
    ":custom-chrome",
    ":extend-content",
    ":has-background-content",
    ":material-background",
    ":native-material",
    ":shader-background",
    ":no-titlebar-bg",
    ":no-titlebar-divider",
    ":no-bottombar-divider")]
public class VelaWindow : Window
{
    public static readonly StyledProperty<bool> UseCustomTitleBarProperty =
        AvaloniaProperty.Register<VelaWindow, bool>(nameof(UseCustomTitleBar), true);

    public static readonly StyledProperty<bool> IsTitleBarVisibleProperty =
        AvaloniaProperty.Register<VelaWindow, bool>(nameof(IsTitleBarVisible), true);

    public static readonly StyledProperty<double> TitleBarHeightProperty =
        AvaloniaProperty.Register<VelaWindow, double>(nameof(TitleBarHeight), 38.0);

    public static readonly StyledProperty<bool> ShowTitleProperty =
        AvaloniaProperty.Register<VelaWindow, bool>(nameof(ShowTitle), true);

    public static readonly StyledProperty<bool> ShowTitleBarDividerProperty =
        AvaloniaProperty.Register<VelaWindow, bool>(nameof(ShowTitleBarDivider), true);

    public static readonly StyledProperty<bool> ExtendContentIntoTitleBarProperty =
        AvaloniaProperty.Register<VelaWindow, bool>(nameof(ExtendContentIntoTitleBar), false);

    public static readonly StyledProperty<IBrush?> TitleBarBackgroundProperty =
        AvaloniaProperty.Register<VelaWindow, IBrush?>(nameof(TitleBarBackground));

    public static readonly StyledProperty<IBrush?> TitleBarBorderBrushProperty =
        AvaloniaProperty.Register<VelaWindow, IBrush?>(nameof(TitleBarBorderBrush));

    public static readonly StyledProperty<object?> TitleBarContentProperty =
        AvaloniaProperty.Register<VelaWindow, object?>(nameof(TitleBarContent));

    public static readonly StyledProperty<object?> TitleBarLeftContentProperty =
        AvaloniaProperty.Register<VelaWindow, object?>(nameof(TitleBarLeftContent));

    public static readonly StyledProperty<object?> TitleBarCenterContentProperty =
        AvaloniaProperty.Register<VelaWindow, object?>(nameof(TitleBarCenterContent));

    public static readonly StyledProperty<object?> TitleBarRightContentProperty =
        AvaloniaProperty.Register<VelaWindow, object?>(nameof(TitleBarRightContent));

    public static readonly StyledProperty<bool> IsBottomBarVisibleProperty =
        AvaloniaProperty.Register<VelaWindow, bool>(nameof(IsBottomBarVisible), true);

    public static readonly StyledProperty<double> BottomBarHeightProperty =
        AvaloniaProperty.Register<VelaWindow, double>(nameof(BottomBarHeight), 30.0);

    public static readonly StyledProperty<bool> ShowBottomBarDividerProperty =
        AvaloniaProperty.Register<VelaWindow, bool>(nameof(ShowBottomBarDivider), true);

    public static readonly StyledProperty<IBrush?> BottomBarBackgroundProperty =
        AvaloniaProperty.Register<VelaWindow, IBrush?>(nameof(BottomBarBackground));

    public static readonly StyledProperty<IBrush?> BottomBarBorderBrushProperty =
        AvaloniaProperty.Register<VelaWindow, IBrush?>(nameof(BottomBarBorderBrush));

    public static readonly StyledProperty<Thickness> BottomBarPaddingProperty =
        AvaloniaProperty.Register<VelaWindow, Thickness>(nameof(BottomBarPadding), new Thickness(14, 0));

    public static readonly StyledProperty<object?> BottomBarContentProperty =
        AvaloniaProperty.Register<VelaWindow, object?>(nameof(BottomBarContent));

    public static readonly StyledProperty<object?> BottomBarLeftContentProperty =
        AvaloniaProperty.Register<VelaWindow, object?>(nameof(BottomBarLeftContent));

    public static readonly StyledProperty<object?> BottomBarCenterContentProperty =
        AvaloniaProperty.Register<VelaWindow, object?>(nameof(BottomBarCenterContent));

    public static readonly StyledProperty<object?> BottomBarRightContentProperty =
        AvaloniaProperty.Register<VelaWindow, object?>(nameof(BottomBarRightContent));

    public static readonly StyledProperty<object?> BackgroundContentProperty =
        AvaloniaProperty.Register<VelaWindow, object?>(nameof(BackgroundContent));

    public static readonly StyledProperty<double> BackgroundContentOpacityProperty =
        AvaloniaProperty.Register<VelaWindow, double>(nameof(BackgroundContentOpacity), 1.0);

    public static readonly StyledProperty<VelaWindowMaterial> BackgroundMaterialProperty =
        AvaloniaProperty.Register<VelaWindow, VelaWindowMaterial>(nameof(BackgroundMaterial), VelaWindowMaterial.Solid);

    public static readonly StyledProperty<VelaShaderBackgroundPreset> BackgroundPresetProperty =
        AvaloniaProperty.Register<VelaWindow, VelaShaderBackgroundPreset>(nameof(BackgroundPreset), VelaShaderBackgroundPreset.Mesh);

    public static readonly StyledProperty<bool> BackgroundAnimatedProperty =
        AvaloniaProperty.Register<VelaWindow, bool>(nameof(BackgroundAnimated), false);

    public static readonly StyledProperty<double> BackgroundIntensityProperty =
        AvaloniaProperty.Register<VelaWindow, double>(nameof(BackgroundIntensity), 0.72);

    public static readonly StyledProperty<double> BackgroundSpeedProperty =
        AvaloniaProperty.Register<VelaWindow, double>(nameof(BackgroundSpeed), 1.0);

    public static readonly StyledProperty<string?> BackgroundShaderCodeProperty =
        AvaloniaProperty.Register<VelaWindow, string?>(nameof(BackgroundShaderCode));

    public static readonly StyledProperty<string?> BackgroundShaderFileProperty =
        AvaloniaProperty.Register<VelaWindow, string?>(nameof(BackgroundShaderFile));

    public static readonly StyledProperty<double> BackgroundTransitionDurationProperty =
        AvaloniaProperty.Register<VelaWindow, double>(nameof(BackgroundTransitionDuration), 0.65);

    public static readonly StyledProperty<bool> BackgroundForceSoftwareRenderingProperty =
        AvaloniaProperty.Register<VelaWindow, bool>(nameof(BackgroundForceSoftwareRendering), false);

    public static readonly StyledProperty<bool> ShowTitleBarBackgroundProperty =
        AvaloniaProperty.Register<VelaWindow, bool>(nameof(ShowTitleBarBackground), true);

    public static readonly StyledProperty<CornerRadius> RootCornerRadiusProperty =
        AvaloniaProperty.Register<VelaWindow, CornerRadius>(nameof(RootCornerRadius));

    public static readonly StyledProperty<global::Avalonia.Controls.Controls> HostsProperty =
        AvaloniaProperty.Register<VelaWindow, global::Avalonia.Controls.Controls>(nameof(Hosts));

    public static readonly StyledProperty<VelaWindowChromeMode> WindowChromeModeProperty =
        AvaloniaProperty.Register<VelaWindow, VelaWindowChromeMode>(nameof(WindowChromeMode), VelaWindowChromeMode.Auto);

    public static readonly StyledProperty<bool> CanPinProperty =
        AvaloniaProperty.Register<VelaWindow, bool>(nameof(CanPin), false);

    public static readonly StyledProperty<bool> CanFullScreenProperty =
        AvaloniaProperty.Register<VelaWindow, bool>(nameof(CanFullScreen), false);

    public static readonly StyledProperty<VelaTitleBarVisibilityMode> TitleBarVisibilityOnFullScreenProperty =
        AvaloniaProperty.Register<VelaWindow, VelaTitleBarVisibilityMode>(
            nameof(TitleBarVisibilityOnFullScreen),
            VelaTitleBarVisibilityMode.AutoHidden);

    public static readonly StyledProperty<int> TitleBarAutoHideDelayProperty =
        AvaloniaProperty.Register<VelaWindow, int>(
            nameof(TitleBarAutoHideDelay),
            1000,
            coerce: (_, v) => Math.Max(0, v));

    public static readonly StyledProperty<int> TitleBarAutoShowDelayProperty =
        AvaloniaProperty.Register<VelaWindow, int>(
            nameof(TitleBarAutoShowDelay),
            300,
            coerce: (_, v) => Math.Max(0, v));

    public static readonly StyledProperty<double> MaxWidthScreenRatioProperty =
        AvaloniaProperty.Register<VelaWindow, double>(nameof(MaxWidthScreenRatio), double.NaN);

    public static readonly StyledProperty<double> MaxHeightScreenRatioProperty =
        AvaloniaProperty.Register<VelaWindow, double>(nameof(MaxHeightScreenRatio), double.NaN);

    public static readonly StyledProperty<VelaTrayIcon?> TrayIconProperty =
        AvaloniaProperty.Register<VelaWindow, VelaTrayIcon?>(nameof(TrayIcon));

    public static readonly DirectProperty<VelaWindow, Thickness> TitleBarPaddingProperty =
        AvaloniaProperty.RegisterDirect<VelaWindow, Thickness>(nameof(TitleBarPadding), o => o.TitleBarPadding);

    public static readonly DirectProperty<VelaWindow, bool> ShowCustomCaptionButtonsProperty =
        AvaloniaProperty.RegisterDirect<VelaWindow, bool>(nameof(ShowCustomCaptionButtons), o => o.ShowCustomCaptionButtons);

    public static readonly DirectProperty<VelaWindow, bool> ShowDefaultTitleProperty =
        AvaloniaProperty.RegisterDirect<VelaWindow, bool>(nameof(ShowDefaultTitle), o => o.ShowDefaultTitle);

    public static readonly DirectProperty<VelaWindow, bool> EffectiveTitleBarVisibleProperty =
        AvaloniaProperty.RegisterDirect<VelaWindow, bool>(nameof(EffectiveTitleBarVisible), o => o.EffectiveTitleBarVisible);

    public static readonly DirectProperty<VelaWindow, bool> EffectiveBottomBarVisibleProperty =
        AvaloniaProperty.RegisterDirect<VelaWindow, bool>(nameof(EffectiveBottomBarVisible), o => o.EffectiveBottomBarVisible);

    public static readonly DirectProperty<VelaWindow, bool> IsShaderBackgroundActiveProperty =
        AvaloniaProperty.RegisterDirect<VelaWindow, bool>(nameof(IsShaderBackgroundActive), o => o.IsShaderBackgroundActive);

    public static readonly DirectProperty<VelaWindow, CornerRadius> EffectiveRootCornerRadiusProperty =
        AvaloniaProperty.RegisterDirect<VelaWindow, CornerRadius>(nameof(EffectiveRootCornerRadius), o => o.EffectiveRootCornerRadius);

    public static readonly DirectProperty<VelaWindow, WindowState> PreviousVisibleWindowStateProperty =
        AvaloniaProperty.RegisterDirect<VelaWindow, WindowState>(nameof(PreviousVisibleWindowState), o => o.PreviousVisibleWindowState);

    public bool UseCustomTitleBar { get => GetValue(UseCustomTitleBarProperty); set => SetValue(UseCustomTitleBarProperty, value); }
    public bool IsTitleBarVisible { get => GetValue(IsTitleBarVisibleProperty); set => SetValue(IsTitleBarVisibleProperty, value); }
    public double TitleBarHeight { get => GetValue(TitleBarHeightProperty); set => SetValue(TitleBarHeightProperty, value); }
    public bool ShowTitle { get => GetValue(ShowTitleProperty); set => SetValue(ShowTitleProperty, value); }
    public bool ShowTitleBarDivider { get => GetValue(ShowTitleBarDividerProperty); set => SetValue(ShowTitleBarDividerProperty, value); }
    public bool ExtendContentIntoTitleBar { get => GetValue(ExtendContentIntoTitleBarProperty); set => SetValue(ExtendContentIntoTitleBarProperty, value); }
    public IBrush? TitleBarBackground { get => GetValue(TitleBarBackgroundProperty); set => SetValue(TitleBarBackgroundProperty, value); }
    public IBrush? TitleBarBorderBrush { get => GetValue(TitleBarBorderBrushProperty); set => SetValue(TitleBarBorderBrushProperty, value); }
    public object? TitleBarContent { get => GetValue(TitleBarContentProperty); set => SetValue(TitleBarContentProperty, value); }
    public object? TitleBarLeftContent { get => GetValue(TitleBarLeftContentProperty); set => SetValue(TitleBarLeftContentProperty, value); }
    public object? TitleBarCenterContent { get => GetValue(TitleBarCenterContentProperty); set => SetValue(TitleBarCenterContentProperty, value); }
    public object? TitleBarRightContent { get => GetValue(TitleBarRightContentProperty); set => SetValue(TitleBarRightContentProperty, value); }

    public bool IsBottomBarVisible { get => GetValue(IsBottomBarVisibleProperty); set => SetValue(IsBottomBarVisibleProperty, value); }
    public double BottomBarHeight { get => GetValue(BottomBarHeightProperty); set => SetValue(BottomBarHeightProperty, value); }
    public bool ShowBottomBarDivider { get => GetValue(ShowBottomBarDividerProperty); set => SetValue(ShowBottomBarDividerProperty, value); }
    public IBrush? BottomBarBackground { get => GetValue(BottomBarBackgroundProperty); set => SetValue(BottomBarBackgroundProperty, value); }
    public IBrush? BottomBarBorderBrush { get => GetValue(BottomBarBorderBrushProperty); set => SetValue(BottomBarBorderBrushProperty, value); }
    public Thickness BottomBarPadding { get => GetValue(BottomBarPaddingProperty); set => SetValue(BottomBarPaddingProperty, value); }
    public object? BottomBarContent { get => GetValue(BottomBarContentProperty); set => SetValue(BottomBarContentProperty, value); }
    public object? BottomBarLeftContent { get => GetValue(BottomBarLeftContentProperty); set => SetValue(BottomBarLeftContentProperty, value); }
    public object? BottomBarCenterContent { get => GetValue(BottomBarCenterContentProperty); set => SetValue(BottomBarCenterContentProperty, value); }
    public object? BottomBarRightContent { get => GetValue(BottomBarRightContentProperty); set => SetValue(BottomBarRightContentProperty, value); }

    public object? BackgroundContent { get => GetValue(BackgroundContentProperty); set => SetValue(BackgroundContentProperty, value); }
    public double BackgroundContentOpacity { get => GetValue(BackgroundContentOpacityProperty); set => SetValue(BackgroundContentOpacityProperty, value); }
    public VelaWindowMaterial BackgroundMaterial { get => GetValue(BackgroundMaterialProperty); set => SetValue(BackgroundMaterialProperty, value); }
    public VelaShaderBackgroundPreset BackgroundPreset { get => GetValue(BackgroundPresetProperty); set => SetValue(BackgroundPresetProperty, value); }
    public bool BackgroundAnimated { get => GetValue(BackgroundAnimatedProperty); set => SetValue(BackgroundAnimatedProperty, value); }
    public double BackgroundIntensity { get => GetValue(BackgroundIntensityProperty); set => SetValue(BackgroundIntensityProperty, value); }
    public double BackgroundSpeed { get => GetValue(BackgroundSpeedProperty); set => SetValue(BackgroundSpeedProperty, value); }
    public string? BackgroundShaderCode { get => GetValue(BackgroundShaderCodeProperty); set => SetValue(BackgroundShaderCodeProperty, value); }
    public string? BackgroundShaderFile { get => GetValue(BackgroundShaderFileProperty); set => SetValue(BackgroundShaderFileProperty, value); }
    public double BackgroundTransitionDuration { get => GetValue(BackgroundTransitionDurationProperty); set => SetValue(BackgroundTransitionDurationProperty, value); }
    public bool BackgroundForceSoftwareRendering { get => GetValue(BackgroundForceSoftwareRenderingProperty); set => SetValue(BackgroundForceSoftwareRenderingProperty, value); }
    public bool ShowTitleBarBackground { get => GetValue(ShowTitleBarBackgroundProperty); set => SetValue(ShowTitleBarBackgroundProperty, value); }
    public CornerRadius RootCornerRadius { get => GetValue(RootCornerRadiusProperty); set => SetValue(RootCornerRadiusProperty, value); }
    public global::Avalonia.Controls.Controls Hosts { get => GetValue(HostsProperty); set => SetValue(HostsProperty, value); }
    public VelaWindowChromeMode WindowChromeMode { get => GetValue(WindowChromeModeProperty); set => SetValue(WindowChromeModeProperty, value); }
    public bool CanPin { get => GetValue(CanPinProperty); set => SetValue(CanPinProperty, value); }
    public bool CanFullScreen { get => GetValue(CanFullScreenProperty); set => SetValue(CanFullScreenProperty, value); }
    public VelaTitleBarVisibilityMode TitleBarVisibilityOnFullScreen { get => GetValue(TitleBarVisibilityOnFullScreenProperty); set => SetValue(TitleBarVisibilityOnFullScreenProperty, value); }
    public int TitleBarAutoHideDelay { get => GetValue(TitleBarAutoHideDelayProperty); set => SetValue(TitleBarAutoHideDelayProperty, value); }
    public int TitleBarAutoShowDelay { get => GetValue(TitleBarAutoShowDelayProperty); set => SetValue(TitleBarAutoShowDelayProperty, value); }
    public double MaxWidthScreenRatio { get => GetValue(MaxWidthScreenRatioProperty); set => SetValue(MaxWidthScreenRatioProperty, value); }
    public double MaxHeightScreenRatio { get => GetValue(MaxHeightScreenRatioProperty); set => SetValue(MaxHeightScreenRatioProperty, value); }
    public VelaTrayIcon? TrayIcon { get => GetValue(TrayIconProperty); set => SetValue(TrayIconProperty, value); }

    private Thickness _titleBarPadding;
    public Thickness TitleBarPadding
    {
        get => _titleBarPadding;
        private set => SetAndRaise(TitleBarPaddingProperty, ref _titleBarPadding, value);
    }

    private bool _showCustomCaptionButtons;
    public bool ShowCustomCaptionButtons
    {
        get => _showCustomCaptionButtons;
        private set => SetAndRaise(ShowCustomCaptionButtonsProperty, ref _showCustomCaptionButtons, value);
    }

    private bool _showDefaultTitle = true;
    public bool ShowDefaultTitle
    {
        get => _showDefaultTitle;
        private set => SetAndRaise(ShowDefaultTitleProperty, ref _showDefaultTitle, value);
    }

    private bool _effectiveTitleBarVisible = true;
    public bool EffectiveTitleBarVisible
    {
        get => _effectiveTitleBarVisible;
        private set => SetAndRaise(EffectiveTitleBarVisibleProperty, ref _effectiveTitleBarVisible, value);
    }

    private bool _effectiveBottomBarVisible;
    public bool EffectiveBottomBarVisible
    {
        get => _effectiveBottomBarVisible;
        private set => SetAndRaise(EffectiveBottomBarVisibleProperty, ref _effectiveBottomBarVisible, value);
    }

    private bool _isShaderBackgroundActive;
    public bool IsShaderBackgroundActive
    {
        get => _isShaderBackgroundActive;
        private set => SetAndRaise(IsShaderBackgroundActiveProperty, ref _isShaderBackgroundActive, value);
    }

    private CornerRadius _effectiveRootCornerRadius;
    public CornerRadius EffectiveRootCornerRadius
    {
        get => _effectiveRootCornerRadius;
        private set => SetAndRaise(EffectiveRootCornerRadiusProperty, ref _effectiveRootCornerRadius, value);
    }

    private WindowState _previousVisibleWindowState = WindowState.Normal;
    public WindowState PreviousVisibleWindowState
    {
        get => _previousVisibleWindowState;
        private set => SetAndRaise(PreviousVisibleWindowStateProperty, ref _previousVisibleWindowState, value);
    }

    protected override Type StyleKeyOverride => typeof(VelaWindow);

    private Border? _titleBar;
    private Button? _pinButton;
    private Button? _fullScreenButton;
    private Button? _minimizeButton;
    private Button? _maximizeButton;
    private Button? _closeButton;
    private long _lastTitleClickTicks;
    private bool _isFullScreenTitleBarRevealed;
    private readonly DispatcherTimer _hideTitleBarTimer = new() { Interval = TimeSpan.FromMilliseconds(1000) };
    private readonly DispatcherTimer _showTitleBarTimer = new() { Interval = TimeSpan.FromMilliseconds(300) };

    static VelaWindow()
    {
        UseCustomTitleBarProperty.Changed.AddClassHandler<VelaWindow>((w, _) => w.SyncChrome());
        WindowChromeModeProperty.Changed.AddClassHandler<VelaWindow>((w, _) => w.SyncChrome());
        TitleBarHeightProperty.Changed.AddClassHandler<VelaWindow>((w, _) => w.SyncChrome());
        IsTitleBarVisibleProperty.Changed.AddClassHandler<VelaWindow>((w, _) => w.SyncBarState());
        TitleBarVisibilityOnFullScreenProperty.Changed.AddClassHandler<VelaWindow>((w, _) => w.SyncFullScreenTitleBar());
        TitleBarAutoHideDelayProperty.Changed.AddClassHandler<VelaWindow>((w, e) =>
        {
            if (e.NewValue is int ms) w._hideTitleBarTimer.Interval = TimeSpan.FromMilliseconds(ms);
        });
        TitleBarAutoShowDelayProperty.Changed.AddClassHandler<VelaWindow>((w, e) =>
        {
            if (e.NewValue is int ms) w._showTitleBarTimer.Interval = TimeSpan.FromMilliseconds(ms);
        });
        MaxWidthScreenRatioProperty.Changed.AddClassHandler<VelaWindow>((w, _) => w.UpdateMaxSizeFromScreenRatio());
        MaxHeightScreenRatioProperty.Changed.AddClassHandler<VelaWindow>((w, _) => w.UpdateMaxSizeFromScreenRatio());
        ExtendContentIntoTitleBarProperty.Changed.AddClassHandler<VelaWindow>((w, _) => w.SyncBarState());
        ShowTitleProperty.Changed.AddClassHandler<VelaWindow>((w, _) => w.SyncBarState());
        ShowTitleBarDividerProperty.Changed.AddClassHandler<VelaWindow>((w, _) => w.SyncBarState());
        TitleBarContentProperty.Changed.AddClassHandler<VelaWindow>((w, _) => w.SyncBarState());
        TitleBarCenterContentProperty.Changed.AddClassHandler<VelaWindow>((w, _) => w.SyncBarState());
        IsBottomBarVisibleProperty.Changed.AddClassHandler<VelaWindow>((w, _) => w.SyncBarState());
        ShowBottomBarDividerProperty.Changed.AddClassHandler<VelaWindow>((w, _) => w.SyncBarState());
        BottomBarContentProperty.Changed.AddClassHandler<VelaWindow>((w, _) => w.SyncBarState());
        BottomBarLeftContentProperty.Changed.AddClassHandler<VelaWindow>((w, _) => w.SyncBarState());
        BottomBarCenterContentProperty.Changed.AddClassHandler<VelaWindow>((w, _) => w.SyncBarState());
        BottomBarRightContentProperty.Changed.AddClassHandler<VelaWindow>((w, _) => w.SyncBarState());
        BackgroundContentProperty.Changed.AddClassHandler<VelaWindow>((w, _) => w.SyncBarState());
        BackgroundMaterialProperty.Changed.AddClassHandler<VelaWindow>((w, _) => w.SyncMaterial());
        ShowTitleBarBackgroundProperty.Changed.AddClassHandler<VelaWindow>((w, _) => w.SyncBarState());
        RootCornerRadiusProperty.Changed.AddClassHandler<VelaWindow>((w, _) => w.SyncWindowState());
        WindowStateProperty.Changed.AddClassHandler<VelaWindow>((w, e) =>
        {
            if (e.OldValue is WindowState oldState and (WindowState.Normal or WindowState.Maximized))
                w.PreviousVisibleWindowState = oldState;
            w.SyncWindowState();
        });
        TopmostProperty.Changed.AddClassHandler<VelaWindow>((w, _) => w.SyncWindowState());
        TrayIconProperty.Changed.AddClassHandler<VelaWindow>((w, e) => w.OnTrayIconChanged(e.OldValue as VelaTrayIcon, e.NewValue as VelaTrayIcon));
    }

    public VelaWindow()
    {
        Hosts = [];
        _hideTitleBarTimer.Tick += OnHideTitleBarTimerTick;
        _showTitleBarTimer.Tick += OnShowTitleBarTimerTick;
        ScalingChanged += (_, _) => UpdateMaxSizeFromScreenRatio();
        PositionChanged += (_, _) => UpdateMaxSizeFromScreenRatio();
        Opened += (_, _) => UpdateMaxSizeFromScreenRatio();

        SyncChrome();
        SyncWindowState();
        SyncMaterial();
        SyncBarState();
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (_titleBar is not null) _titleBar.PointerPressed -= OnTitleBarPointerPressed;
        if (_pinButton is not null) _pinButton.Click -= OnPinClick;
        if (_fullScreenButton is not null) _fullScreenButton.Click -= OnFullScreenClick;
        if (_minimizeButton is not null) _minimizeButton.Click -= OnMinimizeClick;
        if (_maximizeButton is not null) _maximizeButton.Click -= OnMaximizeClick;
        if (_closeButton is not null) _closeButton.Click -= OnCloseClick;

        _titleBar = e.NameScope.Find<Border>("PART_TitleBar");
        _pinButton = e.NameScope.Find<Button>("PART_PinButton");
        _fullScreenButton = e.NameScope.Find<Button>("PART_FullScreenButton");
        _minimizeButton = e.NameScope.Find<Button>("PART_MinimizeButton");
        _maximizeButton = e.NameScope.Find<Button>("PART_MaximizeButton");
        _closeButton = e.NameScope.Find<Button>("PART_CloseButton");

        if (_titleBar is not null)
        {
            _titleBar.PointerPressed += OnTitleBarPointerPressed;
            // On macOS, native traffic lights and drag handling are managed directly to avoid double-handling conflicts.
            if (OperatingSystem.IsMacOS())
                WindowDecorationProperties.SetElementRole(_titleBar, WindowDecorationsElementRole.None);
        }
        if (_pinButton is not null) _pinButton.Click += OnPinClick;
        if (_fullScreenButton is not null) _fullScreenButton.Click += OnFullScreenClick;
        if (_minimizeButton is not null) _minimizeButton.Click += OnMinimizeClick;
        if (_maximizeButton is not null) _maximizeButton.Click += OnMaximizeClick;
        if (_closeButton is not null) _closeButton.Click += OnCloseClick;

        SyncChrome();
        SyncWindowState();
        SyncMaterial();
        SyncBarState();
    }

    private void SyncChrome()
    {
        var isMac = OperatingSystem.IsMacOS();
        var effectiveMacChrome = WindowChromeMode switch
        {
            VelaWindowChromeMode.MacOS => true,
            VelaWindowChromeMode.Windows => false,
            _ => isMac,
        };

        PseudoClasses.Set(":macos", isMac);
        PseudoClasses.Set(":macos-chrome", effectiveMacChrome);
        PseudoClasses.Set(":custom-chrome", UseCustomTitleBar);

        if (UseCustomTitleBar)
        {
            ExtendClientAreaToDecorationsHint = true;
            ExtendClientAreaTitleBarHeightHint = TitleBarHeight;
            WindowDecorations = isMac ? WindowDecorations.Full : WindowDecorations.BorderOnly;

            ShowCustomCaptionButtons = ResolveCustomCaptionButtonsVisible(UseCustomTitleBar, WindowChromeMode, isMac);
            TitleBarPadding = !ShowCustomCaptionButtons
                ? new Thickness(78, 0, 12, 0)
                : (effectiveMacChrome ? new Thickness(12, 0, 12, 0) : new Thickness(12, 0, 6, 0));
        }
        else
        {
            ExtendClientAreaToDecorationsHint = false;
            ExtendClientAreaTitleBarHeightHint = -1;
            WindowDecorations = WindowDecorations.Full;
            TitleBarPadding = new Thickness(12, 0);
            ShowCustomCaptionButtons = false;
        }
    }

    internal static bool ResolveCustomCaptionButtonsVisible(bool useCustomTitleBar, VelaWindowChromeMode mode, bool isMacOs)
    {
        if (!useCustomTitleBar)
            return false;

        var effectiveMacChrome = mode switch
        {
            VelaWindowChromeMode.MacOS => true,
            VelaWindowChromeMode.Windows => false,
            _ => isMacOs,
        };

        return !(isMacOs && effectiveMacChrome);
    }

    internal static CornerRadius ResolveEffectiveCornerRadius(CornerRadius rootCornerRadius, WindowState state) =>
        state is WindowState.Maximized or WindowState.FullScreen
            ? new CornerRadius(0)
            : rootCornerRadius;

    internal static IReadOnlyList<WindowTransparencyLevel> ResolveTransparencyLevels(VelaWindowMaterial material) =>
        material switch
        {
            VelaWindowMaterial.Mica =>
            [
                WindowTransparencyLevel.Mica,
                WindowTransparencyLevel.AcrylicBlur,
                WindowTransparencyLevel.Blur,
                WindowTransparencyLevel.None
            ],
            VelaWindowMaterial.AcrylicBlur =>
            [
                WindowTransparencyLevel.AcrylicBlur,
                WindowTransparencyLevel.Blur,
                WindowTransparencyLevel.None
            ],
            _ => [WindowTransparencyLevel.None]
        };

    internal static bool ResolveEffectiveTitleBarVisible(
        bool useCustomTitleBar,
        bool isTitleBarVisible,
        WindowState state,
        VelaTitleBarVisibilityMode fullScreenMode,
        bool isFullScreenRevealed)
    {
        if (!useCustomTitleBar)
            return false;

        if (state == WindowState.FullScreen)
        {
            return fullScreenMode switch
            {
                VelaTitleBarVisibilityMode.Visible => true,
                VelaTitleBarVisibilityMode.Hidden => false,
                VelaTitleBarVisibilityMode.AutoHidden => isFullScreenRevealed,
                _ => isTitleBarVisible,
            };
        }

        return isTitleBarVisible;
    }

    private void SyncWindowState()
    {
        var isMaximized = WindowState == WindowState.Maximized;
        var isFullScreen = WindowState == WindowState.FullScreen;
        PseudoClasses.Set(":maximized", isMaximized);
        PseudoClasses.Set(":fullscreen", isFullScreen);
        PseudoClasses.Set(":topmost", Topmost);

        EffectiveRootCornerRadius = ResolveEffectiveCornerRadius(RootCornerRadius, WindowState);

        SyncFullScreenTitleBar();
    }

    private void SyncFullScreenTitleBar()
    {
        _hideTitleBarTimer.Stop();
        _showTitleBarTimer.Stop();

        if (WindowState != WindowState.FullScreen)
        {
            _isFullScreenTitleBarRevealed = false;
        }
        else if (TitleBarVisibilityOnFullScreen == VelaTitleBarVisibilityMode.AutoHidden)
        {
            _isFullScreenTitleBarRevealed = false;
        }

        SyncBarState();
    }

    private void UpdateMaxSizeFromScreenRatio()
    {
        var hasWidthRatio = !double.IsNaN(MaxWidthScreenRatio) && MaxWidthScreenRatio > 0 && MaxWidthScreenRatio <= 1.0;
        var hasHeightRatio = !double.IsNaN(MaxHeightScreenRatio) && MaxHeightScreenRatio > 0 && MaxHeightScreenRatio <= 1.0;
        if (!hasWidthRatio && !hasHeightRatio)
            return;

        var screen = Screens?.ScreenFromWindow(this) ?? Screens?.Primary;
        if (screen is null || screen.Scaling <= 0)
            return;

        if (hasWidthRatio)
            MaxWidth = (screen.WorkingArea.Width / screen.Scaling) * MaxWidthScreenRatio;

        if (hasHeightRatio)
            MaxHeight = (screen.WorkingArea.Height / screen.Scaling) * MaxHeightScreenRatio;
    }

    private void SyncMaterial()
    {
        var material = BackgroundMaterial;
        var isShader = material == VelaWindowMaterial.Shader;
        var isNativeMaterial = material is VelaWindowMaterial.Mica or VelaWindowMaterial.AcrylicBlur;
        var isMaterial = material != VelaWindowMaterial.Solid;

        IsShaderBackgroundActive = isShader;
        PseudoClasses.Set(":material-background", isMaterial);
        PseudoClasses.Set(":native-material", isNativeMaterial);
        PseudoClasses.Set(":shader-background", isShader);

        TransparencyLevelHint = ResolveTransparencyLevels(material);

        SyncBarState();
    }

    private void SyncBarState()
    {
        var hasMaterialOrCustomBg = BackgroundContent is not null || BackgroundMaterial != VelaWindowMaterial.Solid;

        PseudoClasses.Set(":extend-content", ExtendContentIntoTitleBar);
        PseudoClasses.Set(":has-background-content", BackgroundContent is not null);
        PseudoClasses.Set(":no-titlebar-bg", !ShowTitleBarBackground || hasMaterialOrCustomBg);
        PseudoClasses.Set(":no-titlebar-divider", !ShowTitleBarDivider);
        PseudoClasses.Set(":no-bottombar-divider", !ShowBottomBarDivider);

        ShowDefaultTitle = ShowTitle && TitleBarContent is null && TitleBarCenterContent is null;

        EffectiveTitleBarVisible = ResolveEffectiveTitleBarVisible(
            UseCustomTitleBar,
            IsTitleBarVisible,
            WindowState,
            TitleBarVisibilityOnFullScreen,
            _isFullScreenTitleBarRevealed);

        var hasBottomContent = BottomBarContent is not null
            || BottomBarLeftContent is not null
            || BottomBarCenterContent is not null
            || BottomBarRightContent is not null;

        EffectiveBottomBarVisible = IsBottomBarVisible && hasBottomContent;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (WindowState != WindowState.FullScreen
            || TitleBarVisibilityOnFullScreen != VelaTitleBarVisibilityMode.AutoHidden
            || !UseCustomTitleBar)
        {
            return;
        }

        var y = e.GetPosition(this).Y;
        var revealEdge = 8.0;
        var titleBarBottom = Math.Max(TitleBarHeight, 32.0);

        if (!_isFullScreenTitleBarRevealed && y <= revealEdge)
        {
            _hideTitleBarTimer.Stop();
            if (TitleBarAutoShowDelay <= 0)
            {
                _isFullScreenTitleBarRevealed = true;
                SyncBarState();
            }
            else if (!_showTitleBarTimer.IsEnabled)
            {
                _showTitleBarTimer.Start();
            }
        }
        else if (_isFullScreenTitleBarRevealed && y > titleBarBottom)
        {
            _showTitleBarTimer.Stop();
            if (TitleBarAutoHideDelay <= 0)
            {
                _isFullScreenTitleBarRevealed = false;
                SyncBarState();
            }
            else if (!_hideTitleBarTimer.IsEnabled)
            {
                _hideTitleBarTimer.Start();
            }
        }
        else if (_isFullScreenTitleBarRevealed && y <= titleBarBottom)
        {
            _hideTitleBarTimer.Stop();
        }
        else if (!_isFullScreenTitleBarRevealed && y > revealEdge)
        {
            _showTitleBarTimer.Stop();
        }
    }

    private void OnShowTitleBarTimerTick(object? sender, EventArgs e)
    {
        _showTitleBarTimer.Stop();
        if (WindowState == WindowState.FullScreen && TitleBarVisibilityOnFullScreen == VelaTitleBarVisibilityMode.AutoHidden)
        {
            _isFullScreenTitleBarRevealed = true;
            SyncBarState();
        }
    }

    private void OnHideTitleBarTimerTick(object? sender, EventArgs e)
    {
        _hideTitleBarTimer.Stop();
        if (WindowState == WindowState.FullScreen && TitleBarVisibilityOnFullScreen == VelaTitleBarVisibilityMode.AutoHidden)
        {
            _isFullScreenTitleBarRevealed = false;
            SyncBarState();
        }
    }

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        // Ignore clicks originating inside interactive controls hosted in titlebar slots
        if (e.Source is Control src && IsInteractiveElement(src))
            return;

        var now = Environment.TickCount64;
        if (CanResize && now - _lastTitleClickTicks < 340)
        {
            _lastTitleClickTicks = 0;
            ToggleMaximize();
            e.Handled = true;
            return;
        }
        _lastTitleClickTicks = now;

        BeginMoveDrag(e);
    }

    private static bool IsInteractiveElement(Control control)
    {
        Control? cur = control;
        while (cur is not null)
        {
            if (cur.Name == "PART_TitleBar")
                return false;

            if (cur is Button
                or TextBox
                or ToggleButton
                or ComboBox
                or Slider
                or SelectingItemsControl
                or MenuBase
                or MenuItem
                or VelaInput
                or VelaSelect
                or VelaComboBox
                or VelaSwitch
                or VelaCheckBox
                or VelaSegmentedControl
                or VelaTabs
                or VelaDropdownButton
                or VelaSplitButton
                or VelaCopyButton
                or VelaMenuBar
                or VelaMenuBarItem
                or VelaToolBar
                or VelaToolBarButton
                or VelaToolBarToggleButton)
            {
                return true;
            }

            cur = cur.Parent as Control;
        }
        return false;
    }

    private void OnPinClick(object? sender, RoutedEventArgs e)
    {
        if (!CanPin) return;
        Topmost = !Topmost;
    }

    private void OnFullScreenClick(object? sender, RoutedEventArgs e) => ToggleFullScreen();

    private void OnMinimizeClick(object? sender, RoutedEventArgs e)
    {
        if (!CanMinimize) return;
        WindowState = WindowState.Minimized;
    }

    private void OnMaximizeClick(object? sender, RoutedEventArgs e) => ToggleMaximize();

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();

    public void ToggleMaximize()
    {
        if (!CanResize || !CanMaximize) return;
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    public void ToggleFullScreen()
    {
        if (!CanFullScreen) return;
        WindowState = WindowState == WindowState.FullScreen
            ? PreviousVisibleWindowState
            : WindowState.FullScreen;
    }

    /// <summary>
    /// Closes this window even when the attached <see cref="TrayIcon"/> has <see cref="VelaTrayIcon.CloseToTray"/> enabled.
    /// </summary>
    public void CloseBypassingTray()
    {
        if (TrayIcon is not null)
            TrayIcon.RequestRealClose();
        else
            Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        _hideTitleBarTimer.Stop();
        _showTitleBarTimer.Stop();

        if (TrayIcon is not null)
        {
            TrayIcon.DetachFromWindow();
            TrayIcon.Detach();
        }
        base.OnClosed(e);
    }

    private void OnTrayIconChanged(VelaTrayIcon? oldTray, VelaTrayIcon? newTray)
    {
        if (oldTray is not null)
        {
            LogicalChildren.Remove(oldTray);
            if (oldTray.TargetWindow == this)
                oldTray.DetachFromWindow();
            oldTray.Detach();
        }

        if (newTray is not null)
        {
            if (!LogicalChildren.Contains(newTray))
                LogicalChildren.Add(newTray);
            newTray.AttachToWindow(this);
        }
    }
}
