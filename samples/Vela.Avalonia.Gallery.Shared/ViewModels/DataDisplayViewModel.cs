using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vela.Avalonia.Controls;
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
    public partial string EmptyTitle { get; set; } = "Inbox is clear";

    [ObservableProperty]
    public partial string EmptyDescription { get; set; } = "Reviews and mentions will appear here.";

    [ObservableProperty]
    public partial Geometry? EmptyIcon { get; set; }

    public DataDisplayViewModel(IGalleryShellService shell)
    {
        _shell = shell;
        EmptyIcon = _shell.FindIcon("VelaIconInbox");
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
        switch (_emptyStateIdx)
        {
            case 0:
                EmptyIcon = _shell.FindIcon("VelaIconInbox");
                EmptyTitle = "Inbox is clear";
                EmptyDescription = "Reviews and mentions will appear here.";
                break;
            case 1:
                EmptyIcon = _shell.FindIcon("VelaIconSearch");
                EmptyTitle = "No results";
                EmptyDescription = "Try widening your filter or clearing tags.";
                break;
            default:
                EmptyIcon = _shell.FindIcon("VelaIconCheckCircle");
                EmptyTitle = "All checks passed";
                EmptyDescription = "0 warnings · all packages up to date.";
                break;
        }
    }

    [RelayCommand]
    private void CreateWorkflow() =>
        _shell.ShowToast("Workflow created", "Default CI pipeline ready.", VelaToastTone.Success);

    [RelayCommand]
    private void CopyEndpoint() =>
        _shell.ShowToast("Copied", "Endpoint copied to clipboard.", VelaToastTone.Success);

    [RelayCommand]
    private void OpenDiagnostics() =>
        _shell.ShowToast("Diagnostics", "Springs idle · 0 shader warnings.", VelaToastTone.Info);
}
