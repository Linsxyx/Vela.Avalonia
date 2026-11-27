using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vela.Avalonia.Controls;
using Vela.Avalonia.Gallery.Services;

namespace Vela.Avalonia.Gallery.ViewModels;

public partial class ActionsViewModel : ViewModelBase
{
    private readonly IGalleryShellService _shell;

    [ObservableProperty]
    public partial bool IsSaving { get; set; }

    [ObservableProperty]
    public partial string SaveButtonText { get; set; } = "Save changes";

    [ObservableProperty]
    public partial string SplitButtonLabel { get; set; } = "Deploy now";

    [ObservableProperty]
    public partial string SplitStatus { get; set; } = "Ready";

    [ObservableProperty]
    public partial string HoldStatus { get; set; } = "Hold to confirm";

    public event Action? ResetHoldRequested;

    public ActionsViewModel(IGalleryShellService shell)
    {
        _shell = shell;
    }

    [RelayCommand]
    private async Task SaveChangesAsync()
    {
        if (IsSaving) return;
        IsSaving = true;
        await Task.Delay(850);
        IsSaving = false;
        SaveButtonText = "Saved ✓";
        await Task.Delay(1300);
        SaveButtonText = "Save changes";
    }

    [RelayCommand]
    private void ExecuteSplitPrimary()
    {
        SplitStatus = $"Executed: {SplitButtonLabel}";
        _shell.ShowToast("Split Action", SplitButtonLabel, VelaToastTone.Success);
    }

    [RelayCommand]
    private void SetSplitMode(string label)
    {
        SplitButtonLabel = label;
        SplitStatus = $"Mode: {label}";
    }

    [RelayCommand]
    private void SplitRollback() =>
        _shell.ShowToast("Rollback queued", "Reverting to previous artifact.", VelaToastTone.Danger);

    [RelayCommand]
    private void SplitShare() =>
        _shell.ShowToast("Share", "Share sheet opened.", VelaToastTone.Info);

    [RelayCommand]
    private void SplitCopyLink() =>
        _shell.ShowToast("Copied", "https://vela.dev/gallery", VelaToastTone.Success);

    [RelayCommand]
    private void SplitExportMd() =>
        _shell.ShowToast("Exported", "Markdown copied to clipboard.", VelaToastTone.Neutral);

    [RelayCommand]
    private void NotifyCopied() =>
        _shell.ShowToast("Copied", "Copied to clipboard.", VelaToastTone.Success);

    [RelayCommand]
    private void BranchDeleted() =>
        _shell.ShowToast("Branch deleted", "Click Undo to restore.", VelaToastTone.Warning);

    [RelayCommand]
    private void BranchRestored() =>
        _shell.ShowToast("Branch restored", "main-v2 restored.", VelaToastTone.Success);

    [RelayCommand]
    private void FloatingEdit() =>
        _shell.ShowToast("Edit", "FloatingBar edit action.", VelaToastTone.Info);

    [RelayCommand]
    private void FloatingCopy() =>
        _shell.ShowToast("Copied", "Selection copied.", VelaToastTone.Success);

    [RelayCommand]
    private void FloatingDelete() =>
        _shell.ShowToast("Deleted", "Moved to trash.", VelaToastTone.Danger);

    public void UpdateHoldState(bool holding) =>
        HoldStatus = holding ? "Keep holding..." : "Hold to confirm";

    [RelayCommand]
    private void HoldConfirmed(string target)
    {
        var (title, tone) = target switch
        {
            "purge" => ("Cache purged", VelaToastTone.Danger),
            "publish" => ("Published v1.0", VelaToastTone.Success),
            _ => ("Archive sealed", VelaToastTone.Info),
        };
        _shell.ShowToast(title, null, tone);
    }

    [RelayCommand]
    private void ResetAllHold()
    {
        ResetHoldRequested?.Invoke();
        HoldStatus = "Reset complete";
    }
}
