namespace Vela.Avalonia.Tokens;

/// <summary>
/// Unified surface material mode supported across Vela overlay controls (Dialog, Drawer, Popover, CommandPalette).
/// </summary>
public enum VelaSurfaceMaterial
{
    /// <summary>Clean opaque surface (highest legibility &amp; performance, default).</summary>
    Solid,

    /// <summary>Translucent frosted surface backed by Avalonia 12.2 native GPU BackdropEffect blur.</summary>
    Frosted,

    /// <summary>Full optical Liquid Glass surface with SkSL SDF bevel refraction, dispersion, and specular rim.</summary>
    LiquidGlass,

    /// <summary>
    /// SukiUI-inspired deep frosted glass card with high-radius retained GPU blur,
    /// high-contrast milky/cool-slate tint veil, and aspect-compensated diagonal luminous rim border.
    /// </summary>
    GlassCard,
}
