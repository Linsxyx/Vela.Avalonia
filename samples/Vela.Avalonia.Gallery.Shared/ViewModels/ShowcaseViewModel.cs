using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vela.Avalonia.Controls;
using Vela.Avalonia.Gallery.Localization;

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

    // Glass Menu demo state
    [ObservableProperty]
    public partial bool IsFavorite { get; set; } = true;

    [ObservableProperty]
    public partial string GlassMenuStatus { get; set; } = "Added to favorites";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GlassTintLevel))]
    public partial int GlassTintIndex { get; set; } = 1;

    public VelaGlassTintLevel GlassTintLevel => GlassTintIndex switch
    {
        0 => VelaGlassTintLevel.None,
        2 => VelaGlassTintLevel.HighContrast,
        _ => VelaGlassTintLevel.Standard,
    };

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

    [RelayCommand]
    private void ToggleFavorite()
    {
        IsFavorite = !IsFavorite;
        GlassMenuStatus = IsFavorite
            ? Loc.Instance.Pick("Added to favorites", "已添加到收藏")
            : Loc.Instance.Pick("Removed from favorites", "已取消收藏");
    }

    [RelayCommand]
    private void RunGlassAction(string? action)
    {
        if (string.IsNullOrWhiteSpace(action))
            return;

        GlassMenuStatus = action;
    }
}
