using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Vela.Avalonia.Controls;
using Vela.Avalonia.Gallery.Localization;
using Vela.Avalonia.Tokens;

namespace Vela.Avalonia.Gallery.Services;

public sealed class GalleryShellService : IGalleryShellService
{
    private Control? _resourceHost;
    private VelaDialogHost? _dialogHost;
    private VelaDrawerHost? _drawerHost;
    private VelaToastHost? _toastHost;
    private VelaCommandPalette? _commandPalette;
    private VelaNavigationView? _navigationView;
    private Action<string>? _navigateAction;
    private Action? _quitAction;

    public void AttachView(
        Control resourceHost,
        VelaDialogHost dialogHost,
        VelaDrawerHost drawerHost,
        VelaToastHost toastHost,
        VelaCommandPalette commandPalette,
        VelaNavigationView navigationView,
        Action<string> navigateAction)
    {
        _resourceHost = resourceHost;
        _dialogHost = dialogHost;
        _drawerHost = drawerHost;
        _toastHost = toastHost;
        _commandPalette = commandPalette;
        _navigationView = navigationView;
        _navigateAction = navigateAction;
    }

    public void AttachQuitAction(Action quitAction) => _quitAction = quitAction;

    public void ShowToast(
        string title,
        string? description = null,
        VelaToastTone tone = VelaToastTone.Neutral,
        string? actionLabel = null,
        Action? onAction = null)
    {
        _toastHost?.Show(title, description, tone, actionLabel: actionLabel, onAction: onAction);
    }

    public async Task<bool> ShowConfirmDialogAsync(VelaConfirmDialogOptions options)
    {
        if (_dialogHost is null)
            return false;
        return await _dialogHost.ShowConfirmAsync(options);
    }

    public async Task<string?> ShowCreateProjectDialogAsync()
    {
        if (_dialogHost is null)
            return null;

        var loc = Loc.Instance;
        var nameInput = new VelaInput
        {
            Label = loc.Get("StrProjectName"),
            PlaceholderText = "Vela Desktop Studio",
            IsClearable = true,
        };

        var cancelBtn = new VelaButton { Variant = VelaButtonVariant.Secondary, Size = VelaControlSize.Small, Content = loc.Get("StrCancel") };
        var createBtn = new VelaButton { Variant = VelaButtonVariant.Primary, Size = VelaControlSize.Small, Content = loc.Get("StrCreate") };

        cancelBtn.Click += (_, _) => _dialogHost.Close(null);
        createBtn.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(nameInput.Text))
            {
                nameInput.ErrorMessage = loc.Get("StrProjectRequired");
                return;
            }
            _dialogHost.Close(nameInput.Text.Trim());
        };

        var content = new StackPanel
        {
            Spacing = 16,
            Children =
            {
                nameInput,
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 8,
                    Children = { cancelBtn, createBtn },
                },
            },
        };

        return await _dialogHost.ShowAsync<string>(content, new VelaDialogOptions
        {
            Title = loc.Get("StrNewProject"),
            Description = loc.Get("StrEnterWorkspaceName"),
        });
    }

    public async Task ShowDrawerAsync(Control content, VelaDrawerOptions options)
    {
        if (_drawerHost is null)
            return;
        await _drawerHost.ShowAsync(content, options);
    }

    public void CloseDrawer() => _drawerHost?.Close();

    public void OpenCommandPalette() => _commandPalette?.Open();

    public void ClearInactivePageCache() => _navigationView?.ClearInactivePageCache();

    public void NavigateTo(string pageKey) => _navigateAction?.Invoke(pageKey);

    public void SetThemeVariant(bool isDark)
    {
        if (Application.Current is not null)
            Application.Current.RequestedThemeVariant = isDark ? ThemeVariant.Dark : ThemeVariant.Light;
    }

    public void SetAccent(VelaAccent accent)
    {
        (Application.Current as App)?.SetAccent(accent);
    }

    public Geometry? FindIcon(string resourceKey)
    {
        var theme = _resourceHost?.ActualThemeVariant ?? Application.Current?.ActualThemeVariant;
        if (_resourceHost is not null && _resourceHost.TryFindResource(resourceKey, theme, out var res) && res is Geometry g)
            return g;
        if (Application.Current is not null && Application.Current.TryFindResource(resourceKey, theme, out var appRes) && appRes is Geometry ag)
            return ag;
        return null;
    }

    public IBrush? FindBrush(string resourceKey)
    {
        var theme = _resourceHost?.ActualThemeVariant ?? Application.Current?.ActualThemeVariant;
        if (_resourceHost is not null && _resourceHost.TryFindResource(resourceKey, theme, out var res) && res is IBrush b)
            return b;
        if (Application.Current is not null && Application.Current.TryFindResource(resourceKey, theme, out var appRes) && appRes is IBrush ab)
            return ab;
        return null;
    }

    public void RequestQuit() => _quitAction?.Invoke();
}
