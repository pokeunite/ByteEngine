namespace ByteEngine.Core;

public static class Time
{
    public static double DeltaTime { get; private set; }

    public static double TotalTime { get; private set; }

    public static double FixedDeltaTime { get; internal set; } = 1.0 / 60;

    internal static void Update(double deltaTime)
    {
        DeltaTime = deltaTime;
        TotalTime += deltaTime;
    }
}
