using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Vela.Avalonia.Controls;
using Vela.Avalonia.Motion;
using Vela.Avalonia.Tokens;

namespace Vela.Avalonia.Gallery;

/// <summary>
/// Heavy benchmark page (500 interactive visual cards) implementing IVelaNavigationAware
/// so you can inspect real construction count, visual-tree attach count, and scroll preservation.
/// </summary>
internal sealed class HeavyStressPage : UserControl, IVelaNavigationAware
{
    private static int _totalInstancesCreated;
    private readonly int _instanceId;
    private int _attachCount;
    private int _navigatedToCount;
    private readonly TextBlock _statsText;

    public HeavyStressPage()
    {
        _instanceId = ++_totalInstancesCreated;

        _statsText = new TextBlock
        {
            FontSize = 13,
            Foreground = Brushes.Gray,
        };

        var wrap = new WrapPanel
        {
            Orientation = Orientation.Horizontal,
            ItemSpacing = 8,
            LineSpacing = 8,
        };

        for (var i = 1; i <= 500; i++)
        {
            var card = new Border
            {
                Width = 148,
                Height = 52,
                CornerRadius = new CornerRadius(10),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10, 6),
                [!Border.BackgroundProperty] = this.GetResourceObservable("VelaSurfaceBrush").ToBinding(),
                [!Border.BorderBrushProperty] = this.GetResourceObservable("VelaBorderBrush").ToBinding(),
                Child = new StackPanel
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    Children =
                    {
                        new TextBlock { Text = $"Node #{i:000}", FontSize = 12.5, FontWeight = FontWeight.Medium },
                        new TextBlock
                        {
                            Text = $"Inst #{_instanceId} · Ready",
                            FontSize = 11,
                            [!TextBlock.ForegroundProperty] = this.GetResourceObservable("VelaTextMutedBrush").ToBinding(),
                        },
                    },
                },
            };
            wrap.Children.Add(card);
        }

        Content = new ScrollViewer
        {
            Padding = new Thickness(32, 28),
            Content = new StackPanel
            {
                Spacing = 16,
                Children =
                {
                    new TextBlock { Text = "Heavy Stress Benchmark (500 Visual Nodes)", FontSize = 22, FontWeight = FontWeight.SemiBold },
                    _statsText,
                    wrap,
                },
            },
        };
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _attachCount++;
        UpdateStats();
    }

    public void OnNavigatedTo()
    {
        _navigatedToCount++;
        UpdateStats();
    }

    public void OnNavigatedFrom()
    {
    }

    private void UpdateStats()
    {
        _statsText.Text =
            $"Page Instance #{_instanceId} · VisualTree Attaches: {_attachCount} · OnNavigatedTo calls: {_navigatedToCount} — " +
            "Scroll halfway down, switch tabs, and return: KeepAlive preserves scroll position & stays at Attach=1 (<0.2ms); None rebuilds from scratch.";
    }
}

public partial class MainWindow : Window
{
    private static readonly VelaDonutDatum[][] Datasets =
    [
        [
            new("organic", "Organic Search", 4820),
            new("direct", "Direct", 2910),
            new("social", "Social Media", 1640),
            new("referral", "Referrals", 930),
        ],
        [
            new("organic", "Organic Search", 6140),
            new("direct", "Direct", 1850),
            new("social", "Social Media", 3420),
            new("referral", "Referrals", 1490),
        ],
        [
            new("organic", "Enterprise", 18400),
            new("direct", "Team Pro", 12900),
            new("social", "Indie", 6400),
            new("referral", "Add-ons", 3100),
        ],
    ];

    private static readonly string[] DatasetLabels =
    [
        "Q1 visits",
        "Q2 visits",
        "MRR ($)",
    ];

    private readonly Dictionary<VelaAccent, Border> _swatchBorders = new();
    private bool _morphBusy;

    public MainWindow()
    {
        InitializeComponent();

        // Lazy page factory: HeavyStressPage (500 controls) is NOT allocated at app startup!
        StressNavItem.PageFactory = static () => new HeavyStressPage();

        VolumeSlider.Marks = [0, 25, 50, 75, 100];
        DetentSlider.Marks = [0, 50, 100, 150, 200];

        RootNav.PropertyChanged += (_, e) =>
        {
            if (e.Property == VelaNavigationView.IsPaneOpenProperty && PaneOpenSwitch is not null)
                PaneOpenSwitch.IsChecked = RootNav.IsPaneOpen;
        };

        BuildAccentSwatches();
        ApplyDataset(0);
    }

    private void OnPageSwitched(object? sender, VelaPageSwitchMetrics m)
    {
        if (TelemetryText is null) return;
        var label = (m.PageKey as VelaNavItem)?.Header ?? m.PageKey?.ToString() ?? "None";
        var hitText = m.IsCacheHit ? "CACHE HIT" : "BUILD / MISS";
        TelemetryText.Text =
            $"Page: {label}  ·  Switch: {m.Elapsed.TotalMilliseconds:0.00} ms  ·  {hitText}  ·  Cached: {m.CachedCount}  ·  Mode: {m.CacheMode}";

        var dotKey = m.IsCacheHit ? "VelaSuccessBrush" : "VelaWarningBrush";
        if (this.TryFindResource(dotKey, ActualThemeVariant, out var res) && res is IBrush b)
            TelemetryDot.Background = b;
    }

