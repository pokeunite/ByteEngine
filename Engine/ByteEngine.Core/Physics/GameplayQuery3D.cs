using System.Numerics;

using ByteEngine.Core.Characters;
using ByteEngine.Core.Classification;
using ByteEngine.Core.Scene;

using RuntimeScene = ByteEngine.Core.Scene.Scene;

namespace ByteEngine.Core.Physics;

/// <summary>
/// Lightweight gameplay collision queries shared by cameras, character motors,
/// projectiles and other runtime systems.
///
/// v0.10-B adds capsule sweeps and capsule/sphere overlap queries while
/// preserving the existing Raycast and SphereCast API.
/// </summary>
public static class GameplayQuery3D
{
    private const float Epsilon =
        0.000001f;

    private const int MaximumCapsuleSamples =
        16;

    public static bool Raycast(
        RuntimeScene scene,
        Vector3 origin,
        Vector3 direction,
        out RaycastHit3D hit,
        float maxDistance = float.PositiveInfinity,
        GameObject? ignore = null,
        LayerMask? layerMask = null,
        GameObject? source = null,
        bool bypassCollisionMatrix = false,
        bool includeTriggers = true)
    {
        return
            Cast(
                scene,
                origin,
                direction,
                0.0f,
                out hit,
                maxDistance,
                ignore,
                Guid.Empty,
                layerMask ??
                    LayerMask.All,
                source,
                bypassCollisionMatrix,
                includeTriggers,
                false);
    }

    public static bool SphereCast(
        RuntimeScene scene,
        Vector3 origin,
        Vector3 direction,
        float radius,
        out RaycastHit3D hit,
        float maxDistance = float.PositiveInfinity,
        GameObject? ignore = null,
        LayerMask? layerMask = null,
        GameObject? source = null,
        bool bypassCollisionMatrix = false,
        bool includeTriggers = true)
    {
        return
            Cast(
                scene,
                origin,
                direction,
                Math.Max(
                    0.0f,
                    radius),
                out hit,
                maxDistance,
                ignore,
                Guid.Empty,
                layerMask ??
                    LayerMask.All,
                source,
                bypassCollisionMatrix,
                includeTriggers,
                false);
    }

    internal static bool SphereCast(
        RuntimeScene scene,
        Vector3 origin,
        Vector3 direction,
        float radius,
        out RaycastHit3D hit,
        float maxDistance,
        GameObject? ignore,
        Guid secondIgnoredId,
        LayerMask? layerMask = null,
        GameObject? source = null)
    {
        return
            Cast(
                scene,
                origin,
                direction,
                Math.Max(
                    0.0f,
                    radius),
                out hit,
                maxDistance,
                ignore,
                secondIgnoredId,
                layerMask ??
                    LayerMask.All,
                source,
                false,
                true,
                false);
    }

    /// <summary>
    /// Sweeps a world-space capsule along direction and returns the earliest
    /// blocking collider.
    ///
    /// The capsule is represented by two sphere centers plus radius. The
    /// implementation samples the spine densely enough for character/gameplay
    /// use while retaining ByteEngine's existing primitive query backend.
    /// </summary>
    public static bool CapsuleCast(
        RuntimeScene scene,
        Vector3 pointA,
        Vector3 pointB,
        Vector3 direction,
        float radius,
        out RaycastHit3D hit,
        float maxDistance = float.PositiveInfinity,
        GameObject? ignore = null,
        LayerMask? layerMask = null,
        GameObject? source = null,
        bool bypassCollisionMatrix = false,
        bool includeTriggers = true)
    {
        ArgumentNullException.ThrowIfNull(
            scene);

        hit =
            default;

        radius =
            Math.Max(
                radius,
                0.0f);

        if ((!float.IsFinite(
                 maxDistance) &&
             !float.IsPositiveInfinity(
                 maxDistance)) ||
            maxDistance <
                0.0f ||
            direction.LengthSquared() <
                Epsilon)
        {
            return false;
        }

        Vector3 rayDirection =
            Vector3.Normalize(
                direction);

        int sampleCount =
            CapsuleSampleCount(
                pointA,
                pointB,
                radius);

        bool found =
            false;

        float closest =
            maxDistance;

        for (int index =
                 0;
             index <
             sampleCount;
             index++)
        {
            float amount =
                sampleCount <=
                    1
                    ? 0.0f
                    : (float)index /
                      (
                          sampleCount -
                          1
                      );

            Vector3 sample =
                Vector3.Lerp(
                    pointA,
                    pointB,
                    amount);

            if (!Cast(
                    scene,
                    sample,
                    rayDirection,
                    radius,
                    out RaycastHit3D candidate,
                    closest,
                    ignore,
                    Guid.Empty,
                    layerMask ??
                        LayerMask.All,
                    source,
                    bypassCollisionMatrix,
                    includeTriggers,
                    true))
            {
                continue;
            }

            closest =
                candidate.Distance;

            hit =
                candidate;

            found =
                true;
        }

        return found;
    }

