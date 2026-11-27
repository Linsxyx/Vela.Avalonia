using Avalonia;
using Avalonia.Data;
using Avalonia.Markup.Xaml.MarkupExtensions;

namespace Vela.Avalonia.Controls;

/// <summary>
/// Creates leak-free dynamic resource bindings that automatically attach/detach with the target
/// control's logical tree (equivalent to XAML <c>{DynamicResource}</c>), avoiding strong-reference
/// retention on long-lived parent hosts.
/// </summary>
internal static class VelaResourceBinding
{
    public static DynamicResourceExtension Create(string resourceKey) => new(resourceKey);

    public static IDisposable Bind(AvaloniaObject target, AvaloniaProperty property, string resourceKey)
        => target.Bind(property, new DynamicResourceExtension(resourceKey));
}
