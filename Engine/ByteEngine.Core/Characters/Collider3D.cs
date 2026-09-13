using System.Numerics;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Classification;
namespace ByteEngine.Core.Characters;

public abstract class Collider3D : Component
{
    public bool IsTrigger { get; set; }
    public Vector3 Center { get; set; }
    public bool UseProjectMatrix { get; set; } = true;
    public LayerMask CollisionMask { get; set; } = LayerMask.All;
    public abstract Vector3 Size { get; set; }
}

public sealed class BoxCollider3D : Collider3D
{
    public override Vector3 Size { get; set; } = Vector3.One;
}

public sealed class CapsuleCollider3D : Collider3D
{
    private float _radius = .5f;
    private float _height = 2f;

    public Vector3 VisualBounds { get; set; }
    public string AutoFitSource { get; set; } = string.Empty;

    public float Radius
    {
        get => _radius;
        set => _radius = Math.Max(value, .001f);
    }

    public float Height
    {
        get => _height;
        set => _height = Math.Max(value, Radius * 2f);
    }

    public override Vector3 Size
    {
        get => new(Radius * 2f, Height, Radius * 2f);
        set
        {
            Radius = Math.Max(value.X, value.Z) * .5f;
            Height = value.Y;
        }
    }
}
