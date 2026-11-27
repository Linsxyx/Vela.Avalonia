using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vela.Avalonia.Controls;
using Vela.Avalonia.Gallery.Localization;
using Vela.Avalonia.Gallery.Services;

namespace Vela.Avalonia.Gallery.ViewModels;

public partial class ActionsViewModel : ViewModelBase
{
    private readonly IGalleryShellService _shell;
    private string _splitModeKey = "StrDeployNow";
    private bool _isSavedBanner;

    [ObservableProperty]
    public partial bool IsSaving { get; set; }

    [ObservableProperty]
    public partial string SaveButtonText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SplitButtonLabel { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SplitStatus { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string HoldStatus { get; set; } = string.Empty;

    public event Action? ResetHoldRequested;

    public ActionsViewModel(IGalleryShellService shell)
    {
        _shell = shell;
        RefreshStrings();
        Loc.Instance.LanguageChanged += _ => RefreshStrings();
    }

    private void RefreshStrings()
    {
        var loc = Loc.Instance;
        SaveButtonText = loc.Get(_isSavedBanner ? "StrSavedCheck" : "StrSaveChanges");
        SplitButtonLabel = loc.Get(_splitModeKey);
        SplitStatus = loc.Get("StrReady");
        HoldStatus = loc.Get("StrHoldStatusIdle");
    }

    [RelayCommand]
    private async Task SaveChangesAsync()
    {
        if (IsSaving) return;
        IsSaving = true;
        await Task.Delay(850);
        IsSaving = false;
        _isSavedBanner = true;
        SaveButtonText = Loc.Instance.Get("StrSavedCheck");
        await Task.Delay(1300);
        _isSavedBanner = false;
        SaveButtonText = Loc.Instance.Get("StrSaveChanges");
    }

    [RelayCommand]
    private void ExecuteSplitPrimary()
    {
        var loc = Loc.Instance;
        SplitStatus = loc.Pick($"Executed: {SplitButtonLabel}", $"已执行：{SplitButtonLabel}");
        _shell.ShowToast(loc.Pick("Split Action", "拆分操作"), SplitButtonLabel, VelaToastTone.Success);
    }

    [RelayCommand]
    private void SetSplitMode(string modeKey)
    {
        _splitModeKey = modeKey;
        SplitButtonLabel = Loc.Instance.Get(modeKey);
        SplitStatus = Loc.Instance.Pick($"Mode: {SplitButtonLabel}", $"模式：{SplitButtonLabel}");
    }

    [RelayCommand]
    private void SplitRollback() =>
        _shell.ShowToast(
            Loc.Instance.Pick("Rollback queued", "已加入回滚队列"),
            Loc.Instance.Pick("Reverting to previous artifact.", "正在恢复上一稳定版本。"),
            VelaToastTone.Danger);

    [RelayCommand]
    private void SplitShare() =>
        _shell.ShowToast(Loc.Instance.Pick("Share", "分享"), Loc.Instance.Pick("Share sheet opened.", "已打开分享选项。"), VelaToastTone.Info);

    [RelayCommand]
    private void SplitCopyLink() =>
        _shell.ShowToast(Loc.Instance.Pick("Copied", "已复制"), "https://vela.dev/gallery", VelaToastTone.Success);

    [RelayCommand]
    private void SplitExportMd() =>
        _shell.ShowToast(Loc.Instance.Pick("Exported", "已导出"), Loc.Instance.Pick("Markdown copied.", "已复制 Markdown。"), VelaToastTone.Neutral);

    [RelayCommand]
    private void NotifyCopied() =>
        _shell.ShowToast(Loc.Instance.Pick("Copied", "已复制"), Loc.Instance.Pick("Copied to clipboard.", "已写入剪贴板。"), VelaToastTone.Success);

    [RelayCommand]
    private void BranchDeleted() =>
        _shell.ShowToast(Loc.Instance.Pick("Branch deleted", "分支已删除"), Loc.Instance.Pick("Click Undo to restore.", "可点击撤销恢复。"), VelaToastTone.Warning);

    [RelayCommand]
    private void BranchRestored() =>
        _shell.ShowToast(Loc.Instance.Pick("Branch restored", "分支已恢复"), "main-v2", VelaToastTone.Success);

    [RelayCommand]
    private void FloatingEdit() =>
        _shell.ShowToast(Loc.Instance.Get("StrEdit"), Loc.Instance.Pick("FloatingBar action.", "触发悬浮条操作。"), VelaToastTone.Info);

    [RelayCommand]
    private void FloatingCopy() =>
        _shell.ShowToast(Loc.Instance.Pick("Copied", "已复制"), Loc.Instance.Pick("Selection copied.", "已复制选中内容。"), VelaToastTone.Success);

    [RelayCommand]
    private void FloatingDelete() =>
        _shell.ShowToast(Loc.Instance.Get("StrDeleted"), Loc.Instance.Pick("Moved to trash.", "已移至废纸篓。"), VelaToastTone.Danger);

    public void UpdateHoldState(bool holding) =>
        HoldStatus = Loc.Instance.Get(holding ? "StrHoldStatusActive" : "StrHoldStatusIdle");

    [RelayCommand]
    private void HoldConfirmed(string target)
    {
        var loc = Loc.Instance;
        var (title, tone) = target switch
        {
            "purge" => (loc.Pick("Cache purged", "缓存已清空"), VelaToastTone.Danger),
            "publish" => (loc.Pick("Published v1.0", "已发布 v1.0"), VelaToastTone.Success),
            _ => (loc.Pick("Archive sealed", "归档已封存"), VelaToastTone.Info),
        };
        _shell.ShowToast(title, null, tone);
    }

    [RelayCommand]
    private void ResetAllHold()
    {
        ResetHoldRequested?.Invoke();
        HoldStatus = Loc.Instance.Get("StrHoldStatusReset");
    }
}
