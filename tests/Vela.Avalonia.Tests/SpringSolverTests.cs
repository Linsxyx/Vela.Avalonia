using Vela.Avalonia.Motion;
using Xunit;

namespace Vela.Avalonia.Tests;

public sealed class SpringSolverTests
{
    [Fact]
    public void CriticallyDampedSpring_ConvergesWithoutOvershoot()
    {
        var spec = SpringSpec.FromVisualDuration(0.30, bounce: 0.0);
        var spring = new SpringChannel(0.0, spec);
        spring.SetTarget(100.0);

        Assert.True(spring.IsAnimating);

        var maxSeen = 0.0;
        for (var i = 0; i < 300 && spring.IsAnimating; i++)
        {
            spring.Step(1.0 / 60.0);
            if (spring.Value > maxSeen)
                maxSeen = spring.Value;
        }

        Assert.False(spring.IsAnimating);
        Assert.Equal(100.0, spring.Value);
        Assert.Equal(0.0, spring.Velocity);
        Assert.True(maxSeen <= 100.01, $"Critically damped spring should not overshoot (saw {maxSeen:F4}).");
    }

    [Fact]
    public void UnderdampedSpring_OvershootsAndConverges()
    {
        var spec = SpringSpec.FromVisualDuration(0.45, bounce: 0.45);
        var spring = new SpringChannel(0.0, spec);
        spring.SetTarget(100.0);

        var maxSeen = 0.0;
        for (var i = 0; i < 400 && spring.IsAnimating; i++)
        {
            spring.Step(1.0 / 60.0);
            if (spring.Value > maxSeen)
                maxSeen = spring.Value;
        }

        Assert.False(spring.IsAnimating);
        Assert.Equal(100.0, spring.Value);
        Assert.True(maxSeen > 105.0, $"Underdamped spring with bounce=0.45 should overshoot (max={maxSeen:F2}).");
    }

    [Fact]
    public void OverdampedSpring_ConvergesMonotonically()
    {
        var spec = new SpringSpec(Stiffness: 180, Damping: 40, Mass: 1.0);
        var spring = new SpringChannel(0.0, spec);
        spring.SetTarget(50.0);

        var prev = 0.0;
        for (var i = 0; i < 600 && spring.IsAnimating; i++)
        {
            spring.Step(1.0 / 60.0);
            Assert.True(spring.Value >= prev - 1e-6);
            prev = spring.Value;
        }

        Assert.False(spring.IsAnimating);
        Assert.Equal(50.0, spring.Value);
    }

    [Fact]
    public void MidFlightRetarget_PreservesVelocityContinuity()
    {
        var spring = new SpringChannel(0.0, VelaMotionTokens.Springs.Snappy);
        spring.SetTarget(100.0);

        // Advance ~6 frames to build positive velocity
        for (var i = 0; i < 6; i++)
            spring.Step(1.0 / 60.0);

        var velocityBeforeRetarget = spring.Velocity;
        Assert.True(velocityBeforeRetarget > 10.0);

        // Retarget back to 0 mid-flight without resetting velocity
        spring.SetTarget(0.0);
        Assert.Equal(velocityBeforeRetarget, spring.Velocity);

        // First tiny step should carry momentum slightly forward before reversing
        var valueAtRetarget = spring.Value;
        spring.Step(1.0 / 240.0);
        Assert.True(spring.Value > valueAtRetarget);

        // Eventually converges cleanly to 0
        for (var i = 0; i < 400 && spring.IsAnimating; i++)
            spring.Step(1.0 / 60.0);

        Assert.False(spring.IsAnimating);
        Assert.Equal(0.0, spring.Value);
    }

    [Fact]
    public void ReducedMotion_SnapsImmediately()
    {
        var original = VelaMotionOptions.ReducedMotion;
        try
        {
            VelaMotionOptions.ReducedMotion = true;
            var spring = new SpringChannel(0.0, VelaMotionTokens.Springs.Smooth);
            spring.SetTarget(250.0);

            Assert.False(spring.IsAnimating);
            Assert.Equal(250.0, spring.Value);
            Assert.Equal(0.0, spring.Velocity);
        }
        finally
        {
            VelaMotionOptions.ReducedMotion = original;
        }
    }
}
