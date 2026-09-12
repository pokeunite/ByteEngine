using System.Numerics;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Scene;
using RuntimeScene = ByteEngine.Core.Scene.Scene;

namespace ByteEngine.Core.Physics;

public static class GameplayQuery3D
{
    private const float Epsilon = 0.000001f;

    public static bool Raycast(RuntimeScene scene, Vector3 origin, Vector3 direction,
        out RaycastHit3D hit, float maxDistance = float.PositiveInfinity, GameObject? ignore = null) =>
        Cast(scene, origin, direction, 0f, out hit, maxDistance, ignore, Guid.Empty);

    public static bool SphereCast(RuntimeScene scene, Vector3 origin, Vector3 direction, float radius,
        out RaycastHit3D hit, float maxDistance = float.PositiveInfinity, GameObject? ignore = null) =>
        Cast(scene, origin, direction, Math.Max(0f, radius), out hit, maxDistance, ignore, Guid.Empty);

    internal static bool SphereCast(RuntimeScene scene, Vector3 origin, Vector3 direction, float radius,
        out RaycastHit3D hit, float maxDistance, GameObject? ignore, Guid secondIgnoredId) =>
        Cast(scene, origin, direction, Math.Max(0f, radius), out hit, maxDistance, ignore, secondIgnoredId);

    private static bool Cast(RuntimeScene scene, Vector3 origin, Vector3 direction, float radius,
        out RaycastHit3D hit, float maxDistance, GameObject? ignore, Guid secondIgnoredId)
    {
        ArgumentNullException.ThrowIfNull(scene);
        hit = default;
        if ((!float.IsFinite(maxDistance) && !float.IsPositiveInfinity(maxDistance)) ||
            maxDistance < 0f || direction.LengthSquared() < Epsilon) return false;

        Vector3 rayDirection = Vector3.Normalize(direction);
        float closest = maxDistance;
        bool found = false;
        foreach (GameObject gameObject in scene.GameObjects)
        {
            if (!gameObject.ActiveInHierarchy || ReferenceEquals(gameObject, ignore) || gameObject.Id == secondIgnoredId) continue;
            foreach (Collider3D collider in gameObject.Components.OfType<Collider3D>())
            {
                if (!collider.Enabled || !TryIntersect(origin, rayDirection, radius, collider, out float distance, out Vector3 normal) ||
                    distance < 0f || distance > closest) continue;
                closest = distance;
                hit = new RaycastHit3D(gameObject, collider, origin + rayDirection * distance, normal, distance);
                found = true;
            }
        }
        return found;
    }

    private static bool TryIntersect(Vector3 origin, Vector3 direction, float radius, Collider3D collider,
        out float distance, out Vector3 normal) => collider switch
        {
            BoxCollider3D box => IntersectBox(origin, direction, radius, box, out distance, out normal),
            CapsuleCollider3D capsule => IntersectCapsule(origin, direction, radius, capsule, out distance, out normal),
            _ => NoHit(out distance, out normal)
        };

    private static bool IntersectBox(Vector3 origin, Vector3 direction, float castRadius, BoxCollider3D box,
        out float distance, out Vector3 normal)
    {
        distance = 0f; normal = Vector3.Zero;
        Matrix4x4 world = box.Transform.WorldMatrix;
        if (!Matrix4x4.Invert(world, out Matrix4x4 inverse)) return false;
        Vector3 localOrigin = Vector3.Transform(origin, inverse);
        Vector3 localDirection = Vector3.TransformNormal(direction, inverse);
        Vector3 scale = Vector3.Abs(box.Transform.WorldScale);
        Vector3 expansion = new(castRadius / Math.Max(scale.X, Epsilon), castRadius / Math.Max(scale.Y, Epsilon), castRadius / Math.Max(scale.Z, Epsilon));
        Vector3 half = Vector3.Abs(box.Size) * .5f + expansion;
        Vector3 min = box.Center - half, max = box.Center + half;
        float entry = 0f, exit = float.PositiveInfinity;
        Vector3 entryNormal = -direction;
        for (int axis = 0; axis < 3; axis++)
        {
            float o = Get(localOrigin, axis), d = Get(localDirection, axis), lo = Get(min, axis), hi = Get(max, axis);
            if (Math.Abs(d) < Epsilon) { if (o < lo || o > hi) return false; continue; }
            float near = (lo - o) / d, far = (hi - o) / d;
            Vector3 nearNormal = Axis(axis, d > 0f ? -1f : 1f);
            if (near > far) (near, far) = (far, near);
            if (near > entry) { entry = near; entryNormal = nearNormal; }
            exit = Math.Min(exit, far);
            if (entry > exit || exit < 0f) return false;
        }
        distance = Math.Max(0f, entry);
        if (distance == 0f) { normal = -direction; return true; }
        normal = Vector3.Normalize(Vector3.TransformNormal(entryNormal, Matrix4x4.Transpose(inverse)));
        return true;
    }

