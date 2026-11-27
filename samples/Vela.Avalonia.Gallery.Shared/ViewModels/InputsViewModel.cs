using CommunityToolkit.Mvvm.ComponentModel;
using Vela.Avalonia.Gallery.Localization;

namespace Vela.Avalonia.Gallery.ViewModels;

public partial class InputsViewModel : ViewModelBase
{
    public IReadOnlyList<double> VolumeMarks { get; } = [0, 25, 50, 75, 100];
    public IReadOnlyList<double> ZoomMarks { get; } = [0, 50, 100, 150, 200];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlanSummary))]
    public partial int SelectedPlanIndex { get; set; }

    public string PlanSummary => SelectedPlanIndex switch
    {
        0 => Loc.Instance.Get("StrPlanMonthly"),
        1 => Loc.Instance.Get("StrPlanQuarterly"),
        2 => Loc.Instance.Get("StrPlanYearly"),
        _ => string.Empty,
    };

    [ObservableProperty]
    public partial double Volume { get; set; } = 64;

    [ObservableProperty]
    public partial double CanvasZoom { get; set; } = 100;

    [ObservableProperty]
    public partial double ScrubWidth { get; set; } = 320;

    [ObservableProperty]
    public partial double ScrubRadius { get; set; } = 18;

    [ObservableProperty]
    public partial double ScrubOpacity { get; set; } = 85;

    [ObservableProperty]
    public partial double ScrubRotation { get; set; }

    [ObservableProperty]
    public partial double ColorTemperature { get; set; } = 5600;

    [ObservableProperty]
    public partial double RulerZoom { get; set; } = 100;

    public InputsViewModel()
    {
        Loc.Instance.LanguageChanged += _ => OnPropertyChanged(nameof(PlanSummary));
    }
}
