using System.Diagnostics;
using System.Numerics;
using ByteEngine.Core.Construction;

namespace ByteEngine.Sandbox.Goblin;

internal static class GoblinBenchmark
{
    public static void Run()
    {
        var green = new SwarmFlowField(48, 32, 1, new Vector2(-24, -16));
        var red = new SwarmFlowField(48, 32, 1, new Vector2(-24, -16));
        green.Rebuild(44, 16);
        red.Rebuild(3, 16);
        var horde = new SwarmHorde(600);
        for (int i = 0; i < 250; i++)
        {
            float z = -12 + i * .096f;
            horde.Spawn(new Vector2(-18 - i % 3, z), 0);
            horde.Spawn(new Vector2(18 + i % 3, z), 1);
        }
        for (int i = 0; i < 60; i++) Step();
        GC.Collect();
        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var timer = Stopwatch.StartNew();
        const int frames = 600;
        for (int i = 0; i < frames; i++) Step();
        timer.Stop();
        long bytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        Console.WriteLine($"500-unit swarm, {frames} simulation frames: " +
            $"{timer.Elapsed.TotalMilliseconds / frames:0.###} ms/frame, " +
            $"{bytes / (double)frames:0.#} B/frame allocated, {horde.Count} active.");
        Console.WriteLine("CPU simulation only: excludes BEPU vehicle, OpenGL rendering, drivers, and VSync.");
        return;

        void Step()
        {
            horde.Step(green, red, 1.55f, 1f / 60f);
            horde.ResolveClashes(.38f, 24);
            horde.Squish(new Vector2(-2, 0), new Vector2(2, 0),
                1.15f, 5, 2, 1);
            for (int i = 0; i < 4 && horde.Count < 500; i++)
            {
                float z = -12 + i * 6;
                horde.Spawn(new Vector2(-18, z), 0);
                if (horde.Count < 500) horde.Spawn(new Vector2(18, z), 1);
            }
        }
    }
}
