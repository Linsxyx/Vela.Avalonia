using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Styling;
using Vela.Avalonia.Controls;
using Vela.Avalonia.Tokens;
using Xunit;

namespace Vela.Avalonia.Tests;

public class VelaPerformanceAndLeakTests
{
    private sealed record RowModel(string Name, string Role);

    [Fact]
    public void VelaObservableList_ReplaceAll_FiresSingleResetNotification()
    {
        var list = new VelaObservableList<int>();
        var events = new List<NotifyCollectionChangedAction>();
        list.CollectionChanged += (_, e) => events.Add(e.Action);

        list.ReplaceAll(Enumerable.Range(0, 1000));

        Assert.Equal(1000, list.Count);
        Assert.Single(events);
        Assert.Equal(NotifyCollectionChangedAction.Reset, events[0]);
    }

    [Fact]
    public void VelaDataGridRowControl_ReusesCellHostsAndControls_OnDataContextRecycle()
    {
        var grid = new VelaDataGrid();
        var col1 = new VelaDataGridTextColumn { Header = "Name", ValueSelector = x => ((RowModel)x).Name };
        var col2 = new VelaDataGridTextColumn { Header = "Role", ValueSelector = x => ((RowModel)x).Role };
        grid.Columns.Add(col1);
        grid.Columns.Add(col2);
        grid._visibleColumns.Clear();
        grid._visibleColumns.Add(col1);
        grid._visibleColumns.Add(col2);

        var rowCtrl = new VelaDataGridRowControl();
        var state1 = new VelaDataGridRowState(grid, new RowModel("Alice", "Engineer"), 0, isSelected: false);
        rowCtrl.DataContext = state1;

        var panel = Assert.IsType<StackPanel>(rowCtrl.Child);
        Assert.Equal(2, panel.Children.Count);
        var host0Before = Assert.IsType<Border>(panel.Children[0]);
        var host1Before = Assert.IsType<Border>(panel.Children[1]);
        var text0Before = Assert.IsType<TextBlock>(host0Before.Child);
        var text1Before = Assert.IsType<TextBlock>(host1Before.Child);
        Assert.Equal("Alice", text0Before.Text);
        Assert.Equal("Engineer", text1Before.Text);

        // Simulate virtualized scroll recycling to a completely different item
        var state2 = new VelaDataGridRowState(grid, new RowModel("Bob", "Designer"), 1, isSelected: true);
        rowCtrl.DataContext = state2;

        Assert.Same(host0Before, panel.Children[0]);
        Assert.Same(host1Before, panel.Children[1]);
        Assert.Same(text0Before, host0Before.Child);
        Assert.Same(text1Before, host1Before.Child);
        Assert.Equal("Bob", text0Before.Text);
        Assert.Equal("Designer", text1Before.Text);
        Assert.Contains(":selected", rowCtrl.Classes);
    }

    [Fact]
    public void VelaTreeTableRowControl_ReusesCellHostsAndControls_OnDataContextRecycle()
    {
        var tree = new VelaTreeTable();
        var col1 = new VelaDataGridTextColumn { Header = "Name", ValueSelector = x => ((RowModel)x).Name };
        var col2 = new VelaDataGridTextColumn { Header = "Role", ValueSelector = x => ((RowModel)x).Role };
        tree.Columns.Add(col1);
        tree.Columns.Add(col2);
        tree._visibleColumns.Clear();
        tree._visibleColumns.Add(col1);
        tree._visibleColumns.Add(col2);

        var rowCtrl = new VelaTreeTableRowControl();
        var state1 = new VelaTreeTableRowState(tree, new RowModel("Root", "Dir"), null, 0, 0, true, true, false, false, null);
        rowCtrl.DataContext = state1;

        var panel = Assert.IsType<StackPanel>(rowCtrl.Child);
        Assert.Equal(2, panel.Children.Count);
        var treeHostBefore = Assert.IsType<Border>(panel.Children[0]);
        var roleHostBefore = Assert.IsType<Border>(panel.Children[1]);
        var treeGridBefore = Assert.IsType<Grid>(treeHostBefore.Child);
        var roleTextBefore = Assert.IsType<TextBlock>(roleHostBefore.Child);
        Assert.Equal("Dir", roleTextBefore.Text);

        var state2 = new VelaTreeTableRowState(tree, new RowModel("Child", "File"), state1.Item, 1, 1, false, false, true, true, null);
        rowCtrl.DataContext = state2;

        Assert.Same(treeHostBefore, panel.Children[0]);
        Assert.Same(roleHostBefore, panel.Children[1]);
        Assert.Same(treeGridBefore, treeHostBefore.Child);
        Assert.Same(roleTextBefore, roleHostBefore.Child);
        Assert.Equal("File", roleTextBefore.Text);
    }

    [Fact]
    public void VelaTheme_ResolvesControlThemesAndAccentSwitching_AcrossAllModules()
    {
        var theme = new VelaTheme();
        theme.BeginInit();
        theme.Accent = VelaAccent.Violet;
        theme.Modules = VelaThemeModules.All;
        theme.EndInit();

        Assert.Equal(VelaThemeModules.All, theme.LoadedModules);
        Assert.True(theme.Resources.TryGetResource(typeof(VelaBadge), ThemeVariant.Light, out var badgeTheme));
        Assert.IsType<ControlTheme>(badgeTheme);
        Assert.True(theme.Resources.TryGetResource(typeof(VelaInput), ThemeVariant.Light, out var inputTheme));
        Assert.IsType<ControlTheme>(inputTheme);
        Assert.True(theme.Resources.TryGetResource(typeof(VelaGlassCard), ThemeVariant.Light, out var glassTheme));
        Assert.IsType<ControlTheme>(glassTheme);

        // Switching accent should update in-place without throwing
        theme.Accent = VelaAccent.Coral;
        Assert.True(theme.Resources.TryGetResource("VelaAccentColor", ThemeVariant.Light, out var accentColor));
        Assert.Equal(VelaAccentPalettes.Get(VelaAccent.Coral, isDark: false).Accent, accentColor);
    }

    [Fact]
    public void VelaShaderEffect_SourceCache_IsBoundedByMaxEntries()
    {
        for (var i = 0; i < VelaShaderEffect.MaxSourceCacheEntries + 12; i++)
        {
            var src = $"half4 main(float2 fragCoord) {{ return half4({i % 255 / 255.0:0.###}, 0.2, 0.4, 1.0); }}";
            var ok = VelaShaderEffect.TryFromString(src, out var effect, out var error);
            Assert.True(ok, error);
            Assert.NotNull(effect);
        }

        var cacheField = typeof(VelaShaderEffect).GetField(
            "CacheBySource",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(cacheField);
        var dict = Assert.IsAssignableFrom<System.Collections.IDictionary>(cacheField!.GetValue(null));
        Assert.True(dict.Count <= VelaShaderEffect.MaxSourceCacheEntries);
    }
}
