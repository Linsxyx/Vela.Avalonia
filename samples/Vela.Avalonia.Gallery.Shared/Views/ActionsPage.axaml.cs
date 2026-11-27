using Avalonia.Controls;
using Vela.Avalonia.Gallery.ViewModels;

namespace Vela.Avalonia.Gallery.Views;

public partial class ActionsPage : UserControl
{
    private ActionsViewModel? _vm;

    public ActionsPage()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_vm is not null)
            _vm.ResetHoldRequested -= OnResetHold;

        _vm = DataContext as ActionsViewModel;
        if (_vm is not null)
            _vm.ResetHoldRequested += OnResetHold;
    }

    private void OnResetHold()
    {
        HoldDangerBtn.Reset();
        HoldAccentBtn.Reset();
        HoldNeutralBtn.Reset();
    }

    private void OnSplitProdClick(object? sender, EventArgs e) => _vm?.SetSplitModeCommand.Execute("Deploy now");
    private void OnSplitStagingClick(object? sender, EventArgs e) => _vm?.SetSplitModeCommand.Execute("Deploy staging");
    private void OnSplitDownloadClick(object? sender, EventArgs e) => _vm?.SetSplitModeCommand.Execute("Download");
    private void OnSplitRollbackClick(object? sender, EventArgs e) => _vm?.SplitRollbackCommand.Execute(null);
    private void OnSplitCopyLinkClick(object? sender, EventArgs e) => _vm?.SplitCopyLinkCommand.Execute(null);
    private void OnSplitExportMdClick(object? sender, EventArgs e) => _vm?.SplitExportMdCommand.Execute(null);
    private void OnCopied(object? sender, EventArgs e) => _vm?.NotifyCopiedCommand.Execute(null);
    private void OnBranchDeleted(object? sender, EventArgs e) => _vm?.BranchDeletedCommand.Execute(null);
    private void OnBranchRestored(object? sender, EventArgs e) => _vm?.BranchRestoredCommand.Execute(null);
    private void OnHoldChanged(object? sender, bool holding) => _vm?.UpdateHoldState(holding);
    private void OnHoldPurgeConfirmed(object? sender, EventArgs e) => _vm?.HoldConfirmedCommand.Execute("purge");
    private void OnHoldPublishConfirmed(object? sender, EventArgs e) => _vm?.HoldConfirmedCommand.Execute("publish");
    private void OnHoldSealConfirmed(object? sender, EventArgs e) => _vm?.HoldConfirmedCommand.Execute("seal");
    private void OnFloatingEditClick(object? sender, EventArgs e) => _vm?.FloatingEditCommand.Execute(null);
    private void OnFloatingCopyClick(object? sender, EventArgs e) => _vm?.FloatingCopyCommand.Execute(null);
    private void OnFloatingDeleteClick(object? sender, EventArgs e) => _vm?.FloatingDeleteCommand.Execute(null);
}
