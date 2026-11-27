using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vela.Avalonia.Charts;
using Vela.Avalonia.Gallery.Localization;

namespace Vela.Avalonia.Gallery.ViewModels;

public partial class ChartsViewModel : ViewModelBase
{
    private readonly Random _rng = new(42);

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
    public partial string DonutTotalLabel { get; set; } = string.Empty;

    [ObservableProperty]
    public partial IReadOnlyList<VelaDonutDatum> DonutData { get; set; } = Array.Empty<VelaDonutDatum>();

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
        RefreshAllCharts();

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

        Loc.Instance.LanguageChanged += _ => RefreshAllCharts();
    }

    private void RefreshAllCharts()
    {
        ApplyLineRange(LineRangeIndex);
        ApplyBarRange(BarRangeIndex);
        ApplyDonutDataset(DonutDatasetIndex);
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
        var zh = Loc.Instance.IsChinese;
        var revName = zh ? "净收入" : "Net Revenue";
        var prevName = zh ? "上期对比" : "Prior Period";

        if (idx == 0)
        {
            LineLabels = zh ? ["周一", "周二", "周三", "周四", "周五", "周六", "周日"] : ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"];
            LineSeries =
            [
                new("rev", revName, [12400, 14800, 13900, 18200, 21600, 19400, 24100]),
                new("prev", prevName, [10200, 11400, 12100, 13800, 15200, 14900, 16800]),
            ];
        }
        else if (idx == 1)
        {
            LineLabels = ["W1", "W2", "W3", "W4", "W5", "W6", "W7"];
            LineSeries =
            [
                new("rev", revName, [32000, 38500, 41200, 39800, 46900, 52400, 58100]),
                new("prev", prevName, [26000, 29400, 31100, 33600, 35800, 39200, 42500]),
            ];
        }
        else
        {
            LineLabels = zh ? ["5月", "6月", "7月", "8月", "9月", "10月", "11月"] : ["May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov"];
            LineSeries =
            [
                new("rev", revName, [84000, 96000, 112000, 108000, 134000, 149000, 172000]),
                new("prev", prevName, [62000, 71000, 79000, 86000, 94000, 106000, 118000]),
            ];
        }
    }

    private void ApplyBarRange(int idx)
    {
        var days = Loc.Instance.IsChinese
            ? new[] { "周一", "周二", "周三", "周四", "周五", "周六", "周日" }
            : new[] { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };

        double[] vals = idx == 0
            ? [420, 580, 510, 760, 890, 640, 710]
            : [690, 520, 840, 610, 720, 930, 580];

        var list = new VelaBarDatum[7];
        for (var i = 0; i < 7; i++)
            list[i] = new VelaBarDatum(days[i], vals[i]);
        BarData = list;
    }

    private void ApplyDonutDataset(int idx)
    {
        var zh = Loc.Instance.IsChinese;
        switch (Math.Clamp(idx, 0, 2))
        {
            case 1:
                DonutTotalLabel = zh ? "Q2 访问" : "Q2 visits";
                DonutData =
                [
                    new("organic", zh ? "自然搜索" : "Organic Search", 6140),
                    new("direct", zh ? "直接访问" : "Direct", 1850),
                    new("social", zh ? "社交媒体" : "Social Media", 3420),
                    new("referral", zh ? "外链引荐" : "Referrals", 1490),
                ];
                break;
            case 2:
                DonutTotalLabel = zh ? "月经常性收入" : "MRR ($)";
                DonutData =
                [
                    new("organic", zh ? "企业版" : "Enterprise", 18400),
                    new("direct", zh ? "团队版" : "Team Pro", 12900),
                    new("social", zh ? "个人版" : "Indie", 6400),
                    new("referral", zh ? "增值包" : "Add-ons", 3100),
                ];
                break;
            default:
                DonutTotalLabel = zh ? "Q1 访问" : "Q1 visits";
                DonutData =
                [
                    new("organic", zh ? "自然搜索" : "Organic Search", 4820),
                    new("direct", zh ? "直接访问" : "Direct", 2910),
                    new("social", zh ? "社交媒体" : "Social Media", 1640),
                    new("referral", zh ? "外链引荐" : "Referrals", 930),
                ];
                break;
        }
    }
}
