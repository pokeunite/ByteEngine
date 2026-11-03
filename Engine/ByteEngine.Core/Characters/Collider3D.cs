using System.Numerics;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Classification;

namespace ByteEngine.Core.Characters;

public abstract class Collider3D : Component
{
    public bool IsTrigger { get; set; }
    private bool _queryOnly;
    [System.ComponentModel.Description("Participates in raycasts and shape queries, but produces no physical contacts or trigger events. Useful for damage hitboxes.")]
    public bool QueryOnly {get=>_queryOnly;set{if(_queryOnly==value)return;_queryOnly=value;AttachedGameObject?.Scene?.InvalidateQueryBounds();}}
    private Vector3 _center;
    public Vector3 Center {get=>_center;set{if(_center==value)return;_center=value;AttachedGameObject?.Scene?.InvalidateQueryBounds();}}
    public bool UseProjectMatrix { get; set; } = true;
    public LayerMask CollisionMask { get; set; } = LayerMask.All;
    public abstract Vector3 Size { get; set; }
}

public sealed class BoxCollider3D : Collider3D
{
    private Vector3 _size=Vector3.One;
    public override Vector3 Size {get=>_size;set{if(_size==value)return;_size=value;AttachedGameObject?.Scene?.InvalidateQueryBounds();}}
}

public sealed class CapsuleCollider3D : Collider3D
{
    private float _radius = .5f;
    private float _height = 2f;

    public CapsuleCollider3D()
    {
        /*
         * Character roots use a feet/pivot origin. A default two metre capsule
         * must therefore sit from Y=0..2 rather than -1..1, otherwise adding a
         * fresh capsule can immediately place half of it inside the floor.
         */
        Center =
            new Vector3(
                0f,
                1f,
                0f);
    }

    public Vector3 VisualBounds { get; set; }
    public string AutoFitSource { get; set; } = string.Empty;

    public float Radius
    {
        get => _radius;
        set {float next=Math.Max(value,.001f);if(_radius==next)return;_radius=next;AttachedGameObject?.Scene?.InvalidateQueryBounds();}
    }

    public float Height
    {
        get => _height;
        set {float next=Math.Max(value,Radius*2f);if(_height==next)return;_height=next;AttachedGameObject?.Scene?.InvalidateQueryBounds();}
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
