using Avalonia.Controls;
using Avalonia.Interactivity;
using Vela.Avalonia.Gallery.ViewModels;

namespace Vela.Avalonia.Gallery.Views;

public partial class PickersPage : UserControl
{
    private PickersViewModel? Vm => DataContext as PickersViewModel;

    public PickersPage()
    {
        InitializeComponent();
    }

    private void OnRenameClick(object? sender, EventArgs e) => Vm?.MenuActionCommand.Execute("rename");
    private void OnDuplicateClick(object? sender, EventArgs e) => Vm?.MenuActionCommand.Execute("duplicate");
    private void OnShareClick(object? sender, EventArgs e) => Vm?.MenuActionCommand.Execute("share");
    private void OnDeleteClick(object? sender, EventArgs e) => Vm?.MenuActionCommand.Execute("delete");

    private void OnCtxEditClick(object? sender, EventArgs e) => Vm?.MenuActionCommand.Execute("ctx-edit");
    private void OnCtxCopyClick(object? sender, EventArgs e) => Vm?.MenuActionCommand.Execute("ctx-copy");
    private void OnCtxDeleteClick(object? sender, EventArgs e) => Vm?.MenuActionCommand.Execute("ctx-delete");

    private void OnOpenContextMenuClick(object? sender, RoutedEventArgs e)
    {
        DemoContextMenu.ShowAt(ContextMenuArea, showAtPointer: false);
    }

    private void OnMenuBarNewClick(object? sender, EventArgs e) => Vm?.MenuActionCommand.Execute("mb-new");
    private void OnMenuBarExportClick(object? sender, EventArgs e) => Vm?.MenuActionCommand.Execute("mb-export");
    private void OnMenuBarGridClick(object? sender, EventArgs e) => Vm?.MenuActionCommand.Execute("mb-grid");
    private void OnMenuBarZoomClick(object? sender, EventArgs e) => Vm?.MenuActionCommand.Execute("mb-zoom");
}
