using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Vela.Avalonia.Gallery.ViewModels;

public partial class ShowcaseViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial decimal PaymentAmount { get; set; } = 42.50m;

    [ObservableProperty]
    public partial decimal InitialBalance { get; set; } = 1284.10m;

    [ObservableProperty]
    public partial bool IsLooping { get; set; } = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlaybackSpeed))]
    public partial int SpeedIndex { get; set; } = 1;

    public double PlaybackSpeed => SpeedIndex switch
    {
        0 => 0.5,
        2 => 1.5,
        3 => 2.0,
        _ => 1.0,
    };

    [ObservableProperty]
    public partial int AmountPresetIndex { get; set; }

    partial void OnAmountPresetIndexChanged(int value)
    {
        PaymentAmount = value switch
        {
            1 => 128.00m,
            2 => 1499.00m,
            _ => 42.50m,
        };
    }

    [RelayCommand]
    private void SelectAmountPreset(int index) => AmountPresetIndex = index;
}
