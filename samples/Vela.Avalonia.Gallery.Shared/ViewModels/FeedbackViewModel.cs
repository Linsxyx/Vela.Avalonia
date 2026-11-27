using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vela.Avalonia.Controls;
using Vela.Avalonia.Gallery.Localization;
using Vela.Avalonia.Gallery.Services;
using Vela.Avalonia.Tokens;

namespace Vela.Avalonia.Gallery.ViewModels;

public partial class FeedbackViewModel : ViewModelBase
{
    private readonly IGalleryShellService _shell;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedMaterial))]
    public partial int OverlayMaterialIndex { get; set; } = 2;

    public VelaSurfaceMaterial SelectedMaterial => OverlayMaterialIndex switch
    {
        1 => VelaSurfaceMaterial.Frosted,
        2 => VelaSurfaceMaterial.GlassCard,
        3 => VelaSurfaceMaterial.LiquidGlass,
        _ => VelaSurfaceMaterial.Solid,
    };

    [ObservableProperty]
    public partial double ProgressValue { get; set; } = 42;

    [ObservableProperty]
    public partial bool IsProgressIndeterminate { get; set; }

    [ObservableProperty]
    public partial bool IsSkeletonLoading { get; set; } = true;

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    public event Action? RestoreAlertsRequested;

    public FeedbackViewModel(IGalleryShellService shell)
    {
        _shell = shell;
    }

    [RelayCommand]
    private async Task ShowConfirmDialogAsync()
    {
        var loc = Loc.Instance;
        var mat = SelectedMaterial;
        var ok = await _shell.ShowConfirmDialogAsync(new VelaConfirmDialogOptions
        {
            Title = loc.Pick($"Publish v0.1.0? ({mat})", $"发布 v0.1.0？（{mat}）"),
            Description = loc.Pick("Tag commit and push Vela.Avalonia packages.", "将打标签并推送 Vela.Avalonia 软件包。"),
            ConfirmText = loc.Pick("Publish", "发布"),
            CancelText = loc.Get("StrCancel"),
            Material = mat,
            Style = mat == VelaSurfaceMaterial.GlassCard ? VelaDialogStyle.Centered : VelaDialogStyle.Standard,
            Tone = mat == VelaSurfaceMaterial.GlassCard ? VelaDialogTone.Accent : VelaDialogTone.Neutral,
        });

        _shell.ShowToast(
            ok ? loc.Pick("Published", "已发布") : loc.Pick("Cancelled", "已取消"),
            ok ? loc.Pick($"Confirmed ({mat})", $"已确认（{mat}）") : null,
            ok ? VelaToastTone.Success : VelaToastTone.Neutral);
    }

    [RelayCommand]
    private async Task ShowDestructiveDialogAsync()
    {
        var loc = Loc.Instance;
        var ok = await _shell.ShowConfirmDialogAsync(new VelaConfirmDialogOptions
        {
            Title = loc.Pick("Delete workspace cache?", "确认清空工作区缓存？"),
            Description = loc.Pick("Cached page snapshots will be cleared.", "所有已缓存的页面快照将被立即清除。"),
            ConfirmText = loc.Get("StrDelete"),
            CancelText = loc.Pick("Keep", "保留"),
            IsDestructive = true,
            Material = SelectedMaterial,
        });

        if (ok)
        {
            _shell.ClearInactivePageCache();
            _shell.ShowToast(loc.Pick("Cache deleted", "缓存已删除"), null, VelaToastTone.Danger);
        }
    }

    [RelayCommand]
    private async Task ShowCustomDialogAsync()
    {
        var created = await _shell.ShowCreateProjectDialogAsync();
        if (!string.IsNullOrEmpty(created))
            _shell.ShowToast(Loc.Instance.Pick("Project created", "项目已创建"), created, VelaToastTone.Success);
    }

    [RelayCommand]
    private async Task ShowPinnedGlassDialogAsync()
    {
        var loc = Loc.Instance;
        var ok = await _shell.ShowConfirmDialogAsync(new VelaConfirmDialogOptions
        {
            Title = loc.Pick("Revoke API Key?", "吊销生产 API 密钥？"),
            Description = loc.Pick("Click backdrop to test pinned bounce physics.", "点击遮罩测试锁定弹窗的弹簧回弹效果。"),
            ConfirmText = loc.Pick("Revoke", "吊销"),
            CancelText = loc.Pick("Keep", "保留"),
            IsDestructive = true,
            CloseOnBackdropClick = false,
            ShowCloseButton = false,
            Material = VelaSurfaceMaterial.GlassCard,
            Style = VelaDialogStyle.Centered,
            Tone = VelaDialogTone.Danger,
        });

        if (ok)
            _shell.ShowToast(loc.Pick("API key revoked", "API 密钥已吊销"), null, VelaToastTone.Danger);
    }

    [RelayCommand]
    private async Task OpenDrawerAsync(string side)
    {
        var loc = Loc.Instance;
        var isLeft = string.Equals(side, "left", StringComparison.OrdinalIgnoreCase);
        var mat = SelectedMaterial;
        var closeBtn = new VelaButton
        {
            Variant = VelaButtonVariant.Primary,
            Size = VelaControlSize.Small,
            Content = isLeft ? loc.Pick("Close", "关闭") : loc.Pick("Apply & Close", "应用并关闭"),
        };
        closeBtn.Click += (_, _) =>
        {
            _shell.CloseDrawer();
            if (!isLeft)
                _shell.ShowToast(loc.Pick("Inspector updated", "检查器已更新"), null, VelaToastTone.Success);
        };

        var body = new StackPanel
        {
            Spacing = 14,
            Children =
            {
                new VelaInput { Label = loc.Pick("Title", "标题"), Text = "VelaDrawerHost", IsClearable = true },
                new VelaCheckBox { Content = loc.Pick("Close on backdrop click", "点击遮罩关闭"), IsChecked = true },
                new VelaBadge { Tone = VelaBadgeTone.Accent, IsPill = true, Content = mat.ToString() },
                closeBtn,
            },
        };

        await _shell.ShowDrawerAsync(body, new VelaDrawerOptions
        {
            Title = isLeft ? loc.Pick("Quick Filters", "快捷筛选") : loc.Get("StrInspector"),
            Description = mat.ToString(),
            Side = isLeft ? VelaDrawerSide.Left : VelaDrawerSide.Right,
            Width = isLeft ? 340 : 380,
            Material = mat,
        });
    }

    [RelayCommand]
    private void ShowToast(string tone)
    {
        var loc = Loc.Instance;
        switch (tone)
        {
            case "success":
                _shell.ShowToast(loc.Pick("Saved", "已保存"), loc.Pick("Configuration synced.", "配置已同步。"), VelaToastTone.Success);
                break;
            case "undo":
                _shell.ShowToast(loc.Pick("Archived 3 items", "已归档 3 项"), loc.Pick("Hover to pause timer.", "悬停可暂停自动消失。"), VelaToastTone.Info, loc.Get("StrUndo"),
                    () => _shell.ShowToast(loc.Pick("Restored", "已恢复"), loc.Pick("3 items restored.", "已恢复 3 个条目。"), VelaToastTone.Success));
                break;
            case "warning":
                _shell.ShowToast(loc.Pick("High memory", "内存占用较高"), loc.Pick("Try LRU page cache.", "建议切换为 LRU 页面缓存。"), VelaToastTone.Warning);
                break;
            case "danger":
                _shell.ShowToast(loc.Pick("Connection lost", "连接已断开"), loc.Pick("Retrying in 5s...", "5 秒后尝试重连..."), VelaToastTone.Danger);
                break;
        }
    }

    [RelayCommand]
    private void SetProgress(string percentText)
    {
        if (double.TryParse(percentText, out var value))
        {
            IsProgressIndeterminate = false;
            ProgressValue = value;
        }
    }

    [RelayCommand]
    private async Task SimulateBusyAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        await Task.Delay(2000);
        IsBusy = false;
        _shell.ShowToast(Loc.Instance.Pick("Task complete", "任务完成"), Loc.Instance.Pick("Indexed in 2.0s.", "耗时 2.0 秒完成索引。"), VelaToastTone.Success);
    }

    [RelayCommand]
    private void RestoreAlerts() => RestoreAlertsRequested?.Invoke();
}
