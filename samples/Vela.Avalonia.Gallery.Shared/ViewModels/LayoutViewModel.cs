using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vela.Avalonia.Controls;
using Vela.Avalonia.Gallery.Localization;
using Vela.Avalonia.Gallery.Services;
using Vela.Avalonia.Tokens;

namespace Vela.Avalonia.Gallery.ViewModels;

public partial class LayoutViewModel : ViewModelBase
{
    private readonly IGalleryShellService _shell;
    private static readonly string[] DockTabKeys = ["StrDockHome", "StrDockExplore", "StrDockSaved", "StrDockProfile"];
    private static readonly string[] ExtraFolders = ["Themes", "VelaStepper.axaml", "Templates", "Parts"];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DockMaterial))]
    [NotifyPropertyChangedFor(nameof(ShowDockShader))]
    public partial int DockMaterialIndex { get; set; }

    public VelaSurfaceMaterial DockMaterial => DockMaterialIndex switch
    {
        1 => VelaSurfaceMaterial.Frosted,
        2 => VelaSurfaceMaterial.LiquidGlass,
        _ => VelaSurfaceMaterial.Solid,
    };

    public bool ShowDockShader => DockMaterial != VelaSurfaceMaterial.Solid;

    [ObservableProperty]
    public partial int DockSelectedIndex { get; set; } = 2;

    [ObservableProperty]
    public partial int DockBadgeCount { get; set; } = 3;

    [ObservableProperty]
    public partial string? DockBadgeText { get; set; } = "3";

    [ObservableProperty]
    public partial string DockStatus { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TabsVariant))]
    public partial int TabsVariantIndex { get; set; }

    public VelaTabsVariant TabsVariant =>
        TabsVariantIndex == 1 ? VelaTabsVariant.Underline : VelaTabsVariant.Pill;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StepperOrientation))]
    public partial int StepperOrientationIndex { get; set; }

    public VelaStepperOrientation StepperOrientation =>
        StepperOrientationIndex == 1 ? VelaStepperOrientation.Vertical : VelaStepperOrientation.Horizontal;

    [ObservableProperty]
    public partial int CurrentStep { get; set; } = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PaginationSummary))]
    public partial int CurrentPage { get; set; } = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TotalPages))]
    [NotifyPropertyChangedFor(nameof(PaginationSummary))]
    public partial int PaginationTotalIndex { get; set; } = 1;

    public int TotalPages => PaginationTotalIndex switch
    {
        0 => 5,
        2 => 24,
        _ => 12,
    };

    public string PaginationSummary
    {
        get
        {
            var page = Math.Clamp(CurrentPage, 1, TotalPages);
            var start = (page - 1) * 10 + 1;
            var end = page * 10;
            return Loc.Instance.IsChinese
                ? $"第 {start}–{end} 项 / 共 {TotalPages * 10} 项 · 第 {page}/{TotalPages} 页"
                : $"{start}–{end} of {TotalPages * 10} · Page {page}/{TotalPages}";
        }
    }

    [ObservableProperty]
    public partial string SplitSharesText { get; set; } = string.Empty;

    public event Action<bool>? StepperErrorToggleRequested;
    public event Action? StepperCompleteAllRequested;
    public event Action<string>? BreadcrumbDrillRequested;
    public event Action? ResetSplitRequested;

    private int _breadcrumbExtraDepth;

    public LayoutViewModel(IGalleryShellService shell)
    {
        _shell = shell;
        RefreshStrings();
        Loc.Instance.LanguageChanged += _ => RefreshStrings();
    }

    private void RefreshStrings()
    {
        UpdateDockTabStatus(DockSelectedIndex);
        OnPropertyChanged(nameof(PaginationSummary));
        var loc = Loc.Instance;
        SplitSharesText = $"{loc.Get("StrExplorer")} · {loc.Get("StrEditor")} · {loc.Get("StrInspector")}";
    }

    partial void OnDockSelectedIndexChanged(int value) => UpdateDockTabStatus(value);

    private void UpdateDockTabStatus(int index)
    {
        if (index >= 0 && index < DockTabKeys.Length)
        {
            var tab = Loc.Instance.Get(DockTabKeys[index]);
            DockStatus = Loc.Instance.Pick($"Tab: {tab}", $"当前标签：{tab}");
        }
    }

    partial void OnPaginationTotalIndexChanged(int value)
    {
        if (CurrentPage > TotalPages)
            CurrentPage = TotalPages;
    }

    [RelayCommand]
    private void IncrementDockBadge()
    {
        DockBadgeCount++;
        DockBadgeText = DockBadgeCount.ToString();
    }

    [RelayCommand]
    private void ClearDockBadge()
    {
        DockBadgeCount = 0;
        DockBadgeText = null;
    }

    public void OnDockSearchSubmitted(string query)
    {
        var loc = Loc.Instance;
        DockStatus = string.IsNullOrWhiteSpace(query)
            ? loc.Pick("Empty search", "空搜索")
            : loc.Pick($"Search: \"{query}\"", $"搜索：「{query}」");
        _shell.ShowToast(loc.Pick("Dock Search", "悬浮栏搜索"), DockStatus, VelaToastTone.Info);
    }

    [RelayCommand]
    private void PreviousStep()
    {
        if (CurrentStep > 0) CurrentStep--;
    }

    [RelayCommand]
    private void NextStep()
    {
        if (CurrentStep < 4) CurrentStep++;
    }

    [RelayCommand]
    private void ToggleStepError() => StepperErrorToggleRequested?.Invoke(true);

    [RelayCommand]
    private void CompleteAllSteps()
    {
        StepperCompleteAllRequested?.Invoke();
        CurrentStep = 4;
    }

    public void OnStepSelected(int index, string? title) =>
        _shell.ShowToast(Loc.Instance.Pick($"Step {index + 1}", $"步骤 {index + 1}"), title, VelaToastTone.Neutral);

    [RelayCommand]
    private void DrillBreadcrumb()
    {
        if (_breadcrumbExtraDepth >= ExtraFolders.Length)
        {
            _shell.ShowToast(
                Loc.Instance.Pick("Max depth", "已达最大深度"),
                Loc.Instance.Pick("Click any crumb to navigate up.", "点击任意上级路径返回。"),
                VelaToastTone.Info);
            return;
        }
        var label = ExtraFolders[_breadcrumbExtraDepth++];
        BreadcrumbDrillRequested?.Invoke(label);
    }

    public void SyncBreadcrumbDepth(int totalItems) =>
        _breadcrumbExtraDepth = Math.Max(0, totalItems - 3);

    [RelayCommand]
    private void ResetSplit() => ResetSplitRequested?.Invoke();

    public void UpdateSplitShares(IReadOnlyList<double> shares)
    {
        if (shares.Count >= 3)
        {
            var loc = Loc.Instance;
            SplitSharesText = $"{loc.Get("StrExplorer")} {shares[0]:0}% · {loc.Get("StrEditor")} {shares[1]:0}% · {loc.Get("StrInspector")} {shares[2]:0}%";
        }
    }
}
