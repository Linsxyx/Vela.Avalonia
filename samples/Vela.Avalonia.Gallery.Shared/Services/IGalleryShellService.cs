using Avalonia.Controls;
using Avalonia.Media;
using Vela.Avalonia.Controls;
using Vela.Avalonia.Tokens;

namespace Vela.Avalonia.Gallery.Services;

public interface IGalleryShellService
{
    void ShowToast(
        string title,
        string? description = null,
        VelaToastTone tone = VelaToastTone.Neutral,
        string? actionLabel = null,
        Action? onAction = null);

    Task<bool> ShowConfirmDialogAsync(VelaConfirmDialogOptions options);
    Task<string?> ShowCreateProjectDialogAsync();
    Task ShowDrawerAsync(Control content, VelaDrawerOptions options);
    void CloseDrawer();

    void OpenCommandPalette();
    void ClearInactivePageCache();
    void NavigateTo(string pageKey);

    void SetThemeVariant(bool isDark);
    void SetAccent(VelaAccent accent);

    Geometry? FindIcon(string resourceKey);
    IBrush? FindBrush(string resourceKey);

    void RequestQuit();
}
