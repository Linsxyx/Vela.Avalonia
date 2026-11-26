using Avalonia;
using Avalonia.Controls;

namespace Vela.Avalonia.Motion;

/// <summary>
/// Drives a control's spring animation loop via <see cref="TopLevel.RequestAnimationFrame"/>.
/// Automatically goes completely idle when all springs reach rest.
/// </summary>
public sealed class SpringRunner
{
    private readonly Visual _owner;
    private readonly Func<double, bool> _onTick;
    private bool _scheduled;
    private TimeSpan? _lastFrame;

    public bool IsRunning => _scheduled;

    public SpringRunner(Visual owner, Func<double, bool> onTick)
    {
        _owner = owner;
        _onTick = onTick;
    }

    public void Wake()
    {
        if (_scheduled)
            return;

        var topLevel = TopLevel.GetTopLevel(_owner);
        if (topLevel is null)
        {
            _onTick(0.016);
            return;
        }

        _scheduled = true;
        _lastFrame = null;
        topLevel.RequestAnimationFrame(OnFrame);
    }

    public void Stop()
    {
        _scheduled = false;
        _lastFrame = null;
    }

    private void OnFrame(TimeSpan timestamp)
    {
        if (!_scheduled)
            return;

        var topLevel = TopLevel.GetTopLevel(_owner);
        if (topLevel is null)
        {
            Stop();
            return;
        }

        var dt = _lastFrame.HasValue
            ? (timestamp - _lastFrame.Value).TotalSeconds
            : 1.0 / 60.0;
        _lastFrame = timestamp;

        var keepRunning = _onTick(dt);
        if (keepRunning && _scheduled)
        {
            topLevel.RequestAnimationFrame(OnFrame);
        }
        else
        {
            Stop();
        }
    }
}
