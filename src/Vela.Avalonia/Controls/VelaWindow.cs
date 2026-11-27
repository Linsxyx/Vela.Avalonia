using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform;

namespace Vela.Avalonia.Controls;

/// <summary>
/// Optional modern frameless desktop window shell for Vela.Avalonia:
/// - Opt-in replacement for <see cref="Window"/> with zero forced coupling
/// - Seamless native macOS traffic-light integration via <see cref="Window.ExtendClientAreaToDecorationsHint"/>
/// - Restrained custom monochrome Minimize / Maximize / Close caption buttons on Windows &amp; Linux
/// - Customizable top bar (left/center/right slots or full <see cref="TitleBarContent"/> + overlay mode)
/// - Customizable bottom bar (left/center/right slots or full <see cref="BottomBarContent"/>)
/// - Replaceable full-window <see cref="BackgroundContent"/> layer (Shader, wallpaper, gradient, or custom visual)
/// </summary>
[PseudoClasses(
    ":macos",
    ":maximized",
    ":custom-chrome",
    ":extend-content",
    ":has-background-content",
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

    public static readonly StyledProperty<VelaTrayIcon?> TrayIconProperty =
        AvaloniaProperty.Register<VelaWindow, VelaTrayIcon?>(nameof(TrayIcon));

    public static readonly DirectProperty<VelaWindow, Thickness> TitleBarPaddingProperty =
        AvaloniaProperty.RegisterDirect<VelaWindow, Thickness>(nameof(TitleBarPadding), o => o.TitleBarPadding);

    public static readonly DirectProperty<VelaWindow, bool> ShowCustomCaptionButtonsProperty =
        AvaloniaProperty.RegisterDirect<VelaWindow, bool>(nameof(ShowCustomCaptionButtons), o => o.ShowCustomCaptionButtons);

    public static readonly DirectProperty<VelaWindow, bool> ShowDefaultTitleProperty =
        AvaloniaProperty.RegisterDirect<VelaWindow, bool>(nameof(ShowDefaultTitle), o => o.ShowDefaultTitle);

    public static readonly DirectProperty<VelaWindow, bool> EffectiveBottomBarVisibleProperty =
        AvaloniaProperty.RegisterDirect<VelaWindow, bool>(nameof(EffectiveBottomBarVisible), o => o.EffectiveBottomBarVisible);

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

    private bool _effectiveBottomBarVisible;
    public bool EffectiveBottomBarVisible
    {
        get => _effectiveBottomBarVisible;
        private set => SetAndRaise(EffectiveBottomBarVisibleProperty, ref _effectiveBottomBarVisible, value);
    }

    protected override Type StyleKeyOverride => typeof(VelaWindow);

    private Border? _titleBar;
    private Button? _minimizeButton;
    private Button? _maximizeButton;
    private Button? _closeButton;
    private long _lastTitleClickTicks;

    static VelaWindow()
    {
        UseCustomTitleBarProperty.Changed.AddClassHandler<VelaWindow>((w, _) => w.SyncChrome());
        TitleBarHeightProperty.Changed.AddClassHandler<VelaWindow>((w, _) => w.SyncChrome());
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
        WindowStateProperty.Changed.AddClassHandler<VelaWindow>((w, _) => w.SyncWindowState());
        TrayIconProperty.Changed.AddClassHandler<VelaWindow>((w, e) => w.OnTrayIconChanged(e.OldValue as VelaTrayIcon, e.NewValue as VelaTrayIcon));
    }

    public VelaWindow()
    {
        SyncChrome();
        SyncWindowState();
        SyncBarState();
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (_titleBar is not null) _titleBar.PointerPressed -= OnTitleBarPointerPressed;
        if (_minimizeButton is not null) _minimizeButton.Click -= OnMinimizeClick;
        if (_maximizeButton is not null) _maximizeButton.Click -= OnMaximizeClick;
        if (_closeButton is not null) _closeButton.Click -= OnCloseClick;

        _titleBar = e.NameScope.Find<Border>("PART_TitleBar");
        _minimizeButton = e.NameScope.Find<Button>("PART_MinimizeButton");
        _maximizeButton = e.NameScope.Find<Button>("PART_MaximizeButton");
        _closeButton = e.NameScope.Find<Button>("PART_CloseButton");

        if (_titleBar is not null) _titleBar.PointerPressed += OnTitleBarPointerPressed;
        if (_minimizeButton is not null) _minimizeButton.Click += OnMinimizeClick;
        if (_maximizeButton is not null) _maximizeButton.Click += OnMaximizeClick;
        if (_closeButton is not null) _closeButton.Click += OnCloseClick;

        SyncChrome();
        SyncWindowState();
        SyncBarState();
    }

    private void SyncChrome()
    {
        var isMac = OperatingSystem.IsMacOS();
        PseudoClasses.Set(":macos", isMac);
        PseudoClasses.Set(":custom-chrome", UseCustomTitleBar);

        if (UseCustomTitleBar)
        {
            ExtendClientAreaToDecorationsHint = true;
            ExtendClientAreaTitleBarHeightHint = TitleBarHeight;
            WindowDecorations = isMac ? WindowDecorations.Full : WindowDecorations.BorderOnly;

            TitleBarPadding = isMac ? new Thickness(78, 0, 12, 0) : new Thickness(12, 0, 6, 0);
            ShowCustomCaptionButtons = !isMac;
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

    private void SyncWindowState()
    {
        PseudoClasses.Set(":maximized", WindowState == WindowState.Maximized);
    }

    private void SyncBarState()
    {
        PseudoClasses.Set(":extend-content", ExtendContentIntoTitleBar);
        PseudoClasses.Set(":has-background-content", BackgroundContent is not null);
        PseudoClasses.Set(":no-titlebar-divider", !ShowTitleBarDivider);
        PseudoClasses.Set(":no-bottombar-divider", !ShowBottomBarDivider);

        ShowDefaultTitle = ShowTitle && TitleBarContent is null && TitleBarCenterContent is null;

        var hasBottomContent = BottomBarContent is not null
            || BottomBarLeftContent is not null
            || BottomBarCenterContent is not null
            || BottomBarRightContent is not null;

        EffectiveBottomBarVisible = IsBottomBarVisible && hasBottomContent;
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

    private void OnMinimizeClick(object? sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void OnMaximizeClick(object? sender, RoutedEventArgs e) => ToggleMaximize();

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();

    private void ToggleMaximize()
    {
        if (!CanResize) return;
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
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
