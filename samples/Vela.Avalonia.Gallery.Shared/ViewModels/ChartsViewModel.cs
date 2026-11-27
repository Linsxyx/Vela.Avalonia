using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vela.Avalonia.Charts;

namespace Vela.Avalonia.Gallery.ViewModels;

public partial class ChartsViewModel : ViewModelBase
{
    private readonly Random _rng = new(42);

    private static readonly VelaDonutDatum[][] DonutDatasets =
    [
        [
            new("organic", "Organic Search", 4820),
            new("direct", "Direct", 2910),
            new("social", "Social Media", 1640),
            new("referral", "Referrals", 930),
        ],
        [
            new("organic", "Organic Search", 6140),
            new("direct", "Direct", 1850),
            new("social", "Social Media", 3420),
            new("referral", "Referrals", 1490),
        ],
        [
            new("organic", "Enterprise", 18400),
            new("direct", "Team Pro", 12900),
            new("social", "Indie", 6400),
            new("referral", "Add-ons", 3100),
        ],
    ];

    private static readonly string[] DonutLabels = ["Q1 visits", "Q2 visits", "MRR ($)"];

    [ObservableProperty]
    public partial int LineRangeIndex { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<string> LineLabels { get; set; } = Array.Empty<string>();

    [ObservableProperty]
    public partial IReadOnlyList<VelaLineSeries> LineSeries { get; set; } = Array.Empty<VelaLineSeries>();

    [ObservableProperty]
    public partial int BarRangeIndex { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<VelaBarDatum> BarData { get; set; } = Array.Empty<VelaBarDatum>();

    [ObservableProperty]
    public partial int DonutDatasetIndex { get; set; }

    [ObservableProperty]
    public partial string DonutTotalLabel { get; set; } = DonutLabels[0];

    [ObservableProperty]
    public partial IReadOnlyList<VelaDonutDatum> DonutData { get; set; } = DonutDatasets[0];

    [ObservableProperty]
    public partial double CpuLoad { get; set; } = 42;

    [ObservableProperty]
    public partial double MemoryLoad { get; set; } = 74;

    [ObservableProperty]
    public partial double GpuSync { get; set; } = 89;

    public IReadOnlyList<double> SparkSessions { get; } = [18, 22, 19, 27, 34, 31, 42, 46, 44, 58];
    public IReadOnlyList<double> SparkBudget { get; } = [4.2, 3.8, 3.5, 2.9, 2.6, 2.2, 2.1, 1.9, 1.85, 1.8];
    public IReadOnlyList<double> SparkErrors { get; } = [0.22, 0.18, 0.25, 0.14, 0.11, 0.09, 0.08, 0.05, 0.06, 0.04];

    public IReadOnlyList<VelaHeatmapEntry> HeatmapEntries { get; }

    public ChartsViewModel()
    {
        ApplyLineRange(0);
        ApplyBarRange(0);
        ApplyDonutDataset(0);

        var entries = new List<VelaHeatmapEntry>(260);
        var today = DateTime.Today;
        var seed = new Random(2026);
        for (var i = 0; i < 260; i++)
        {
            var d = today.AddDays(-i);
            var roll = seed.NextDouble();
            var count = roll < 0.28 ? 0 : roll < 0.55 ? seed.Next(1, 4) : roll < 0.82 ? seed.Next(4, 10) : seed.Next(10, 18);
            entries.Add(new VelaHeatmapEntry(d, count));
        }
        HeatmapEntries = entries;
    }

    partial void OnLineRangeIndexChanged(int value) => ApplyLineRange(value);
    partial void OnBarRangeIndexChanged(int value) => ApplyBarRange(value);
    partial void OnDonutDatasetIndexChanged(int value) => ApplyDonutDataset(value);

    [RelayCommand]
    private void RandomizeGauges()
    {
        CpuLoad = _rng.Next(15, 96);
        MemoryLoad = _rng.Next(25, 95);
        GpuSync = _rng.Next(10, 99);
    }

    private void ApplyLineRange(int idx)
    {
        if (idx == 0)
        {
            LineLabels = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"];
            LineSeries =
            [
                new("rev", "Net Revenue", [12400, 14800, 13900, 18200, 21600, 19400, 24100]),
                new("prev", "Prior Period", [10200, 11400, 12100, 13800, 15200, 14900, 16800]),
            ];
        }
        else if (idx == 1)
        {
            LineLabels = ["W1", "W2", "W3", "W4", "W5", "W6", "W7"];
            LineSeries =
            [
                new("rev", "Net Revenue", [32000, 38500, 41200, 39800, 46900, 52400, 58100]),
                new("prev", "Prior Period", [26000, 29400, 31100, 33600, 35800, 39200, 42500]),
            ];
        }
        else
        {
            LineLabels = ["May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov"];
            LineSeries =
            [
                new("rev", "Net Revenue", [84000, 96000, 112000, 108000, 134000, 149000, 172000]),
                new("prev", "Prior Period", [62000, 71000, 79000, 86000, 94000, 106000, 118000]),
            ];
        }
    }

    private void ApplyBarRange(int idx)
    {
        BarData = idx == 0
            ? [new("Mon", 420), new("Tue", 580), new("Wed", 510), new("Thu", 760), new("Fri", 890), new("Sat", 640), new("Sun", 710)]
            : [new("Mon", 690), new("Tue", 520), new("Wed", 840), new("Thu", 610), new("Fri", 720), new("Sat", 930), new("Sun", 580)];
    }

    private void ApplyDonutDataset(int idx)
    {
        var safe = Math.Clamp(idx, 0, DonutDatasets.Length - 1);
        DonutTotalLabel = DonutLabels[safe];
        DonutData = DonutDatasets[safe];
    }
}
