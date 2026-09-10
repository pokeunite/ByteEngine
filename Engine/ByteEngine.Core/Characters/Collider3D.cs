using System.Numerics;
using ByteEngine.Core.Scene;
namespace ByteEngine.Core.Characters;

public abstract class Collider3D : Component
{
    public bool IsTrigger { get; set; }
    public Vector3 Center { get; set; }
    public abstract Vector3 Size { get; set; }
}

public sealed class BoxCollider3D : Collider3D
{
    public override Vector3 Size { get; set; } = Vector3.One;
}
