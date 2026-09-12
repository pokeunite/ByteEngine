using System.Numerics;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Scene;

namespace ByteEngine.Core.Physics;

public readonly record struct RaycastHit3D(
    GameObject GameObject,
    Collider3D Collider,
    Vector3 Point,
    Vector3 Normal,
    float Distance);
