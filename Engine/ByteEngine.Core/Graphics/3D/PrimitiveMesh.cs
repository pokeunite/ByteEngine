using System.Numerics;

namespace ByteEngine.Core.Graphics.ThreeD;

public enum PrimitiveMeshType
{
    Cube,
    Plane,
    Sphere
}

public static class PrimitiveMesh
{
    public static Mesh Create(PrimitiveMeshType type) => type switch
    {
        PrimitiveMeshType.Cube => CreateCube(),
        PrimitiveMeshType.Plane => CreatePlane(),
        PrimitiveMeshType.Sphere => CreateSphere(),
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    private static Mesh CreateCube()
    {
        var vertices = new List<float>();
        var indices = new List<uint>();
        AddFace(vertices, indices, new(-.5f, -.5f, .5f), new(.5f, -.5f, .5f), new(.5f, .5f, .5f), new(-.5f, .5f, .5f), new(0f, 0f, 1f));
        AddFace(vertices, indices, new(.5f, -.5f, -.5f), new(-.5f, -.5f, -.5f), new(-.5f, .5f, -.5f), new(.5f, .5f, -.5f), new(0f, 0f, -1f));
        AddFace(vertices, indices, new(.5f, -.5f, .5f), new(.5f, -.5f, -.5f), new(.5f, .5f, -.5f), new(.5f, .5f, .5f), new(1f, 0f, 0f));
        AddFace(vertices, indices, new(-.5f, -.5f, -.5f), new(-.5f, -.5f, .5f), new(-.5f, .5f, .5f), new(-.5f, .5f, -.5f), new(-1f, 0f, 0f));
        AddFace(vertices, indices, new(-.5f, .5f, .5f), new(.5f, .5f, .5f), new(.5f, .5f, -.5f), new(-.5f, .5f, -.5f), new(0f, 1f, 0f));
        AddFace(vertices, indices, new(-.5f, -.5f, -.5f), new(.5f, -.5f, -.5f), new(.5f, -.5f, .5f), new(-.5f, -.5f, .5f), new(0f, -1f, 0f));
        return new Mesh(vertices.ToArray(), indices.ToArray());
    }

    private static Mesh CreatePlane() => new(
        new float[]
        {
            -.5f, 0f, -.5f, 0f, 1f, 0f, 0f, 0f,
             .5f, 0f, -.5f, 0f, 1f, 0f, 1f, 0f,
             .5f, 0f,  .5f, 0f, 1f, 0f, 1f, 1f,
            -.5f, 0f,  .5f, 0f, 1f, 0f, 0f, 1f
        },
        new uint[] { 0, 2, 1, 0, 3, 2 });

    private static Mesh CreateSphere(int segments = 24, int rings = 16)
    {
        var vertices = new List<float>();
        var indices = new List<uint>();
        for (int y = 0; y <= rings; y++)
        {
            float vertical = (float)y / rings;
            float phi = vertical * MathF.PI;
            for (int x = 0; x <= segments; x++)
            {
                float horizontal = (float)x / segments;
                float theta = horizontal * MathF.Tau;
                float normalX = MathF.Sin(phi) * MathF.Cos(theta);
                float normalY = MathF.Cos(phi);
                float normalZ = MathF.Sin(phi) * MathF.Sin(theta);
                vertices.AddRange(new[]
                {
                    normalX * .5f, normalY * .5f, normalZ * .5f,
                    normalX, normalY, normalZ,
                    horizontal, 1f - vertical
                });
            }
        }

        for (int y = 0; y < rings; y++)
        {
            for (int x = 0; x < segments; x++)
            {
                uint first = (uint)(y * (segments + 1) + x);
                uint second = first + (uint)segments + 1;
                indices.AddRange(new[] { first, second, first + 1, first + 1, second, second + 1 });
            }
        }

        return new Mesh(vertices.ToArray(), indices.ToArray());
    }

    private static void AddFace(
        List<float> vertices,
        List<uint> indices,
        Vector3 a,
        Vector3 b,
        Vector3 c,
        Vector3 d,
        Vector3 normal)
    {
        uint start = (uint)(vertices.Count / 8);
        AddVertex(vertices, a, normal, 0f, 0f);
        AddVertex(vertices, b, normal, 1f, 0f);
        AddVertex(vertices, c, normal, 1f, 1f);
        AddVertex(vertices, d, normal, 0f, 1f);
        indices.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
    }

    private static void AddVertex(
        List<float> vertices,
        Vector3 position,
        Vector3 normal,
        float u,
        float v) => vertices.AddRange(new[]
        {
            position.X, position.Y, position.Z,
            normal.X, normal.Y, normal.Z,
            u, v
        });
}
