using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Vela.Avalonia.Controls;
using Vela.Avalonia.Gallery.ViewModels;

namespace Vela.Avalonia.Gallery.Views;

public partial class OverviewPage : UserControl
{
    public OverviewPage()
    {
        InitializeComponent();
        ConfigureDeploymentsGrid();
    }

    private void ConfigureDeploymentsGrid()
    {
        DeploymentsGrid.Columns.Add(new VelaDataGridTextColumn
        {
            Header = "Service",
            Width = new GridLength(1.9, GridUnitType.Star),
            MinWidth = 136,
            FontWeight = FontWeight.Medium,
            ValueSelector = o => ((OverviewDeploymentRow)o).Service,
        });

        DeploymentsGrid.Columns.Add(new VelaDataGridTextColumn
        {
            Header = "Version",
            Width = new GridLength(96),
            MinWidth = 82,
            IsMonospace = true,
            IsMuted = true,
            ValueSelector = o => ((OverviewDeploymentRow)o).Version,
        });

        DeploymentsGrid.Columns.Add(new VelaDataGridBadgeColumn
        {
            Header = "Status",
            Width = new GridLength(102),
            MinWidth = 88,
            ShowDot = true,
            IsPill = true,
            ValueSelector = o => ((OverviewDeploymentRow)o).Status,
            ToneSelector = o => ((OverviewDeploymentRow)o).StatusTone,
        });

        DeploymentsGrid.Columns.Add(new VelaDataGridTextColumn
        {
            Header = "P95",
            Width = new GridLength(82),
            MinWidth = 72,
            HorizontalAlignment = HorizontalAlignment.Right,
            IsMonospace = true,
            StringFormat = "{0:0.0} ms",
            ValueSelector = o => ((OverviewDeploymentRow)o).LatencyMs,
        });

        DeploymentsGrid.Columns.Add(new VelaDataGridTextColumn
        {
            Header = "Owner",
            Width = new GridLength(1.2, GridUnitType.Star),
            MinWidth = 100,
            IsMuted = true,
            ValueSelector = o => ((OverviewDeploymentRow)o).Owner,
        });
    }

    private void OnPackageCopied(object? sender, EventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.Overview.NotifyPackageCopied();
    }

    private async void OnQuickDeployConfirmed(object? sender, EventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.Overview.CompleteQuickDeploy();

        await Task.Delay(1400);
        Dispatcher.UIThread.Post(() => QuickDeployHold.Reset());
    }

    private void OnDeploymentRowDoubleTapped(object? sender, VelaDataItemEventArgs e)
    {
        if (DataContext is MainViewModel vm && e.Item is OverviewDeploymentRow row)
            vm.Overview.NotifyRowInspected(row);
    }

    private void OnQuickJumpInvoked(object? sender, VelaDataItemEventArgs e)
    {
        if (DataContext is not MainViewModel vm)
            return;

        var tag = (e.Item as VelaListBoxItem)?.Tag as string;
        if (string.IsNullOrEmpty(tag))
            return;

        if (tag == "drawer")
            _ = vm.OpenShellSettingsCommand.ExecuteAsync(null);
        else
            vm.Overview.NavigateToCommand.Execute(tag);
    }
}
