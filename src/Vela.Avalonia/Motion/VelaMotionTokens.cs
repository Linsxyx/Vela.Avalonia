using Avalonia.Animation.Easings;

namespace Vela.Avalonia.Motion;

/// <summary>
/// Physical parameters for a 2nd-order damped harmonic spring.
/// </summary>
public readonly record struct SpringSpec(
    double Stiffness,
    double Damping,
    double Mass = 1.0,
    double RestDelta = 0.001,
    double RestSpeed = 0.005)
{
    /// <summary>
    /// Converts perceptual (visualDuration, bounce) parameters into physical stiffness &amp; damping,
    /// preserving velocity across mid-flight retargeting.
    /// </summary>
    public static SpringSpec FromVisualDuration(
        double visualDurationSeconds,
        double bounce = 0.0,
        double restDelta = 0.001,
        double? restSpeed = null)
    {
        var root = (2.0 * Math.PI) / (visualDurationSeconds * 1.2);
        var stiffness = root * root;
        var damping = 2.0 * (1.0 - bounce) * root;
        return new SpringSpec(stiffness, damping, 1.0, restDelta, restSpeed ?? restDelta * 10.0);
    }
}

/// <summary>
/// Curated motion presets inspired by Arc UI's motion-tokens.
/// </summary>
public static class VelaMotionTokens
{
    public static class Durations
    {
        public static readonly TimeSpan Instant = TimeSpan.FromMilliseconds(120);
        public static readonly TimeSpan Fast = TimeSpan.FromMilliseconds(160);
        public static readonly TimeSpan Exit = TimeSpan.FromMilliseconds(180);
        public static readonly TimeSpan Standard = TimeSpan.FromMilliseconds(240);
        public static readonly TimeSpan Considered = TimeSpan.FromMilliseconds(480);
    }

    public static class Easings
    {
        public static readonly Easing Enter = new SplineEasing(0.16, 1.0, 0.3, 1.0);
        public static readonly Easing Exit = new SplineEasing(0.7, 0.0, 0.84, 0.0);
        public static readonly Easing Standard = new SplineEasing(0.22, 1.0, 0.36, 1.0);
        public static readonly Easing InOut = new SplineEasing(0.65, 0.0, 0.35, 1.0);
    }

    public static class Springs
    {
        public static readonly SpringSpec Responsive = new(Stiffness: 520, Damping: 44);
        public static readonly SpringSpec Gentle = new(Stiffness: 340, Damping: 34);

        /// <summary>Presses, toggles, thumbs, and small indicators. Critically damped (no overshoot).</summary>
        public static readonly SpringSpec Snappy = SpringSpec.FromVisualDuration(0.26, bounce: 0.0);

        /// <summary>Switch thumb glide. Critically damped.</summary>
        public static readonly SpringSpec SwitchGlide = SpringSpec.FromVisualDuration(0.30, bounce: 0.0, restDelta: 0.05);

        /// <summary>Panels, sidebar expand/collapse, page transitions. Critically damped.</summary>
        public static readonly SpringSpec Smooth = SpringSpec.FromVisualDuration(0.38, bounce: 0.0, restDelta: 0.05);

        /// <summary>Shape morphs, shared layout highlights, and width changes. No overshoot.</summary>
        public static readonly SpringSpec Morph = SpringSpec.FromVisualDuration(0.42, bounce: 0.0, restDelta: 0.05);

        /// <summary>Elastic slider rubber-band snap-back with lively bounce.</summary>
        public static readonly SpringSpec SnapBack = SpringSpec.FromVisualDuration(0.50, bounce: 0.42, restDelta: 0.05);

        /// <summary>Trailing tooltip bubble behind elastic slider thumb.</summary>
        public static readonly SpringSpec TrailingBubble = new(Stiffness: 420, Damping: 30, Mass: 0.7, RestDelta: 0.02, RestSpeed: 0.1);

        /// <summary>Donut chart reveal and dataset morphing.</summary>
        public static readonly SpringSpec ChartReveal = SpringSpec.FromVisualDuration(0.72, bounce: 0.0, restDelta: 0.0005, restSpeed: 0.001);
        public static readonly SpringSpec ChartSettle = SpringSpec.FromVisualDuration(0.50, bounce: 0.0, restDelta: 0.0005, restSpeed: 0.001);
        public static readonly SpringSpec ChartPop = SpringSpec.FromVisualDuration(0.26, bounce: 0.0, restDelta: 0.002, restSpeed: 0.004);
    }
}
