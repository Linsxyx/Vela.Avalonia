using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.XamlIl.Runtime;

namespace Vela.Avalonia;

internal sealed class VelaModuleServiceProvider : IServiceProvider, IAvaloniaXamlIlParentStackProvider
{
    private static readonly Lazy<VelaModuleServiceProvider> SharedFallbackLazy = new(CreateFallback);
    private readonly IEnumerable<object> _parents;

    public static VelaModuleServiceProvider Fallback => SharedFallbackLazy.Value;

    public VelaModuleServiceProvider(IResourceDictionary parentResources)
    {
        _parents = [parentResources];
    }

    public IEnumerable<object> Parents => _parents;

    public object? GetService(Type serviceType)
        => serviceType == typeof(IAvaloniaXamlIlParentStackProvider) ? this : null;

    private static VelaModuleServiceProvider CreateFallback()
    {
        // AOT-safe: VelaTheme loads VelaIcons + VelaDesignTokens via compiled XAML without ResourceInclude(Uri).
        var foundation = new VelaTheme();
        return new VelaModuleServiceProvider(foundation.Resources);
    }
}

/// <summary>Standalone AOT-safe resource module for Core Vela controls.</summary>
public sealed class VelaCoreModule : ResourceDictionary
{
    public VelaCoreModule(IServiceProvider? sp = null) => AvaloniaXamlLoader.Load(sp ?? VelaModuleServiceProvider.Fallback, this);
}

/// <summary>Standalone AOT-safe resource module for Shell &amp; Navigation Vela controls.</summary>
public sealed class VelaShellModule : ResourceDictionary
{
    public VelaShellModule(IServiceProvider? sp = null) => AvaloniaXamlLoader.Load(sp ?? VelaModuleServiceProvider.Fallback, this);
}

/// <summary>Standalone AOT-safe resource module for Advanced Inputs &amp; Pickers.</summary>
public sealed class VelaInputsModule : ResourceDictionary
{
    public VelaInputsModule(IServiceProvider? sp = null) => AvaloniaXamlLoader.Load(sp ?? VelaModuleServiceProvider.Fallback, this);
}

/// <summary>Standalone AOT-safe resource module for Data Views (ListBox, TreeView, DataGrid, TreeTable).</summary>
public sealed class VelaDataViewsModule : ResourceDictionary
{
    public VelaDataViewsModule(IServiceProvider? sp = null) => AvaloniaXamlLoader.Load(sp ?? VelaModuleServiceProvider.Fallback, this);
}

/// <summary>Standalone AOT-safe resource module for Glass surfaces (LiquidGlass, GlassCard).</summary>
public sealed class VelaGlassModule : ResourceDictionary
{
    public VelaGlassModule(IServiceProvider? sp = null) => AvaloniaXamlLoader.Load(sp ?? VelaModuleServiceProvider.Fallback, this);
}
