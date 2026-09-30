using System.Numerics;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;

namespace ByteEngine.Core.Construction;

/// <summary>Fixed-capacity burst particles rendered as one dynamic mesh.</summary>
public sealed class ImpactParticles3D : Component
{
    private const int Capacity = 1024;
    private const int VerticesPerParticle = 5;
    private const int FloatsPerVertex = 8;
    private readonly Vector3[] _positions = new Vector3[Capacity];
    private readonly Vector3[] _velocities = new Vector3[Capacity];
    private readonly float[] _remaining = new float[Capacity];
    private readonly float[] _initialLife = new float[Capacity];
    private readonly float[] _vertices = new float[Capacity * VerticesPerParticle * FloatsPerVertex];
    private readonly Random _random = new(4711);
    private readonly Material _material = new()
    {
        BaseColor = new Vector4(1, .55f, .09f, .9f),
        Shading = MaterialShadingMode.Unlit,
        BlendMode = BlendMode3D.AlphaBlend
    };
    private Mesh? _mesh;
    private int _next;

    public int ActiveCount { get; private set; }

    public void Emit(Vector3 position, int count, float intensity = 1)
    {
        if (!float.IsFinite(intensity) || intensity <= 0) return;
        count = Math.Clamp(count, 0, Capacity);
        for (int i = 0; i < count; i++)
        {
            int slot = _next++ % Capacity;
            if (_remaining[slot] <= 0) ActiveCount++;
            _positions[slot] = position;
            float angle = (float)_random.NextDouble() * MathF.Tau;
            float speed = (.8f + (float)_random.NextDouble() * 3.5f) * intensity;
            _velocities[slot] = new Vector3(MathF.Cos(angle) * speed,
                (1.3f + (float)_random.NextDouble() * 3) * intensity,
                MathF.Sin(angle) * speed);
            _initialLife[slot] = _remaining[slot] =
                .3f + (float)_random.NextDouble() * .45f;
        }
    }

    protected override void OnUpdate() => Advance(
        Math.Clamp((float)ByteEngine.Core.Time.DeltaTime, 0, .1f));

    public void Advance(float elapsed)
    {
        if (!float.IsFinite(elapsed) || elapsed < 0)
            throw new ArgumentOutOfRangeException(nameof(elapsed));
        for (int i = 0; i < Capacity; i++)
        {
            if (_remaining[i] <= 0) continue;
            _remaining[i] -= elapsed;
            if (_remaining[i] <= 0)
            {
                _remaining[i] = 0;
                ActiveCount--;
                continue;
            }
            _velocities[i] += new Vector3(0, -9.81f, 0) * elapsed;
            _positions[i] += _velocities[i] * elapsed;
        }
    }

    protected override void OnRender(RenderContext context)
    {
        if (!context.Has3DCamera || ActiveCount == 0) return;
        _mesh ??= CreateMesh();
        Array.Clear(_vertices);
        for (int i = 0; i < Capacity; i++)
        {
            if (_remaining[i] <= 0) continue;
            Vector3 p = _positions[i];
            float size = .08f * _remaining[i] / _initialLife[i];
            int start = i * VerticesPerParticle * FloatsPerVertex;
            Write(start, p + new Vector3(-size, 0, -size));
            Write(start + 8, p + new Vector3(size, 0, -size));
            Write(start + 16, p + new Vector3(size, 0, size));
            Write(start + 24, p + new Vector3(-size, 0, size));
            Write(start + 32, p + new Vector3(0, size * 2, 0));
        }
        _mesh.UpdateVertices(_vertices, updateBounds: false);
        context.RenderWorld.Submit(_mesh, _material, Matrix4x4.Identity,
            frustumCulling: false, castShadows: false);
    }

    protected override void OnDestroy() => _mesh?.Dispose();

    private Mesh CreateMesh()
    {
        var indices = new uint[Capacity * 12];
        for (int slot = 0; slot < Capacity; slot++)
        {
            uint v = (uint)(slot * VerticesPerParticle);
            int at = slot * 12;
            indices[at] = v; indices[at + 1] = v + 1; indices[at + 2] = v + 4;
            indices[at + 3] = v + 1; indices[at + 4] = v + 2; indices[at + 5] = v + 4;
            indices[at + 6] = v + 2; indices[at + 7] = v + 3; indices[at + 8] = v + 4;
            indices[at + 9] = v + 3; indices[at + 10] = v; indices[at + 11] = v + 4;
        }
        return new Mesh(_vertices, indices, dynamicVertices: true);
    }

    private void Write(int at, Vector3 position)
    {
        _vertices[at] = position.X;
        _vertices[at + 1] = position.Y;
        _vertices[at + 2] = position.Z;
        _vertices[at + 4] = 1;
    }
}
