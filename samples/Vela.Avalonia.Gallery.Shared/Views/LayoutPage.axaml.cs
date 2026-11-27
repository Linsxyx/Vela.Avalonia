using System.ComponentModel;
using Avalonia.Controls;
using Vela.Avalonia.Controls;
using Vela.Avalonia.Gallery.Localization;
using Vela.Avalonia.Gallery.ViewModels;

namespace Vela.Avalonia.Gallery.Views;

public partial class LayoutPage : UserControl
{
    private LayoutViewModel? _vm;

    public LayoutPage()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        SyncLocalizedItems();
        Loc.Instance.LanguageChanged += OnLanguageChanged;
    }

    private void OnLanguageChanged(AppLanguage _) => SyncLocalizedItems();

    private void SyncLocalizedItems()
    {
        var loc = Loc.Instance;

        if (ShowcaseDockBar.Items.Count >= 4)
        {
            ShowcaseDockBar.Items[0].Header = loc.Get("StrDockHome");
            ShowcaseDockBar.Items[1].Header = loc.Get("StrDockExplore");
            ShowcaseDockBar.Items[2].Header = loc.Get("StrDockSaved");
            ShowcaseDockBar.Items[3].Header = loc.Get("StrDockProfile");
        }

        if (DemoStepper.Steps.Count >= 4)
        {
            DemoStepper.Steps[0].Title = loc.Get("StrStepAccount");
            DemoStepper.Steps[0].Description = loc.Get("StrStepAccountDesc");
            DemoStepper.Steps[1].Title = loc.Get("StrStepRuntime");
            DemoStepper.Steps[1].Description = loc.Get("StrStepRuntimeDesc");
            DemoStepper.Steps[2].Title = loc.Get("StrStepSecurity");
            DemoStepper.Steps[2].Description = loc.Get("StrStepSecurityDesc");
            DemoStepper.Steps[3].Title = loc.Get("StrStepDeploy");
            DemoStepper.Steps[3].Description = loc.Get("StrStepDeployDesc");
        }

        if (DemoSplitPanels.Panes.Count >= 3)
        {
            DemoSplitPanels.Panes[0].Header = loc.Get("StrExplorer");
            DemoSplitPanels.Panes[1].Header = loc.Get("StrEditor");
            DemoSplitPanels.Panes[2].Header = loc.Get("StrInspector");
        }
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_vm is not null)
        {
            _vm.PropertyChanged -= OnVmPropertyChanged;
            _vm.StepperErrorToggleRequested -= OnToggleStepperError;
            _vm.StepperCompleteAllRequested -= OnCompleteAllSteps;
            _vm.BreadcrumbDrillRequested -= OnDrillBreadcrumb;
            _vm.ResetSplitRequested -= OnResetSplit;
        }

        _vm = DataContext as LayoutViewModel;
        if (_vm is not null)
        {
            _vm.PropertyChanged += OnVmPropertyChanged;
            _vm.StepperErrorToggleRequested += OnToggleStepperError;
            _vm.StepperCompleteAllRequested += OnCompleteAllSteps;
            _vm.BreadcrumbDrillRequested += OnDrillBreadcrumb;
            _vm.ResetSplitRequested += OnResetSplit;
            if (ShowcaseDockBar.Items.Count > 2)
                ShowcaseDockBar.Items[2].Badge = _vm.DockBadgeText;
        }
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LayoutViewModel.DockBadgeText) && _vm is not null && ShowcaseDockBar.Items.Count > 2)
            ShowcaseDockBar.Items[2].Badge = _vm.DockBadgeText;
    }

    private void OnDockSearchSubmitted(object? sender, string query) => _vm?.OnDockSearchSubmitted(query);

    private void OnStepSelected(object? sender, int index)
    {
        if (index >= 0 && index < DemoStepper.Steps.Count)
            _vm?.OnStepSelected(index, DemoStepper.Steps[index].Title);
    }

    private void OnToggleStepperError(bool _)
    {
        var idx = Math.Clamp(DemoStepper.CurrentStep, 0, DemoStepper.Steps.Count - 1);
        var step = DemoStepper.Steps[idx];
        step.ErrorText = string.IsNullOrEmpty(step.ErrorText)
            ? Loc.Instance.Pick("Certificate expired", "签名证书已过期")
            : null;
    }

    private void OnCompleteAllSteps()
    {
        foreach (var step in DemoStepper.Steps)
            step.ErrorText = null;
    }

    private void OnBreadcrumbItemClicked(object? sender, VelaBreadcrumbItem item)
    {
        var idx = DemoBreadcrumb.Items.IndexOf(item);
        if (idx < 0) return;
        while (DemoBreadcrumb.Items.Count > idx + 1)
            DemoBreadcrumb.Items.RemoveAt(DemoBreadcrumb.Items.Count - 1);
        _vm?.SyncBreadcrumbDepth(DemoBreadcrumb.Items.Count);
    }

    private void OnDrillBreadcrumb(string label) =>
        DemoBreadcrumb.Items.Add(new VelaBreadcrumbItem { Label = label });

    private void OnResetSplit() => DemoSplitPanels.ResetLayout();

    private void OnSplitLayoutChanged(object? sender, IReadOnlyList<double> shares) =>
        _vm?.UpdateSplitShares(shares);
}
