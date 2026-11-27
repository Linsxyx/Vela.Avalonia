using CommunityToolkit.Mvvm.ComponentModel;

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
        0 => "Monthly · $12/mo",
        1 => "Quarterly · $30/qtr",
        2 => "Yearly · $96/yr (20% off)",
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
}
