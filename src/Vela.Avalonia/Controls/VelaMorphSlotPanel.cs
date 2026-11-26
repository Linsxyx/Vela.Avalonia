using Avalonia;
using Avalonia.Controls;
using Vela.Avalonia.Motion;

namespace Vela.Avalonia.Controls;

/// <summary>
/// Measures its child at unconstrained natural width while smoothly springing its own
/// reported layout width on <see cref="VelaMotionTokens.Springs.Morph"/>.
/// </summary>
public sealed class VelaMorphSlotPanel : Panel
{
    private readonly SpringChannel _widthSpring = new(0.0, VelaMotionTokens.Springs.Morph);
    private readonly SpringRunner _runner;
    private bool _hasMeasured;
    private double _naturalWidth;

    public VelaMorphSlotPanel()
    {
        _runner = new SpringRunner(this, OnTick);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _runner.Stop();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        double maxW = 0;
        double maxH = 0;

        foreach (var child in Children)
        {
            child.Measure(new Size(double.PositiveInfinity, availableSize.Height));
            maxW = Math.Max(maxW, child.DesiredSize.Width);
            maxH = Math.Max(maxH, child.DesiredSize.Height);
        }

        if (maxW > 0)
        {
            if (!_hasMeasured || VelaMotionOptions.ReducedMotion)
            {
                _hasMeasured = true;
                _naturalWidth = maxW;
                _widthSpring.Jump(maxW);
            }
            else if (Math.Abs(maxW - _naturalWidth) > 0.5)
            {
                _naturalWidth = maxW;
                _widthSpring.SetTarget(maxW, VelaMotionTokens.Springs.Morph);
                _runner.Wake();
            }
        }

        var reportedW = _hasMeasured ? Math.Max(0, _widthSpring.Value) : maxW;
        return new Size(Math.Min(reportedW, availableSize.Width), maxH);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var child in Children)
        {
            var cw = child.DesiredSize.Width;
            var ch = child.DesiredSize.Height;
            var x = (finalSize.Width - cw) / 2.0;
            var y = (finalSize.Height - ch) / 2.0;
            child.Arrange(new Rect(x, y, cw, ch));
        }
        return finalSize;
    }

    private bool OnTick(double dt)
    {
        var active = _widthSpring.Step(dt);
        InvalidateMeasure();
        return active;
    }
}
