using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vela.Avalonia.Controls;
using Vela.Avalonia.Gallery.Localization;
using Vela.Avalonia.Gallery.Services;

namespace Vela.Avalonia.Gallery.ViewModels;

public partial class DataDisplayViewModel : ViewModelBase
{
    private readonly IGalleryShellService _shell;
    private bool _metricsToggle;
    private int _avatarStatusIdx;
    private int _emptyStateIdx;

    private static readonly VelaAvatarStatus[] Statuses =
    [
        VelaAvatarStatus.Online,
        VelaAvatarStatus.Busy,
        VelaAvatarStatus.Away,
        VelaAvatarStatus.Offline,
        VelaAvatarStatus.None,
    ];

    [ObservableProperty]
    public partial double Revenue { get; set; } = 48250;

    [ObservableProperty]
    public partial double ActiveUsers { get; set; } = 12940;

    [ObservableProperty]
    public partial double SwitchLatency { get; set; } = 0.12;

    [ObservableProperty]
    public partial VelaAvatarStatus AvatarStatus { get; set; } = VelaAvatarStatus.Online;

    [ObservableProperty]
    public partial string EmptyTitle { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string EmptyDescription { get; set; } = string.Empty;

    [ObservableProperty]
    public partial Geometry? EmptyIcon { get; set; }

    public DataDisplayViewModel(IGalleryShellService shell)
    {
        _shell = shell;
        ApplyEmptyState();
        Loc.Instance.LanguageChanged += _ => ApplyEmptyState();
    }

    public void EnsureIconsLoaded()
    {
        EmptyIcon ??= _shell.FindIcon("VelaIconInbox");
    }

    [RelayCommand]
    private void SimulateMetrics()
    {
        _metricsToggle = !_metricsToggle;
        Revenue = _metricsToggle ? 74620 : 48250;
        ActiveUsers = _metricsToggle ? 19480 : 12940;
        SwitchLatency = _metricsToggle ? 0.08 : 0.12;
    }

    [RelayCommand]
    private void CycleAvatarStatus()
    {
        _avatarStatusIdx = (_avatarStatusIdx + 1) % Statuses.Length;
        AvatarStatus = Statuses[_avatarStatusIdx];
    }

    [RelayCommand]
    private void CycleEmptyState()
    {
        _emptyStateIdx = (_emptyStateIdx + 1) % 3;
        ApplyEmptyState();
    }

    private void ApplyEmptyState()
    {
        var loc = Loc.Instance;
        switch (_emptyStateIdx)
        {
            case 0:
                EmptyIcon = _shell.FindIcon("VelaIconInbox");
                EmptyTitle = loc.Get("StrEmptyInboxTitle");
                EmptyDescription = loc.Get("StrEmptyInboxDesc");
                break;
            case 1:
                EmptyIcon = _shell.FindIcon("VelaIconSearch");
                EmptyTitle = loc.Get("StrEmptySearchTitle");
                EmptyDescription = loc.Get("StrEmptySearchDesc");
                break;
            default:
                EmptyIcon = _shell.FindIcon("VelaIconCheckCircle");
                EmptyTitle = loc.Get("StrEmptyDoneTitle");
                EmptyDescription = loc.Get("StrEmptyDoneDesc");
                break;
        }
    }

    [RelayCommand]
    private void CreateWorkflow() =>
        _shell.ShowToast(
            Loc.Instance.Pick("Workflow created", "工作流已创建"),
            Loc.Instance.Pick("Default CI pipeline ready.", "默认 CI 流水线已就绪。"),
            VelaToastTone.Success);

    [RelayCommand]
    private void CopyEndpoint() =>
        _shell.ShowToast(
            Loc.Instance.Pick("Copied", "已复制"),
            Loc.Instance.Pick("Endpoint copied.", "接口地址已复制。"),
            VelaToastTone.Success);

    [RelayCommand]
    private void OpenDiagnostics() =>
        _shell.ShowToast(
            Loc.Instance.Get("StrDiagnostics"),
            Loc.Instance.Pick("Springs idle · 0 shader warnings.", "弹簧空载 · 0 着色器警告。"),
            VelaToastTone.Info);
}
