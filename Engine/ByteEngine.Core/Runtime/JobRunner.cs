using System.Threading.Tasks;

namespace ByteEngine.Core.Runtime;

/// <summary>
/// Small worker-thread helper used to parallelize CPU-bound preparation
/// work while keeping GL submission and final ordering on the owner thread.
/// Determinism rules:
/// - Each index is processed exactly once.
/// - Callers must preserve result ordering explicitly (for example by writing
///   to index-addressable buffers or collecting results in ascending index order).
/// - Gameplay-affecting work must not depend on execution order of individual
///   bodies; shared mutable state must be protected or avoided.
/// </summary>
public static class JobRunner
{
    public const int DefaultParallelThreshold = 64;

    public static void For(int count, Action<int> body)
    {
        if (count <= 0) return;
        if (count < DefaultParallelThreshold)
        {
            for (int i = 0; i < count; i++) body(i);
            return;
        }

        Parallel.For(0, count, body);
    }

    public static void For(int fromInclusive, int toExclusive, Action<int> body)
    {
        if (toExclusive <= fromInclusive) return;
        int count = toExclusive - fromInclusive;
        if (count < DefaultParallelThreshold)
        {
            for (int i = fromInclusive; i < toExclusive; i++) body(i);
            return;
        }

        Parallel.For(fromInclusive, toExclusive, body);
    }
}
