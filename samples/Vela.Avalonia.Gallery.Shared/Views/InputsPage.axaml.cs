using Avalonia.Controls;

namespace Vela.Avalonia.Gallery.Views;

public partial class InputsPage : UserControl
{
    public InputsPage()
    {
        InitializeComponent();
        DemoTagInput.Tags.Add("Avalonia 12.2");
        DemoTagInput.Tags.Add("Liquid Glass");
        DemoTagInput.Tags.Add("OKLCH");
    }
}
