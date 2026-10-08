namespace ByteEngine.Core.Runtime;

/// <summary>Bounded accumulator shared by native and portable scene simulation.</summary>
public sealed class FixedStepClock
{
    private double _remainder;
    public double StepSeconds { get; set; } = 1.0 / 60;
    public int MaximumCatchUpSteps { get; set; } = 8;
    public double InterpolationAlpha { get; private set; }
    public double DroppedSeconds { get; private set; }

    public int Advance(double seconds, Action<double> tick)
    {
        ArgumentNullException.ThrowIfNull(tick);
        if (!double.IsFinite(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
        double step = double.IsFinite(StepSeconds) ? Math.Clamp(StepSeconds, .001, .1) : 1.0 / 60;
        int maximum = Math.Clamp(MaximumCatchUpSteps, 1, 64);
        _remainder += seconds;
        int count = 0;
        while (_remainder + 1e-10 >= step && count < maximum)
        {
            tick(step);
            _remainder = Math.Max(0, _remainder - step);
            count++;
        }
        if (_remainder >= step)
        {
            double dropped = Math.Floor(_remainder / step) * step;
            DroppedSeconds += dropped;
            _remainder -= dropped;
        }
        InterpolationAlpha = Math.Clamp(_remainder / step, 0, 1);
        return count;
    }

    public void Reset() { _remainder = 0; InterpolationAlpha = 0; DroppedSeconds = 0; }
}
