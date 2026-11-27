using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vela.Avalonia.Controls;
using Vela.Avalonia.Gallery.Localization;
using Vela.Avalonia.Gallery.Services;

namespace Vela.Avalonia.Gallery.ViewModels;

public partial class ShadersViewModel : ViewModelBase
{
    private readonly IGalleryShellService _shell;

    [ObservableProperty]
    public partial bool EnableRefraction { get; set; } = true;

    [ObservableProperty]
    public partial double BlurRadius { get; set; } = 4;

    [ObservableProperty]
    public partial double BevelWidth { get; set; } = 20;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Refraction))]
    public partial double RefractionPercent { get; set; } = 90;

    public double Refraction => RefractionPercent / 100.0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Dispersion))]
    public partial double DispersionPercent { get; set; } = 65;

    public double Dispersion => DispersionPercent / 100.0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShaderPreset))]
    public partial int ShaderPresetIndex { get; set; }

    public VelaShaderBackgroundPreset ShaderPreset => ShaderPresetIndex switch
    {
        1 => VelaShaderBackgroundPreset.Aurora,
        2 => VelaShaderBackgroundPreset.Silk,
        _ => VelaShaderBackgroundPreset.Mesh,
    };

    [ObservableProperty]
    public partial bool AnimateShader { get; set; } = true;

    [ObservableProperty]
    public partial Color PickerTintColor { get; set; } = Color.Parse("#6850B9");

    [ObservableProperty]
    public partial Color? ShaderPrimaryColor { get; set; }

    public ShadersViewModel(IGalleryShellService shell)
    {
        _shell = shell;
    }

    partial void OnPickerTintColorChanged(Color value)
    {
        ShaderPrimaryColor = value;
    }

    [RelayCommand]
    private void SyncShaderToTheme()
    {
        ShaderPrimaryColor = null;
        _shell.ShowToast(
            Loc.Instance.Get("StrSynced"),
            Loc.Instance.Pick("Shader tracks VelaTheme accent.", "着色器已同步主题强调色。"),
            VelaToastTone.Info);
    }
}
