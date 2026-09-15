using System.Numerics;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Scene;

namespace ByteEngine.Core.Physics;

/// <summary>
/// Contact as seen by one GameObject. Normal points away from Other toward Self,
/// making it directly useful for grounding and response logic.
/// </summary>
public readonly record struct PhysicsContact3D(
    GameObject Self,
    Collider3D SelfCollider,
    GameObject Other,
    Collider3D OtherCollider,
    Vector3 Point,
    Vector3 Normal,
    float Penetration,
    bool IsTrigger);

/// <summary>
/// World-facing contact pair. Normal points from A toward B.
/// </summary>
public readonly record struct PhysicsContactPair3D(
    GameObject A,
    Collider3D ColliderA,
    GameObject B,
    Collider3D ColliderB,
    Vector3 Point,
    Vector3 Normal,
    float Penetration,
    bool IsTrigger);
