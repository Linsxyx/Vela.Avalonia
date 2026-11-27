using System.ComponentModel;
using Avalonia.Controls;
using Vela.Avalonia.Controls;
using Vela.Avalonia.Gallery.ViewModels;

namespace Vela.Avalonia.Gallery.Views;

public partial class LayoutPage : UserControl
{
    private LayoutViewModel? _vm;

    public LayoutPage()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
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
        step.ErrorText = string.IsNullOrEmpty(step.ErrorText) ? "Certificate expired" : null;
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
