using ByteEngine.Core.Characters;
using ByteEngine.Core.Scene;

namespace ByteEngine.Core.Physics;

/// <summary>
/// A collider currently overlapping a gameplay query shape.
/// </summary>
public readonly record struct OverlapHit3D(
    GameObject GameObject,
    Collider3D Collider);
