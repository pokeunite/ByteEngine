using System.Numerics;

namespace ByteEngine.Core.Graphics.ThreeD;

/// <summary>
/// View frustum extracted from a System.Numerics view-projection matrix.
/// </summary>
public readonly struct Frustum3D
{
    private readonly FrustumPlane _left;
    private readonly FrustumPlane _right;
    private readonly FrustumPlane _bottom;
    private readonly FrustumPlane _top;
    private readonly FrustumPlane _near;
    private readonly FrustumPlane _far;

    public Frustum3D(
        Matrix4x4 viewProjection)
    {
        /*
         * System.Numerics uses row-vector transformation semantics:
         *
         * clip = position * matrix
         *
         * Frustum planes are therefore extracted from matrix columns.
         * CreatePerspectiveFieldOfView uses a 0..W depth range, giving:
         *
         * near: Z >= 0
         * far : Z <= W
         */
        _left =
            FrustumPlane.Create(
                viewProjection.M11 +
                viewProjection.M14,
                viewProjection.M21 +
                viewProjection.M24,
                viewProjection.M31 +
                viewProjection.M34,
                viewProjection.M41 +
                viewProjection.M44);

        _right =
            FrustumPlane.Create(
                viewProjection.M14 -
                viewProjection.M11,
                viewProjection.M24 -
                viewProjection.M21,
                viewProjection.M34 -
                viewProjection.M31,
                viewProjection.M44 -
                viewProjection.M41);

        _bottom =
            FrustumPlane.Create(
                viewProjection.M12 +
                viewProjection.M14,
                viewProjection.M22 +
                viewProjection.M24,
                viewProjection.M32 +
                viewProjection.M34,
                viewProjection.M42 +
                viewProjection.M44);

        _top =
            FrustumPlane.Create(
                viewProjection.M14 -
                viewProjection.M12,
                viewProjection.M24 -
                viewProjection.M22,
                viewProjection.M34 -
                viewProjection.M32,
                viewProjection.M44 -
                viewProjection.M42);

        _near =
            FrustumPlane.Create(
                viewProjection.M13,
                viewProjection.M23,
                viewProjection.M33,
                viewProjection.M43);

        _far =
            FrustumPlane.Create(
                viewProjection.M14 -
                viewProjection.M13,
                viewProjection.M24 -
                viewProjection.M23,
                viewProjection.M34 -
                viewProjection.M33,
                viewProjection.M44 -
                viewProjection.M43);
    }

    public bool Intersects(
        BoundingBox3D bounds)
    {
        if (!bounds.IsValid)
        {
            return true;
        }

        return
            !_left.IsOutside(
                bounds) &&
            !_right.IsOutside(
                bounds) &&
            !_bottom.IsOutside(
                bounds) &&
            !_top.IsOutside(
                bounds) &&
            !_near.IsOutside(
                bounds) &&
            !_far.IsOutside(
                bounds);
    }

    private readonly struct FrustumPlane
    {
        private readonly Vector3 _normal;
        private readonly float _distance;

        private FrustumPlane(
            Vector3 normal,
            float distance)
        {
            _normal =
                normal;

            _distance =
                distance;
        }

        public static FrustumPlane Create(
            float x,
            float y,
            float z,
            float distance)
        {
            Vector3 normal =
                new(
                    x,
                    y,
                    z);

            float length =
                normal.Length();

            if (length <=
                    0.000001f ||
                !float.IsFinite(
                    length))
            {
                return
                    new FrustumPlane(
                        Vector3.Zero,
                        float.PositiveInfinity);
            }

            float inverseLength =
                1.0f /
                length;

            return
                new FrustumPlane(
                    normal *
                    inverseLength,
                    distance *
                    inverseLength);
        }

        public bool IsOutside(
            BoundingBox3D bounds)
        {
            if (_normal ==
                Vector3.Zero)
            {
                return false;
            }

            Vector3 positive =
                new(
                    _normal.X >= 0.0f
                        ? bounds.Maximum.X
                        : bounds.Minimum.X,

                    _normal.Y >= 0.0f
                        ? bounds.Maximum.Y
                        : bounds.Minimum.Y,

                    _normal.Z >= 0.0f
                        ? bounds.Maximum.Z
                        : bounds.Minimum.Z);

            return
                Vector3.Dot(
                    _normal,
                    positive) +
                _distance <
                0.0f;
        }
    }
}