    public static IReadOnlyList<OverlapHit3D> OverlapSphere(
        RuntimeScene scene,
        Vector3 center,
        float radius,
        GameObject? ignore = null,
        LayerMask? layerMask = null,
        GameObject? source = null,
        bool bypassCollisionMatrix = false,
        bool includeTriggers = true)
    {
        return
            OverlapCapsule(
                scene,
                center,
                center,
                radius,
                ignore,
                layerMask,
                source,
                bypassCollisionMatrix,
                includeTriggers);
    }

    /// <summary>
    /// Returns all colliders overlapping the supplied world-space capsule.
    /// </summary>
    public static IReadOnlyList<OverlapHit3D> OverlapCapsule(
        RuntimeScene scene,
        Vector3 pointA,
        Vector3 pointB,
        float radius,
        GameObject? ignore = null,
        LayerMask? layerMask = null,
        GameObject? source = null,
        bool bypassCollisionMatrix = false,
        bool includeTriggers = true)
    {
        ArgumentNullException.ThrowIfNull(
            scene);

        radius =
            Math.Max(
                radius,
                0.0f);

        LayerMask mask =
            layerMask ??
            LayerMask.All;

        Collider3D? sourceCollider =
            FindSourceCollider(
                source);

        var hits =
            new List<OverlapHit3D>();

        foreach (GameObject gameObject
                 in scene.GameObjects)
        {
            if (!gameObject.ActiveInHierarchy ||
                ShouldIgnore(
                    gameObject,
                    ignore,
                    Guid.Empty) ||
                !mask.Contains(
                    gameObject.Layer))
            {
                continue;
            }

            foreach (Collider3D collider
                     in gameObject.Components
                         .OfType<Collider3D>())
            {
                if (!collider.Enabled ||
                    (
                        !includeTriggers &&
                        collider.IsTrigger
                    ) ||
                    (
                        !bypassCollisionMatrix &&
                        source !=
                            null &&
                        !CollisionFilter.ShouldInteract(
                            source,
                            sourceCollider,
                            gameObject,
                            collider,
                            scene.Classification)
                    ) ||
                    !OverlapsCapsule(
                        pointA,
                        pointB,
                        radius,
                        collider))
                {
                    continue;
                }

                hits.Add(
                    new OverlapHit3D(
                        gameObject,
                        collider));
            }
        }

        return hits;
    }

