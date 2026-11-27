using Avalonia.Controls;
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
}
