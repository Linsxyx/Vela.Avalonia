using System;
using System.Runtime.CompilerServices;
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
    private static readonly ConditionalWeakTable<Border, RoundedBackdropClipBinder> s_clipBinders = new();

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

            var binder = s_clipBinders.GetValue(target, static b => new RoundedBackdropClipBinder(b));
            binder.Attach();
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

        if (s_clipBinders.TryGetValue(target, out var binder))
        {
            binder.Detach();
            s_clipBinders.Remove(target);
        }

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

    /// <summary>
    /// Synchronizes <see cref="Visual.Clip"/> to a rounded geometry matching <see cref="Border.CornerRadius"/>
    /// and <see cref="Visual.Bounds"/>. Avalonia 12's compositor clips <see cref="Visual.BackdropEffect"/>
    /// to <see cref="Visual.Clip"/> rather than <see cref="Border.CornerRadius"/>, so without an explicit
    /// rounded clip a blurred pill/circle leaks rectangular blur corners outside its curved border.
    /// </summary>
    private sealed class RoundedBackdropClipBinder
    {
        private readonly Border _border;
        private bool _attached;
        private double _lastWidth = double.NaN;
        private double _lastHeight = double.NaN;
        private CornerRadius _lastCornerRadius;

        public RoundedBackdropClipBinder(Border border)
        {
            _border = border;
        }

        public void Attach()
        {
            if (!_attached)
            {
                _attached = true;
                _border.PropertyChanged += OnBorderPropertyChanged;
            }

            Sync();
        }

        public void Detach()
        {
            if (_attached)
            {
                _attached = false;
                _border.PropertyChanged -= OnBorderPropertyChanged;
            }

            _lastWidth = double.NaN;
            _lastHeight = double.NaN;
            _lastCornerRadius = default;
            if (_border.Clip is not null)
                _border.Clip = null;
        }

        private void OnBorderPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == Visual.BoundsProperty || e.Property == Border.CornerRadiusProperty)
                Sync();
        }

        private void Sync()
        {
            var bounds = _border.Bounds;
            var cr = _border.CornerRadius;
            var w = bounds.Width;
            var h = bounds.Height;

            var isZeroRadius = cr.TopLeft <= 0.0 && cr.TopRight <= 0.0 && cr.BottomRight <= 0.0 && cr.BottomLeft <= 0.0;
            if (w <= 0.0 || h <= 0.0 || isZeroRadius)
            {
                if (_border.Clip is not null)
                    _border.Clip = null;
                _lastWidth = w;
                _lastHeight = h;
                _lastCornerRadius = cr;
                return;
            }

            if (Math.Abs(_lastWidth - w) < 0.01 &&
                Math.Abs(_lastHeight - h) < 0.01 &&
                _lastCornerRadius == cr &&
                _border.Clip is not null)
            {
                return;
            }

            _lastWidth = w;
            _lastHeight = h;
            _lastCornerRadius = cr;

            var maxR = Math.Min(w, h) * 0.5;
            if (cr.IsUniform)
            {
                var r = Math.Clamp(cr.TopLeft, 0.0, maxR);
                _border.Clip = new RectangleGeometry(new Rect(0, 0, w, h), r, r);
            }
            else
            {
                _border.Clip = BuildAsymmetricRoundedRect(w, h, cr);
            }
        }

        private static PathGeometry BuildAsymmetricRoundedRect(double w, double h, CornerRadius cr)
        {
            var tl = Math.Max(0.0, cr.TopLeft);
            var tr = Math.Max(0.0, cr.TopRight);
            var br = Math.Max(0.0, cr.BottomRight);
            var bl = Math.Max(0.0, cr.BottomLeft);

            // CSS border-radius proportional scaling to prevent adjacent corner overlap
            var scale = 1.0;
            if (tl + tr > w) scale = Math.Min(scale, w / (tl + tr));
            if (bl + br > w) scale = Math.Min(scale, w / (bl + br));
            if (tl + bl > h) scale = Math.Min(scale, h / (tl + bl));
            if (tr + br > h) scale = Math.Min(scale, h / (tr + br));

            tl *= scale;
            tr *= scale;
            br *= scale;
            bl *= scale;

            var segments = new PathSegments
            {
                new LineSegment { Point = new Point(w - tr, 0) }
            };
            if (tr > 0)
                segments.Add(new ArcSegment { Point = new Point(w, tr), Size = new Size(tr, tr), SweepDirection = SweepDirection.Clockwise });
            segments.Add(new LineSegment { Point = new Point(w, h - br) });
            if (br > 0)
                segments.Add(new ArcSegment { Point = new Point(w - br, h), Size = new Size(br, br), SweepDirection = SweepDirection.Clockwise });
            segments.Add(new LineSegment { Point = new Point(bl, h) });
            if (bl > 0)
                segments.Add(new ArcSegment { Point = new Point(0, h - bl), Size = new Size(bl, bl), SweepDirection = SweepDirection.Clockwise });
            segments.Add(new LineSegment { Point = new Point(0, tl) });
            if (tl > 0)
                segments.Add(new ArcSegment { Point = new Point(tl, 0), Size = new Size(tl, tl), SweepDirection = SweepDirection.Clockwise });

            return new PathGeometry
            {
                Figures = new PathFigures
                {
                    new PathFigure
                    {
                        StartPoint = new Point(tl, 0),
                        IsClosed = true,
                        IsFilled = true,
                        Segments = segments,
                    }
                }
            };
        }
    }
}
