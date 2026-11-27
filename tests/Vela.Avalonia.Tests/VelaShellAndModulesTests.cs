using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using SkiaSharp;
using Vela.Avalonia.Controls;
using Vela.Avalonia.Tokens;
using Xunit;

namespace Vela.Avalonia.Tests;

public class VelaShellAndModulesTests
{
    private sealed class DummyCommand : ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) { }
    }

    private static bool HasResourceKey(IResourceDictionary dict, object key)
    {
        if (dict is ResourceDictionary rd && rd.ContainsKey(key))
            return true;

        foreach (var merged in dict.MergedDictionaries)
        {
            if (merged is IResourceDictionary child && HasResourceKey(child, key))
                return true;
        }

        return false;
    }

    [Fact]
    public void VelaTheme_LoadsOnlyRequestedModules_DuringInit_AndSupportsRuntimeToggle()
    {
        var theme = new VelaTheme();
        theme.BeginInit();
        theme.Accent = VelaAccent.Blue;
        theme.Modules = VelaThemeModules.Core | VelaThemeModules.Shell;
        theme.EndInit();

        Assert.Equal(VelaThemeModules.Core | VelaThemeModules.Shell, theme.LoadedModules);
        Assert.True(HasResourceKey(theme.Resources, typeof(VelaButton)));
        Assert.True(HasResourceKey(theme.Resources, typeof(VelaStatusBar)));
        Assert.True(HasResourceKey(theme.Resources, typeof(VelaToolBar)));
        Assert.True(HasResourceKey(theme.Resources, typeof(VelaMenuBar)));
        Assert.False(HasResourceKey(theme.Resources, typeof(VelaDataGrid)));
        Assert.False(HasResourceKey(theme.Resources, typeof(VelaGlassCard)));

        theme.EnableModules(VelaThemeModules.DataViews);
        Assert.True((theme.LoadedModules & VelaThemeModules.DataViews) == VelaThemeModules.DataViews);
        Assert.True(HasResourceKey(theme.Resources, typeof(VelaDataGrid)));

        theme.DisableModules(VelaThemeModules.Shell);
        Assert.False((theme.LoadedModules & VelaThemeModules.Shell) == VelaThemeModules.Shell);
        Assert.False(HasResourceKey(theme.Resources, typeof(VelaStatusBar)));
    }

    [Fact]
    public void VelaTheme_DefaultsToAllModules_WhenEnsureModulesLoadedCalled()
    {
        var theme = new VelaTheme();
        theme.EnsureModulesLoaded();

        Assert.Equal(VelaThemeModules.All, theme.LoadedModules);
        Assert.True(HasResourceKey(theme.Resources, typeof(VelaButton)));
        Assert.True(HasResourceKey(theme.Resources, typeof(VelaWindow)));
        Assert.True(HasResourceKey(theme.Resources, typeof(VelaSelect)));
        Assert.True(HasResourceKey(theme.Resources, typeof(VelaDataGrid)));
        Assert.True(HasResourceKey(theme.Resources, typeof(VelaGlassCard)));
    }

    [Fact]
    public void VelaNavigationView_TracksBottomBarAndBackgroundState()
    {
        var nav = new VelaNavigationView();
        Assert.False(nav.EffectiveBottomBarVisible);
        Assert.DoesNotContain(":has-background-content", nav.Classes);

        nav.BottomBarLeftContent = "Ready";
        Assert.True(nav.EffectiveBottomBarVisible);

        nav.IsBottomBarVisible = false;
        Assert.False(nav.EffectiveBottomBarVisible);

        nav.IsBottomBarVisible = true;
        nav.BottomBarLeftContent = null;
        nav.BottomBarContent = new VelaStatusBar();
        Assert.True(nav.EffectiveBottomBarVisible);

        nav.BackgroundContent = new VelaBackgroundLayer();
        Assert.Contains(":has-background-content", nav.Classes);

        nav.ShowTopBarDivider = false;
        nav.ShowBottomBarDivider = false;
        Assert.Contains(":no-topbar-divider", nav.Classes);
        Assert.Contains(":no-bottombar-divider", nav.Classes);
    }

    [Fact]
    public void VelaStatusBarItem_SyncsInteractiveAndTonePseudoClasses()
    {
        var item = new VelaStatusBarItem { Text = "Connected", ShowIndicatorDot = true, Tone = VelaStatusTone.Success };
        Assert.DoesNotContain(":interactive", item.Classes);
        Assert.Contains(":has-dot", item.Classes);
        Assert.Contains(":success", item.Classes);

        item.Command = new DummyCommand();
        Assert.Contains(":interactive", item.Classes);

        item.Tone = VelaStatusTone.Warning;
        Assert.DoesNotContain(":success", item.Classes);
        Assert.Contains(":warning", item.Classes);
    }

    [Fact]
    public void VelaToolBar_PropagatesOrientationAndCompactStateToChildren()
    {
        var toolbar = new VelaToolBar();
        var btn = new VelaToolBarButton { Label = "Save" };
        var toggle = new VelaToolBarToggleButton { Label = "Grid" };
        var sep = new VelaToolBarSeparator();

        toolbar.Items.Add(btn);
        toolbar.Items.Add(sep);
        toolbar.TrailingItems.Add(toggle);

        toolbar.Orientation = Orientation.Vertical;
        toolbar.IsCompact = true;

        Assert.Equal(Orientation.Vertical, sep.Orientation);
        Assert.True(btn.IsCompact);
        Assert.True(toggle.IsCompact);
        Assert.Contains(":vertical", toolbar.Classes);
        Assert.Contains(":compact", toolbar.Classes);
    }

    [Fact]
    public void VelaMenuBar_SwitchesActiveItemExclusively_AndClosesAll()
    {
        var bar = new VelaMenuBar();
        var file = new VelaMenuBarItem { Header = "File" };
        var edit = new VelaMenuBarItem { Header = "Edit" };
        bar.Items.Add(file);
        bar.Items.Add(edit);

        bar.OpenItem(file);
        Assert.Same(file, bar.ActiveItem);
        Assert.True(file.IsOpen);
        Assert.False(edit.IsOpen);

        bar.OpenItem(edit);
        Assert.Same(edit, bar.ActiveItem);
        Assert.False(file.IsOpen);
        Assert.True(edit.IsOpen);

        bar.CloseAll();
        Assert.Null(bar.ActiveItem);
        Assert.False(file.IsOpen);
        Assert.False(edit.IsOpen);
    }

    [Fact]
    public void VelaShaderBackground_AndBackgroundLayer_HavePerformanceFriendlyDefaults()
    {
        var shader = new VelaShaderBackground();
        Assert.Equal(30.0, shader.MaxFrameRate);
        Assert.True(shader.PauseWhenWindowInactive);
        Assert.False(shader.IsHitTestVisible);

        var layer = new VelaBackgroundLayer();
        Assert.Equal(Stretch.UniformToFill, layer.Stretch);
        Assert.Equal(0.0, layer.OverlayOpacity);
        Assert.False(layer.IsHitTestVisible);
    }

    [Fact]
    public void VelaLiquidGlassLens_ClampsSnapshotRegionToDeviceClipBounds()
    {
        var fullDeviceRect = new SKRect(40, 448, 360, 516);

        // Full window redraw: keeps complete control bounds
        var fullClip = new SKRectI(0, 0, 2000, 1400);
        Assert.Equal(new SKRectI(40, 448, 360, 516), VelaLiquidGlassLens.ComputeSnapshotRegion(fullDeviceRect, fullClip));

        // Partial dirty-rect redraw overlapping only the right endcap: never reads stale pixels left of clip.Left
        var partialClip = new SKRectI(326, 400, 700, 600);
        Assert.Equal(new SKRectI(326, 448, 360, 516), VelaLiquidGlassLens.ComputeSnapshotRegion(fullDeviceRect, partialClip));

        // Disjoint dirty-rect: returns empty
        var disjointClip = new SKRectI(400, 400, 700, 600);
        Assert.True(VelaLiquidGlassLens.ComputeSnapshotRegion(fullDeviceRect, disjointClip).IsEmpty);
    }
}