    private static bool Cast(
        RuntimeScene scene,
        Vector3 origin,
        Vector3 direction,
        float radius,
        out RaycastHit3D hit,
        float maxDistance,
        GameObject? ignore,
        Guid secondIgnoredId,
        LayerMask layerMask,
        GameObject? source,
        bool bypassCollisionMatrix,
        bool includeTriggers,
        bool ignoreInitialOverlapWhenMovingOut)
    {
        ArgumentNullException.ThrowIfNull(
            scene);

        hit =
            default;

        if ((!float.IsFinite(
                 maxDistance) &&
             !float.IsPositiveInfinity(
                 maxDistance)) ||
            maxDistance <
                0.0f ||
            direction.LengthSquared() <
                Epsilon)
        {
            return false;
        }

        Vector3 rayDirection =
            Vector3.Normalize(
                direction);

        float closest =
            maxDistance;

        bool found =
            false;

        Collider3D? sourceCollider =
            FindSourceCollider(
                source);

        foreach (GameObject gameObject
                 in scene.GameObjects)
        {
            if (!gameObject.ActiveInHierarchy ||
                ShouldIgnore(
                    gameObject,
                    ignore,
                    secondIgnoredId) ||
                !layerMask.Contains(
                    gameObject.Layer))
            {
                continue;
            }

            foreach (Collider3D collider
                     in gameObject.Components
                         .OfType<Collider3D>())
            {
                if (!collider.Enabled ||
                    (
                        !includeTriggers &&
                        collider.IsTrigger
                    ) ||
                    (
                        !bypassCollisionMatrix &&
                        source !=
                            null &&
                        !CollisionFilter.ShouldInteract(
                            source,
                            sourceCollider,
                            gameObject,
                            collider,
                            scene.Classification)
                    ) ||
                    !TryIntersect(
                        origin,
                        rayDirection,
                        radius,
                        collider,
                        out float distance,
                        out Vector3 normal) ||
                    distance <
                        0.0f ||
                    distance >
                        closest)
                {
                    continue;
                }

                if (ignoreInitialOverlapWhenMovingOut &&
                    distance <=
                        Epsilon &&
                    Vector3.Dot(
                        rayDirection,
                        normal) >
                        0.0001f)
                {
                    continue;
                }

                closest =
                    distance;

                hit =
                    new RaycastHit3D(
                        gameObject,
                        collider,
                        origin +
                            rayDirection *
                            distance,
                        normal,
                        distance);

                found =
                    true;
            }
        }

        return found;
    }

    private static Collider3D? FindSourceCollider(
        GameObject? source)
    {
        if (source ==
            null)
        {
            return null;
        }

        foreach (GameObject objectInHierarchy
                 in EnumerateSelfAndDescendants(
                     source))
        {
            Collider3D? collider =
                objectInHierarchy.Components
                    .OfType<Collider3D>()
                    .FirstOrDefault(
                        item =>
                            item.Enabled &&
                            !item.IsTrigger);

            if (collider !=
                null)
            {
                return collider;
            }
        }

        return null;
    }

