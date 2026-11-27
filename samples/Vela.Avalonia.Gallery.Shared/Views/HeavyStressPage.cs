using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Vela.Avalonia.Controls;
using Vela.Avalonia.Gallery.Localization;

namespace Vela.Avalonia.Gallery.Views;

/// <summary>
/// 500-node visual stress page implementing IVelaNavigationAware for VelaPageDeck benchmarks.
/// </summary>
public sealed class HeavyStressPage : UserControl, IVelaNavigationAware
{
    private static int _totalInstancesCreated;
    private readonly int _instanceId;
    private int _attachCount;
    private int _navigatedToCount;
    private readonly TextBlock _titleText;
    private readonly TextBlock _statsText;

    public HeavyStressPage()
    {
        _instanceId = ++_totalInstancesCreated;

        _titleText = new TextBlock
        {
            FontSize = 20,
            FontWeight = FontWeight.SemiBold,
        };

        _statsText = new TextBlock
        {
            FontSize = 12.5,
            [!TextBlock.ForegroundProperty] = this.GetResourceObservable("VelaTextSecondaryBrush").ToBinding(),
        };

        var wrap = new WrapPanel
        {
            Orientation = Orientation.Horizontal,
            ItemSpacing = 8,
            LineSpacing = 8,
        };

        for (var i = 1; i <= 500; i++)
        {
            var card = new Border
            {
                Width = 148,
                Height = 50,
                CornerRadius = new CornerRadius(10),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10, 6),
                [!Border.BackgroundProperty] = this.GetResourceObservable("VelaSurfaceBrush").ToBinding(),
                [!Border.BorderBrushProperty] = this.GetResourceObservable("VelaBorderBrush").ToBinding(),
                Child = new StackPanel
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    Children =
                    {
                        new TextBlock { Text = $"Node #{i:000}", FontSize = 12.5, FontWeight = FontWeight.Medium },
                        new TextBlock
                        {
                            Text = $"Inst #{_instanceId}",
                            FontSize = 11,
                            [!TextBlock.ForegroundProperty] = this.GetResourceObservable("VelaTextMutedBrush").ToBinding(),
                        },
                    },
                },
            };
            wrap.Children.Add(card);
        }

        Content = new ScrollViewer
        {
            Padding = new Thickness(28, 22),
            Content = new StackPanel
            {
                Spacing = 14,
                Children =
                {
                    _titleText,
                    _statsText,
                    wrap,
                },
            },
        };

        UpdateStats();
        Loc.Instance.LanguageChanged += OnLanguageChanged;
    }

    private void OnLanguageChanged(AppLanguage _) => UpdateStats();

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _attachCount++;
        UpdateStats();
    }

    public void OnNavigatedTo()
    {
        _navigatedToCount++;
        UpdateStats();
    }

    public void OnNavigatedFrom()
    {
    }

    private void UpdateStats()
    {
        _titleText.Text = Loc.Instance.Get("StrStressTitle");
        _statsText.Text = Loc.Instance.Format("StrStressStatsFormat", _instanceId, _attachCount, _navigatedToCount);
    }
}
