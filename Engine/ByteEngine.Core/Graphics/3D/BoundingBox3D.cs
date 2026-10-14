using System.Numerics;

namespace ByteEngine.Core.Graphics.ThreeD;

/// <summary>
/// Axis-aligned 3D bounds used by the rendering system.
/// </summary>
public readonly record struct BoundingBox3D(
    Vector3 Minimum,
    Vector3 Maximum)
{
    public Vector3 Center =>
        (Minimum + Maximum) *
        0.5f;

    public Vector3 Size =>
        Maximum -
        Minimum;

    public Vector3 Extents =>
        Size *
        0.5f;

    public bool IsValid =>
        float.IsFinite(Minimum.X) &&
        float.IsFinite(Minimum.Y) &&
        float.IsFinite(Minimum.Z) &&
        float.IsFinite(Maximum.X) &&
        float.IsFinite(Maximum.Y) &&
        float.IsFinite(Maximum.Z) &&
        Minimum.X <= Maximum.X &&
        Minimum.Y <= Maximum.Y &&
        Minimum.Z <= Maximum.Z;

    public static BoundingBox3D Empty =>
        new(
            Vector3.Zero,
            Vector3.Zero);

    internal static BoundingBox3D FromInterleavedVertices(
        float[] vertices)
    {
        ArgumentNullException.ThrowIfNull(
            vertices);

        if (vertices.Length <
            8)
        {
            return Empty;
        }

        Vector3 minimum =
            new(
                float.PositiveInfinity,
                float.PositiveInfinity,
                float.PositiveInfinity);

        Vector3 maximum =
            new(
                float.NegativeInfinity,
                float.NegativeInfinity,
                float.NegativeInfinity);

        bool found =
            false;

        for (int index =
                 0;
             index + 2 <
             vertices.Length;
             index +=
             8)
        {
            Vector3 position =
                new(
                    vertices[index],
                    vertices[index + 1],
                    vertices[index + 2]);

            if (!float.IsFinite(
                    position.X) ||
                !float.IsFinite(
                    position.Y) ||
                !float.IsFinite(
                    position.Z))
            {
                continue;
            }

            minimum =
                Vector3.Min(
                    minimum,
                    position);

            maximum =
                Vector3.Max(
                    maximum,
                    position);

            found =
                true;
        }

        return found
            ? new BoundingBox3D(
                minimum,
                maximum)
            : Empty;
    }

    public BoundingBox3D Transform(Matrix4x4 matrix)
    {
        // Transform the interval directly. This also covers non-uniform scale,
        // reflection and shear without allocating eight corners per submission.
        Vector3 x0 = new Vector3(matrix.M11, matrix.M12, matrix.M13) * Minimum.X;
        Vector3 x1 = new Vector3(matrix.M11, matrix.M12, matrix.M13) * Maximum.X;
        Vector3 y0 = new Vector3(matrix.M21, matrix.M22, matrix.M23) * Minimum.Y;
        Vector3 y1 = new Vector3(matrix.M21, matrix.M22, matrix.M23) * Maximum.Y;
        Vector3 z0 = new Vector3(matrix.M31, matrix.M32, matrix.M33) * Minimum.Z;
        Vector3 z1 = new Vector3(matrix.M31, matrix.M32, matrix.M33) * Maximum.Z;
        Vector3 translation = new(matrix.M41, matrix.M42, matrix.M43);
        return new BoundingBox3D(Vector3.Min(x0, x1) + Vector3.Min(y0, y1) + Vector3.Min(z0, z1) + translation,
            Vector3.Max(x0, x1) + Vector3.Max(y0, y1) + Vector3.Max(z0, z1) + translation);
    }

    public Vector3[] GetCorners()
    {
        return
        [
            new Vector3(
                Minimum.X,
                Minimum.Y,
                Minimum.Z),

            new Vector3(
                Maximum.X,
                Minimum.Y,
                Minimum.Z),

            new Vector3(
                Maximum.X,
                Maximum.Y,
                Minimum.Z),

            new Vector3(
                Minimum.X,
                Maximum.Y,
                Minimum.Z),

            new Vector3(
                Minimum.X,
                Minimum.Y,
                Maximum.Z),

            new Vector3(
                Maximum.X,
                Minimum.Y,
                Maximum.Z),

            new Vector3(
                Maximum.X,
                Maximum.Y,
                Maximum.Z),

            new Vector3(
                Minimum.X,
                Maximum.Y,
                Maximum.Z)
        ];
    }
}
