using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Vela.Avalonia.Charts;
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

public partial class MainView : UserControl
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
    private bool _draggingGlass;
    private Point _glassDragStartPointer;
    private Point _glassDragStartPos;

    public MainView()
    {
        InitializeComponent();

        // Lazy page factory: HeavyStressPage (500 controls) is NOT allocated at app startup!
        StressNavItem.PageFactory = static () => new HeavyStressPage();

        FrameworkComboBox.ItemsSource = new[]
        {
            "Avalonia 12.2",
            "SkiaSharp",
            ".NET 10 LTS",
            "ReactiveUI",
            "CommunityToolkit.Mvvm",
            "Vela.Avalonia",
            "Arc Design Tokens",
        };
        FrameworkComboBox.SelectedItem = "Avalonia 12.2";

        VolumeSlider.Marks = [0, 25, 50, 75, 100];
        DetentSlider.Marks = [0, 50, 100, 150, 200];

        RootNav.PropertyChanged += (_, e) =>
        {
            if (e.Property == VelaNavigationView.IsPaneOpenProperty && PaneOpenSwitch is not null)
                PaneOpenSwitch.IsChecked = RootNav.IsPaneOpen;
        };

        BuildAccentSwatches();
        PopulateCommandPalette();
        InitPhase6Samples();
        InitPhase7Charts();
        ApplyDataset(0);
    }

    private readonly Random _rng = new(42);

    private void InitPhase7Charts()
    {
        ApplyLineRange(0);
        ApplyBarRange(0);

        Spark1.Values = [18, 22, 19, 27, 34, 31, 42, 46, 44, 58];
        Spark2.Values = [4.2, 3.8, 3.5, 2.9, 2.6, 2.2, 2.1, 1.9, 1.85, 1.8];
        Spark3.Values = [0.22, 0.18, 0.25, 0.14, 0.11, 0.09, 0.08, 0.05, 0.06, 0.04];

        var entries = new List<VelaHeatmapEntry>();
        var today = DateTime.Today;
        var seed = new Random(2026);
        for (var i = 0; i < 260; i++)
        {
            var d = today.AddDays(-i);
            var roll = seed.NextDouble();
            var count = roll < 0.28 ? 0 : roll < 0.55 ? seed.Next(1, 4) : roll < 0.82 ? seed.Next(4, 10) : seed.Next(10, 18);
            entries.Add(new VelaHeatmapEntry(d, count));
        }
        ShowcaseHeatmap.Entries = entries;
    }

    private void OnLineRangeChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (ShowcaseLineChart is null || LineRangeSegmented is null) return;
        ApplyLineRange(LineRangeSegmented.SelectedIndex);
    }

    private void ApplyLineRange(int idx)
    {
        if (idx == 0)
        {
            ShowcaseLineChart.Labels = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"];
            ShowcaseLineChart.Series =
            [
                new("rev", "Net Revenue", [12400, 14800, 13900, 18200, 21600, 19400, 24100]),
                new("prev", "Prior Period", [10200, 11400, 12100, 13800, 15200, 14900, 16800]),
            ];
        }
        else if (idx == 1)
        {
            ShowcaseLineChart.Labels = ["W1", "W2", "W3", "W4", "W5", "W6", "W7"];
            ShowcaseLineChart.Series =
            [
                new("rev", "Net Revenue", [32000, 38500, 41200, 39800, 46900, 52400, 58100]),
                new("prev", "Prior Period", [26000, 29400, 31100, 33600, 35800, 39200, 42500]),
            ];
        }
        else
        {
            ShowcaseLineChart.Labels = ["May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov"];
            ShowcaseLineChart.Series =
            [
                new("rev", "Net Revenue", [84000, 96000, 112000, 108000, 134000, 149000, 172000]),
                new("prev", "Prior Period", [62000, 71000, 79000, 86000, 94000, 106000, 118000]),
            ];
        }
    }

    private void OnBarRangeChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (ShowcaseBarChart is null || BarRangeSegmented is null) return;
        ApplyBarRange(BarRangeSegmented.SelectedIndex);
    }

    private void ApplyBarRange(int idx)
    {
        ShowcaseBarChart.Data = idx == 0
            ? [
                new("Mon", 420), new("Tue", 580), new("Wed", 510),
                new("Thu", 760), new("Fri", 890), new("Sat", 640), new("Sun", 710),
              ]
            : [
                new("Mon", 690), new("Tue", 520), new("Wed", 840),
                new("Thu", 610), new("Fri", 720), new("Sat", 930), new("Sun", 580),
              ];
    }

    private void OnRandomizeGaugesClick(object? sender, RoutedEventArgs e)
    {
        GaugeCpu.Value = _rng.Next(15, 96);
        GaugeMem.Value = _rng.Next(25, 95);
        GaugeGpu.Value = _rng.Next(10, 99);
    }

    private bool _metricsToggle;

    private void InitPhase6Samples()
    {
        DemoTagInput.Tags.Add("Avalonia 12.2");
        DemoTagInput.Tags.Add("Liquid Glass");
        DemoTagInput.Tags.Add("OKLCH");

        var today = DateTime.Today;
        InlineCalendar.SelectedDate = today;
        DemoDatePicker.SelectedDate = today.AddDays(5);
    }

    private void OnSimulateMetricsClick(object? sender, RoutedEventArgs e)
    {
        _metricsToggle = !_metricsToggle;
        MetricRevenue.Value = _metricsToggle ? 74620 : 48250;
        MetricUsers.Value = _metricsToggle ? 19480 : 12940;
        MetricLatency.Value = _metricsToggle ? 0.08 : 0.12;
    }

    private void OnRadioLayoutChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (PlanRadioCards is null || RadioLayoutSegmented is null) return;
        PlanRadioCards.Layout = RadioLayoutSegmented.SelectedIndex == 1
            ? VelaRadioCardsLayout.List
            : VelaRadioCardsLayout.Grid;
    }

    private VelaSurfaceMaterial GetSelectedOverlayMaterial() => OverlayMaterialSegmented.SelectedIndex switch
    {
        1 => VelaSurfaceMaterial.Frosted,
        2 => VelaSurfaceMaterial.LiquidGlass,
        _ => VelaSurfaceMaterial.Solid,
    };

    private async void OnTestMaterialDialogClick(object? sender, RoutedEventArgs e)
    {
        var material = GetSelectedOverlayMaterial();
        var ok = await DialogHost.ShowConfirmAsync(new VelaConfirmDialogOptions
        {
            Title = $"Dialog · {material} Material",
            Description = "Every Vela overlay defaults to Solid and can opt into Frosted (BackdropEffect) or full optical LiquidGlass with one property.",
            ConfirmText = "Looks great",
            CancelText = "Close",
            Material = material,
        });

        if (ok)
            ToastHost.Show($"Material: {material}", "Confirmed from unified overlay material dialog.", VelaToastTone.Success);
    }

    private async void OnTestMaterialDrawerClick(object? sender, RoutedEventArgs e)
    {
        var material = GetSelectedOverlayMaterial();
        var closeBtn = new VelaButton { Variant = VelaButtonVariant.Primary, Size = VelaControlSize.Small, Content = "Close Drawer" };
        closeBtn.Click += (_, _) => DrawerHost.Close();

        await DrawerHost.ShowAsync(new StackPanel
        {
            Spacing = 14,
            Children =
            {
                new TextBlock
                {
                    Text = $"This side drawer is rendered using VelaSurfaceMaterial.{material}. Notice how underlying content blurs and refracts cleanly along the rounded glass bezel.",
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 13.5,
                },
                new VelaBadge { Tone = VelaBadgeTone.Accent, IsPill = true, Content = material.ToString() },
                closeBtn,
            },
        }, new VelaDrawerOptions
        {
            Title = $"Drawer · {material}",
            Description = "Unified overlay surface material demo.",
            Side = VelaDrawerSide.Right,
            Width = 380,
            Material = material,
        });
    }

    private void PopulateCommandPalette()
    {
        Geometry? Icon(string key) => this.TryFindResource(key, ActualThemeVariant, out var r) ? r as Geometry : null;

        CommandPalette.Commands.Add(new VelaCommandItem
        {
            Title = "Go to Navigation & Performance",
            Description = "Benchmark VelaPageDeck caching",
            Group = "Navigation",
            Icon = Icon("VelaIconCompass"),
            Shortcut = "G N",
            Execute = () => RootNav.SelectedIndex = 0,
        });
        CommandPalette.Commands.Add(new VelaCommandItem
        {
            Title = "Go to Flagship Lab",
            Description = "CommandPalette, FloatingBar, ConfirmMorph, ScrubInput",
            Group = "Navigation",
            Icon = Icon("VelaIconHome"),
            Shortcut = "G F",
            Execute = () => RootNav.SelectedIndex = 1,
        });
        CommandPalette.Commands.Add(new VelaCommandItem
        {
            Title = "Go to Liquid Glass Lab",
            Description = "GPU SDF optical refraction playground",
            Group = "Navigation",
            Icon = Icon("VelaIconSparkles"),
            Shortcut = "G L",
            Execute = () => RootNav.SelectedIndex = 2,
        });
        CommandPalette.Commands.Add(new VelaCommandItem
        {
            Title = "Go to Pickers & Cards",
            Description = "RadioCards, TagInput, Calendar, DatePicker, MetricCard",
            Group = "Navigation",
            Icon = Icon("VelaIconCalendar"),
            Execute = () => RootNav.SelectedIndex = 3,
        });
        CommandPalette.Commands.Add(new VelaCommandItem
        {
            Title = "Go to Select & Menus",
            Description = "Select, ComboBox, Dropdown, Popover, ContextMenu",
            Group = "Navigation",
            Icon = Icon("VelaIconList"),
            Execute = () => RootNav.SelectedIndex = 5,
        });
        CommandPalette.Commands.Add(new VelaCommandItem
        {
            Title = "Go to Data & Charts",
            Description = "VelaDonutChart interactive ring",
            Group = "Navigation",
            Icon = Icon("VelaIconChart"),
            Execute = () => RootNav.SelectedIndex = 10,
        });
        CommandPalette.Commands.Add(new VelaCommandItem
        {
            Title = "Go to Desktop Kit",
            Description = "SettingsGroup, Stepper, Avatar, EmptyState, Copy, Split, Breadcrumb, Pagination",
            Group = "Navigation",
            Icon = Icon("VelaIconSettings"),
            Shortcut = "G D",
            Execute = () => RootNav.SelectedIndex = 11,
        });
        CommandPalette.Commands.Add(new VelaCommandItem
        {
            Title = "Go to Motion & Shaders Studio",
            Description = "ShaderBackground, HoldToConfirm, ColorPicker, RulerSlider, ResizablePanels, BusyOverlay",
            Group = "Navigation",
            Icon = Icon("VelaIconSparkles"),
            Shortcut = "G M",
            Execute = () => RootNav.SelectedIndex = 12,
        });
        CommandPalette.Commands.Add(new VelaCommandItem
        {
            Title = "Toggle Dark Mode",
            Description = "Switch Light and Dark OKLCH themes",
            Group = "Appearance",
            Icon = Icon("VelaIconPalette"),
            Shortcut = "⌘D",
            Execute = () => DarkModeSwitch.IsChecked = !(DarkModeSwitch.IsChecked == true),
        });
        CommandPalette.Commands.Add(new VelaCommandItem
        {
            Title = "Toggle Sidebar Collapse",
            Description = "Expand or compact navigation icon rail",
            Group = "Appearance",
            Icon = Icon("VelaIconSidebar"),
            Shortcut = "⌘\\",
            Execute = () => RootNav.IsPaneOpen = !RootNav.IsPaneOpen,
        });
        CommandPalette.Commands.Add(new VelaCommandItem
        {
            Title = "Toggle Command Palette Liquid Glass",
            Description = "Switch solid surface vs optical glass palette",
            Group = "Appearance",
            Icon = Icon("VelaIconSparkles"),
            Execute = () =>
            {
                CommandPalette.UseLiquidGlass = !CommandPalette.UseLiquidGlass;
                if (PaletteGlassSwitch is not null)
                    PaletteGlassSwitch.IsChecked = CommandPalette.UseLiquidGlass;
                ToastHost.Show("Palette material updated",
                    CommandPalette.UseLiquidGlass ? "Switched to Liquid Glass mode." : "Switched to Solid mode.",
                    VelaToastTone.Info);
            },
        });
        CommandPalette.Commands.Add(new VelaCommandItem
        {
            Title = "Clear Inactive Page Cache",
            Description = "Evict hidden cached pages from VelaPageDeck",
            Group = "Actions",
            Icon = Icon("VelaIconTrash"),
            Execute = () =>
            {
                RootNav.ClearInactivePageCache();
                ToastHost.Show("Cache cleared", "Inactive cached pages evicted.", VelaToastTone.Success);
            },
        });
    }

    private void OnOpenCommandPaletteClick(object? sender, RoutedEventArgs e)
        => CommandPalette.Open();

    private void OnPaletteGlassModeChanged(object? sender, RoutedEventArgs e)
    {
        if (CommandPalette is null || PaletteGlassSwitch is null) return;
        CommandPalette.UseLiquidGlass = PaletteGlassSwitch.IsChecked == true;
    }

    private void OnFloatingEditClick(object? sender, EventArgs e)
        => ToastHost.Show("FloatingBar: Edit", "Action fired from liquid glass bar.", VelaToastTone.Info);

    private void OnFloatingCopyClick(object? sender, EventArgs e)
        => ToastHost.Show("FloatingBar: Copy", "Copied selection to clipboard.", VelaToastTone.Success);

    private void OnFloatingDeleteClick(object? sender, EventArgs e)
        => ToastHost.Show("FloatingBar: Delete", "Item moved to trash.", VelaToastTone.Danger);

    private void OnBranchDeleted(object? sender, EventArgs e)
        => ToastHost.Show("Branch deleted", "You can still click Undo inside the morphing pill.", VelaToastTone.Warning);

    private void OnBranchRestored(object? sender, EventArgs e)
        => ToastHost.Show("Branch restored", "main-v2 is back.", VelaToastTone.Success);

    private void OnTabsVariantChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DemoTabs is null || TabsVariantSegmented is null) return;
        DemoTabs.Variant = TabsVariantSegmented.SelectedIndex == 1
            ? VelaTabsVariant.Underline
            : VelaTabsVariant.Pill;
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

    private async void OnShowConfirmDialogClick(object? sender, RoutedEventArgs e)
    {
        var ok = await DialogHost.ShowConfirmAsync(new VelaConfirmDialogOptions
        {
            Title = "Publish release v0.1.0?",
            Description = "This will tag the commit and push NuGet packages for Vela.Avalonia.",
            ConfirmText = "Publish",
            CancelText = "Cancel",
        });

        ToastHost.Show(
            ok ? "Release published" : "Publish cancelled",
            ok ? "NuGet package queued for indexing." : "No changes were made.",
            ok ? VelaToastTone.Success : VelaToastTone.Neutral);
    }

    private async void OnShowDestructiveDialogClick(object? sender, RoutedEventArgs e)
    {
        var ok = await DialogHost.ShowConfirmAsync(new VelaConfirmDialogOptions
        {
            Title = "Delete workspace cache?",
            Description = "All cached page snapshots and local preferences will be cleared immediately.",
            ConfirmText = "Delete cache",
            CancelText = "Keep",
            IsDestructive = true,
        });

        if (ok)
        {
            RootNav.ClearInactivePageCache();
            ToastHost.Show("Cache cleared", "Inactive cached pages were evicted.", VelaToastTone.Danger);
        }
    }

    private async void OnShowCustomDialogClick(object? sender, RoutedEventArgs e)
    {
        var nameInput = new VelaInput
        {
            Label = "Project name",
            PlaceholderText = "e.g. Vela Desktop Studio",
            IsClearable = true,
        };

        var cancelBtn = new VelaButton { Variant = VelaButtonVariant.Secondary, Size = VelaControlSize.Small, Content = "Cancel" };
        var createBtn = new VelaButton { Variant = VelaButtonVariant.Primary, Size = VelaControlSize.Small, Content = "Create" };

        cancelBtn.Click += (_, _) => DialogHost.Close(null);
        createBtn.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(nameInput.Text))
            {
                nameInput.ErrorMessage = "Please enter a project name.";
                return;
            }
            DialogHost.Close(nameInput.Text.Trim());
        };

        var content = new StackPanel
        {
            Spacing = 18,
            Children =
            {
                nameInput,
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 10,
                    Children = { cancelBtn, createBtn },
                },
            },
        };

        var createdName = await DialogHost.ShowAsync<string>(content, new VelaDialogOptions
        {
            Title = "Create new project",
            Description = "Configure your workspace name to get started.",
        });

        if (!string.IsNullOrEmpty(createdName))
        {
            ToastHost.Show("Project created", $"Workspace \"{createdName}\" is ready.", VelaToastTone.Success);
        }
    }

    private void OnToastSuccessClick(object? sender, RoutedEventArgs e)
        => ToastHost.Show("Changes saved", "Your theme configuration has been synced.", VelaToastTone.Success);

    private void OnToastUndoClick(object? sender, RoutedEventArgs e)
        => ToastHost.Show(
            "Archived 3 items",
            "Hover to pause auto-dismiss or click Undo.",
            VelaToastTone.Info,
            actionLabel: "Undo",
            onAction: () => ToastHost.Show("Restored", "The 3 items are back in your inbox.", VelaToastTone.Success));

    private void OnToastWarningClick(object? sender, RoutedEventArgs e)
        => ToastHost.Show("High memory usage", "Consider switching PageCacheMode to LRU (Max 3).", VelaToastTone.Warning);

    private void OnToastDangerClick(object? sender, RoutedEventArgs e)
        => ToastHost.Show("Connection lost", "Retrying WebSocket handshake in 5 seconds...", VelaToastTone.Danger);

    private void OnMenuRenameClick(object? sender, EventArgs e)
        => ToastHost.Show("Rename", "Triggered from VelaDropdownButton.", VelaToastTone.Info);

    private void OnMenuDuplicateClick(object? sender, EventArgs e)
        => ToastHost.Show("Duplicated", "Created a copy of the document.", VelaToastTone.Success);

    private void OnMenuShareClick(object? sender, EventArgs e)
        => ToastHost.Show("Link copied", "Share URL copied to clipboard.", VelaToastTone.Info);

    private void OnMenuDeleteClick(object? sender, EventArgs e)
        => ToastHost.Show("Moved to Trash", "Document deleted.", VelaToastTone.Danger, actionLabel: "Undo",
            onAction: () => ToastHost.Show("Restored", "Document restored.", VelaToastTone.Success));

    private void OnClosePopoverClick(object? sender, RoutedEventArgs e)
        => QuickSettingsPopover.IsOpen = false;

    private void OnContextEditClick(object? sender, RoutedEventArgs e)
        => ToastHost.Show("ContextMenu: Edit", "Invoked native themed ContextMenu item.", VelaToastTone.Info);

    private void OnContextCopyClick(object? sender, RoutedEventArgs e)
        => ToastHost.Show("ContextMenu: Copy", "Path copied to clipboard.", VelaToastTone.Success);

    private void OnContextDeleteClick(object? sender, RoutedEventArgs e)
        => ToastHost.Show("ContextMenu: Delete", "Danger menu item triggered.", VelaToastTone.Danger);

    private void OnProgress0Click(object? sender, RoutedEventArgs e) => SetDemoProgress(0);
    private void OnProgress35Click(object? sender, RoutedEventArgs e) => SetDemoProgress(35);
    private void OnProgress72Click(object? sender, RoutedEventArgs e) => SetDemoProgress(72);
    private void OnProgress100Click(object? sender, RoutedEventArgs e) => SetDemoProgress(100);

    private void SetDemoProgress(double value)
    {
        IndeterminateSwitch.IsChecked = false;
        DemoProgress.IsIndeterminate = false;
        DemoProgress.Value = value;
    }

    private void OnIndeterminateChanged(object? sender, RoutedEventArgs e)
    {
        if (DemoProgress is null || IndeterminateSwitch is null) return;
        DemoProgress.IsIndeterminate = IndeterminateSwitch.IsChecked == true;
    }

    private void OnSkeletonToggleChanged(object? sender, RoutedEventArgs e)
    {
        if (SkelAvatar is null || SkeletonLoadingSwitch is null) return;
        var loading = SkeletonLoadingSwitch.IsChecked == true;
        SkelAvatar.IsLoading = loading;
        SkelLine1.IsLoading = loading;
        SkelLine2.IsLoading = loading;
    }

    private void OnRestoreAlertsClick(object? sender, RoutedEventArgs e)
    {
        AlertInfo.Restore();
        AlertSuccess.Restore();
        AlertWarning.Restore();
    }

    private async void OnOpenRightDrawerClick(object? sender, RoutedEventArgs e)
    {
        var closeBtn = new VelaButton { Variant = VelaButtonVariant.Primary, Size = VelaControlSize.Small, Content = "Apply & Close" };
        closeBtn.Click += (_, _) =>
        {
            DrawerHost.Close();
            ToastHost.Show("Inspector updated", "Drawer changes applied.", VelaToastTone.Success);
        };

        var body = new StackPanel
        {
            Spacing = 16,
            Children =
            {
                new VelaInput { Label = "Component Title", Text = "VelaDrawerHost", IsClearable = true },
                new VelaInput { Label = "Namespace", Text = "Vela.Avalonia.Controls" },
                new VelaCheckBox { Content = "Close on backdrop click", IsChecked = true },
                new VelaCheckBox { Content = "Trap Escape key", IsChecked = true },
                closeBtn,
            },
        };

        await DrawerHost.ShowAsync(body, new VelaDrawerOptions
        {
            Title = "Inspector Drawer",
            Description = "Side sheet sliding smoothly from the right edge.",
            Side = VelaDrawerSide.Right,
            Width = 380,
        });
    }

    private async void OnOpenLeftDrawerClick(object? sender, RoutedEventArgs e)
    {
        var closeBtn = new VelaButton { Variant = VelaButtonVariant.Secondary, Size = VelaControlSize.Small, Content = "Close" };
        closeBtn.Click += (_, _) => DrawerHost.Close();

        var body = new StackPanel
        {
            Spacing = 14,
            Children =
            {
                new TextBlock
                {
                    Text = "Left drawers work great for quick filters, workspace switchers, or secondary navigation trees.",
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 13,
                },
                new VelaBadge { Tone = VelaBadgeTone.Info, IsPill = true, Content = "Left Edge · 340px" },
                closeBtn,
            },
        };

        await DrawerHost.ShowAsync(body, new VelaDrawerOptions
        {
            Title = "Quick Filters",
            Description = "Slide-over sheet from the left edge.",
            Side = VelaDrawerSide.Left,
            Width = 340,
        });
    }

    private void OnGlassDragStart(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(DraggableGlassPill).Properties.IsLeftButtonPressed)
            return;

        _draggingGlass = true;
        _glassDragStartPointer = e.GetPosition(GlassStageCanvas);
        _glassDragStartPos = new Point(Canvas.GetLeft(DraggableGlassPill), Canvas.GetTop(DraggableGlassPill));
        e.Pointer.Capture(DraggableGlassPill);
        e.Handled = true;
    }

    private void OnGlassDragMove(object? sender, PointerEventArgs e)
    {
        if (!_draggingGlass) return;
        var current = e.GetPosition(GlassStageCanvas);
        var nextX = _glassDragStartPos.X + (current.X - _glassDragStartPointer.X);
        var nextY = _glassDragStartPos.Y + (current.Y - _glassDragStartPointer.Y);

        var maxX = Math.Max(0, GlassStageCanvas.Bounds.Width - DraggableGlassPill.Bounds.Width);
        var maxY = Math.Max(0, GlassStageCanvas.Bounds.Height - DraggableGlassPill.Bounds.Height);
        Canvas.SetLeft(DraggableGlassPill, Math.Clamp(nextX, 0, maxX));
        Canvas.SetTop(DraggableGlassPill, Math.Clamp(nextY, 0, maxY));
        e.Handled = true;
    }

    private void OnGlassDragEnd(object? sender, PointerReleasedEventArgs e)
    {
        if (!_draggingGlass) return;
        _draggingGlass = false;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    private void OnGlassRefractionToggle(object? sender, RoutedEventArgs e)
    {
        if (DraggableGlassPill is null || GlassRefractionSwitch is null) return;
        var enabled = GlassRefractionSwitch.IsChecked == true;
        DraggableGlassPill.EnableRefraction = enabled;
        GlassDockSample.EnableRefraction = enabled;
    }

    private void OnGlassParamChanged(object? sender, double value)
    {
        if (DraggableGlassPill is null || GlassBlurSlider is null || GlassBevelSlider is null ||
            GlassRefractionSlider is null || GlassDispersionSlider is null)
            return;

        DraggableGlassPill.BlurRadius = GlassBlurSlider.Value;
        DraggableGlassPill.BevelWidth = GlassBevelSlider.Value;
        DraggableGlassPill.Refraction = GlassRefractionSlider.Value / 100.0;
        DraggableGlassPill.Dispersion = GlassDispersionSlider.Value / 100.0;

        GlassDockSample.BlurRadius = Math.Max(0, GlassBlurSlider.Value);
        GlassDockSample.Refraction = GlassRefractionSlider.Value / 100.0;
        GlassDockSample.Dispersion = GlassDispersionSlider.Value / 100.0;
    }

    private void ApplyDataset(int index)
    {
        ShowcaseDonut.TotalLabel = DatasetLabels[index];
        ShowcaseDonut.Data = Datasets[index];
    }

    // ─── Desktop Kit (Batch 1) Handlers ──────────────────────────────────

    private int _avatarStatusIdx;
    private int _emptyStateIdx;
    private static readonly string[] ExtraBreadcrumbFolders = ["Themes", "VelaStepper.axaml", "Templates", "Part_Host"];

    private void OnDemoCopied(object? sender, EventArgs e)
        => ToastHost.Show("Copied to clipboard", "Ready to paste anywhere.", VelaToastTone.Success);

    private void OnSettingsDiagnosticsClick(object? sender, EventArgs e)
        => ToastHost.Show("Diagnostics", "All 60fps spring runners idle · 0 shader warnings.", VelaToastTone.Info);

    private void OnStepperOrientationChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DemoStepper is null || StepperOrientationSegmented is null) return;
        DemoStepper.Orientation = StepperOrientationSegmented.SelectedIndex == 1
            ? VelaStepperOrientation.Vertical
            : VelaStepperOrientation.Horizontal;
    }

    private void OnDemoStepSelected(object? sender, int index)
        => ToastHost.Show($"Returned to Step {index + 1}", DemoStepper.Steps[index].Title ?? string.Empty, VelaToastTone.Neutral);

    private void OnStepperPrevClick(object? sender, RoutedEventArgs e)
    {
        if (DemoStepper.CurrentStep > 0)
            DemoStepper.CurrentStep--;
    }

    private void OnStepperNextClick(object? sender, RoutedEventArgs e)
    {
        if (DemoStepper.CurrentStep < DemoStepper.Steps.Count)
            DemoStepper.CurrentStep++;
    }

    private void OnStepperErrorToggleClick(object? sender, RoutedEventArgs e)
    {
        var idx = Math.Clamp(DemoStepper.CurrentStep, 0, DemoStepper.Steps.Count - 1);
        var step = DemoStepper.Steps[idx];
        step.ErrorText = string.IsNullOrEmpty(step.ErrorText) ? "Signing certificate expired" : null;
    }

    private void OnStepperCompleteAllClick(object? sender, RoutedEventArgs e)
    {
        foreach (var s in DemoStepper.Steps)
            s.ErrorText = null;
        DemoStepper.CurrentStep = DemoStepper.Steps.Count;
    }

    private void OnSplitPrimaryClick(object? sender, RoutedEventArgs e)
    {
        SplitActionStatus.Text = $"Executed primary action: \"{PrimarySplitButton.Content}\"";
        ToastHost.Show("Split Action Fired", PrimarySplitButton.Content?.ToString() ?? string.Empty, VelaToastTone.Success);
    }

    private void OnSplitModeProd(object? sender, EventArgs e)
    {
        PrimarySplitButton.Content = "Deploy now";
        SplitActionStatus.Text = "Primary action switched to Production deploy.";
    }

    private void OnSplitModeStaging(object? sender, EventArgs e)
    {
        PrimarySplitButton.Content = "Deploy staging";
        SplitActionStatus.Text = "Primary action morphed width to \"Deploy staging\".";
    }

    private void OnSplitModeDownload(object? sender, EventArgs e)
    {
        PrimarySplitButton.Content = "Download";
        SplitActionStatus.Text = "Primary action morphed width to \"Download\".";
    }

    private void OnSplitModeRollback(object? sender, EventArgs e)
        => ToastHost.Show("Rollback queued", "Reverting to previous stable artifact.", VelaToastTone.Danger);

    private void OnSplitShareClick(object? sender, RoutedEventArgs e)
        => ToastHost.Show("Share sheet", "Opened workspace sharing options.", VelaToastTone.Info);

    private void OnSplitCopyLink(object? sender, EventArgs e)
        => ToastHost.Show("Link copied", "https://vela.dev/gallery/desktop-kit", VelaToastTone.Success);

    private void OnSplitExportMd(object? sender, EventArgs e)
        => ToastHost.Show("Markdown exported", "Saved summary to clipboard.", VelaToastTone.Neutral);

    private void OnCycleAvatarStatusClick(object? sender, RoutedEventArgs e)
    {
        var statuses = new[]
        {
            VelaAvatarStatus.Online,
            VelaAvatarStatus.Busy,
            VelaAvatarStatus.Away,
            VelaAvatarStatus.Offline,
            VelaAvatarStatus.None,
        };
        _avatarStatusIdx = (_avatarStatusIdx + 1) % statuses.Length;
        StatusDemoAvatar.Status = statuses[_avatarStatusIdx];
    }

    private void OnCycleEmptyStateClick(object? sender, RoutedEventArgs e)
    {
        _emptyStateIdx = (_emptyStateIdx + 1) % 3;
        Geometry? Res(string key) => this.TryFindResource(key, ActualThemeVariant, out var r) && r is Geometry g ? g : null;

        switch (_emptyStateIdx)
        {
            case 0:
                DemoEmptyState.Icon = Res("VelaIconInbox");
                DemoEmptyState.Title = "Inbox is clear";
                DemoEmptyState.Description = "New deployment reviews, mentions, and CI summaries will appear right here.";
                break;
            case 1:
                DemoEmptyState.Icon = Res("VelaIconSearch");
                DemoEmptyState.Title = "No matching records";
                DemoEmptyState.Description = "Try widening your date filter or clearing active tags to see all workspace items.";
                break;
            default:
                DemoEmptyState.Icon = Res("VelaIconCheckCircle");
                DemoEmptyState.Title = "All checks passed";
                DemoEmptyState.Description = "Zero warnings, zero failed assertions, and every package is up to date.";
                break;
        }
    }

    private void OnEmptyStateActionClick(object? sender, RoutedEventArgs e)
        => ToastHost.Show("Workflow created", "Initialized default desktop CI pipeline.", VelaToastTone.Success);

    private void OnBreadcrumbItemClicked(object? sender, VelaBreadcrumbItem item)
    {
        var idx = DemoBreadcrumb.Items.IndexOf(item);
        if (idx < 0) return;
        while (DemoBreadcrumb.Items.Count > idx + 1)
            DemoBreadcrumb.Items.RemoveAt(DemoBreadcrumb.Items.Count - 1);
    }

    private void OnBreadcrumbDrillClick(object? sender, RoutedEventArgs e)
    {
        var depth = DemoBreadcrumb.Items.Count - 3;
        if (depth >= ExtraBreadcrumbFolders.Length)
        {
            ToastHost.Show("Max demo depth", "Click any ancestor crumb to jump back up.", VelaToastTone.Info);
            return;
        }

        DemoBreadcrumb.Items.Add(new VelaBreadcrumbItem
        {
            Label = ExtraBreadcrumbFolders[Math.Max(0, depth)],
        });
    }

    private void OnDemoPageChanged(object? sender, int page)
        => UpdatePaginationSummary();

    private void OnPaginationTotalChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DemoPagination is null || PaginationTotalSegmented is null) return;
        DemoPagination.TotalPages = PaginationTotalSegmented.SelectedIndex switch
        {
            0 => 5,
            2 => 24,
            _ => 12,
        };
        if (DemoPagination.CurrentPage > DemoPagination.TotalPages)
            DemoPagination.CurrentPage = DemoPagination.TotalPages;
        UpdatePaginationSummary();
    }

    private void UpdatePaginationSummary()
    {
        if (DemoPagination is null || PaginationSummaryText is null) return;
        var page = DemoPagination.CurrentPage;
        var total = DemoPagination.TotalPages;
        var start = (page - 1) * 10 + 1;
        var end = page * 10;
        PaginationSummaryText.Text = $"Showing items {start}–{end} of {total * 10} (Page {page} of {total})";
    }

    // ─── Motion & Shaders Studio (Batch 2) Handlers ──────────────────────

    private void OnShaderPresetChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (HeroShaderBg is null || ShaderPresetSegmented is null) return;
        HeroShaderBg.Preset = ShaderPresetSegmented.SelectedIndex switch
        {
            1 => VelaShaderBackgroundPreset.Aurora,
            2 => VelaShaderBackgroundPreset.Silk,
            _ => VelaShaderBackgroundPreset.Mesh,
        };
    }

    private void OnShaderAnimateChanged(object? sender, RoutedEventArgs e)
    {
        if (HeroShaderBg is null || ShaderAnimateSwitch is null) return;
        HeroShaderBg.IsAnimated = ShaderAnimateSwitch.IsChecked == true;
    }

    private void OnShaderTintPicked(object? sender, Color color)
    {
        if (HeroShaderBg is null) return;
        HeroShaderBg.PrimaryColor = color;
    }

    private void OnShaderResetTintClick(object? sender, RoutedEventArgs e)
    {
        HeroShaderBg.PrimaryColor = null;
        HeroShaderBg.SecondaryColor = null;
        HeroShaderBg.TertiaryColor = null;
        ToastHost.Show("Synced to Theme", "Shader palette now tracks VelaTheme Accent live.", VelaToastTone.Info);
    }

    private void OnHoldStateChanged(object? sender, bool holding)
    {
        if (HoldStatusText is null) return;
        HoldStatusText.Text = holding
            ? "Keep holding to confirm..."
            : "Tip: release halfway to watch spring rewind.";
    }

    private void OnHoldDangerConfirmed(object? sender, EventArgs e)
        => ToastHost.Show("Cache purged", "Local snapshot store cleared safely.", VelaToastTone.Danger);

    private void OnHoldAccentConfirmed(object? sender, EventArgs e)
        => ToastHost.Show("Published v1.0", "Release pipeline started.", VelaToastTone.Success);

    private void OnHoldNeutralConfirmed(object? sender, EventArgs e)
        => ToastHost.Show("Archive sealed", "Bundle locked with SHA-256 signature.", VelaToastTone.Info);

    private void OnHoldResetAllClick(object? sender, RoutedEventArgs e)
    {
        HoldDangerBtn.Reset();
        HoldAccentBtn.Reset();
        HoldNeutralBtn.Reset();
        HoldStatusText.Text = "All hold-to-confirm buttons rewound.";
    }

    private void OnBusySwitchChanged(object? sender, RoutedEventArgs e)
    {
        if (DemoBusyOverlay is null || BusyDemoSwitch is null) return;
        DemoBusyOverlay.IsBusy = BusyDemoSwitch.IsChecked == true;
    }

    private async void OnSimulateBusyTaskClick(object? sender, RoutedEventArgs e)
    {
        if (DemoBusyOverlay is null) return;
        DemoBusyOverlay.IsBusy = true;
        if (BusyDemoSwitch is not null) BusyDemoSwitch.IsChecked = true;
        await Task.Delay(2200);
        DemoBusyOverlay.IsBusy = false;
        if (BusyDemoSwitch is not null) BusyDemoSwitch.IsChecked = false;
        ToastHost.Show("Task completed", "Workspace shaders indexed in 2.2s.", VelaToastTone.Success);
    }

    private void OnInlineColorChanged(object? sender, Color color)
    {
        if (ColorPreviewBanner is null || ColorPreviewReadout is null) return;
        ColorPreviewBanner.Background = new SolidColorBrush(color);
        ColorPreviewReadout.Text = InlineColorPicker.HexText;
    }

    private void OnResetSplitClick(object? sender, RoutedEventArgs e)
    {
        DemoSplitPanels.ResetLayout();
    }

    private void OnSplitLayoutChanged(object? sender, IReadOnlyList<double> shares)
    {
        if (ResizableSharesText is null || shares.Count < 3) return;
        ResizableSharesText.Text =
            $"Live split shares — Explorer: {shares[0]:0.0}% · Editor: {shares[1]:0.0}% · Inspector: {shares[2]:0.0}% (double-click any divider to reset)";
    }

    // ─── App Rail Sidebar Handlers ───────────────────────────────────────

    private void OnPaneVariantChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (RootNav is null || sender is not VelaSegmentedControl seg) return;
        var idx = seg.SelectedIndex;
        RootNav.PaneVariant = idx switch
        {
            1 => VelaNavigationPaneVariant.Floating,
            2 => VelaNavigationPaneVariant.Frosted,
            _ => VelaNavigationPaneVariant.Standard,
        };

        if (PaneVariantSegmented is not null && !ReferenceEquals(seg, PaneVariantSegmented) && PaneVariantSegmented.SelectedIndex != idx)
            PaneVariantSegmented.SelectedIndex = idx;
        if (SettingsPaneVariantSegmented is not null && !ReferenceEquals(seg, SettingsPaneVariantSegmented) && SettingsPaneVariantSegmented.SelectedIndex != idx)
            SettingsPaneVariantSegmented.SelectedIndex = idx;
    }

    private void OnTopBarVisibleChanged(object? sender, RoutedEventArgs e)
    {
        if (RootNav is null || TopBarVisibleSwitch is null) return;
        RootNav.ShowTopBar = TopBarVisibleSwitch.IsChecked == true;
    }

    private void OnSettingsSearchSwitchChanged(object? sender, RoutedEventArgs e)
    {
        if (RootNav is null || SettingsSearchSwitch is null) return;
        var vis = SettingsSearchSwitch.IsChecked == true;
        RootNav.IsSearchVisible = vis;
        if (SearchVisibleSwitch is not null && SearchVisibleSwitch.IsChecked != vis)
            SearchVisibleSwitch.IsChecked = vis;
    }
}
