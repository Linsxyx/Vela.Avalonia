using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vela.Avalonia.Controls;
using Vela.Avalonia.Gallery.Services;
using Vela.Avalonia.Tokens;

namespace Vela.Avalonia.Gallery.ViewModels;

public partial class SystemTrayViewModel : ViewModelBase
{
    private static readonly string[] GlyphKeys =
    [
        "VelaIconSparkles",
        "VelaIconBell",
        "VelaIconRocket",
        "VelaIconShield",
        "VelaIconCompass",
    ];

    private readonly IGalleryShellService _shell;
    private readonly Action _toggleTheme;
    private VelaTrayIcon? _trayIcon;
    private Bitmap? _lightBmp;
    private Bitmap? _darkBmp;

    [ObservableProperty]
    public partial string PlatformBadgeText { get; set; } = "Desktop Tray Active";

    [ObservableProperty]
    public partial VelaBadgeTone PlatformBadgeTone { get; set; } = VelaBadgeTone.Success;

    [ObservableProperty]
    public partial string StatusText { get; set; } = "Tray active";

    [ObservableProperty]
    public partial Bitmap? PreviewLightBitmap { get; set; }

    [ObservableProperty]
    public partial Bitmap? PreviewDarkBitmap { get; set; }

    [ObservableProperty]
    public partial bool IsBackgroundSyncEnabled { get; set; } = true;

    [ObservableProperty]
    public partial int GlyphIndex { get; set; }

    [ObservableProperty]
    public partial int ThemeModeIndex { get; set; }

    [ObservableProperty]
    public partial int BadgeVariantIndex { get; set; } = 1;

