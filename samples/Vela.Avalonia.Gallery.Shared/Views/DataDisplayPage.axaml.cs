using Avalonia.Controls;
using Vela.Avalonia.Gallery.ViewModels;

namespace Vela.Avalonia.Gallery.Views;

public partial class DataDisplayPage : UserControl
{
    public DataDisplayPage()
    {
        InitializeComponent();
        AttachedToVisualTree += (_, _) => (DataContext as DataDisplayViewModel)?.EnsureIconsLoaded();
    }

    private void OnCopied(object? sender, EventArgs e) =>
        (DataContext as DataDisplayViewModel)?.CopyEndpointCommand.Execute(null);
}
