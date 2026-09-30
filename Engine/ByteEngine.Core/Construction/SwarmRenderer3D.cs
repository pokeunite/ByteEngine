using System.Numerics;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;

namespace ByteEngine.Core.Construction;

/// <summary>Two dynamic mesh submissions for the whole crowd, not 500 scene objects.</summary>
public sealed class SwarmRenderer3D : Component
{
    private const int VerticesPerUnit = 5;
    private const int FloatsPerVertex = 8;
    private readonly Material[] _materials =
    {
        new() { BaseColor = new Vector4(.15f, .8f, .25f, 1), Shading = MaterialShadingMode.Unlit },
        new() { BaseColor = new Vector4(.9f, .18f, .15f, 1), Shading = MaterialShadingMode.Unlit }
    };
    private readonly Mesh?[] _meshes = new Mesh?[2];
    private readonly float[][] _vertices = new float[2][];
    private SwarmHorde? _horde;
    public Vector4 Team0Color { get; set; } = new(.15f, .8f, .25f, 1);
    public Vector4 Team1Color { get; set; } = new(.9f, .18f, .15f, 1);
    public float UnitRadius { get; set; } = .16f;
    public float UnitHeight { get; set; } = .42f;

    public SwarmHorde? Horde
    {
        get => _horde;
        set
        {
            if (ReferenceEquals(_horde, value)) return;
            ReleaseMeshes();
            _horde = value;
        }
    }

    protected override void OnRender(RenderContext context)
    {
        if (!context.Has3DCamera || _horde == null) return;
        EnsureMeshes(_horde.Capacity);
        Array.Clear(_vertices[0]);
        Array.Clear(_vertices[1]);
        for (int slot = 0; slot < _horde.Capacity; slot++)
        {
            if (!_horde.TryGetSlot(slot, out Vector2 position, out byte team)) continue;
            int group = team == 0 ? 0 : 1;
            WriteUnit(_vertices[group], slot, position,
                Math.Max(.01f, UnitRadius), Math.Max(.02f, UnitHeight));
        }
        for (int group = 0; group < 2; group++)
        {
            _materials[group].BaseColor = group == 0 ? Team0Color : Team1Color;
            _meshes[group]!.UpdateVertices(_vertices[group], updateBounds: false);
            context.RenderWorld.Submit(_meshes[group]!, _materials[group],
                Matrix4x4.Identity, frustumCulling: false, castShadows: false);
        }
    }

    protected override void OnDestroy() => ReleaseMeshes();

    private void EnsureMeshes(int capacity)
    {
        if (_meshes[0] != null && _vertices[0].Length == capacity * VerticesPerUnit * FloatsPerVertex)
            return;
        ReleaseMeshes();
        var indices = new uint[capacity * 12];
        for (int slot = 0; slot < capacity; slot++)
        {
            uint v = (uint)(slot * VerticesPerUnit);
            int i = slot * 12;
            indices[i] = v; indices[i + 1] = v + 1; indices[i + 2] = v + 4;
            indices[i + 3] = v + 1; indices[i + 4] = v + 2; indices[i + 5] = v + 4;
            indices[i + 6] = v + 2; indices[i + 7] = v + 3; indices[i + 8] = v + 4;
            indices[i + 9] = v + 3; indices[i + 10] = v; indices[i + 11] = v + 4;
        }
        for (int group = 0; group < 2; group++)
        {
            _vertices[group] = new float[capacity * VerticesPerUnit * FloatsPerVertex];
            _meshes[group] = new Mesh(_vertices[group], indices, dynamicVertices: true);
        }
    }

    private static void WriteUnit(float[] data, int slot, Vector2 center,
        float radius, float height)
    {
        int start = slot * VerticesPerUnit * FloatsPerVertex;
        Write(data, start, center.X - radius, .03f, center.Y - radius);
        Write(data, start + 8, center.X + radius, .03f, center.Y - radius);
        Write(data, start + 16, center.X + radius, .03f, center.Y + radius);
        Write(data, start + 24, center.X - radius, .03f, center.Y + radius);
        Write(data, start + 32, center.X, height, center.Y);
    }

    private static void Write(float[] data, int index, float x, float y, float z)
    {
        data[index] = x;
        data[index + 1] = y;
        data[index + 2] = z;
        data[index + 4] = 1;
    }

    private void ReleaseMeshes()
    {
        for (int i = 0; i < 2; i++)
        {
            _meshes[i]?.Dispose();
            _meshes[i] = null;
        }
    }
}
