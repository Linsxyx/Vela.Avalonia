using Avalonia.Controls;
using Vela.Avalonia.Gallery.ViewModels;

namespace Vela.Avalonia.Gallery.Views;

public partial class FeedbackPage : UserControl
{
    private FeedbackViewModel? _vm;

    public FeedbackPage()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_vm is not null)
            _vm.RestoreAlertsRequested -= OnRestoreAlerts;

        _vm = DataContext as FeedbackViewModel;
        if (_vm is not null)
            _vm.RestoreAlertsRequested += OnRestoreAlerts;
    }

    private void OnRestoreAlerts()
    {
        AlertInfo.Restore();
        AlertSuccess.Restore();
        AlertWarning.Restore();
    }
}
