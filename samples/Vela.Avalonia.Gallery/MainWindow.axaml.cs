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
    private bool _draggingGlass;
    private Point _glassDragStartPointer;
    private Point _glassDragStartPos;

    public MainWindow()
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
}
