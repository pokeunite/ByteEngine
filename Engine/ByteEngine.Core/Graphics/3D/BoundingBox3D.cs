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

    public BoundingBox3D Transform(
        Matrix4x4 matrix)
    {
        Vector3[] corners =
            GetCorners();

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

        foreach (Vector3 corner
                 in corners)
        {
            Vector3 transformed =
                Vector3.Transform(
                    corner,
                    matrix);

            minimum =
                Vector3.Min(
                    minimum,
                    transformed);

            maximum =
                Vector3.Max(
                    maximum,
                    transformed);
        }

        return
            new BoundingBox3D(
                minimum,
                maximum);
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
