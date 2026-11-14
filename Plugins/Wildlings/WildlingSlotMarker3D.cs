using System.Numerics;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;

namespace Wildlings;

/// <summary>
/// Editable follow target for one party member.
///
/// Parent this object to the trainer and drag it in the scene view: the ring's
/// world position becomes that wildling's formation slot, so the authored local
/// offset is also the distance the creature keeps from the player. The ring is
/// drawn as a flat coloured band on the ground so the slot is visible while
/// authoring and while playtesting.
/// </summary>
public sealed class WildlingSlotMarker3D : Component
{
    /// <summary>Party slot this ring drives (1 = leader, 2, 3).</summary>
    public int SlotIndex { get; set; } = 1;

    /// <summary>Band colour, RGB 0..1. Also used for the emissive tint.</summary>
    public Vector3 RingColor { get; set; } = new(1f, 0f, 0f);

    /// <summary>Outer radius of the band in metres (the object's own scale multiplies this).</summary>
    public float RingRadius { get; set; } = .6f;

    /// <summary>Lift above the marker origin so the band does not z-fight the ground.</summary>
    public float GroundBias { get; set; } = .02f;

    public bool Visible { get; set; } = true;

    private static Mesh? _sharedRing;
    private Material? _material;

    protected override void OnRender(RenderContext context)
    {
        if (!Visible || !context.Has3DCamera)
        {
            return;
        }

        Mesh ring = _sharedRing ??= BuildRing();
        _material ??= new Material
        {
            Metallic = 0f,
            Roughness = 1f,
            EmissionEnabled = true,
            EmissionIntensity = 1.4f
        };

        Vector3 color = RingColor;
        _material.BaseColor = new Vector4(color, 1f);
        _material.EmissionColor = color;

        // Unit band -> radius, lifted off the ground, then placed by the marker
        // transform (which inherits the trainer, so the slot travels with the player).
        Matrix4x4 matrix =
            Matrix4x4.CreateScale(MathF.Max(.05f, RingRadius)) *
            Matrix4x4.CreateTranslation(0f, GroundBias, 0f) *
            Transform.RenderMatrix;

        context.RenderWorld.Submit(
            ring,
            _material,
            matrix,
            RenderQueue3D.Opaque,
            true,
            false,
            false);
    }

    /// <summary>Flat annulus in the XZ plane, eight floats per vertex (position, normal, uv).</summary>
    private static Mesh BuildRing()
    {
        const int segments = 64;
        const float outer = 1f;
        const float inner = .84f;

        var vertices = new List<float>(segments * 16);
        var indices = new List<uint>(segments * 6);

        for (int index = 0; index <= segments; index++)
        {
            float angle = MathF.Tau * index / segments;
            float sin = MathF.Sin(angle);
            float cos = MathF.Cos(angle);

            AddVertex(vertices, sin * outer, cos * outer);
            AddVertex(vertices, sin * inner, cos * inner);
        }

        for (int index = 0; index < segments; index++)
        {
            uint outerCurrent = (uint)(index * 2);
            uint innerCurrent = outerCurrent + 1;
            uint outerNext = outerCurrent + 2;
            uint innerNext = outerCurrent + 3;

            indices.Add(outerCurrent);
            indices.Add(innerCurrent);
            indices.Add(innerNext);
            indices.Add(outerCurrent);
            indices.Add(innerNext);
            indices.Add(outerNext);
        }

        return new Mesh(vertices.ToArray(), indices.ToArray());
    }

    private static void AddVertex(List<float> vertices, float x, float z)
    {
        // Flat band lying on the ground: normal points up.
        vertices.Add(x);
        vertices.Add(0f);
        vertices.Add(z);
        vertices.Add(0f);
        vertices.Add(1f);
        vertices.Add(0f);
        vertices.Add(x * .5f + .5f);
        vertices.Add(z * .5f + .5f);
    }
}
