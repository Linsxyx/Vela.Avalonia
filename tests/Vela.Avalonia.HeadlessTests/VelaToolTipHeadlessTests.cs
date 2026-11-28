using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Vela.Avalonia.Controls;
using Vela.Avalonia.HeadlessTests.Infrastructure;
using Vela.Avalonia.Motion;
using Xunit;

namespace Vela.Avalonia.HeadlessTests;

public class VelaToolTipHeadlessTests
{
    [AvaloniaFact]
    public void ToolTip_UsesElevatedPopupSurfaceWithUnclippedShadowMarginAndWrapping()
    {
        var previousReducedMotion = VelaMotionOptions.ReducedMotion;
        VelaMotionOptions.ReducedMotion = true;
        try
        {
            var tip = new ToolTip
            {
                Content = "This is a long tooltip message that should wrap cleanly inside MaxWidth."
            };
            var window = new Window { Width = 400, Height = 200, Content = tip };
            window.Show();
            window.UpdateLayout();

            var surface = tip.GetVisualDescendants().OfType<VelaPopupSurface>().FirstOrDefault();
            Assert.NotNull(surface);
            Assert.Equal("PART_Surface", surface!.Name);
            Assert.True(surface.Margin.Left >= 8 && surface.Margin.Bottom >= 12,
                $"Expected generous shadow margin on PART_Surface, got {surface.Margin}");
            Assert.Equal(new Thickness(1), surface.BorderThickness);
            Assert.Equal(new CornerRadius(10), surface.CornerRadius);
            Assert.Equal(1.0, surface.Opacity);

            // Elevated surface card uses distinct raised background and foreground (not inverted black-on-white)
            var bg = Assert.IsAssignableFrom<ISolidColorBrush>(tip.Background);
            var fg = Assert.IsAssignableFrom<ISolidColorBrush>(tip.Foreground);
            Assert.NotEqual(bg.Color, fg.Color);
            Assert.True(bg.Color.R > 200 && bg.Color.G > 200 && bg.Color.B > 200,
                $"Expected light raised surface background in Light theme, got {bg.Color}");

            // Generated TextBlock inside ContentPresenter should wrap within MaxWidth
            var textBlock = tip.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault();
            Assert.NotNull(textBlock);
            Assert.Equal(TextWrapping.Wrap, textBlock!.TextWrapping);

            window.Close();
        }
        finally
        {
            VelaMotionOptions.ReducedMotion = previousReducedMotion;
        }
    }

    [AvaloniaFact]
    public void ToolTip_InvertedClass_AppliesHighContrastStyle()
    {
        var tip = new ToolTip { Content = "Settings" };
        tip.Classes.Add("inverted");

        var window = new Window { Width = 400, Height = 200, Content = tip };
        window.Show();
        window.UpdateLayout();

        var bg = Assert.IsAssignableFrom<ISolidColorBrush>(tip.Background);
        var fg = Assert.IsAssignableFrom<ISolidColorBrush>(tip.Foreground);
        Assert.Equal(new Thickness(0), tip.BorderThickness);
        Assert.True(bg.Color.R < 30 && bg.Color.G < 30 && bg.Color.B < 30,
            $"Expected dark inverted background in Light theme, got {bg.Color}");
        Assert.True(fg.Color.R > 220 && fg.Color.G > 220 && fg.Color.B > 220,
            $"Expected light inverted foreground in Light theme, got {fg.Color}");

        window.Close();
    }

    [AvaloniaFact]
    public void ToolTip_SpringEntrance_AnimatesAndSettlesWithoutLeaking()
    {
        var previousReducedMotion = VelaMotionOptions.ReducedMotion;
        VelaMotionOptions.ReducedMotion = false;
        try
        {
            HeadlessLeakVerifier.AssertControlCollected(
                () => new ToolTip { Content = "Settings" },
                exercise: (window, ctrl) =>
                {
                    var tip = (ToolTip)ctrl;
                    var surface = tip.GetVisualDescendants().OfType<VelaPopupSurface>().FirstOrDefault();
                    Assert.NotNull(surface);

                    // Advance a few frames while spring entrance is active
                    for (var i = 0; i < 6; i++)
                    {
                        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                        Dispatcher.UIThread.RunJobs();
                    }

                    Assert.True(surface!.Opacity > 0.0, "Expected spring entrance to advance surface opacity");
                },
                label: "ToolTip with VelaPopupSurface spring entrance");
        }
        finally
        {
            VelaMotionOptions.ReducedMotion = previousReducedMotion;
        }
    }
}
