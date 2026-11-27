using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform;

namespace Vela.Avalonia.Controls;

/// <summary>
/// Optional modern frameless desktop window shell for Vela.Avalonia:
/// - Opt-in replacement for <see cref="Window"/> with zero forced coupling
/// - Seamless native macOS traffic-light integration via <see cref="Window.ExtendClientAreaToDecorationsHint"/>
/// - Restrained custom monochrome Minimize / Maximize / Close caption buttons on Windows &amp; Linux
/// - Customizable left, center, and right title-bar slots with drag-to-move and double-click-to-maximize
/// </summary>
[PseudoClasses(":macos", ":maximized", ":custom-chrome")]
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

    public static readonly StyledProperty<object?> TitleBarLeftContentProperty =
        AvaloniaProperty.Register<VelaWindow, object?>(nameof(TitleBarLeftContent));

    public static readonly StyledProperty<object?> TitleBarCenterContentProperty =
        AvaloniaProperty.Register<VelaWindow, object?>(nameof(TitleBarCenterContent));

    public static readonly StyledProperty<object?> TitleBarRightContentProperty =
        AvaloniaProperty.Register<VelaWindow, object?>(nameof(TitleBarRightContent));

    public static readonly StyledProperty<VelaTrayIcon?> TrayIconProperty =
        AvaloniaProperty.Register<VelaWindow, VelaTrayIcon?>(nameof(TrayIcon));

    public static readonly DirectProperty<VelaWindow, Thickness> TitleBarPaddingProperty =
        AvaloniaProperty.RegisterDirect<VelaWindow, Thickness>(nameof(TitleBarPadding), o => o.TitleBarPadding);

    public static readonly DirectProperty<VelaWindow, bool> ShowCustomCaptionButtonsProperty =
        AvaloniaProperty.RegisterDirect<VelaWindow, bool>(nameof(ShowCustomCaptionButtons), o => o.ShowCustomCaptionButtons);

    public bool UseCustomTitleBar { get => GetValue(UseCustomTitleBarProperty); set => SetValue(UseCustomTitleBarProperty, value); }
    public bool IsTitleBarVisible { get => GetValue(IsTitleBarVisibleProperty); set => SetValue(IsTitleBarVisibleProperty, value); }
    public double TitleBarHeight { get => GetValue(TitleBarHeightProperty); set => SetValue(TitleBarHeightProperty, value); }
    public bool ShowTitle { get => GetValue(ShowTitleProperty); set => SetValue(ShowTitleProperty, value); }
    public bool ShowTitleBarDivider { get => GetValue(ShowTitleBarDividerProperty); set => SetValue(ShowTitleBarDividerProperty, value); }
    public object? TitleBarLeftContent { get => GetValue(TitleBarLeftContentProperty); set => SetValue(TitleBarLeftContentProperty, value); }
    public object? TitleBarCenterContent { get => GetValue(TitleBarCenterContentProperty); set => SetValue(TitleBarCenterContentProperty, value); }
    public object? TitleBarRightContent { get => GetValue(TitleBarRightContentProperty); set => SetValue(TitleBarRightContentProperty, value); }
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
        WindowStateProperty.Changed.AddClassHandler<VelaWindow>((w, _) => w.SyncWindowState());
        TrayIconProperty.Changed.AddClassHandler<VelaWindow>((w, e) => w.OnTrayIconChanged(e.OldValue as VelaTrayIcon, e.NewValue as VelaTrayIcon));
    }

    public VelaWindow()
    {
        SyncChrome();
        SyncWindowState();
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
            if (cur is Button or TextBox or ToggleButton or ComboBox)
                return true;
            if (cur.Name == "PART_TitleBar")
                return false;
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
