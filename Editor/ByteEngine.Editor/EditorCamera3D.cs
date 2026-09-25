using System.Numerics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;

namespace ByteEngine.Editor;

internal sealed class EditorCamera3D
{
    public Vector3 Position { get; set; } = new(5f, 4f, 7f);
    public float Yaw { get; set; } = -135f;
    public float Pitch { get; set; } = -22f;
    public float FieldOfView { get; set; } = 60f;

    public Vector3 Forward
    {
        get
        {
            float yaw = Yaw * MathF.PI / 180f;
            float pitch = Pitch * MathF.PI / 180f;
            return Vector3.Normalize(new Vector3(
                MathF.Cos(pitch) * MathF.Cos(yaw),
                MathF.Sin(pitch),
                MathF.Cos(pitch) * MathF.Sin(yaw)));
        }
    }

    public Vector3 Right => Vector3.Normalize(Vector3.Cross(Forward, Vector3.UnitY));
    public Matrix4x4 View => Matrix4x4.CreateLookAt(Position, Position + Forward, Vector3.UnitY);

    public Matrix4x4 Projection(float aspectRatio) => Matrix4x4.CreatePerspectiveFieldOfView(
        FieldOfView * MathF.PI / 180f,
        Math.Max(aspectRatio, .001f),
        .05f,
        2000f);

    public void Reset()
    {
        Position = new Vector3(5f, 4f, 7f);
        Yaw = -135f;
        Pitch = -22f;
    }

    public void Frame(GameObject target)
    {
        Vector3 minimum = new(float.PositiveInfinity);
        Vector3 maximum = new(float.NegativeInfinity);
        bool hasGeometry = false;

        void Include(GameObject item)
        {
            foreach (MeshRenderer renderer in item.Components.OfType<MeshRenderer>())
            {
                if (!renderer.Visible || renderer.Mesh == null)
                    continue;

                BoundingBox3D bounds = renderer.Mesh.LocalBounds.Transform(item.Transform.WorldMatrix);
                if (!bounds.IsValid)
                    continue;

                minimum = Vector3.Min(minimum, bounds.Minimum);
                maximum = Vector3.Max(maximum, bounds.Maximum);
                hasGeometry = true;
            }
            foreach (SkeletalMeshRenderer renderer in item.Components.OfType<SkeletalMeshRenderer>())
            {
                if (!renderer.Visible || !renderer.TryGetCurrentModelBounds(out BoundingBox3D modelBounds))
                    continue;

                BoundingBox3D bounds = modelBounds.Transform(item.Transform.WorldMatrix);
                if (!bounds.IsValid)
                    continue;

                minimum = Vector3.Min(minimum, bounds.Minimum);
                maximum = Vector3.Max(maximum, bounds.Maximum);
                hasGeometry = true;
            }

            foreach (GameObject child in item.Children)
                Include(child);
        }

        Include(target);
        if (!hasGeometry)
        {
            Vector3 size = target.Transform.WorldScale;
            float largestDimension = Math.Max(Math.Max(Math.Abs(size.X), Math.Abs(size.Y)), Math.Abs(size.Z));
            Position = target.Transform.WorldPosition - Forward * (largestDimension * 3f + 2f);
            return;
        }

        Frame(new BoundingBox3D(minimum, maximum));
    }

    public void Frame(BoundingBox3D bounds)
    {
        if (!bounds.IsValid) return;
        Vector3 center = bounds.Center;
        float radius = bounds.Size.Length() * 0.5f;
        float halfFov = Math.Clamp(FieldOfView, 1f, 170f) * MathF.PI / 360f;
        float distance = MathF.Max(radius / MathF.Sin(halfFov) * 1.35f, radius + 0.065f);
        Position = center - Forward * distance;
    }
}
