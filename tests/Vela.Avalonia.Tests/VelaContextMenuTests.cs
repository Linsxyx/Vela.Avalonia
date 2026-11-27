using Avalonia.Controls;
using Avalonia.Input;
using Vela.Avalonia.Controls;
using Xunit;

namespace Vela.Avalonia.Tests;

public class VelaContextMenuTests
{
    [Fact]
    public void AttachedMenuProperty_RoundTripsOnControl()
    {
        var border = new Border();
        var menu = new VelaContextMenu();
        VelaContextMenu.SetMenu(border, menu);

        Assert.Same(menu, VelaContextMenu.GetMenu(border));

        VelaContextMenu.SetMenu(border, null);
        Assert.Null(VelaContextMenu.GetMenu(border));
    }

    [Fact]
    public void OpeningCancel_PreventsMenuFromOpening()
    {
        var target = new Border();
        var menu = new VelaContextMenu();
        menu.Items.Add(new VelaMenuItem { Header = "Play" });
        menu.Opening += (_, e) => e.Cancel = true;

        menu.ShowAt(target);

        Assert.False(menu.IsOpen);
    }

    [Fact]
    public void ComputeNextFocusableIndex_SkipsDisabledAndWraps()
    {
        var flags = new[] { false, true, false, true, false };

        Assert.Equal(1, VelaContextMenu.ComputeNextFocusableIndex(flags, -1, Key.Down));
        Assert.Equal(3, VelaContextMenu.ComputeNextFocusableIndex(flags, 1, Key.Down));
        Assert.Equal(1, VelaContextMenu.ComputeNextFocusableIndex(flags, 3, Key.Down));

        Assert.Equal(3, VelaContextMenu.ComputeNextFocusableIndex(flags, -1, Key.Up));
        Assert.Equal(1, VelaContextMenu.ComputeNextFocusableIndex(flags, 3, Key.Up));
        Assert.Equal(3, VelaContextMenu.ComputeNextFocusableIndex(flags, 1, Key.Up));

        Assert.Equal(1, VelaContextMenu.ComputeNextFocusableIndex(flags, 3, Key.Home));
        Assert.Equal(3, VelaContextMenu.ComputeNextFocusableIndex(flags, 1, Key.End));
    }

    [Fact]
    public void ComputeNextFocusableIndex_ReturnsNegativeOneWhenAllDisabled()
    {
        var flags = new[] { false, false };

        Assert.Equal(-1, VelaContextMenu.ComputeNextFocusableIndex(flags, -1, Key.Down));
        Assert.Equal(-1, VelaContextMenu.ComputeNextFocusableIndex(flags, 0, Key.Home));
    }
}
