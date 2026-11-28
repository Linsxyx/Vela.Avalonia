using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Vela.Avalonia.Controls;
using Xunit;

namespace Vela.Avalonia.Tests;

public class VelaGlassMenuTests
{
    [Fact]
    public void OpenCloseToggle_RaisesEventsAndUpdatesIsOpen()
    {
        var menu = new VelaGlassMenu();
        var openedCount = 0;
        var closedCount = 0;
        menu.Opened += (_, _) => openedCount++;
        menu.Closed += (_, _) => closedCount++;

        Assert.False(menu.IsOpen);

        menu.Open();
        Assert.True(menu.IsOpen);
        Assert.Equal(1, openedCount);

        menu.Toggle();
        Assert.False(menu.IsOpen);
        Assert.Equal(1, closedCount);

        menu.Toggle();
        Assert.True(menu.IsOpen);
        Assert.Equal(2, openedCount);
    }

    [Fact]
    public void TriggerMetrics_CircularWhenNoLabel()
    {
        var menu = new VelaGlassMenu { TriggerSize = 40.0 };
        var (w, h, r) = menu.ComputeTriggerMetrics();

        Assert.Equal(40.0, w);
        Assert.Equal(40.0, h);
        Assert.Equal(20.0, r);
    }

    [Fact]
    public void MenuHeight_AccountsForItemsSeparatorsAndSubmenuDrillIn()
    {
        var moveSub = new VelaMenuItem { Header = "Move to" };
        moveSub.Items.Add(new VelaMenuItem { Header = "Architecture" });
        moveSub.Items.Add(new VelaMenuItem { Header = "Archive" });

        var menu = new VelaGlassMenu();
        menu.Items.Add(new VelaMenuItem { Header = "Favorite" });
        menu.Items.Add(new VelaMenuItem { Header = "Copy link" });
        menu.Items.Add(new VelaMenuItem { IsSeparator = true });
        menu.Items.Add(moveSub);
        menu.Items.Add(new VelaMenuItem { Header = "Delete", IsDestructive = true });

        var rootHeight = menu.ComputeActiveMenuHeight();
        // 4 items (34) + 1 separator (9) + 4 spacings (2) + 12 padding = 165
        Assert.Equal(165.0, rootHeight);

        Assert.False(menu.IsInSubmenu);
        menu.NavigateInto(moveSub);
        Assert.True(menu.IsInSubmenu);
        Assert.Same(moveSub, menu.ActiveSubmenuParent);

        var subHeight = menu.ComputeActiveMenuHeight();
        // Back (32) + sep (9) + 2 items (34) + 3 spacings (2) + 12 padding = 127
        Assert.Equal(127.0, subHeight);

        Assert.True(menu.NavigateBack());
        Assert.False(menu.IsInSubmenu);
        Assert.False(menu.NavigateBack());
    }

    [Fact]
    public void SubmenuDrillIn_IgnoredForLeafItems()
    {
        var leaf = new VelaMenuItem { Header = "Download" };
        var menu = new VelaGlassMenu();
        menu.Items.Add(leaf);

        menu.NavigateInto(leaf);
        Assert.False(menu.IsInSubmenu);
        Assert.Null(menu.ActiveSubmenuParent);
    }

    [Fact]
    public void BackdropCacheHelper_AppliesAndReleasesRoundedVisualClip()
    {
        var border = new Border
        {
            CornerRadius = new CornerRadius(19),
        };
        border.Arrange(new Rect(0, 0, 38, 38));

        VelaBackdropCacheHelper.ApplyBlur(border, enabled: true, radius: 36);

        var rectClip = Assert.IsType<RectangleGeometry>(border.Clip);
        Assert.Equal(new Rect(0, 0, 38, 38), rectClip.Rect);
        Assert.Equal(19, rectClip.RadiusX);
        Assert.Equal(19, rectClip.RadiusY);

        border.CornerRadius = new CornerRadius(16, 0, 0, 16);
        Assert.IsType<PathGeometry>(border.Clip);

        VelaBackdropCacheHelper.Release(border);
        Assert.Null(border.Clip);
        Assert.Null(border.BackdropEffect);
        Assert.Null(border.BackdropEffectCache);
    }
}