    [ObservableProperty]
    public partial int BadgeToneIndex { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BadgeCountLabel))]
    public partial double BadgeCount { get; set; } = 3;

    public string BadgeCountLabel
    {
        get
        {
            var c = (int)Math.Round(BadgeCount);
            return c > 99 ? $"{c} (99+)" : c.ToString();
        }
    }

    [ObservableProperty]
    public partial bool ShowProgressRing { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressLabel))]
    public partial double ProgressPercent { get; set; } = 68;

    public string ProgressLabel => $"{Math.Round(ProgressPercent):0}%";

    [ObservableProperty]
    public partial int PresentationIndex { get; set; }

    [ObservableProperty]
    public partial int MaterialIndex { get; set; }

    [ObservableProperty]
    public partial int PlacementIndex { get; set; }

    [ObservableProperty]
    public partial bool CloseToTray { get; set; }

    [ObservableProperty]
    public partial bool MinimizeToTray { get; set; }

    [ObservableProperty]
    public partial bool ShowIconPlate { get; set; }

    public SystemTrayViewModel(IGalleryShellService shell, Action toggleTheme)
    {
        _shell = shell;
        _toggleTheme = toggleTheme;
    }

    public void EnsurePreviewTrayAttached()
    {
        if (_trayIcon is not null)
            return;

        var previewTray = new VelaTrayIcon
        {
            ToolTipText = "Vela Studio",
            BadgeVariant = VelaTrayBadgeVariant.Dot,
            BadgeTone = VelaTrayBadgeTone.Accent,
            BadgeCount = 3,
            Presentation = VelaTrayPresentation.VelaMenu,
            IconGeometry = _shell.FindIcon("VelaIconSparkles"),
        };
        AttachTrayIcon(previewTray);
    }

    public void AttachTrayIcon(VelaTrayIcon trayIcon)
    {
        if (_trayIcon is not null)
        {
            _trayIcon.RenderedIconChanged -= OnRenderedIconChanged;
            _trayIcon.Clicked -= OnTrayClicked;
            _trayIcon.FlyoutOpened -= OnFlyoutOpened;
            _trayIcon.FlyoutClosed -= OnFlyoutClosed;
        }

        _trayIcon = trayIcon;
        _trayIcon.BadgeCount = (int)Math.Round(BadgeCount);
        _trayIcon.RenderedIconChanged += OnRenderedIconChanged;
        _trayIcon.Clicked += OnTrayClicked;
        _trayIcon.FlyoutOpened += OnFlyoutOpened;
        _trayIcon.FlyoutClosed += OnFlyoutClosed;

        var supported = VelaTrayIcon.IsPlatformSupported && _trayIcon.IsAttached;
        PlatformBadgeText = supported ? "Desktop Active" : "Preview Mode";
        PlatformBadgeTone = supported ? VelaBadgeTone.Success : VelaBadgeTone.Info;

        RefreshPreviews();
    }

    partial void OnGlyphIndexChanged(int value)
    {
        if (_trayIcon is null) return;
        var key = GlyphKeys[Math.Clamp(value, 0, GlyphKeys.Length - 1)];
        var icon = _shell.FindIcon(key);
        if (icon is not null)
        {
            _trayIcon.IconGeometry = icon;
            RefreshPreviews();
        }
    }

    partial void OnThemeModeIndexChanged(int value)
    {
        if (_trayIcon is null) return;
        _trayIcon.IconThemeMode = value switch
        {
            1 => VelaTrayIconThemeMode.MonochromeDark,
            2 => VelaTrayIconThemeMode.MonochromeLight,
            3 => VelaTrayIconThemeMode.Template,
            _ => VelaTrayIconThemeMode.Auto,
        };
        RefreshPreviews();
    }

    partial void OnBadgeVariantIndexChanged(int value)
    {
        if (_trayIcon is null) return;
        _trayIcon.BadgeVariant = value switch
        {
            1 => VelaTrayBadgeVariant.Dot,
            2 => VelaTrayBadgeVariant.Count,
            _ => VelaTrayBadgeVariant.None,
        };
        RefreshPreviews();
    }

    partial void OnBadgeToneIndexChanged(int value)
    {
        if (_trayIcon is null) return;
        _trayIcon.BadgeTone = value switch
        {
            1 => VelaTrayBadgeTone.Success,
            2 => VelaTrayBadgeTone.Warning,
            3 => VelaTrayBadgeTone.Danger,
            4 => VelaTrayBadgeTone.Info,
            _ => VelaTrayBadgeTone.Accent,
        };
        RefreshPreviews();
    }

    partial void OnBadgeCountChanged(double value)
    {
        if (_trayIcon is null) return;
        _trayIcon.BadgeCount = (int)Math.Round(value);
        RefreshPreviews();
    }

    partial void OnShowProgressRingChanged(bool value) => SyncProgress();
    partial void OnProgressPercentChanged(double value) => SyncProgress();

    partial void OnPresentationIndexChanged(int value)
    {
        if (_trayIcon is null) return;
        _trayIcon.Presentation = value switch
        {
            1 => VelaTrayPresentation.VelaPopover,
            2 => VelaTrayPresentation.Native,
            3 => VelaTrayPresentation.CommandOnly,
            _ => VelaTrayPresentation.VelaMenu,
        };
        StatusText = $"Presentation: {_trayIcon.Presentation}";
    }

    partial void OnMaterialIndexChanged(int value)
    {
        if (_trayIcon is null) return;
        _trayIcon.SurfaceMaterial = value switch
        {
            1 => VelaSurfaceMaterial.Frosted,
            2 => VelaSurfaceMaterial.GlassCard,
            3 => VelaSurfaceMaterial.LiquidGlass,
            _ => VelaSurfaceMaterial.Solid,
        };
        StatusText = $"Material: {_trayIcon.SurfaceMaterial}";
    }

    partial void OnPlacementIndexChanged(int value)
    {
        if (_trayIcon is null) return;
        _trayIcon.FlyoutPlacement = value switch
        {
            1 => VelaTrayFlyoutPlacement.TopRight,
            2 => VelaTrayFlyoutPlacement.BottomRight,
            3 => VelaTrayFlyoutPlacement.TopLeft,
            4 => VelaTrayFlyoutPlacement.BottomLeft,
            _ => VelaTrayFlyoutPlacement.Auto,
        };
        StatusText = $"Placement: {_trayIcon.FlyoutPlacement}";
    }

    partial void OnCloseToTrayChanged(bool value)
    {
        if (_trayIcon is null) return;
        _trayIcon.CloseToTray = value;
        StatusText = $"CloseToTray: {value}";
    }

    partial void OnMinimizeToTrayChanged(bool value)
    {
        if (_trayIcon is null) return;
        _trayIcon.MinimizeToTray = value;
        StatusText = $"MinimizeToTray: {value}";
    }

    partial void OnShowIconPlateChanged(bool value)
    {
        if (_trayIcon is null) return;
        _trayIcon.IconBackground = value ? _shell.FindBrush("VelaAccentSubtleBrush") : null;
        RefreshPreviews();
    }

    [RelayCommand]
    private void OpenFlyout()
    {
        if (_trayIcon is null) return;
        if (!VelaTrayIcon.IsPlatformSupported)
        {
            _shell.ShowToast("Desktop only", "Tray flyout runs on desktop targets.", VelaToastTone.Info);
            return;
        }
        if (_trayIcon.EffectivePresentation is VelaTrayPresentation.Native or VelaTrayPresentation.CommandOnly)
        {
            _shell.ShowToast("Presentation", $"Mode is {_trayIcon.EffectivePresentation}. Switch to VelaMenu or VelaPopover.", VelaToastTone.Info);
            return;
        }
        _trayIcon.ShowFlyout();
    }

    [RelayCommand]
    private void HideWindowBriefly()
    {
        if (_trayIcon is null || !VelaTrayIcon.IsPlatformSupported)
        {
            _shell.ShowToast("Desktop only", "Available on desktop targets.", VelaToastTone.Info);
            return;
        }
        StatusText = "Hidden to tray (restoring in 3s)...";
        _trayIcon.HideWindowToTray();

        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            _trayIcon?.RestoreWindow();
            StatusText = "Window restored";
        };
        timer.Start();
    }

    [RelayCommand]
    private void ToggleTrayVisible()
    {
        if (_trayIcon is null) return;
        _trayIcon.IsVisible = !_trayIcon.IsVisible;
        StatusText = _trayIcon.IsVisible ? "Tray visible" : "Tray hidden";
    }

    [RelayCommand]
    private void TrayRestoreWindow()
    {
        _trayIcon?.HideFlyout();
        _trayIcon?.RestoreWindow();
        StatusText = "Restored from tray";
    }

    [RelayCommand]
    private void TrayToggleTheme()
    {
        _toggleTheme();
        StatusText = "Theme toggled via tray";
    }

    [RelayCommand]
    private void TrayToggleSync()
    {
        IsBackgroundSyncEnabled = !IsBackgroundSyncEnabled;
        StatusText = IsBackgroundSyncEnabled ? "Sync enabled" : "Sync paused";
    }

    [RelayCommand]
    private void TraySendToast()
    {
        _trayIcon?.RestoreWindow();
        _shell.ShowToast("System Tray", "Triggered from tray menu.", VelaToastTone.Success);
        StatusText = "Toast sent from tray";
    }

    [RelayCommand]
    private void TrayQuit() => _shell.RequestQuit();

    private void SyncProgress()
    {
        if (_trayIcon is null) return;
        var pct = Math.Clamp(ProgressPercent, 0.0, 100.0);
        _trayIcon.Progress = ShowProgressRing ? pct / 100.0 : null;
        RefreshPreviews();
    }

    private void OnRenderedIconChanged(object? sender, EventArgs e) => RefreshPreviews();
    private void OnTrayClicked(object? sender, EventArgs e) => StatusText = $"Clicked ({_trayIcon?.Presentation})";
    private void OnFlyoutOpened(object? sender, EventArgs e) => StatusText = $"Opened {_trayIcon?.Presentation}";
    private void OnFlyoutClosed(object? sender, EventArgs e) => StatusText = "Flyout closed";

    private void RefreshPreviews()
    {
        if (_trayIcon is null) return;
        var nextLight = _trayIcon.RenderPreviewBitmap(64, isDarkOverride: false);
        var nextDark = _trayIcon.RenderPreviewBitmap(64, isDarkOverride: true);

        PreviewLightBitmap = nextLight;
        PreviewDarkBitmap = nextDark;

        _lightBmp?.Dispose();
        _darkBmp?.Dispose();
        _lightBmp = nextLight;
        _darkBmp = nextDark;
    }
}
