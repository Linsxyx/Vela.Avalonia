using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vela.Avalonia.Controls;
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
        var mat = SelectedMaterial;
        var ok = await _shell.ShowConfirmDialogAsync(new VelaConfirmDialogOptions
        {
            Title = $"Publish v0.1.0? ({mat})",
            Description = "Tag commit and push Vela.Avalonia packages.",
            ConfirmText = "Publish",
            CancelText = "Cancel",
            Material = mat,
            Style = mat == VelaSurfaceMaterial.GlassCard ? VelaDialogStyle.Centered : VelaDialogStyle.Standard,
            Tone = mat == VelaSurfaceMaterial.GlassCard ? VelaDialogTone.Accent : VelaDialogTone.Neutral,
        });

        _shell.ShowToast(
            ok ? "Published" : "Cancelled",
            ok ? $"Confirmed with {mat} material." : null,
            ok ? VelaToastTone.Success : VelaToastTone.Neutral);
    }

    [RelayCommand]
    private async Task ShowDestructiveDialogAsync()
    {
        var ok = await _shell.ShowConfirmDialogAsync(new VelaConfirmDialogOptions
        {
            Title = "Delete workspace cache?",
            Description = "Cached page snapshots will be cleared.",
            ConfirmText = "Delete",
            CancelText = "Keep",
            IsDestructive = true,
            Material = SelectedMaterial,
        });

        if (ok)
        {
            _shell.ClearInactivePageCache();
            _shell.ShowToast("Cache deleted", null, VelaToastTone.Danger);
        }
    }

    [RelayCommand]
    private async Task ShowCustomDialogAsync()
    {
        var created = await _shell.ShowCreateProjectDialogAsync();
        if (!string.IsNullOrEmpty(created))
            _shell.ShowToast("Project created", created, VelaToastTone.Success);
    }

    [RelayCommand]
    private async Task ShowPinnedGlassDialogAsync()
    {
        var ok = await _shell.ShowConfirmDialogAsync(new VelaConfirmDialogOptions
        {
            Title = "Revoke API Key?",
            Description = "Click backdrop to test pinned bounce physics.",
            ConfirmText = "Revoke",
            CancelText = "Keep",
            IsDestructive = true,
            CloseOnBackdropClick = false,
            ShowCloseButton = false,
            Material = VelaSurfaceMaterial.GlassCard,
            Style = VelaDialogStyle.Centered,
            Tone = VelaDialogTone.Danger,
        });

        if (ok)
            _shell.ShowToast("API key revoked", null, VelaToastTone.Danger);
    }

    [RelayCommand]
    private async Task OpenDrawerAsync(string side)
    {
        var isLeft = string.Equals(side, "left", StringComparison.OrdinalIgnoreCase);
        var mat = SelectedMaterial;
        var closeBtn = new VelaButton
        {
            Variant = VelaButtonVariant.Primary,
            Size = VelaControlSize.Small,
            Content = isLeft ? "Close" : "Apply & Close",
        };
        closeBtn.Click += (_, _) =>
        {
            _shell.CloseDrawer();
            if (!isLeft)
                _shell.ShowToast("Inspector updated", null, VelaToastTone.Success);
        };

        var body = new StackPanel
        {
            Spacing = 14,
            Children =
            {
                new VelaInput { Label = "Title", Text = "VelaDrawerHost", IsClearable = true },
                new VelaCheckBox { Content = "Close on backdrop click", IsChecked = true },
                new VelaBadge { Tone = VelaBadgeTone.Accent, IsPill = true, Content = mat.ToString() },
                closeBtn,
            },
        };

        await _shell.ShowDrawerAsync(body, new VelaDrawerOptions
        {
            Title = isLeft ? "Quick Filters" : "Inspector",
            Description = $"{mat} material",
            Side = isLeft ? VelaDrawerSide.Left : VelaDrawerSide.Right,
            Width = isLeft ? 340 : 380,
            Material = mat,
        });
    }

    [RelayCommand]
    private void ShowToast(string tone)
    {
        switch (tone)
        {
            case "success":
                _shell.ShowToast("Saved", "Configuration synced.", VelaToastTone.Success);
                break;
            case "undo":
                _shell.ShowToast("Archived 3 items", "Hover to pause timer.", VelaToastTone.Info, "Undo",
                    () => _shell.ShowToast("Restored", "3 items restored.", VelaToastTone.Success));
                break;
            case "warning":
                _shell.ShowToast("High memory", "Try LRU page cache.", VelaToastTone.Warning);
                break;
            case "danger":
                _shell.ShowToast("Connection lost", "Retrying in 5s...", VelaToastTone.Danger);
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
        _shell.ShowToast("Task complete", "Indexed in 2.0s.", VelaToastTone.Success);
    }

    [RelayCommand]
    private void RestoreAlerts() => RestoreAlertsRequested?.Invoke();
}
