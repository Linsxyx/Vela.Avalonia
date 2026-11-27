using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Vela.Avalonia;

/// <summary>Standalone AOT-safe resource module for Core Vela controls.</summary>
public sealed class VelaCoreModule : ResourceDictionary
{
    public VelaCoreModule(IServiceProvider? sp = null) => AvaloniaXamlLoader.Load(sp, this);
}

/// <summary>Standalone AOT-safe resource module for Shell &amp; Navigation Vela controls.</summary>
public sealed class VelaShellModule : ResourceDictionary
{
    public VelaShellModule(IServiceProvider? sp = null) => AvaloniaXamlLoader.Load(sp, this);
}

/// <summary>Standalone AOT-safe resource module for Advanced Inputs &amp; Pickers.</summary>
public sealed class VelaInputsModule : ResourceDictionary
{
    public VelaInputsModule(IServiceProvider? sp = null) => AvaloniaXamlLoader.Load(sp, this);
}

/// <summary>Standalone AOT-safe resource module for Data Views (ListBox, TreeView, DataGrid, TreeTable).</summary>
public sealed class VelaDataViewsModule : ResourceDictionary
{
    public VelaDataViewsModule(IServiceProvider? sp = null) => AvaloniaXamlLoader.Load(sp, this);
}

/// <summary>Standalone AOT-safe resource module for Glass surfaces (LiquidGlass, GlassCard).</summary>
public sealed class VelaGlassModule : ResourceDictionary
{
    public VelaGlassModule(IServiceProvider? sp = null) => AvaloniaXamlLoader.Load(sp, this);
}