    private static bool IntersectCapsule(Vector3 origin, Vector3 direction, float castRadius, CapsuleCollider3D capsule,
        out float distance, out Vector3 normal)
    {
        Vector3 center = Vector3.Transform(capsule.Center, capsule.Transform.WorldMatrix);
        Vector3 scale = Vector3.Abs(capsule.Transform.WorldScale);
        float radius = capsule.Radius * Math.Max(scale.X, scale.Z) + castRadius;
        float halfSegment = Math.Max(0f, (capsule.Height * .5f - capsule.Radius) * scale.Y);
        Vector3 a = center - capsule.Transform.Up * halfSegment, b = center + capsule.Transform.Up * halfSegment;
        Vector3 closest = ClosestPoint(origin, a, b);
        if (Vector3.DistanceSquared(origin, closest) <= radius * radius) { distance = 0f; normal = -direction; return true; }

        Vector3 ba = b - a, oa = origin - a;
        float baba = Vector3.Dot(ba, ba), bard = Vector3.Dot(ba, direction), baoa = Vector3.Dot(ba, oa);
        float rdoa = Vector3.Dot(direction, oa), oaoa = Vector3.Dot(oa, oa);
        float qa = baba - bard * bard, qb = baba * rdoa - baoa * bard;
        float qc = baba * oaoa - baoa * baoa - radius * radius * baba;
        if (Math.Abs(qa) > Epsilon)
        {
            float h = qb * qb - qa * qc;
            if (h >= 0f)
            {
                float t = (-qb - MathF.Sqrt(h)) / qa, y = baoa + t * bard;
                if (t >= 0f && y > 0f && y < baba)
                {
                    distance = t; Vector3 point = origin + direction * t, spine = a + ba * (y / baba);
                    normal = Vector3.Normalize(point - spine); return true;
                }
            }
        }
        bool hitA = Sphere(origin, direction, a, radius, out float ta), hitB = Sphere(origin, direction, b, radius, out float tb);
        if (!hitA && !hitB) return NoHit(out distance, out normal);
        Vector3 cap;
        if (hitA && (!hitB || ta <= tb)) { distance = ta; cap = a; } else { distance = tb; cap = b; }
        normal = Vector3.Normalize(origin + direction * distance - cap); return true;
    }

    private static bool Sphere(Vector3 origin, Vector3 direction, Vector3 center, float radius, out float distance)
    {
        Vector3 offset = origin - center; float b = Vector3.Dot(direction, offset);
        float h = b * b - (Vector3.Dot(offset, offset) - radius * radius);
        if (h < 0f) { distance = 0f; return false; }
        distance = -b - MathF.Sqrt(h); return distance >= 0f;
    }

    private static Vector3 ClosestPoint(Vector3 point, Vector3 a, Vector3 b)
    {
        Vector3 segment = b - a; float length = segment.LengthSquared();
        return length < Epsilon ? a : a + segment * Math.Clamp(Vector3.Dot(point - a, segment) / length, 0f, 1f);
    }

    private static bool NoHit(out float distance, out Vector3 normal) { distance = 0f; normal = Vector3.Zero; return false; }
    private static float Get(Vector3 value, int axis) => axis == 0 ? value.X : axis == 1 ? value.Y : value.Z;
    private static Vector3 Axis(int axis, float value) => axis == 0 ? new(value, 0, 0) : axis == 1 ? new(0, value, 0) : new(0, 0, value);
}
