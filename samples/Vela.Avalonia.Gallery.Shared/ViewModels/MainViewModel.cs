using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vela.Avalonia.Controls;
using Vela.Avalonia.Gallery.Services;
using Vela.Avalonia.Motion;
using Vela.Avalonia.Tokens;

namespace Vela.Avalonia.Gallery.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly IGalleryShellService _shell;

    public ActionsViewModel Actions { get; }
    public InputsViewModel Inputs { get; }
    public PickersViewModel Pickers { get; }
    public LayoutViewModel Layout { get; }
    public FeedbackViewModel Feedback { get; }
    public DataDisplayViewModel DataDisplay { get; }
    public ShadersViewModel Shaders { get; }
    public ChartsViewModel Charts { get; }
    public SystemTrayViewModel SystemTray { get; }

    public ObservableCollection<AccentSwatchViewModel> AccentSwatches { get; } = new();

    [ObservableProperty]
    public partial int SelectedNavIndex { get; set; }

    [ObservableProperty]
    public partial bool IsDarkMode { get; set; }

    [ObservableProperty]
    public partial bool IsPaneOpen { get; set; } = true;

    [ObservableProperty]
    public partial bool ShowTopBar { get; set; } = true;

    [ObservableProperty]
    public partial bool IsSearchVisible { get; set; }

    [ObservableProperty]
    public partial bool ReducedMotion { get; set; }

    [ObservableProperty]
    public partial bool CommandPaletteGlass { get; set; }

    [ObservableProperty]
    public partial VelaAccent SelectedAccent { get; set; } = VelaAccent.Violet;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayMode))]
    public partial int DisplayModeIndex { get; set; }

    public VelaNavigationDisplayMode DisplayMode =>
        DisplayModeIndex == 1 ? VelaNavigationDisplayMode.Top : VelaNavigationDisplayMode.Left;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PaneVariant))]
    public partial int PaneVariantIndex { get; set; }

    public VelaNavigationPaneVariant PaneVariant => PaneVariantIndex switch
    {
        1 => VelaNavigationPaneVariant.Floating,
        2 => VelaNavigationPaneVariant.Frosted,
        _ => VelaNavigationPaneVariant.Standard,
    };

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PageCacheMode))]
    [NotifyPropertyChangedFor(nameof(TelemetrySummary))]
    public partial int CacheModeIndex { get; set; }

    public VelaPageCacheMode PageCacheMode => CacheModeIndex switch
    {
        1 => VelaPageCacheMode.Lru,
        2 => VelaPageCacheMode.None,
        _ => VelaPageCacheMode.KeepAlive,
    };

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PageTransitionMode))]
    [NotifyPropertyChangedFor(nameof(TelemetrySummary))]
    public partial int TransitionModeIndex { get; set; }

    public VelaPageTransitionMode PageTransitionMode =>
        TransitionModeIndex == 1 ? VelaPageTransitionMode.CompositorFade : VelaPageTransitionMode.Instant;

    [ObservableProperty]
    public partial string TelemetryText { get; set; } = "Ready · KeepAlive · Instant";

    [ObservableProperty]
    public partial bool LastSwitchCacheHit { get; set; } = true;

    public string TelemetrySummary => $"{PageCacheMode} · {PageTransitionMode}";

    public MainViewModel(IGalleryShellService shell)
    {
        _shell = shell;

        Actions = new ActionsViewModel(shell);
        Inputs = new InputsViewModel();
        Pickers = new PickersViewModel(shell);
        Layout = new LayoutViewModel(shell);
        Feedback = new FeedbackViewModel(shell);
        DataDisplay = new DataDisplayViewModel(shell);
        Shaders = new ShadersViewModel(shell);
        Charts = new ChartsViewModel();
        SystemTray = new SystemTrayViewModel(shell, ToggleDarkMode);

        foreach (var accent in Enum.GetValues<VelaAccent>())
        {
            var palette = VelaAccentPalettes.Get(accent, isDark: false);
            AccentSwatches.Add(new AccentSwatchViewModel(accent, palette.ControlOn));
        }

        RefreshSwatchRings();
    }

    partial void OnIsDarkModeChanged(bool value)
    {
        _shell.SetThemeVariant(value);
        RefreshSwatchRings();
    }

    partial void OnReducedMotionChanged(bool value)
    {
        VelaMotionOptions.ReducedMotion = value;
    }

    partial void OnSelectedAccentChanged(VelaAccent value)
    {
        _shell.SetAccent(value);
        RefreshSwatchRings();
    }

    [RelayCommand]
    private void SelectAccent(VelaAccent accent)
    {
        SelectedAccent = accent;
    }

    [RelayCommand]
    private void ToggleDarkMode()
    {
        IsDarkMode = !IsDarkMode;
    }

    [RelayCommand]
    private void TogglePane()
    {
        IsPaneOpen = !IsPaneOpen;
    }

    [RelayCommand]
    private void OpenCommandPalette()
    {
        _shell.OpenCommandPalette();
    }

    [RelayCommand]
    private void ClearPageCache()
    {
        _shell.ClearInactivePageCache();
        TelemetryText = "Inactive page cache cleared";
        _shell.ShowToast("Cache cleared", "Inactive cached pages evicted.", VelaToastTone.Success);
    }

    public void RecordPageSwitch(VelaPageSwitchMetrics m)
    {
        var label = (m.PageKey as VelaNavItem)?.Header ?? m.PageKey?.ToString() ?? "Page";
        var hit = m.IsCacheHit ? "HIT" : "MISS";
        LastSwitchCacheHit = m.IsCacheHit;
        TelemetryText = $"{label} · {m.Elapsed.TotalMilliseconds:0.00} ms · {hit} · {m.CachedCount} cached";
    }

    public IEnumerable<VelaCommandItem> BuildCommandPaletteItems()
    {
        Geometry? Icon(string key) => _shell.FindIcon(key);

        yield return NavCmd("Overview", "Shell, theme & cache", "overview", "VelaIconCompass", "G O");
        yield return NavCmd("Actions", "Buttons, split, copy, confirm", "actions", "VelaIconComponents", "G A");
        yield return NavCmd("Inputs & Sliders", "Input, tags, switches, sliders", "inputs", "VelaIconSliders", "G I");
        yield return NavCmd("Pickers & Menus", "RadioCards, select, calendar, color", "pickers", "VelaIconCalendar", "G P");
        yield return NavCmd("Navigation & Layout", "DockBar, tabs, stepper, split panes", "layout", "VelaIconLayers", "G N");
        yield return NavCmd("Overlays & Feedback", "Dialogs, drawers, toasts, progress", "feedback", "VelaIconBell", "G F");
        yield return NavCmd("Data Display", "KPIs, badges, avatars, settings", "display", "VelaIconHome", "G D");
        yield return NavCmd("Shaders & Glass", "Liquid Glass & SkSL shaders", "shaders", "VelaIconSparkles", "G S");
        yield return NavCmd("Charts", "6 vector chart types", "charts", "VelaIconChart", "G C");
        yield return NavCmd("System Tray", "Tray icon & flyout studio", "tray", "VelaIconBell", "G T");
        yield return NavCmd("Stress Test", "500-node VelaPageDeck benchmark", "stress", "VelaIconTrendingUp", "G B");

        yield return new VelaCommandItem
        {
            Title = "Toggle Dark Mode",
            Group = "Appearance",
            Icon = Icon("VelaIconPalette"),
            Shortcut = "⌘D",
            Execute = ToggleDarkMode,
        };
        yield return new VelaCommandItem
        {
            Title = "Toggle Sidebar",
            Group = "Appearance",
            Icon = Icon("VelaIconSidebar"),
            Shortcut = "⌘\\",
            Execute = TogglePane,
        };
        yield return new VelaCommandItem
        {
            Title = "Toggle Palette Liquid Glass",
            Group = "Appearance",
            Icon = Icon("VelaIconSparkles"),
            Execute = () =>
            {
                CommandPaletteGlass = !CommandPaletteGlass;
                _shell.ShowToast("Command Palette", CommandPaletteGlass ? "Liquid Glass enabled" : "Solid mode enabled", VelaToastTone.Info);
            },
        };
        yield return new VelaCommandItem
        {
            Title = "Clear Inactive Page Cache",
            Group = "Actions",
            Icon = Icon("VelaIconTrash"),
            Execute = ClearPageCache,
        };

        VelaCommandItem NavCmd(string title, string desc, string key, string iconKey, string shortcut) => new()
        {
            Title = title,
            Description = desc,
            Group = "Pages",
            Icon = Icon(iconKey),
            Shortcut = shortcut,
            Execute = () => _shell.NavigateTo(key),
        };
    }

    private void RefreshSwatchRings()
    {
        foreach (var item in AccentSwatches)
            item.UpdateRing(item.Accent == SelectedAccent, IsDarkMode);
    }
}
