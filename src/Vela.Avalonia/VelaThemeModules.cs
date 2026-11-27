namespace Vela.Avalonia;

/// <summary>
/// Controls which theme resource modules are loaded by <see cref="VelaTheme"/>.
/// Foundation tokens, icons, primitives, scrollbars, and tooltips are always loaded.
/// </summary>
[Flags]
public enum VelaThemeModules
{
    /// <summary>Foundation only (tokens, icons, primitives, scrollbars, tooltips).</summary>
    None = 0,

    /// <summary>Core actions, basic inputs, badges, menus, popovers, progress, alerts, avatars, and feedback.</summary>
    Core = 1 << 0,

    /// <summary>Window chrome, navigation view, tabs, breadcrumbs, pagination, stepper, dock/floating bars, layout, and metric cards.</summary>
    Shell = 1 << 1,

    /// <summary>Advanced inputs and pickers (Select, ComboBox, SegmentedControl, Sliders, ScrubInput, RadioCards, TagInput, DatePicker, ColorPicker).</summary>
    Inputs = 1 << 2,

    /// <summary>Data views (ListBox, TreeView, DataGrid, TreeTable).</summary>
    DataViews = 1 << 3,

    /// <summary>Glass surfaces (LiquidGlass, GlassCard).</summary>
    Glass = 1 << 4,

    /// <summary>All built-in Vela.Avalonia modules (default for backward compatibility).</summary>
    All = Core | Shell | Inputs | DataViews | Glass,
}
