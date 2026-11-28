using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Vela.Avalonia.Controls;

/// <summary>
/// Implemented by controls that own an Avalonia 12.2 native <see cref="RetainedBackdropEffectCache"/>
/// so inactive cached pages (e.g. inside <see cref="VelaPageDeck"/>) can release offscreen GPU
/// blur textures immediately when hidden and recreate them on demand when shown again.
/// </summary>
internal interface IVelaBackdropCacheOwner
{
    void SuspendBackdropCache();
    void ResumeBackdropCache();
}

internal static class VelaBackdropCacheHelper
{
    public static void ApplyBlur(Border? target, bool enabled, double radius)
    {
        if (target is null)
            return;

        if (enabled && radius > 0.1)
        {
            target.BackdropEffectCache ??= new RetainedBackdropEffectCache();
            if (target.BackdropEffect is BlurEffect existing)
                existing.Radius = radius;
            else
                target.BackdropEffect = new BlurEffect { Radius = radius };
        }
        else
        {
            Release(target);
        }
    }

    public static void Release(Border? target)
    {
        if (target is null)
            return;

        if (target.BackdropEffect is not null)
            target.BackdropEffect = null;
        if (target.BackdropEffectCache is not null)
            target.BackdropEffectCache = null;
    }

    public static void SuspendSubtree(Visual? root)
    {
        if (root is null)
            return;

        if (root is IVelaBackdropCacheOwner self)
            self.SuspendBackdropCache();

        foreach (var descendant in root.GetVisualDescendants())
        {
            if (descendant is IVelaBackdropCacheOwner owner)
                owner.SuspendBackdropCache();
        }
    }

    public static void ResumeSubtree(Visual? root)
    {
        if (root is null)
            return;

        if (root is IVelaBackdropCacheOwner self)
            self.ResumeBackdropCache();

        foreach (var descendant in root.GetVisualDescendants())
        {
            if (descendant is IVelaBackdropCacheOwner owner)
                owner.ResumeBackdropCache();
        }
    }
}