    private void OnCacheModeChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (RootNav is null || CacheModeSegmented is null) return;
        RootNav.PageCacheMode = CacheModeSegmented.SelectedIndex switch
        {
            0 => VelaPageCacheMode.KeepAlive,
            1 => VelaPageCacheMode.Lru,
            2 => VelaPageCacheMode.None,
            _ => VelaPageCacheMode.KeepAlive,
        };
        RootNav.MaxCachedPages = 3;
    }

    private void OnTransitionModeChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (RootNav is null || TransitionModeSegmented is null) return;
        RootNav.PageTransitionMode = TransitionModeSegmented.SelectedIndex == 1
            ? VelaPageTransitionMode.CompositorFade
            : VelaPageTransitionMode.Instant;
    }

    private void OnClearCacheClick(object? sender, RoutedEventArgs e)
    {
        RootNav.ClearInactivePageCache();
        TelemetryText.Text = "Inactive cached pages cleared (active page retained).";
    }

    private void BuildAccentSwatches()
    {
        var currentAccent = (Application.Current as App)?.Theme?.Accent ?? VelaAccent.Violet;

        foreach (var accent in Enum.GetValues<VelaAccent>())
        {
            var palette = VelaAccentPalettes.Get(accent, isDark: false);
            var dot = new Border
            {
                Width = 22,
                Height = 22,
                CornerRadius = new CornerRadius(999),
                Background = new SolidColorBrush(palette.ControlOn),
                BorderThickness = new Thickness(2),
                BorderBrush = accent == currentAccent ? Brushes.Black : Brushes.Transparent,
                Cursor = new Cursor(StandardCursorType.Hand),
            };
            ToolTip.SetTip(dot, accent.ToString());
            dot.PointerPressed += (_, _) => SelectAccent(accent);
            _swatchBorders[accent] = dot;
            AccentSwatchesPanel.Children.Add(dot);
        }

        UpdateSwatchSelection(currentAccent);
    }

    private void SelectAccent(VelaAccent accent)
    {
        (Application.Current as App)?.SetAccent(accent);
        UpdateSwatchSelection(accent);
    }

    private void UpdateSwatchSelection(VelaAccent active)
    {
        var isDark = Application.Current?.ActualThemeVariant == ThemeVariant.Dark;
        var ring = isDark ? Brushes.White : Brushes.Black;
        foreach (var (accent, border) in _swatchBorders)
            border.BorderBrush = accent == active ? ring : Brushes.Transparent;
    }

    private void OnDisplayModeChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (RootNav is null || DisplayModeSegmented is null) return;
        RootNav.DisplayMode = DisplayModeSegmented.SelectedIndex == 1
            ? VelaNavigationDisplayMode.Top
            : VelaNavigationDisplayMode.Left;
    }

    private void OnPaneOpenSwitchChanged(object? sender, RoutedEventArgs e)
    {
        if (RootNav is null || PaneOpenSwitch is null) return;
        RootNav.IsPaneOpen = PaneOpenSwitch.IsChecked == true;
    }

    private void OnSearchVisibleChanged(object? sender, RoutedEventArgs e)
    {
        if (RootNav is null || SearchVisibleSwitch is null) return;
        RootNav.IsSearchVisible = SearchVisibleSwitch.IsChecked == true;
    }

    private void OnDarkModeChanged(object? sender, RoutedEventArgs e)
    {
        if (Application.Current is null) return;
        var isDark = DarkModeSwitch.IsChecked == true;
        Application.Current.RequestedThemeVariant = isDark ? ThemeVariant.Dark : ThemeVariant.Light;
        var currentAccent = (Application.Current as App)?.Theme?.Accent ?? VelaAccent.Violet;
        UpdateSwatchSelection(currentAccent);
    }

    private void OnReducedMotionChanged(object? sender, RoutedEventArgs e)
    {
        VelaMotionOptions.ReducedMotion = ReducedMotionSwitch.IsChecked == true;
    }

    private async void OnMorphButtonClick(object? sender, RoutedEventArgs e)
    {
        if (_morphBusy) return;
        _morphBusy = true;

        MorphActionButton.IsLoading = true;
        await Task.Delay(900);
        MorphActionButton.IsLoading = false;
        MorphActionButton.Content = "Saved changes ✓";

        await Task.Delay(1400);
        MorphActionButton.Content = "Save changes";
        _morphBusy = false;
    }

    private void OnPlanSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (PlanStatusText is null) return;
        PlanStatusText.Text = PlanSegmented.SelectedIndex switch
        {
            0 => "Active plan: Monthly — flexible billing, cancel anytime.",
            1 => "Active plan: Quarterly — billed every 3 months with priority support.",
            2 => "Active plan: Yearly — 20% off with annual invoice and SSO included.",
            _ => string.Empty,
        };
    }

    private void OnChartDatasetChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (ShowcaseDonut is null) return;
        ApplyDataset(Math.Clamp(ChartDatasetSegmented.SelectedIndex, 0, Datasets.Length - 1));
    }

    private void ApplyDataset(int index)
    {
        ShowcaseDonut.TotalLabel = DatasetLabels[index];
        ShowcaseDonut.Data = Datasets[index];
    }
}
