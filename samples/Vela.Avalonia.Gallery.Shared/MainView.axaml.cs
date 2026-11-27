using Avalonia.Controls;
using Avalonia.Media;
using Vela.Avalonia.Controls;
using Vela.Avalonia.Gallery.Localization;
using Vela.Avalonia.Gallery.Services;
using Vela.Avalonia.Gallery.ViewModels;
using Vela.Avalonia.Gallery.Views;

namespace Vela.Avalonia.Gallery;

public partial class MainView : UserControl
{
    private MainViewModel? _viewModel;

    public MainView()
    {
        InitializeComponent();
        RootNav.PageFactory = CreatePage;
        Loc.Instance.LanguageChanged += OnLanguageChanged;
    }

    public void Initialize(GalleryShellService shell, MainViewModel viewModel, VelaTrayIcon? trayIcon = null)
    {
        _viewModel = viewModel;
        DataContext = viewModel;

        shell.AttachView(
            resourceHost: this,
            dialogHost: DialogHost,
            drawerHost: DrawerHost,
            toastHost: ToastHost,
            commandPalette: CommandPalette,
            navigationView: RootNav,
            navigateAction: NavigateToKey);

        RebuildCommandPalette();

        if (trayIcon is not null)
            viewModel.SystemTray.AttachTrayIcon(trayIcon);
        else
            viewModel.SystemTray.EnsurePreviewTrayAttached();
    }

    private void OnLanguageChanged(AppLanguage _)
    {
        RebuildCommandPalette();
    }

    private void RebuildCommandPalette()
    {
        if (_viewModel is null)
            return;

        CommandPalette.Commands.Clear();
        foreach (var cmd in _viewModel.BuildCommandPaletteItems())
            CommandPalette.Commands.Add(cmd);
    }

    private object? CreatePage(object item)
    {
        if (item is not VelaNavItem navItem || _viewModel is null)
            return null;

        return (navItem.Tag as string) switch
        {
            "overview" => new OverviewPage { DataContext = _viewModel },
            "actions" => new ActionsPage { DataContext = _viewModel.Actions },
            "inputs" => new InputsPage { DataContext = _viewModel.Inputs },
            "pickers" => new PickersPage { DataContext = _viewModel.Pickers },
            "layout" => new LayoutPage { DataContext = _viewModel.Layout },
            "feedback" => new FeedbackPage { DataContext = _viewModel.Feedback },
            "display" => new DataDisplayPage { DataContext = _viewModel.DataDisplay },
            "shaders" => new ShadersPage { DataContext = _viewModel.Shaders },
            "charts" => new ChartsPage { DataContext = _viewModel.Charts },
            "tray" => new SystemTrayPage { DataContext = _viewModel.SystemTray },
            "stress" => new HeavyStressPage(),
            _ => null,
        };
    }

    private void NavigateToKey(string pageKey)
    {
        var match = FindNavItemByTag(RootNav.Items, pageKey)
                    ?? FindNavItemByTag(RootNav.FooterItems, pageKey);
        if (match is not null)
            RootNav.SelectedItem = match;
    }

    private static VelaNavItem? FindNavItemByTag(IEnumerable<object> items, string tag)
    {
        foreach (var entry in items)
        {
            if (entry is not VelaNavItem nav)
                continue;
            if (string.Equals(nav.Tag as string, tag, StringComparison.OrdinalIgnoreCase))
                return nav;
            var child = FindNavItemByTag(nav.Children, tag);
            if (child is not null)
                return child;
        }
        return null;
    }

    private void OnPageSwitched(object? sender, VelaPageSwitchMetrics metrics)
    {
        _viewModel?.RecordPageSwitch(metrics);

        var dotKey = metrics.IsCacheHit ? "VelaSuccessBrush" : "VelaWarningBrush";
        if (this.TryFindResource(dotKey, ActualThemeVariant, out var res) && res is IBrush brush)
            TelemetryDot.Background = brush;
    }
}
