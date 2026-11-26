namespace Vela.Avalonia.Motion;

/// <summary>
/// Exact analytical 2nd-order damped harmonic oscillator: m*x'' + c*x' + k*(x - target) = 0.
/// Unconditionally stable across 60Hz/120Hz displays and preserves velocity when retargeted mid-flight.
/// </summary>
public sealed class SpringChannel
{
    public double Value { get; private set; }
    public double Target { get; private set; }
    public double Velocity { get; private set; }
    public SpringSpec Spec { get; private set; }
    public bool IsAnimating { get; private set; }

    public SpringChannel(double initial = 0.0, SpringSpec? spec = null)
    {
        Value = initial;
        Target = initial;
        Spec = spec ?? VelaMotionTokens.Springs.Snappy;
    }

    public void Jump(double value)
    {
        Value = value;
        Target = value;
        Velocity = 0.0;
        IsAnimating = false;
    }

    public void SetTarget(double target, SpringSpec? spec = null, double? initialVelocity = null)
    {
        if (spec.HasValue)
            Spec = spec.Value;

        Target = target;
        if (initialVelocity.HasValue)
            Velocity = initialVelocity.Value;

        if (VelaMotionOptions.ReducedMotion)
        {
            Jump(target);
            return;
        }

        if (Math.Abs(Value - Target) <= Spec.RestDelta && Math.Abs(Velocity) <= Spec.RestSpeed)
        {
            Value = Target;
            Velocity = 0.0;
            IsAnimating = false;
            return;
        }

        IsAnimating = true;
    }

    public bool Step(double dtSeconds)
    {
        if (!IsAnimating)
            return false;

        if (VelaMotionOptions.ReducedMotion)
        {
            Jump(Target);
            return false;
        }

        var dt = Math.Clamp(dtSeconds, 0.0, 0.064);
        if (dt <= 0.0)
            return true;

        var m = Math.Max(Spec.Mass, 1e-4);
        var k = Math.Max(Spec.Stiffness, 1e-4);
        var c = Math.Max(Spec.Damping, 0.0);

        var x0 = Value - Target;
        var v0 = Velocity;

        var omega0 = Math.Sqrt(k / m);
        var zeta = c / (2.0 * Math.Sqrt(k * m));

        double x;
        double v;

        if (zeta < 1.0 - 1e-6)
        {
            var omegaD = omega0 * Math.Sqrt(1.0 - zeta * zeta);
            var exp = Math.Exp(-zeta * omega0 * dt);
            var cos = Math.Cos(omegaD * dt);
            var sin = Math.Sin(omegaD * dt);
            var b = (v0 + zeta * omega0 * x0) / omegaD;

            x = exp * (x0 * cos + b * sin);
            v = exp * ((b * omegaD - zeta * omega0 * x0) * cos - (x0 * omegaD + zeta * omega0 * b) * sin);
        }
        else if (Math.Abs(zeta - 1.0) <= 1e-6)
        {
            var exp = Math.Exp(-omega0 * dt);
            var b = v0 + omega0 * x0;

            x = exp * (x0 + b * dt);
            v = exp * (v0 - omega0 * b * dt);
        }
        else
        {
            var gamma = omega0 * Math.Sqrt(zeta * zeta - 1.0);
            var r1 = -omega0 * zeta + gamma;
            var r2 = -omega0 * zeta - gamma;
            var c1 = (v0 - r2 * x0) / (r1 - r2);
            var c2 = x0 - c1;
            var e1 = Math.Exp(r1 * dt);
            var e2 = Math.Exp(r2 * dt);

            x = c1 * e1 + c2 * e2;
            v = c1 * r1 * e1 + c2 * r2 * e2;
        }

        if (Math.Abs(x) <= Spec.RestDelta && Math.Abs(v) <= Spec.RestSpeed)
        {
            Value = Target;
            Velocity = 0.0;
            IsAnimating = false;
            return false;
        }

        Value = Target + x;
        Velocity = v;
        return true;
    }
}
