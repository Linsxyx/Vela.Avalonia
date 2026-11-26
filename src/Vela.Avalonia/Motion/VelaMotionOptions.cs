namespace Vela.Avalonia.Motion;

/// <summary>
/// Global motion preferences shared across all Vela.Avalonia controls.
/// </summary>
public static class VelaMotionOptions
{
    private static bool _reducedMotion;

    /// <summary>
    /// When true, spring physics snap immediately or fall back to minimal crossfades.
    /// </summary>
    public static bool ReducedMotion
    {
        get => _reducedMotion;
        set
        {
            if (_reducedMotion == value) return;
            _reducedMotion = value;
            ReducedMotionChanged?.Invoke(null, EventArgs.Empty);
        }
    }

    public static event EventHandler? ReducedMotionChanged;
}
