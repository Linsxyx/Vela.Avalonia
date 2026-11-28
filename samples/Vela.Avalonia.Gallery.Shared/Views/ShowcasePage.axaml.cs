using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Vela.Avalonia.Controls;
using Vela.Avalonia.Gallery.Localization;

namespace Vela.Avalonia.Gallery.Views;

public partial class ShowcasePage : UserControl
{
    public ShowcasePage()
    {
        InitializeComponent();
        PaymentFlowControl.PropertyChanged += OnPaymentFlowPropertyChanged;
        Loc.Instance.LanguageChanged += OnLanguageChanged;
        UpdatePlayPauseLabel();
    }

    private void OnPaymentFlowPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == VelaPaymentFlow.IsPlayingProperty)
            UpdatePlayPauseLabel();
    }

    private void OnLanguageChanged(AppLanguage _) => UpdatePlayPauseLabel();

    private void UpdatePlayPauseLabel()
    {
        PlayPauseBtn.Content = PaymentFlowControl.IsPlaying
            ? Loc.Instance.Get("StrPause")
            : Loc.Instance.Get("StrPlay");
    }

    private void OnPlayPauseClick(object? sender, RoutedEventArgs e)
    {
        PaymentFlowControl.TogglePlayPause();
        UpdatePlayPauseLabel();
    }

    private void OnRestartClick(object? sender, RoutedEventArgs e)
    {
        PaymentFlowControl.Restart();
        UpdatePlayPauseLabel();
    }

    private void OnStepNextClick(object? sender, RoutedEventArgs e)
    {
        PaymentFlowControl.StepStage(1);
        UpdatePlayPauseLabel();
    }

    private void OnToggleGlassMenuClick(object? sender, RoutedEventArgs e)
    {
        HeroGlassMenu.Toggle();
    }
}
