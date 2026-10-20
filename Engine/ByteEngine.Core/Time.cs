namespace ByteEngine.Core;

public static class Time
{
    public static double DeltaTime { get; private set; }

    public static double TotalTime { get; private set; }

    public static double FixedDeltaTime { get; internal set; } = 1.0 / 60;

    /// <summary>Host frame time, unaffected by a scene's simulation time scale.</summary>
    public static double UnscaledDeltaTime { get; private set; }

    internal static DeltaScope EnterSceneScale(float scale)
    {
        double prior = DeltaTime, fixedPrior = FixedDeltaTime;
        DeltaTime = UnscaledDeltaTime * Math.Clamp(float.IsFinite(scale) ? scale : 1f, 0f, 4f);
        return new DeltaScope(prior, fixedPrior);
    }

    internal readonly struct DeltaScope(double prior, double fixedPrior) : IDisposable
    {
        public void Dispose() { DeltaTime = prior; FixedDeltaTime = fixedPrior; }
    }
    internal static DeltaScope EnterObjectScale(float scale)
    {
        var scope=new DeltaScope(DeltaTime,FixedDeltaTime);DeltaTime*=scale;FixedDeltaTime*=scale;return scope;
    }

    internal static void Update(double deltaTime)
    {
        DeltaTime = deltaTime;
        UnscaledDeltaTime = deltaTime;
        TotalTime += deltaTime;
    }
}
