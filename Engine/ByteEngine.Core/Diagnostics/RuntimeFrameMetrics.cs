namespace ByteEngine.Core.Diagnostics;

/// <summary>CPU measurements for the last scene update, not process-wide or GPU time.</summary>
public readonly record struct RuntimeFrameMetrics(double UpdateMs, double PhysicsMs,
    double LateUpdateMs, long AllocatedBytes, int PhysicsTicks, int BroadPhaseCandidates);
