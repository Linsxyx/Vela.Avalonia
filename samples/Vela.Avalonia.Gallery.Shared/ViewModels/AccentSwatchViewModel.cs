using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Vela.Avalonia.Tokens;

namespace Vela.Avalonia.Gallery.ViewModels;

public partial class AccentSwatchViewModel : ObservableObject
{
    public VelaAccent Accent { get; }
    public string Name { get; }
    public IBrush SwatchBrush { get; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    [ObservableProperty]
    public partial IBrush RingBrush { get; set; } = Brushes.Transparent;

    public AccentSwatchViewModel(VelaAccent accent, Color color)
    {
        Accent = accent;
        Name = accent.ToString();
        SwatchBrush = new SolidColorBrush(color);
    }

    public void UpdateRing(bool isSelected, bool isDark)
    {
        IsSelected = isSelected;
        RingBrush = isSelected ? (isDark ? Brushes.White : Brushes.Black) : Brushes.Transparent;
    }
}
