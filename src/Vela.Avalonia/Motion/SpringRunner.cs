using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;

namespace Vela.Avalonia.Motion;

/// <summary>
/// Drives a control's spring animation loop via a coalesced per-<see cref="TopLevel"/>
/// <see cref="TopLevel.RequestAnimationFrame"/> pump.
/// All active springs in the same window share a single animation-frame callback with zero per-frame
/// delegate allocations, and the pump goes completely idle when all springs reach rest.
/// </summary>
public sealed class SpringRunner
{
    private static readonly ConditionalWeakTable<TopLevel, TopLevelSpringPump> Pumps = new();

    private readonly Visual _owner;
    private readonly Func<double, bool> _onTick;
    private TopLevelSpringPump? _pump;
    private bool _running;

    public bool IsRunning => _running;

    public SpringRunner(Visual owner, Func<double, bool> onTick)
    {
        _owner = owner;
        _onTick = onTick;
    }

    public void Wake()
    {
        if (_running)
            return;

        var topLevel = TopLevel.GetTopLevel(_owner);
        if (topLevel is null)
        {
            _onTick(0.016);
            return;
        }

        _running = true;
        _pump = Pumps.GetValue(topLevel, static tl => new TopLevelSpringPump(tl));
        _pump.Add(this);
    }

    public void Stop()
    {
        if (!_running)
            return;

        _running = false;
        _pump?.Remove(this);
        _pump = null;
    }

    private bool Step(double dt)
    {
        if (!_running)
            return false;

        if (TopLevel.GetTopLevel(_owner) is null)
        {
            _running = false;
            _pump = null;
            return false;
        }

        var keepRunning = _onTick(dt);
        if (!keepRunning)
        {
            _running = false;
            _pump = null;
        }

        return keepRunning && _running;
    }

    private sealed class TopLevelSpringPump
    {
        private readonly WeakReference<TopLevel> _topLevelRef;
        private readonly List<SpringRunner> _active = new(8);
        private readonly List<SpringRunner> _snapshot = new(8);
        private readonly Action<TimeSpan> _frameCallback;
        private bool _scheduled;
        private TimeSpan? _lastFrame;

        public TopLevelSpringPump(TopLevel topLevel)
        {
            _topLevelRef = new WeakReference<TopLevel>(topLevel);
            _frameCallback = OnFrame;
        }

        public void Add(SpringRunner runner)
        {
            if (!_active.Contains(runner))
                _active.Add(runner);

            ScheduleIfNeeded();
        }

        public void Remove(SpringRunner runner)
        {
            _active.Remove(runner);
            if (_active.Count == 0)
            {
                _scheduled = false;
                _lastFrame = null;
            }
        }

        private void ScheduleIfNeeded()
        {
            if (_scheduled || _active.Count == 0)
                return;

            if (!_topLevelRef.TryGetTarget(out var topLevel))
            {
                _active.Clear();
                return;
            }

            _scheduled = true;
            _lastFrame = null;
            topLevel.RequestAnimationFrame(_frameCallback);
        }

        private void OnFrame(TimeSpan timestamp)
        {
            if (!_scheduled)
                return;

            if (!_topLevelRef.TryGetTarget(out var topLevel) || _active.Count == 0)
            {
                _scheduled = false;
                _lastFrame = null;
                _active.Clear();
                return;
            }

            var dt = _lastFrame.HasValue
                ? Math.Clamp((timestamp - _lastFrame.Value).TotalSeconds, 0.001, 0.05)
                : 1.0 / 60.0;
            _lastFrame = timestamp;

            _snapshot.Clear();
            _snapshot.AddRange(_active);

            for (var i = 0; i < _snapshot.Count; i++)
            {
                var runner = _snapshot[i];
                if (!runner.Step(dt))
                    _active.Remove(runner);
            }

            _snapshot.Clear();

            if (_active.Count > 0)
            {
                topLevel.RequestAnimationFrame(_frameCallback);
            }
            else
            {
                _scheduled = false;
                _lastFrame = null;
            }
        }
    }
}
