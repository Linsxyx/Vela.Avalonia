using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vela.Avalonia.Controls;
using Vela.Avalonia.Gallery.Localization;
using Vela.Avalonia.Gallery.Services;
using Vela.Avalonia.Gallery.Views;
using Vela.Avalonia.Motion;
using Vela.Avalonia.Tokens;

namespace Vela.Avalonia.Gallery.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly IGalleryShellService _shell;
    private bool _syncingLanguage;

    public OverviewViewModel Overview { get; }
    public ActionsViewModel Actions { get; }
    public InputsViewModel Inputs { get; }
    public PickersViewModel Pickers { get; }
    public LayoutViewModel Layout { get; }
    public FeedbackViewModel Feedback { get; }
    public DataDisplayViewModel DataDisplay { get; }
    public DataViewsViewModel DataViews { get; }
    public ShadersViewModel Shaders { get; }
    public ChartsViewModel Charts { get; }
    public SystemTrayViewModel SystemTray { get; }
    public ShowcaseViewModel Showcase { get; }

    public ObservableCollection<AccentSwatchViewModel> AccentSwatches { get; } = new();

    [ObservableProperty]
    public partial int SelectedNavIndex { get; set; }

    [ObservableProperty]
    public partial bool IsDarkMode { get; set; }

    [ObservableProperty]
    public partial int LanguageIndex { get; set; }

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
    public partial int CacheModeIndex { get; set; } = 1;

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
    public partial string TelemetryText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool LastSwitchCacheHit { get; set; } = true;

    public string TelemetrySummary => $"{PageCacheMode} · {PageTransitionMode}";

    public MainViewModel(IGalleryShellService shell)
    {
        _shell = shell;

        _syncingLanguage = true;
        LanguageIndex = Loc.Instance.IsChinese ? 1 : 0;
        _syncingLanguage = false;

        Overview = new OverviewViewModel(shell);
        Actions = new ActionsViewModel(shell);
        Inputs = new InputsViewModel();
        Pickers = new PickersViewModel(shell);
        Layout = new LayoutViewModel(shell);
        Feedback = new FeedbackViewModel(shell);
        DataDisplay = new DataDisplayViewModel(shell);
        DataViews = new DataViewsViewModel(shell);
        Shaders = new ShadersViewModel(shell);
        Charts = new ChartsViewModel();
        SystemTray = new SystemTrayViewModel(shell, ToggleDarkMode, ToggleLanguage);
        Showcase = new ShowcaseViewModel();

        foreach (var accent in Enum.GetValues<VelaAccent>())
        {
            var palette = VelaAccentPalettes.Get(accent, isDark: false);
            AccentSwatches.Add(new AccentSwatchViewModel(accent, palette.ControlOn));
        }

        RefreshSwatchRings();
        TelemetryText = Loc.Instance.Pick("Ready · KeepAlive · Instant", "就绪 · KeepAlive · 即时");

        Loc.Instance.LanguageChanged += OnLanguageChanged;
    }

    private void OnLanguageChanged(AppLanguage lang)
    {
        _syncingLanguage = true;
        LanguageIndex = lang == AppLanguage.Zh ? 1 : 0;
        _syncingLanguage = false;
        TelemetryText = Loc.Instance.Pick("Ready · KeepAlive · Instant", "就绪 · KeepAlive · 即时");
    }

    partial void OnLanguageIndexChanged(int value)
    {
        if (_syncingLanguage) return;
        Loc.Instance.SetLanguage(value == 1 ? AppLanguage.Zh : AppLanguage.En);
    }

    partial void OnIsDarkModeChanged(bool value)
    {
        _shell.SetThemeVariant(value);
        Overview.SyncHeroShaderColor(SelectedAccent, value);
        RefreshSwatchRings();
    }

    partial void OnReducedMotionChanged(bool value)
    {
        VelaMotionOptions.ReducedMotion = value;
    }

    partial void OnSelectedAccentChanged(VelaAccent value)
    {
        _shell.SetAccent(value);
        Overview.SyncHeroShaderColor(value, IsDarkMode);
        RefreshSwatchRings();
    }

    [RelayCommand]
    private void SelectAccent(VelaAccent accent) => SelectedAccent = accent;

    [RelayCommand]
    private void ToggleDarkMode() => IsDarkMode = !IsDarkMode;

    [RelayCommand]
    private void ToggleLanguage() => Loc.Instance.ToggleLanguage();

    [RelayCommand]
    private void TogglePane() => IsPaneOpen = !IsPaneOpen;

    [RelayCommand]
    private void OpenCommandPalette() => _shell.OpenCommandPalette();

    [RelayCommand]
    private async Task OpenShellSettingsAsync()
    {
        var loc = Loc.Instance;
        var drawer = new ShellSettingsDrawer { DataContext = this };
        await _shell.ShowDrawerAsync(drawer, new VelaDrawerOptions
        {
            Title = loc.Get("StrShellSettings"),
            Description = loc.Get("StrShellSettingsDesc"),
            Side = VelaDrawerSide.Right,
            Width = 390,
            Material = VelaSurfaceMaterial.GlassCard,
        });
    }

    [RelayCommand]
    private void ClearPageCache()
    {
        _shell.ClearInactivePageCache();
        TelemetryText = Loc.Instance.Pick("Inactive page cache cleared", "已清理非活跃页面缓存");
        _shell.ShowToast(
            Loc.Instance.Pick("Cache cleared", "缓存已清理"),
            Loc.Instance.Pick("Inactive cached pages evicted.", "已释放所有非活跃缓存页面。"),
            VelaToastTone.Success);
    }

    public void RecordPageSwitch(VelaPageSwitchMetrics m)
    {
        var label = (m.PageKey as VelaNavItem)?.Header ?? m.PageKey?.ToString() ?? "Page";
        var hit = m.IsCacheHit
            ? Loc.Instance.Pick("HIT", "命中")
            : Loc.Instance.Pick("MISS", "新建");
        LastSwitchCacheHit = m.IsCacheHit;
        TelemetryText = Loc.Instance.IsChinese
            ? $"{label} · {m.Elapsed.TotalMilliseconds:0.00} ms · {hit} · 已缓存 {m.CachedCount}"
            : $"{label} · {m.Elapsed.TotalMilliseconds:0.00} ms · {hit} · {m.CachedCount} cached";
    }

    public IEnumerable<VelaCommandItem> BuildCommandPaletteItems()
    {
        var loc = Loc.Instance;
        Geometry? Icon(string key) => _shell.FindIcon(key);
        var pagesGroup = loc.Get("StrCmdGroupPages");
        var appearGroup = loc.Get("StrCmdGroupAppearance");
        var actionsGroup = loc.Get("StrCmdGroupActions");

        yield return NavCmd(loc.Get("StrNavOverview"), "overview", "VelaIconCompass", "G O");
        yield return NavCmd(loc.Get("StrNavActions"), "actions", "VelaIconComponents", "G A");
        yield return NavCmd(loc.Get("StrNavInputs"), "inputs", "VelaIconSliders", "G I");
        yield return NavCmd(loc.Get("StrNavPickers"), "pickers", "VelaIconCalendar", "G P");
        yield return NavCmd(loc.Get("StrNavLayout"), "layout", "VelaIconLayers", "G N");
        yield return NavCmd(loc.Get("StrNavFeedback"), "feedback", "VelaIconBell", "G F");
        yield return NavCmd(loc.Get("StrNavDisplay"), "display", "VelaIconHome", "G D");
        yield return NavCmd(loc.Get("StrNavDataViews"), "dataviews", "VelaIconTable", "G V");
        yield return NavCmd(loc.Get("StrNavShaders"), "shaders", "VelaIconSparkles", "G S");
        yield return NavCmd(loc.Get("StrNavCharts"), "charts", "VelaIconChart", "G C");
        yield return NavCmd(loc.Get("StrNavTray"), "tray", "VelaIconBell", "G T");
        yield return NavCmd(loc.Get("StrNavShowcase"), "showcase", "VelaIconSparkles", "G W");
        yield return NavCmd(loc.Get("StrNavStress"), "stress", "VelaIconTrendingUp", "G B");

        yield return new VelaCommandItem
        {
            Title = loc.Get("StrCmdSwitchLang"),
            Group = appearGroup,
            Icon = Icon("VelaIconGlobe"),
            Shortcut = "⌘L",
            Execute = ToggleLanguage,
        };
        yield return new VelaCommandItem
        {
            Title = loc.Get("StrCmdToggleDark"),
            Group = appearGroup,
            Icon = Icon("VelaIconPalette"),
            Shortcut = "⌘D",
            Execute = ToggleDarkMode,
        };
        yield return new VelaCommandItem
        {
            Title = loc.Get("StrCmdToggleSidebar"),
            Group = appearGroup,
            Icon = Icon("VelaIconSidebar"),
            Shortcut = "⌘\\",
            Execute = TogglePane,
        };
        yield return new VelaCommandItem
        {
            Title = loc.Get("StrCmdTogglePaletteGlass"),
            Group = appearGroup,
            Icon = Icon("VelaIconSparkles"),
            Execute = () =>
            {
                CommandPaletteGlass = !CommandPaletteGlass;
                _shell.ShowToast(
                    loc.Pick("Command Palette", "命令面板"),
                    CommandPaletteGlass
                        ? loc.Pick("Liquid Glass enabled", "已开启液态玻璃模式")
                        : loc.Pick("Solid mode enabled", "已切换为纯色模式"),
                    VelaToastTone.Info);
            },
        };
        yield return new VelaCommandItem
        {
            Title = loc.Get("StrShellSettings"),
            Group = appearGroup,
            Icon = Icon("VelaIconSettings"),
            Execute = () => _ = OpenShellSettingsAsync(),
        };
        yield return new VelaCommandItem
        {
            Title = loc.Get("StrCmdClearCache"),
            Group = actionsGroup,
            Icon = Icon("VelaIconTrash"),
            Execute = ClearPageCache,
        };

        VelaCommandItem NavCmd(string title, string key, string iconKey, string shortcut) => new()
        {
            Title = title,
            Group = pagesGroup,
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
