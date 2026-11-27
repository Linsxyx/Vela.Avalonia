using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vela.Avalonia.Charts;
using Vela.Avalonia.Controls;
using Vela.Avalonia.Gallery.Localization;
using Vela.Avalonia.Gallery.Services;
using Vela.Avalonia.Tokens;

namespace Vela.Avalonia.Gallery.ViewModels;

public sealed class OverviewDeploymentRow
{
    public string Service { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public string Region { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public VelaBadgeTone StatusTone { get; init; }
    public double LatencyMs { get; init; }
    public string Owner { get; init; } = string.Empty;
}

public partial class OverviewViewModel : ViewModelBase
{
    private readonly IGalleryShellService _shell;
    private readonly Random _rng = new(2026);
    private int _deployCounter = 4;

    private static readonly string[] RegionIds =
    [
        "ap-northeast-1",
        "ap-southeast-1",
        "us-west-2",
        "eu-central-1",
    ];

    // ── Hero Shader Color ────────────────────────────────────────────────
    [ObservableProperty]
    public partial Color HeroShaderColor { get; set; } = Color.Parse("#7747FF");

    // ── Band 2: KPI Metrics ──────────────────────────────────────────────
    [ObservableProperty]
    public partial double MonthlyRevenue { get; set; } = 48290;

    [ObservableProperty]
    public partial double ActiveDeployments { get; set; } = 1284;

    [ObservableProperty]
    public partial double P95Latency { get; set; } = 18.4;

    [ObservableProperty]
    public partial double UptimePercent { get; set; } = 99.98;

    // ── Band 3A: Line Chart & Sparklines ─────────────────────────────────
    [ObservableProperty]
    public partial int ChartRangeIndex { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<string> ChartLabels { get; set; } = Array.Empty<string>();

    [ObservableProperty]
    public partial IReadOnlyList<VelaLineSeries> ChartSeries { get; set; } = Array.Empty<VelaLineSeries>();

    public IReadOnlyList<double> SparkRequests { get; } = [18, 22, 20, 29, 36, 33, 44, 49, 47, 61];
    public IReadOnlyList<double> SparkLatency { get; } = [26.4, 24.8, 23.2, 21.5, 20.9, 19.8, 19.2, 18.9, 18.6, 18.4];
    public IReadOnlyList<double> SparkErrors { get; } = [0.18, 0.15, 0.19, 0.12, 0.09, 0.07, 0.06, 0.05, 0.04, 0.03];

    // ── Band 3B: Quick Release Form ──────────────────────────────────────
    [ObservableProperty]
    public partial string ReleaseTag { get; set; } = "v2.4.0-rc.3";

    [ObservableProperty]
    public partial int SelectedRegionIndex { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanaryBadgeText))]
    public partial double CanaryPercent { get; set; } = 35;

    public string CanaryBadgeText => Loc.Instance.IsChinese
        ? $"{(int)CanaryPercent}% 灰度"
        : $"{(int)CanaryPercent}% Canary";

    [ObservableProperty]
    public partial bool AutoRollback { get; set; } = true;

    [ObservableProperty]
    public partial bool EdgeWarmup { get; set; } = true;

    // ── Band 4A: Active Deployments DataGrid ─────────────────────────────
    [ObservableProperty]
    public partial string DeploymentSearchText { get; set; } = string.Empty;

    public ObservableCollection<OverviewDeploymentRow> DeploymentRows { get; } = new();

    public string ActiveServicesCountText => Loc.Instance.IsChinese
        ? $"{DeploymentRows.Count} 项服务"
        : $"{DeploymentRows.Count} services";

    // ── Band 4B: Release Schedule Calendar ───────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedScheduleDateText))]
    public partial DateTime? SelectedScheduleDate { get; set; } = DateTime.Today;

    public string SelectedScheduleDateText => (SelectedScheduleDate ?? DateTime.Today).ToString("MMM dd");

    [ObservableProperty]
    public partial double SprintProgress { get; set; } = 78;

    // ── Band 5A: Cluster Health Gauges ───────────────────────────────────
    [ObservableProperty]
    public partial double CpuLoad { get; set; } = 46;

    [ObservableProperty]
    public partial double MemoryLoad { get; set; } = 68;

    [ObservableProperty]
    public partial double GpuSync { get; set; } = 84;

    public OverviewViewModel(IGalleryShellService shell)
    {
        _shell = shell;
        SeedDeployments();
        ApplyChartRange(0);
        SyncHeroShaderColor(VelaAccent.Violet, isDark: false);

        Loc.Instance.LanguageChanged += _ =>
        {
            ApplyChartRange(ChartRangeIndex);
            OnPropertyChanged(nameof(CanaryBadgeText));
            OnPropertyChanged(nameof(ActiveServicesCountText));
        };
    }

    public void SyncHeroShaderColor(VelaAccent accent, bool isDark)
    {
        var palette = VelaAccentPalettes.Get(accent, isDark);
        HeroShaderColor = palette.ControlOn;
    }

    partial void OnChartRangeIndexChanged(int value) => ApplyChartRange(value);

    [RelayCommand]
    private void SimulateLivePulse()
    {
        MonthlyRevenue = Math.Round(42000 + _rng.NextDouble() * 24000, 0);
        ActiveDeployments = _rng.Next(1120, 1680);
        P95Latency = Math.Round(12.2 + _rng.NextDouble() * 14.5, 1);
        UptimePercent = Math.Round(99.91 + _rng.NextDouble() * 0.08, 2);

        CpuLoad = _rng.Next(24, 88);
        MemoryLoad = _rng.Next(38, 92);
        GpuSync = _rng.Next(52, 97);
        SprintProgress = _rng.Next(62, 98);

        ApplyChartRange(ChartRangeIndex, jitter: true);

        _shell.ShowToast(
            Loc.Instance.Pick("Telemetry refreshed", "实时指标已刷新"),
            Loc.Instance.Pick("KPIs, chart series, and cluster gauges updated.", "KPI 指标、折线趋势与集群仪表已同步更新。"),
            VelaToastTone.Info);
    }

    [RelayCommand]
    private void RandomizeGauges()
    {
        CpuLoad = _rng.Next(18, 94);
        MemoryLoad = _rng.Next(28, 93);
        GpuSync = _rng.Next(35, 98);
    }

    [RelayCommand]
    private void NavigateTo(string pageKey) => _shell.NavigateTo(pageKey);

    public void CompleteQuickDeploy()
    {
        var tag = string.IsNullOrWhiteSpace(ReleaseTag) ? $"v2.4.{_deployCounter}" : ReleaseTag.Trim();
        var region = SelectedRegionIndex >= 0 && SelectedRegionIndex < RegionIds.Length
            ? RegionIds[SelectedRegionIndex]
            : RegionIds[0];

        DeploymentRows.Insert(0, new OverviewDeploymentRow
        {
            Service = $"vela-release-{_deployCounter:00}",
            Version = tag,
            Region = region,
            Status = CanaryPercent < 100 ? "Canary" : "Healthy",
            StatusTone = CanaryPercent < 100 ? VelaBadgeTone.Accent : VelaBadgeTone.Success,
            LatencyMs = Math.Round(11.0 + _rng.NextDouble() * 9.0, 1),
            Owner = "Emma Collins",
        });

        _deployCounter++;
        ActiveDeployments += 1;
        ReleaseTag = $"v2.4.0-rc.{_deployCounter}";
        OnPropertyChanged(nameof(ActiveServicesCountText));

        _shell.ShowToast(
            Loc.Instance.Pick($"Deployed {tag}", $"已发布 {tag}"),
            Loc.Instance.IsChinese
                ? $"区域 {region} · {(int)CanaryPercent}% 灰度流量已生效。"
                : $"Region {region} · {(int)CanaryPercent}% canary rollout active.",
            VelaToastTone.Success);
    }

    public void NotifyRowInspected(OverviewDeploymentRow row)
    {
        _shell.ShowToast(
            row.Service,
            $"{row.Version} · {row.Region} · {row.LatencyMs:0.0} ms · {row.Owner}",
            row.StatusTone == VelaBadgeTone.Warning ? VelaToastTone.Warning : VelaToastTone.Info);
    }

    public void NotifyPackageCopied()
    {
        _shell.ShowToast(
            Loc.Instance.Pick("Install command copied", "安装命令已复制"),
            "dotnet add package Vela.Avalonia",
            VelaToastTone.Success);
    }

    private void SeedDeployments()
    {
        DeploymentRows.Clear();
        DeploymentRows.Add(new OverviewDeploymentRow
        {
            Service = "vela-api-gateway",
            Version = "v2.4.0",
            Region = "ap-northeast-1",
            Status = "Healthy",
            StatusTone = VelaBadgeTone.Success,
            LatencyMs = 14.2,
            Owner = "Emma Collins",
        });
        DeploymentRows.Add(new OverviewDeploymentRow
        {
            Service = "vela-auth-edge",
            Version = "v2.3.9",
            Region = "us-west-2",
            Status = "Healthy",
            StatusTone = VelaBadgeTone.Success,
            LatencyMs = 11.8,
            Owner = "Marcus Chen",
        });
        DeploymentRows.Add(new OverviewDeploymentRow
        {
            Service = "vela-billing-worker",
            Version = "v2.4.0-rc2",
            Region = "eu-central-1",
            Status = "Canary",
            StatusTone = VelaBadgeTone.Accent,
            LatencyMs = 22.6,
            Owner = "Kenji Sato",
        });
        DeploymentRows.Add(new OverviewDeploymentRow
        {
            Service = "vela-realtime-sync",
            Version = "v2.3.8",
            Region = "ap-southeast-1",
            Status = "Degraded",
            StatusTone = VelaBadgeTone.Warning,
            LatencyMs = 48.1,
            Owner = "Sofia Lind",
        });
        DeploymentRows.Add(new OverviewDeploymentRow
        {
            Service = "vela-search-indexer",
            Version = "v2.3.7",
            Region = "us-east-1",
            Status = "Healthy",
            StatusTone = VelaBadgeTone.Success,
            LatencyMs = 19.5,
            Owner = "Ada Lovelace",
        });
        DeploymentRows.Add(new OverviewDeploymentRow
        {
            Service = "vela-shader-cdn",
            Version = "v2.4.0",
            Region = "global-edge",
            Status = "Healthy",
            StatusTone = VelaBadgeTone.Success,
            LatencyMs = 8.4,
            Owner = "Liam Park",
        });
    }

    private void ApplyChartRange(int idx, bool jitter = false)
    {
        var zh = Loc.Instance.IsChinese;
        var mrrName = zh ? "净 MRR" : "Net MRR";
        var prevName = zh ? "上期基准" : "Prior Period";

        double[] Scale(double[] values)
        {
            if (!jitter) return values;
            var result = new double[values.Length];
            for (var i = 0; i < values.Length; i++)
                result[i] = Math.Round(values[i] * (0.90 + _rng.NextDouble() * 0.22), 0);
            return result;
        }

        if (idx == 0)
        {
            ChartLabels = zh
                ? ["周一", "周二", "周三", "周四", "周五", "周六", "周日"]
                : ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"];
            ChartSeries =
            [
                new("mrr", mrrName, Scale([14200, 16800, 15900, 20400, 23800, 21600, 26400])),
                new("prev", prevName, [11400, 12800, 13400, 15100, 16900, 16200, 18400]),
            ];
        }
        else if (idx == 1)
        {
            ChartLabels = ["W1", "W2", "W3", "W4", "W5", "W6", "W7"];
            ChartSeries =
            [
                new("mrr", mrrName, Scale([34000, 39200, 42800, 41500, 48900, 53600, 61200])),
                new("prev", prevName, [27500, 30400, 32100, 34900, 37200, 40500, 44100]),
            ];
        }
        else
        {
            ChartLabels = zh
                ? ["5月", "6月", "7月", "8月", "9月", "10月", "11月"]
                : ["May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov"];
            ChartSeries =
            [
                new("mrr", mrrName, Scale([88000, 99000, 114000, 112000, 138000, 154000, 179000])),
                new("prev", prevName, [64000, 73000, 81000, 88000, 96000, 108000, 121000]),
            ];
        }
    }
}