    private static bool ShouldIgnore(
        GameObject candidate,
        GameObject? ignore,
        Guid secondIgnoredId)
    {
        if (candidate.Id ==
            secondIgnoredId)
        {
            return true;
        }

        if (ignore ==
            null)
        {
            return false;
        }

        if (ReferenceEquals(
                candidate,
                ignore))
        {
            return true;
        }

        for (GameObject? parent =
                 candidate.Parent;
             parent !=
             null;
             parent =
                 parent.Parent)
        {
            if (ReferenceEquals(
                    parent,
                    ignore))
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<GameObject> EnumerateSelfAndDescendants(
        GameObject root)
    {
        yield return
            root;

        foreach (GameObject child
                 in root.Children)
        {
            foreach (GameObject descendant
                     in EnumerateSelfAndDescendants(
                         child))
            {
                yield return
                    descendant;
            }
        }
    }

    private static bool TryIntersect(
        Vector3 origin,
        Vector3 direction,
        float radius,
        Collider3D collider,
        out float distance,
        out Vector3 normal)
    {
        return collider switch
        {
            BoxCollider3D box =>
                IntersectBox(
                    origin,
                    direction,
                    radius,
                    box,
                    out distance,
                    out normal),

            CapsuleCollider3D capsule =>
                IntersectCapsule(
                    origin,
                    direction,
                    radius,
                    capsule,
                    out distance,
                    out normal),

            _ =>
                NoHit(
                    out distance,
                    out normal)
        };
    }

    private static bool IntersectBox(
        Vector3 origin,
        Vector3 direction,
        float castRadius,
        BoxCollider3D box,
        out float distance,
        out Vector3 normal)
    {
        distance =
            0.0f;

        normal =
            Vector3.Zero;

        Matrix4x4 world =
            box.Transform.WorldMatrix;

        if (!Matrix4x4.Invert(
                world,
                out Matrix4x4 inverse))
        {
            return false;
        }

        Vector3 localOrigin =
            Vector3.Transform(
                origin,
                inverse);

        Vector3 localDirection =
            Vector3.TransformNormal(
                direction,
                inverse);

        Vector3 scale =
            Vector3.Abs(
                box.Transform.WorldScale);

        Vector3 expansion =
            new(
                castRadius /
                    Math.Max(
                        scale.X,
                        Epsilon),
                castRadius /
                    Math.Max(
                        scale.Y,
                        Epsilon),
                castRadius /
                    Math.Max(
                        scale.Z,
                        Epsilon));

        Vector3 half =
            Vector3.Abs(
                box.Size) *
                0.5f +
            expansion;

        Vector3 minimum =
            box.Center -
            half;

        Vector3 maximum =
            box.Center +
            half;

        bool startsInside =
            localOrigin.X >=
                minimum.X &&
            localOrigin.X <=
                maximum.X &&
            localOrigin.Y >=
                minimum.Y &&
            localOrigin.Y <=
                maximum.Y &&
            localOrigin.Z >=
                minimum.Z &&
            localOrigin.Z <=
                maximum.Z;

        if (startsInside)
        {
            Vector3 localNormal =
                NearestBoxFaceNormal(
                    localOrigin,
                    minimum,
                    maximum);

            normal =
                SafeNormalize(
                    Vector3.TransformNormal(
                        localNormal,
                        Matrix4x4.Transpose(
                            inverse)),
                    -direction);

            distance =
                0.0f;

            return true;
        }

        float entry =
            0.0f;

        float exit =
            float.PositiveInfinity;

        Vector3 entryNormal =
            -direction;

        for (int axis =
                 0;
             axis <
             3;
             axis++)
        {
            float originAxis =
                Get(
                    localOrigin,
                    axis);

            float directionAxis =
                Get(
                    localDirection,
                    axis);

            float low =
                Get(
                    minimum,
                    axis);

            float high =
                Get(
                    maximum,
                    axis);

            if (Math.Abs(
                    directionAxis) <
                Epsilon)
            {
                if (originAxis <
                        low ||
                    originAxis >
                        high)
                {
                    return false;
                }

                continue;
            }

            float near =
                (
                    low -
                    originAxis
                ) /
                directionAxis;

            float far =
                (
                    high -
                    originAxis
                ) /
                directionAxis;

            Vector3 nearNormal =
                Axis(
                    axis,
                    directionAxis >
                        0.0f
                        ? -1.0f
                        : 1.0f);

            if (near >
                far)
            {
                (
                    near,
                    far
                ) =
                    (
                        far,
                        near
                    );
            }

            if (near >
                entry)
            {
                entry =
                    near;

                entryNormal =
                    nearNormal;
            }

            exit =
                Math.Min(
                    exit,
                    far);

            if (entry >
                    exit ||
                exit <
                    0.0f)
            {
                return false;
            }
        }

        distance =
            Math.Max(
                0.0f,
                entry);

        normal =
            SafeNormalize(
                Vector3.TransformNormal(
                    entryNormal,
                    Matrix4x4.Transpose(
                        inverse)),
                -direction);

        return true;
    }

    private static Vector3 NearestBoxFaceNormal(
        Vector3 point,
        Vector3 minimum,
        Vector3 maximum)
    {
        float minDistance =
            point.X -
            minimum.X;

        Vector3 normal =
            -Vector3.UnitX;

        void Consider(
            float candidate,
            Vector3 candidateNormal)
        {
            if (candidate >=
                minDistance)
            {
                return;
            }

            minDistance =
                candidate;

            normal =
                candidateNormal;
        }

        Consider(
            maximum.X -
                point.X,
            Vector3.UnitX);

        Consider(
            point.Y -
                minimum.Y,
            -Vector3.UnitY);

        Consider(
            maximum.Y -
                point.Y,
            Vector3.UnitY);

        Consider(
            point.Z -
                minimum.Z,
            -Vector3.UnitZ);

        Consider(
            maximum.Z -
                point.Z,
            Vector3.UnitZ);

        return normal;
    }

    private static bool IntersectCapsule(
        Vector3 origin,
        Vector3 direction,
        float castRadius,
        CapsuleCollider3D capsule,
        out float distance,
        out Vector3 normal)
    {
        CreateCapsule(
            capsule,
            out Vector3 pointA,
            out Vector3 pointB,
            out float colliderRadius);

        float radius =
            colliderRadius +
            castRadius;

        Vector3 closest =
            ClosestPointOnSegment(
                origin,
                pointA,
                pointB);

        Vector3 initialDelta =
            origin -
            closest;

        if (initialDelta.LengthSquared() <=
            radius *
            radius)
        {
            distance =
                0.0f;

            normal =
                SafeNormalize(
                    initialDelta,
                    -direction);

            return true;
        }

        Vector3 segment =
            pointB -
            pointA;

        Vector3 offset =
            origin -
            pointA;

        float segmentLengthSquared =
            Vector3.Dot(
                segment,
                segment);

        float segmentDotDirection =
            Vector3.Dot(
                segment,
                direction);

        float segmentDotOffset =
            Vector3.Dot(
                segment,
                offset);

        float directionDotOffset =
            Vector3.Dot(
                direction,
                offset);

        float offsetLengthSquared =
            Vector3.Dot(
                offset,
                offset);

        float qa =
            segmentLengthSquared -
            segmentDotDirection *
            segmentDotDirection;

        float qb =
            segmentLengthSquared *
                directionDotOffset -
            segmentDotOffset *
                segmentDotDirection;

        float qc =
            segmentLengthSquared *
                offsetLengthSquared -
            segmentDotOffset *
                segmentDotOffset -
            radius *
                radius *
                segmentLengthSquared;

        if (Math.Abs(
                qa) >
            Epsilon)
        {
            float discriminant =
                qb *
                    qb -
                qa *
                    qc;

            if (discriminant >=
                0.0f)
            {
                float time =
                    (
                        -qb -
                        MathF.Sqrt(
                            discriminant)
                    ) /
                    qa;

                float alongSegment =
                    segmentDotOffset +
                    time *
                    segmentDotDirection;

                if (time >=
                        0.0f &&
                    alongSegment >
                        0.0f &&
                    alongSegment <
                        segmentLengthSquared)
                {
                    distance =
                        time;

                    Vector3 point =
                        origin +
                        direction *
                        time;

                    Vector3 spine =
                        pointA +
                        segment *
                        (
                            alongSegment /
                            segmentLengthSquared
                        );

                    normal =
                        SafeNormalize(
                            point -
                            spine,
                            -direction);

                    return true;
                }
            }
        }

        bool hitA =
            Sphere(
                origin,
                direction,
                pointA,
                radius,
                out float timeA);

        bool hitB =
            Sphere(
                origin,
                direction,
                pointB,
                radius,
                out float timeB);

        if (!hitA &&
            !hitB)
        {
            return
                NoHit(
                    out distance,
                    out normal);
        }

        Vector3 cap;

        if (hitA &&
            (
                !hitB ||
                timeA <=
                    timeB
            ))
        {
            distance =
                timeA;

            cap =
                pointA;
        }
        else
        {
            distance =
                timeB;

            cap =
                pointB;
        }

        normal =
            SafeNormalize(
                origin +
                    direction *
                    distance -
                    cap,
                -direction);

        return true;
    }

    private static bool OverlapsCapsule(
        Vector3 pointA,
        Vector3 pointB,
        float radius,
        Collider3D collider)
    {
        if (collider is
            CapsuleCollider3D capsule)
        {
            CreateCapsule(
                capsule,
                out Vector3 otherA,
                out Vector3 otherB,
                out float otherRadius);

            ClosestPointsOnSegments(
                pointA,
                pointB,
                otherA,
                otherB,
                out Vector3 firstPoint,
                out Vector3 secondPoint);

            float combinedRadius =
                radius +
                otherRadius;

            return
                Vector3.DistanceSquared(
                    firstPoint,
                    secondPoint) <=
                combinedRadius *
                combinedRadius;
        }

        if (collider is
            BoxCollider3D box)
        {
            float lower =
                0.0f;

            float upper =
                1.0f;

            for (int iteration =
                     0;
                 iteration <
                 12;
                 iteration++)
            {
                float firstAmount =
                    lower +
                    (
                        upper -
                        lower
                    ) /
                    3.0f;

                float secondAmount =
                    upper -
                    (
                        upper -
                        lower
                    ) /
                    3.0f;

                float firstDistance =
                    DistanceSquaredToBox(
                        Vector3.Lerp(
                            pointA,
                            pointB,
                            firstAmount),
                        box);

                float secondDistance =
                    DistanceSquaredToBox(
                        Vector3.Lerp(
                            pointA,
                            pointB,
                            secondAmount),
                        box);

                if (firstDistance <
                    secondDistance)
                {
                    upper =
                        secondAmount;
                }
                else
                {
                    lower =
                        firstAmount;
                }
            }

            Vector3 closestSegmentPoint =
                Vector3.Lerp(
                    pointA,
                    pointB,
                    (
                        lower +
                        upper
                    ) *
                    0.5f);

            return
                DistanceSquaredToBox(
                    closestSegmentPoint,
                    box) <=
                radius *
                radius;
        }

        return false;
    }

    private static float DistanceSquaredToBox(
        Vector3 point,
        BoxCollider3D box)
    {
        Matrix4x4 world =
            box.Transform.WorldMatrix;

        if (!Matrix4x4.Invert(
                world,
                out Matrix4x4 inverse))
        {
            return float.PositiveInfinity;
        }

        Vector3 localPoint =
            Vector3.Transform(
                point,
                inverse);

        Vector3 half =
            Vector3.Abs(
                box.Size) *
            0.5f;

        Vector3 localClosest =
            Vector3.Clamp(
                localPoint,
                box.Center -
                    half,
                box.Center +
                    half);

        Vector3 worldClosest =
            Vector3.Transform(
                localClosest,
                world);

        return
            Vector3.DistanceSquared(
                point,
                worldClosest);
    }

    private static void CreateCapsule(
        CapsuleCollider3D capsule,
        out Vector3 pointA,
        out Vector3 pointB,
        out float radius)
    {
        Vector3 center =
            Vector3.Transform(
                capsule.Center,
                capsule.Transform.WorldMatrix);

        Vector3 scale =
            Vector3.Abs(
                capsule.Transform.WorldScale);

        radius =
            capsule.Radius *
            Math.Max(
                scale.X,
                scale.Z);

        float halfSegment =
            Math.Max(
                0.0f,
                capsule.Height *
                    0.5f *
                    scale.Y -
                radius);

        Vector3 up =
            SafeNormalize(
                capsule.Transform.Up,
                Vector3.UnitY);

        pointA =
            center -
            up *
            halfSegment;

        pointB =
            center +
            up *
            halfSegment;
    }

    private static int CapsuleSampleCount(
        Vector3 pointA,
        Vector3 pointB,
        float radius)
    {
        float length =
            Vector3.Distance(
                pointA,
                pointB);

        if (length <=
            Epsilon)
        {
            return 1;
        }

        float spacing =
            Math.Max(
                radius *
                    0.65f,
                0.05f);

        return
            Math.Clamp(
                (int)MathF.Ceiling(
                    length /
                    spacing) +
                1,
                2,
                MaximumCapsuleSamples);
    }

    private static bool Sphere(
        Vector3 origin,
        Vector3 direction,
        Vector3 center,
        float radius,
        out float distance)
    {
        Vector3 offset =
            origin -
            center;

        float projection =
            Vector3.Dot(
                direction,
                offset);

        float discriminant =
            projection *
                projection -
            (
                Vector3.Dot(
                    offset,
                    offset) -
                radius *
                    radius
            );

        if (discriminant <
            0.0f)
        {
            distance =
                0.0f;

            return false;
        }

        distance =
            -projection -
            MathF.Sqrt(
                discriminant);

        return
            distance >=
            0.0f;
    }

    private static Vector3 ClosestPointOnSegment(
        Vector3 point,
        Vector3 pointA,
        Vector3 pointB)
    {
        Vector3 segment =
            pointB -
            pointA;

        float lengthSquared =
            segment.LengthSquared();

        if (lengthSquared <
            Epsilon)
        {
            return pointA;
        }

        return
            pointA +
            segment *
            Math.Clamp(
                Vector3.Dot(
                    point -
                    pointA,
                    segment) /
                lengthSquared,
                0.0f,
                1.0f);
    }

    private static void ClosestPointsOnSegments(
        Vector3 firstA,
        Vector3 firstB,
        Vector3 secondA,
        Vector3 secondB,
        out Vector3 firstPoint,
        out Vector3 secondPoint)
    {
        Vector3 firstDirection =
            firstB -
            firstA;

        Vector3 secondDirection =
            secondB -
            secondA;

        Vector3 offset =
            firstA -
            secondA;

        float firstLengthSquared =
            Vector3.Dot(
                firstDirection,
                firstDirection);

        float secondLengthSquared =
            Vector3.Dot(
                secondDirection,
                secondDirection);

        float secondProjection =
            Vector3.Dot(
                secondDirection,
                offset);

        float firstAmount;
        float secondAmount;

        if (firstLengthSquared <=
                Epsilon &&
            secondLengthSquared <=
                Epsilon)
        {
            firstPoint =
                firstA;

            secondPoint =
                secondA;

            return;
        }

        if (firstLengthSquared <=
            Epsilon)
        {
            firstAmount =
                0.0f;

            secondAmount =
                Math.Clamp(
                    secondProjection /
                    secondLengthSquared,
                    0.0f,
                    1.0f);
        }
        else
        {
            float firstProjection =
                Vector3.Dot(
                    firstDirection,
                    offset);

            if (secondLengthSquared <=
                Epsilon)
            {
                secondAmount =
                    0.0f;

                firstAmount =
                    Math.Clamp(
                        -firstProjection /
                        firstLengthSquared,
                        0.0f,
                        1.0f);
            }
            else
            {
                float cross =
                    Vector3.Dot(
                        firstDirection,
                        secondDirection);

                float denominator =
                    firstLengthSquared *
                        secondLengthSquared -
                    cross *
                        cross;

                firstAmount =
                    denominator !=
                    0.0f
                        ? Math.Clamp(
                            (
                                cross *
                                    secondProjection -
                                firstProjection *
                                    secondLengthSquared
                            ) /
                            denominator,
                            0.0f,
                            1.0f)
                        : 0.0f;

                secondAmount =
                    (
                        cross *
                            firstAmount +
                        secondProjection
                    ) /
                    secondLengthSquared;

                if (secondAmount <
                    0.0f)
                {
                    secondAmount =
                        0.0f;

                    firstAmount =
                        Math.Clamp(
                            -firstProjection /
                            firstLengthSquared,
                            0.0f,
                            1.0f);
                }
                else if (secondAmount >
                         1.0f)
                {
                    secondAmount =
                        1.0f;

                    firstAmount =
                        Math.Clamp(
                            (
                                cross -
                                firstProjection
                            ) /
                            firstLengthSquared,
                            0.0f,
                            1.0f);
                }
            }
        }

        firstPoint =
            firstA +
            firstDirection *
            firstAmount;

        secondPoint =
            secondA +
            secondDirection *
            secondAmount;
    }

    private static bool NoHit(
        out float distance,
        out Vector3 normal)
    {
        distance =
            0.0f;

        normal =
            Vector3.Zero;

        return false;
    }

    private static float Get(
        Vector3 value,
        int axis)
    {
        return
            axis ==
                0
                ? value.X
                : axis ==
                    1
                    ? value.Y
                    : value.Z;
    }

    private static Vector3 Axis(
        int axis,
        float value)
    {
        return
            axis ==
                0
                ? new Vector3(
                    value,
                    0.0f,
                    0.0f)
                : axis ==
                    1
                    ? new Vector3(
                        0.0f,
                        value,
                        0.0f)
                    : new Vector3(
                        0.0f,
                        0.0f,
                        value);
    }

    private static Vector3 SafeNormalize(
        Vector3 value,
        Vector3 fallback)
    {
        return
            value.LengthSquared() >
            Epsilon
                ? Vector3.Normalize(
                    value)
                : fallback;
    }
}
