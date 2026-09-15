using System.Numerics;

using ByteEngine.Core.Characters;
using ByteEngine.Core.Scene;

using RuntimeScene = ByteEngine.Core.Scene.Scene;

namespace ByteEngine.Core.Physics;

/// <summary>
/// Scene-owned lightweight 3D rigid-body world.
///
/// v0.10-A supports:
/// - BoxCollider3D and CapsuleCollider3D
/// - static colliders without Rigidbody3D
/// - dynamic and kinematic Rigidbody3D
/// - gravity, forces, impulses, damping, friction and restitution
/// - project collision matrix / per-collider masks
/// - trigger and collision enter/stay/exit callbacks
/// - sub-stepping and broad-phase AABB rejection
///
/// The solver is intentionally linear-only in this first core pass. Rotational
/// inertia/torque are not faked; they can be added later without changing the
/// collision/contact API introduced here.
/// </summary>
public sealed class PhysicsWorld3D
{
    private const float Epsilon =
        0.000001f;

    private const float ContactSlop =
        0.001f;

    private const float CorrectionPercent =
        0.80f;

    private readonly Dictionary<ContactKey, PhysicsContactPair3D> _previousContacts =
        new();

    private readonly Dictionary<ContactKey, PhysicsContactPair3D> _frameContacts =
        new();

    /*
     * Diagnostics only. These snapshots mirror the most recently completed
     * physics frame and are never read by the solver itself.
     */
    private readonly Dictionary<ContactKey, SolverContactDiagnostics> _previousSolverDiagnostics =
        new();

    private readonly Dictionary<ContactKey, SolverContactDiagnostics> _frameSolverDiagnostics =
        new();

    public Vector3 Gravity { get; set; } =
        new(
            0.0f,
            -9.81f,
            0.0f);

    /// <summary>
    /// Maximum simulation slice. Smaller slices reduce tunneling and improve
    /// solver stability during slow editor frames.
    /// </summary>
    public float MaximumSubstep { get; set; } =
        1.0f /
        60.0f;

    public int MaximumSubsteps { get; set; } =
        8;

    public IReadOnlyCollection<PhysicsContactPair3D> Contacts =>
        _previousContacts.Values;

    public void Step(
        RuntimeScene scene,
        float deltaTime)
    {
        ArgumentNullException.ThrowIfNull(
            scene);

        if (!float.IsFinite(
                deltaTime) ||
            deltaTime <=
                0.0f)
        {
            return;
        }

        float safeDelta =
            Math.Min(
                deltaTime,
                0.13333334f);

        float maxSubstep =
            Math.Clamp(
                MaximumSubstep,
                1.0f /
                1000.0f,
                1.0f /
                15.0f);

        int substeps =
            Math.Clamp(
                (int)MathF.Ceiling(
                    safeDelta /
                    maxSubstep),
                1,
                Math.Max(
                    MaximumSubsteps,
                    1));

        float stepDelta =
            safeDelta /
            substeps;

        Rigidbody3D[] bodies =
            scene.GameObjects
                .Where(
                    gameObject =>
                        gameObject.ActiveInHierarchy)
                .SelectMany(
                    gameObject =>
                        gameObject.Components
                            .OfType<Rigidbody3D>())
                .Where(
                    body =>
                        body.Enabled)
                .ToArray();

        _frameContacts.Clear();
        _frameSolverDiagnostics.Clear();

        for (int step =
                 0;
             step <
             substeps;
             step++)
        {
            foreach (Rigidbody3D body
                     in bodies)
            {
                body.Integrate(
                    stepDelta,
                    Gravity);
            }

            SolveContacts(
                scene);
        }

        foreach (Rigidbody3D body
                 in bodies)
        {
            body.EndPhysicsFrame();
        }

        DispatchContactTransitions();

        _previousContacts.Clear();

        foreach (var pair
                 in _frameContacts)
        {
            _previousContacts[pair.Key] =
                pair.Value;
        }

        _previousSolverDiagnostics.Clear();

        foreach (var pair
                 in _frameSolverDiagnostics)
        {
            _previousSolverDiagnostics[pair.Key] =
                pair.Value;
        }
    }

    public void Reset()
    {
        _previousContacts.Clear();
        _frameContacts.Clear();
        _previousSolverDiagnostics.Clear();
        _frameSolverDiagnostics.Clear();
    }

    private void SolveContacts(
        RuntimeScene scene)
    {
        ColliderEntry[] colliders =
            scene.GameObjects
                .Where(
                    gameObject =>
                        gameObject.ActiveInHierarchy)
                .SelectMany(
                    gameObject =>
                        gameObject.Components
                            .OfType<Collider3D>()
                            .Where(
                                collider =>
                                    collider.Enabled)
                            .Select(
                                collider =>
                                    new ColliderEntry(
                                        gameObject,
                                        collider,
                                        FindBodyForCollider(
                                            gameObject),
                                        CalculateBounds(
                                            collider))))
                .ToArray();

        for (int firstIndex =
                 0;
             firstIndex <
             colliders.Length;
             firstIndex++)
        {
            ColliderEntry first =
                colliders[firstIndex];

            for (int secondIndex =
                     firstIndex +
                     1;
                 secondIndex <
                 colliders.Length;
                 secondIndex++)
            {
                ColliderEntry second =
                    colliders[secondIndex];

                if (ReferenceEquals(
                        first.GameObject,
                        second.GameObject) ||
                    (
                        first.Body != null &&
                        ReferenceEquals(
                            first.Body,
                            second.Body)
                    ) ||
                    !first.Bounds.Intersects(
                        second.Bounds) ||
                    !CollisionFilter.ShouldInteract(
                        first.GameObject,
                        first.Collider,
                        second.GameObject,
                        second.Collider,
                        scene.Classification))
                {
                    continue;
                }

                if (!TryContact(
                        first.Collider,
                        second.Collider,
                        out Vector3 point,
                        out Vector3 normalFromFirstToSecond,
                        out float penetration))
                {
                    continue;
                }

                bool trigger =
                    first.Collider.IsTrigger ||
                    second.Collider.IsTrigger;

                PhysicsContactPair3D pair =
                    new(
                        first.GameObject,
                        first.Collider,
                        second.GameObject,
                        second.Collider,
                        point,
                        normalFromFirstToSecond,
                        Math.Max(
                            penetration,
                            0.0f),
                        trigger);

                ContactKey key =
                    ContactKey.Create(
                        pair);

                _frameContacts[key] =
                    pair;

                SolverContactDiagnostics solverDiagnostics =
                    trigger
                        ? SolverContactDiagnostics.ForTrigger(
                            first.Body,
                            second.Body)
                        : ResolveContact(
                            first,
                            second,
                            normalFromFirstToSecond,
                            penetration);

                _frameSolverDiagnostics[key] =
                    solverDiagnostics;
            }
        }
    }

    private static SolverContactDiagnostics ResolveContact(
        ColliderEntry first,
        ColliderEntry second,
        Vector3 normal,
        float penetration)
    {
        Rigidbody3D? firstBody =
            IsUsableBody(
                first.Body)
                ? first.Body
                : null;

        Rigidbody3D? secondBody =
            IsUsableBody(
                second.Body)
                ? second.Body
                : null;

        float inverseMassFirst =
            firstBody?.InverseMass ??
            0.0f;

        float inverseMassSecond =
            secondBody?.InverseMass ??
            0.0f;

        float inverseMassSum =
            inverseMassFirst +
            inverseMassSecond;

        if (inverseMassSum <=
            Epsilon)
        {
            return
                SolverContactDiagnostics.NoImpulse(
                    firstBody,
                    secondBody,
                    0.0f,
                    0.0f);
        }

        float correctionMagnitude =
            Math.Max(
                penetration -
                ContactSlop,
                0.0f) /
            inverseMassSum *
            CorrectionPercent;

        Vector3 correction =
            normal *
            correctionMagnitude;

        firstBody?.ApplyPositionCorrection(
            -correction *
            inverseMassFirst);

        secondBody?.ApplyPositionCorrection(
            correction *
            inverseMassSecond);

        Vector3 firstVelocity =
            firstBody?.Velocity ??
            Vector3.Zero;

        Vector3 secondVelocity =
            secondBody?.Velocity ??
            Vector3.Zero;

        Vector3 relativeVelocity =
            secondVelocity -
            firstVelocity;

        float normalVelocity =
            Vector3.Dot(
                relativeVelocity,
                normal);

        if (normalVelocity >
            0.0f)
        {
            return
                SolverContactDiagnostics.NoImpulse(
                    firstBody,
                    secondBody,
                    Math.Max(
                        firstBody?.Restitution ??
                            0.0f,
                        secondBody?.Restitution ??
                            0.0f),
                    normalVelocity);
        }

        float restitution =
            Math.Max(
                firstBody?.Restitution ??
                    0.0f,
                secondBody?.Restitution ??
                    0.0f);

        float impulseMagnitude =
            -(
                1.0f +
                restitution
            ) *
            normalVelocity /
            inverseMassSum;

        Vector3 impulse =
            normal *
            impulseMagnitude;

        firstBody?.ApplyVelocityImpulse(
            impulse);

        secondBody?.ApplyVelocityImpulse(
            -impulse);

        /*
         * Coulomb friction from the remaining tangential relative velocity.
         */
        firstVelocity =
            firstBody?.Velocity ??
            Vector3.Zero;

        secondVelocity =
            secondBody?.Velocity ??
            Vector3.Zero;

        relativeVelocity =
            secondVelocity -
            firstVelocity;

        Vector3 tangent =
            relativeVelocity -
            Vector3.Dot(
                relativeVelocity,
                normal) *
            normal;

        float tangentLengthSquared =
            tangent.LengthSquared();

        if (tangentLengthSquared <=
            Epsilon)
        {
            return
                SolverContactDiagnostics.Applied(
                    firstBody,
                    secondBody,
                    restitution,
                    normalVelocity,
                    impulseMagnitude);
        }

        tangent /=
            MathF.Sqrt(
                tangentLengthSquared);

        float tangentImpulseMagnitude =
            -Vector3.Dot(
                relativeVelocity,
                tangent) /
            inverseMassSum;

        float friction =
            MathF.Sqrt(
                Math.Max(
                    firstBody?.Friction ??
                        0.6f,
                    0.0f) *
                Math.Max(
                    secondBody?.Friction ??
                        0.6f,
                    0.0f));

        float maximumFrictionImpulse =
            MathF.Abs(
                impulseMagnitude) *
            friction;

        tangentImpulseMagnitude =
            Math.Clamp(
                tangentImpulseMagnitude,
                -maximumFrictionImpulse,
                maximumFrictionImpulse);

        Vector3 frictionImpulse =
            tangent *
            tangentImpulseMagnitude;

        firstBody?.ApplyVelocityImpulse(
            frictionImpulse);

        secondBody?.ApplyVelocityImpulse(
            -frictionImpulse);

        return
            SolverContactDiagnostics.Applied(
                firstBody,
                secondBody,
                restitution,
                normalVelocity,
                impulseMagnitude);
    }

    private static Rigidbody3D? FindBodyForCollider(
        GameObject gameObject)
    {
        for (GameObject? current =
                 gameObject;
             current !=
             null;
             current =
                 current.Parent)
        {
            Rigidbody3D? body =
                current.GetComponent<Rigidbody3D>();

            if (body !=
                null &&
                body.Enabled)
            {
                return body;
            }
        }

        return null;
    }

    private static bool IsUsableBody(
        Rigidbody3D? body)
    {
        return
            body !=
            null &&
            body.Enabled;
    }

    /// <summary>
    /// Returns what the physics solver currently sees for the selected object.
    /// This is read-only and does not change simulation behavior.
    /// </summary>
    public PhysicsObjectDiagnostics3D GetDiagnostics(
        GameObject selected)
    {
        ArgumentNullException.ThrowIfNull(
            selected);

        Rigidbody3D? localBody =
            selected.GetComponent<Rigidbody3D>();

        Rigidbody3D? resolvedBody =
            FindBodyForCollider(
                selected);

        GameObject[] selectedHierarchy =
            EnumerateSelfAndDescendants(
                selected)
                .ToArray();

        HashSet<Guid> selectedIds =
            selectedHierarchy
                .Select(
                    gameObject =>
                        gameObject.Id)
                .ToHashSet();

        var colliders =
            new List<PhysicsColliderDiagnostics3D>();

        foreach (GameObject gameObject
                 in selectedHierarchy)
        {
            foreach (Collider3D collider
                     in gameObject.Components
                         .OfType<Collider3D>())
            {
                Rigidbody3D? body =
                    FindBodyForCollider(
                        gameObject);

                colliders.Add(
                    new PhysicsColliderDiagnostics3D(
                        gameObject.Name,
                        collider.GetType().Name,
                        collider.Enabled,
                        collider.IsTrigger,
                        body?.GameObject.Name ??
                            "None (Static Collider)",
                        body?.BodyType,
                        body?.Restitution));
            }
        }

        var contacts =
            new List<PhysicsContactDiagnostics3D>();

        foreach (var entry
                 in _previousContacts)
        {
            PhysicsContactPair3D pair =
                entry.Value;

            Rigidbody3D? bodyA =
                FindBodyForCollider(
                    pair.A);

            Rigidbody3D? bodyB =
                FindBodyForCollider(
                    pair.B);

            bool firstMatches =
                selectedIds.Contains(
                    pair.A.Id) ||
                (
                    resolvedBody !=
                        null &&
                    ReferenceEquals(
                        bodyA,
                        resolvedBody)
                );

            bool secondMatches =
                selectedIds.Contains(
                    pair.B.Id) ||
                (
                    resolvedBody !=
                        null &&
                    ReferenceEquals(
                        bodyB,
                        resolvedBody)
                );

            if (!firstMatches &&
                !secondMatches)
            {
                continue;
            }

            bool selectedIsFirst =
                firstMatches;

            _previousSolverDiagnostics.TryGetValue(
                entry.Key,
                out SolverContactDiagnostics solver);

            contacts.Add(
                new PhysicsContactDiagnostics3D(
                    selectedIsFirst
                        ? pair.A.Name
                        : pair.B.Name,
                    selectedIsFirst
                        ? pair.B.Name
                        : pair.A.Name,
                    selectedIsFirst
                        ? pair.ColliderA.GetType().Name
                        : pair.ColliderB.GetType().Name,
                    selectedIsFirst
                        ? pair.ColliderB.GetType().Name
                        : pair.ColliderA.GetType().Name,
                    pair.IsTrigger,
                    pair.Point,
                    selectedIsFirst
                        ? -pair.Normal
                        : pair.Normal,
                    pair.Penetration,
                    solver.BodyAName,
                    solver.BodyBName,
                    solver.RestitutionUsed,
                    solver.RelativeNormalVelocity,
                    solver.NormalImpulseMagnitude,
                    solver.ImpulseApplied));
        }

        return
            new PhysicsObjectDiagnostics3D(
                selected.Name,
                selected.Components
                    .Select(
                        component =>
                            component.GetType().FullName ??
                            component.GetType().Name)
                    .ToArray(),
                CreateBodyDiagnostics(
                    localBody),
                CreateBodyDiagnostics(
                    resolvedBody),
                colliders,
                contacts);
    }

    private static PhysicsBodyDiagnostics3D? CreateBodyDiagnostics(
        Rigidbody3D? body)
    {
        if (body ==
            null)
        {
            return null;
        }

        return
            new PhysicsBodyDiagnostics3D(
                body.GameObject.Name,
                body.BodyType,
                body.Enabled,
                body.Mass,
                body.UseGravity,
                body.GravityScale,
                body.LinearDamping,
                body.Restitution,
                body.Friction,
                body.Velocity);
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

    private void DispatchContactTransitions()
    {
        foreach (var pair
                 in _frameContacts)
        {
            bool existed =
                _previousContacts.ContainsKey(
                    pair.Key);

            DispatchPair(
                pair.Value,
                existed
                    ? ContactPhase.Stay
                    : ContactPhase.Enter);
        }

        foreach (var pair
                 in _previousContacts)
        {
            if (_frameContacts.ContainsKey(
                    pair.Key))
            {
                continue;
            }

            DispatchPair(
                pair.Value,
                ContactPhase.Exit);
        }
    }

    private static void DispatchPair(
        PhysicsContactPair3D pair,
        ContactPhase phase)
    {
        PhysicsContact3D firstContact =
            new(
                pair.A,
                pair.ColliderA,
                pair.B,
                pair.ColliderB,
                pair.Point,
                -pair.Normal,
                pair.Penetration,
                pair.IsTrigger);

        PhysicsContact3D secondContact =
            new(
                pair.B,
                pair.ColliderB,
                pair.A,
                pair.ColliderA,
                pair.Point,
                pair.Normal,
                pair.Penetration,
                pair.IsTrigger);

        if (pair.IsTrigger)
        {
            switch (phase)
            {
                case ContactPhase.Enter:
                    pair.A.DispatchTriggerEnter(
                        firstContact);
                    pair.B.DispatchTriggerEnter(
                        secondContact);
                    break;

                case ContactPhase.Stay:
                    pair.A.DispatchTriggerStay(
                        firstContact);
                    pair.B.DispatchTriggerStay(
                        secondContact);
                    break;

                case ContactPhase.Exit:
                    pair.A.DispatchTriggerExit(
                        firstContact);
                    pair.B.DispatchTriggerExit(
                        secondContact);
                    break;
            }

            return;
        }

        switch (phase)
        {
            case ContactPhase.Enter:
                pair.A.DispatchCollisionEnter(
                    firstContact);
                pair.B.DispatchCollisionEnter(
                    secondContact);
                break;

            case ContactPhase.Stay:
                pair.A.DispatchCollisionStay(
                    firstContact);
                pair.B.DispatchCollisionStay(
                    secondContact);
                break;

            case ContactPhase.Exit:
                pair.A.DispatchCollisionExit(
                    firstContact);
                pair.B.DispatchCollisionExit(
                    secondContact);
                break;
        }
    }

    private static bool TryContact(
        Collider3D first,
        Collider3D second,
        out Vector3 point,
        out Vector3 normal,
        out float penetration)
    {
        if (first is
                BoxCollider3D firstBox &&
            second is
                BoxCollider3D secondBox)
        {
            return
                BoxBox(
                    CreateBox(
                        firstBox),
                    CreateBox(
                        secondBox),
                    out point,
                    out normal,
                    out penetration);
        }

        if (first is
                CapsuleCollider3D firstCapsule &&
            second is
                CapsuleCollider3D secondCapsule)
        {
            return
                CapsuleCapsule(
                    CreateCapsule(
                        firstCapsule),
                    CreateCapsule(
                        secondCapsule),
                    out point,
                    out normal,
                    out penetration);
        }

        if (first is
                CapsuleCollider3D capsule &&
            second is
                BoxCollider3D box)
        {
            return
                CapsuleBox(
                    CreateCapsule(
                        capsule),
                    CreateBox(
                        box),
                    out point,
                    out normal,
                    out penetration);
        }

        if (first is
                BoxCollider3D boxFirst &&
            second is
                CapsuleCollider3D capsuleSecond &&
            CapsuleBox(
                CreateCapsule(
                    capsuleSecond),
                CreateBox(
                    boxFirst),
                out point,
                out Vector3 capsuleToBoxNormal,
                out penetration))
        {
            normal =
                -capsuleToBoxNormal;

            return true;
        }

        point =
            Vector3.Zero;

        normal =
            Vector3.UnitY;

        penetration =
            0.0f;

        return false;
    }

    private static bool BoxBox(
        OrientedBox first,
        OrientedBox second,
        out Vector3 point,
        out Vector3 normal,
        out float penetration)
    {
        Vector3[] firstAxes =
        {
            first.AxisX,
            first.AxisY,
            first.AxisZ
        };

        Vector3[] secondAxes =
        {
            second.AxisX,
            second.AxisY,
            second.AxisZ
        };

        float[] firstHalf =
        {
            first.HalfSize.X,
            first.HalfSize.Y,
            first.HalfSize.Z
        };

        float[] secondHalf =
        {
            second.HalfSize.X,
            second.HalfSize.Y,
            second.HalfSize.Z
        };

        float[,] rotation =
            new float[3, 3];

        float[,] absoluteRotation =
            new float[3, 3];

        for (int row =
                 0;
             row <
             3;
             row++)
        {
            for (int column =
                     0;
                 column <
                 3;
                 column++)
            {
                rotation[row, column] =
                    Vector3.Dot(
                        firstAxes[row],
                        secondAxes[column]);

                absoluteRotation[row, column] =
                    MathF.Abs(
                        rotation[row, column]) +
                    0.00001f;
            }
        }

        Vector3 centerDelta =
            second.Center -
            first.Center;

        float[] translated =
        {
            Vector3.Dot(
                centerDelta,
                firstAxes[0]),

            Vector3.Dot(
                centerDelta,
                firstAxes[1]),

            Vector3.Dot(
                centerDelta,
                firstAxes[2])
        };

        float bestPenetration =
            float.PositiveInfinity;

        Vector3 bestAxis =
            Vector3.UnitY;

        bool TestAxis(
            Vector3 axis,
            float distance,
            float firstRadius,
            float secondRadius,
            float normalization =
                1.0f)
        {
            float overlap =
                firstRadius +
                secondRadius -
                MathF.Abs(
                    distance);

            if (overlap <
                0.0f)
            {
                return false;
            }

            if (normalization <=
                Epsilon)
            {
                return true;
            }

            float normalizedOverlap =
                overlap /
                normalization;

            if (normalizedOverlap <
                bestPenetration)
            {
                Vector3 candidate =
                    axis /
                    normalization;

                if (Vector3.Dot(
                        candidate,
                        centerDelta) <
                    0.0f)
                {
                    candidate =
                        -candidate;
                }

                bestPenetration =
                    normalizedOverlap;

                bestAxis =
                    candidate;
            }

            return true;
        }

        for (int index =
                 0;
             index <
             3;
             index++)
        {
            float firstRadius =
                firstHalf[index];

            float secondRadius =
                secondHalf[0] *
                    absoluteRotation[index, 0] +
                secondHalf[1] *
                    absoluteRotation[index, 1] +
                secondHalf[2] *
                    absoluteRotation[index, 2];

            if (!TestAxis(
                    firstAxes[index],
                    translated[index],
                    firstRadius,
                    secondRadius))
            {
                return NoContact(
                    out point,
                    out normal,
                    out penetration);
            }
        }

        for (int index =
                 0;
             index <
             3;
             index++)
        {
            float firstRadius =
                firstHalf[0] *
                    absoluteRotation[0, index] +
                firstHalf[1] *
                    absoluteRotation[1, index] +
                firstHalf[2] *
                    absoluteRotation[2, index];

            float secondRadius =
                secondHalf[index];

            float distance =
                Vector3.Dot(
                    centerDelta,
                    secondAxes[index]);

            if (!TestAxis(
                    secondAxes[index],
                    distance,
                    firstRadius,
                    secondRadius))
            {
                return NoContact(
                    out point,
                    out normal,
                    out penetration);
            }
        }

        for (int firstAxis =
                 0;
             firstAxis <
             3;
             firstAxis++)
        {
            for (int secondAxis =
                     0;
                 secondAxis <
                 3;
                 secondAxis++)
            {
                Vector3 axis =
                    Vector3.Cross(
                        firstAxes[firstAxis],
                        secondAxes[secondAxis]);

                float axisLength =
                    axis.Length();

                if (axisLength <=
                    0.0001f)
                {
                    continue;
                }

                int firstNext =
                    (
                        firstAxis +
                        1
                    ) %
                    3;

                int firstOther =
                    (
                        firstAxis +
                        2
                    ) %
                    3;

                int secondNext =
                    (
                        secondAxis +
                        1
                    ) %
                    3;

                int secondOther =
                    (
                        secondAxis +
                        2
                    ) %
                    3;

                float firstRadius =
                    firstHalf[firstNext] *
                        absoluteRotation[firstOther, secondAxis] +
                    firstHalf[firstOther] *
                        absoluteRotation[firstNext, secondAxis];

                float secondRadius =
                    secondHalf[secondNext] *
                        absoluteRotation[firstAxis, secondOther] +
                    secondHalf[secondOther] *
                        absoluteRotation[firstAxis, secondNext];

                float distance =
                    MathF.Abs(
                        translated[firstOther] *
                            rotation[firstNext, secondAxis] -
                        translated[firstNext] *
                            rotation[firstOther, secondAxis]);

                if (!TestAxis(
                        axis,
                        distance,
                        firstRadius,
                        secondRadius,
                        axisLength))
                {
                    return NoContact(
                        out point,
                        out normal,
                        out penetration);
                }
            }
        }

        normal =
            bestAxis;

        penetration =
            float.IsFinite(
                bestPenetration)
                ? bestPenetration
                : 0.0f;

        Vector3 firstSurface =
            ClosestPointOnBox(
                first,
                second.Center);

        Vector3 secondSurface =
            ClosestPointOnBox(
                second,
                first.Center);

        point =
            (
                firstSurface +
                secondSurface
            ) *
            0.5f;

        return true;
    }

    private static bool CapsuleCapsule(
        Capsule first,
        Capsule second,
        out Vector3 point,
        out Vector3 normal,
        out float penetration)
    {
        ClosestPointsOnSegments(
            first.A,
            first.B,
            second.A,
            second.B,
            out Vector3 firstPoint,
            out Vector3 secondPoint);

        Vector3 delta =
            secondPoint -
            firstPoint;

        float distanceSquared =
            delta.LengthSquared();

        float radius =
            first.Radius +
            second.Radius;

        if (distanceSquared >
            radius *
            radius)
        {
            return NoContact(
                out point,
                out normal,
                out penetration);
        }

        float distance =
            MathF.Sqrt(
                Math.Max(
                    distanceSquared,
                    0.0f));

        normal =
            distance >
            Epsilon
                ? delta /
                  distance
                : Vector3.UnitY;

        penetration =
            radius -
            distance;

        Vector3 firstSurface =
            firstPoint +
            normal *
            first.Radius;

        Vector3 secondSurface =
            secondPoint -
            normal *
            second.Radius;

        point =
            (
                firstSurface +
                secondSurface
            ) *
            0.5f;

        return true;
    }

    private static bool CapsuleBox(
        Capsule capsule,
        OrientedBox box,
        out Vector3 point,
        out Vector3 normal,
        out float penetration)
    {
        float lower =
            0.0f;

        float upper =
            1.0f;

        /*
         * Squared distance from a segment to a convex box is convex along the
         * segment parameter. A short ternary search is stable and keeps this
         * core implementation compact while still handling rotated boxes.
         */
        for (int iteration =
                 0;
             iteration <
             14;
             iteration++)
        {
            float firstT =
                lower +
                (
                    upper -
                    lower
                ) /
                3.0f;

            float secondT =
                upper -
                (
                    upper -
                    lower
                ) /
                3.0f;

            float firstDistance =
                DistanceSquaredToBox(
                    Vector3.Lerp(
                        capsule.A,
                        capsule.B,
                        firstT),
                    box);

            float secondDistance =
                DistanceSquaredToBox(
                    Vector3.Lerp(
                        capsule.A,
                        capsule.B,
                        secondT),
                    box);

            if (firstDistance <
                secondDistance)
            {
                upper =
                    secondT;
            }
            else
            {
                lower =
                    firstT;
            }
        }

        float t =
            (
                lower +
                upper
            ) *
            0.5f;

        Vector3 segmentPoint =
            Vector3.Lerp(
                capsule.A,
                capsule.B,
                t);

        Vector3 boxPoint =
            ClosestPointOnBox(
                box,
                segmentPoint);

        Vector3 fromBoxToCapsule =
            segmentPoint -
            boxPoint;

        float distanceSquared =
            fromBoxToCapsule.LengthSquared();

        if (distanceSquared >
            capsule.Radius *
            capsule.Radius)
        {
            return NoContact(
                out point,
                out normal,
                out penetration);
        }

        float distance =
            MathF.Sqrt(
                Math.Max(
                    distanceSquared,
                    0.0f));

        if (distance >
            Epsilon)
        {
            /*
             * Required orientation is capsule -> box.
             */
            normal =
                -fromBoxToCapsule /
                distance;

            penetration =
                capsule.Radius -
                distance;

            point =
                boxPoint;

            return true;
        }

        /*
         * Capsule spine is inside/on the box. Pick the nearest box face and
         * push the capsule outward through that face.
         */
        Vector3 relative =
            segmentPoint -
            box.Center;

        float localX =
            Vector3.Dot(
                relative,
                box.AxisX);

        float localY =
            Vector3.Dot(
                relative,
                box.AxisY);

        float localZ =
            Vector3.Dot(
                relative,
                box.AxisZ);

        float faceX =
            box.HalfSize.X -
            MathF.Abs(
                localX);

        float faceY =
            box.HalfSize.Y -
            MathF.Abs(
                localY);

        float faceZ =
            box.HalfSize.Z -
            MathF.Abs(
                localZ);

        Vector3 outward;
        float faceDistance;

        if (faceX <=
                faceY &&
            faceX <=
                faceZ)
        {
            outward =
                box.AxisX *
                (
                    localX >=
                    0.0f
                        ? 1.0f
                        : -1.0f
                );

            faceDistance =
                faceX;
        }
        else if (faceY <=
                 faceZ)
        {
            outward =
                box.AxisY *
                (
                    localY >=
                    0.0f
                        ? 1.0f
                        : -1.0f
                );

            faceDistance =
                faceY;
        }
        else
        {
            outward =
                box.AxisZ *
                (
                    localZ >=
                    0.0f
                        ? 1.0f
                        : -1.0f
                );

            faceDistance =
                faceZ;
        }

        normal =
            -outward;

        penetration =
            capsule.Radius +
            Math.Max(
                faceDistance,
                0.0f);

        point =
            segmentPoint -
            outward *
            Math.Max(
                faceDistance,
                0.0f);

        return true;
    }

    private static OrientedBox CreateBox(
        BoxCollider3D box)
    {
        Vector3 scale =
            Vector3.Abs(
                box.Transform.WorldScale);

        Vector3 halfSize =
            Vector3.Abs(
                box.Size) *
            scale *
            0.5f;

        return
            new OrientedBox(
                Vector3.Transform(
                    box.Center,
                    box.Transform.WorldMatrix),
                SafeNormalize(
                    box.Transform.Right,
                    Vector3.UnitX),
                SafeNormalize(
                    box.Transform.Up,
                    Vector3.UnitY),
                SafeNormalize(
                    box.Transform.Forward,
                    -Vector3.UnitZ),
                halfSize);
    }

    private static Capsule CreateCapsule(
        CapsuleCollider3D capsule)
    {
        Vector3 scale =
            Vector3.Abs(
                capsule.Transform.WorldScale);

        float radius =
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

        Vector3 center =
            Vector3.Transform(
                capsule.Center,
                capsule.Transform.WorldMatrix);

        Vector3 up =
            SafeNormalize(
                capsule.Transform.Up,
                Vector3.UnitY);

        return
            new Capsule(
                center -
                    up *
                    halfSegment,
                center +
                    up *
                    halfSegment,
                radius);
    }

    private static Bounds3D CalculateBounds(
        Collider3D collider)
    {
        if (collider is
            BoxCollider3D box)
        {
            OrientedBox shape =
                CreateBox(
                    box);

            Vector3 extent =
                Vector3.Abs(
                    shape.AxisX) *
                    shape.HalfSize.X +
                Vector3.Abs(
                    shape.AxisY) *
                    shape.HalfSize.Y +
                Vector3.Abs(
                    shape.AxisZ) *
                    shape.HalfSize.Z;

            return
                new Bounds3D(
                    shape.Center -
                        extent,
                    shape.Center +
                        extent);
        }

        if (collider is
            CapsuleCollider3D capsule)
        {
            Capsule shape =
                CreateCapsule(
                    capsule);

            Vector3 minimum =
                Vector3.Min(
                    shape.A,
                    shape.B) -
                new Vector3(
                    shape.Radius);

            Vector3 maximum =
                Vector3.Max(
                    shape.A,
                    shape.B) +
                new Vector3(
                    shape.Radius);

            return
                new Bounds3D(
                    minimum,
                    maximum);
        }

        Vector3 position =
            collider.Transform.WorldPosition;

        return
            new Bounds3D(
                position,
                position);
    }

    private static Vector3 ClosestPointOnBox(
        OrientedBox box,
        Vector3 point)
    {
        Vector3 delta =
            point -
            box.Center;

        Vector3 result =
            box.Center;

        result +=
            box.AxisX *
            Math.Clamp(
                Vector3.Dot(
                    delta,
                    box.AxisX),
                -box.HalfSize.X,
                box.HalfSize.X);

        result +=
            box.AxisY *
            Math.Clamp(
                Vector3.Dot(
                    delta,
                    box.AxisY),
                -box.HalfSize.Y,
                box.HalfSize.Y);

        result +=
            box.AxisZ *
            Math.Clamp(
                Vector3.Dot(
                    delta,
                    box.AxisZ),
                -box.HalfSize.Z,
                box.HalfSize.Z);

        return result;
    }

    private static float DistanceSquaredToBox(
        Vector3 point,
        OrientedBox box)
    {
        Vector3 closest =
            ClosestPointOnBox(
                box,
                point);

        return
            Vector3.DistanceSquared(
                point,
                closest);
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

        float firstT;
        float secondT;

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
            firstT =
                0.0f;

            secondT =
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
                secondT =
                    0.0f;

                firstT =
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

                firstT =
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

                secondT =
                    (
                        cross *
                            firstT +
                        secondProjection
                    ) /
                    secondLengthSquared;

                if (secondT <
                    0.0f)
                {
                    secondT =
                        0.0f;

                    firstT =
                        Math.Clamp(
                            -firstProjection /
                            firstLengthSquared,
                            0.0f,
                            1.0f);
                }
                else if (secondT >
                         1.0f)
                {
                    secondT =
                        1.0f;

                    firstT =
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
            firstT;

        secondPoint =
            secondA +
            secondDirection *
            secondT;
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

    private static bool NoContact(
        out Vector3 point,
        out Vector3 normal,
        out float penetration)
    {
        point =
            Vector3.Zero;

        normal =
            Vector3.UnitY;

        penetration =
            0.0f;

        return false;
    }

    private readonly record struct ColliderEntry(
        GameObject GameObject,
        Collider3D Collider,
        Rigidbody3D? Body,
        Bounds3D Bounds);

    private readonly record struct OrientedBox(
        Vector3 Center,
        Vector3 AxisX,
        Vector3 AxisY,
        Vector3 AxisZ,
        Vector3 HalfSize);

    private readonly record struct Capsule(
        Vector3 A,
        Vector3 B,
        float Radius);

    private readonly record struct Bounds3D(
        Vector3 Minimum,
        Vector3 Maximum)
    {
        public bool Intersects(
            Bounds3D other)
        {
            return
                Minimum.X <=
                    other.Maximum.X &&
                Maximum.X >=
                    other.Minimum.X &&
                Minimum.Y <=
                    other.Maximum.Y &&
                Maximum.Y >=
                    other.Minimum.Y &&
                Minimum.Z <=
                    other.Maximum.Z &&
                Maximum.Z >=
                    other.Minimum.Z;
        }
    }

    private readonly record struct ContactKey(
        Guid FirstObject,
        string FirstCollider,
        Guid SecondObject,
        string SecondCollider)
    {
        public static ContactKey Create(
            PhysicsContactPair3D pair)
        {
            string firstType =
                pair.ColliderA
                    .GetType()
                    .FullName ??
                pair.ColliderA
                    .GetType()
                    .Name;

            string secondType =
                pair.ColliderB
                    .GetType()
                    .FullName ??
                pair.ColliderB
                    .GetType()
                    .Name;

            if (pair.A.Id.CompareTo(
                    pair.B.Id) <=
                0)
            {
                return
                    new ContactKey(
                        pair.A.Id,
                        firstType,
                        pair.B.Id,
                        secondType);
            }

            return
                new ContactKey(
                    pair.B.Id,
                    secondType,
                    pair.A.Id,
                    firstType);
        }
    }

    private readonly record struct SolverContactDiagnostics(
        string BodyAName,
        string BodyBName,
        float RestitutionUsed,
        float RelativeNormalVelocity,
        float NormalImpulseMagnitude,
        bool ImpulseApplied)
    {
        public static SolverContactDiagnostics ForTrigger(
            Rigidbody3D? first,
            Rigidbody3D? second)
        {
            return
                new SolverContactDiagnostics(
                    first?.GameObject.Name ??
                        "None (Static)",
                    second?.GameObject.Name ??
                        "None (Static)",
                    Math.Max(
                        first?.Restitution ??
                            0.0f,
                        second?.Restitution ??
                            0.0f),
                    0.0f,
                    0.0f,
                    false);
        }

        public static SolverContactDiagnostics NoImpulse(
            Rigidbody3D? first,
            Rigidbody3D? second,
            float restitution,
            float normalVelocity)
        {
            return
                new SolverContactDiagnostics(
                    first?.GameObject.Name ??
                        "None (Static)",
                    second?.GameObject.Name ??
                        "None (Static)",
                    restitution,
                    normalVelocity,
                    0.0f,
                    false);
        }

        public static SolverContactDiagnostics Applied(
            Rigidbody3D? first,
            Rigidbody3D? second,
            float restitution,
            float normalVelocity,
            float impulseMagnitude)
        {
            return
                new SolverContactDiagnostics(
                    first?.GameObject.Name ??
                        "None (Static)",
                    second?.GameObject.Name ??
                        "None (Static)",
                    restitution,
                    normalVelocity,
                    impulseMagnitude,
                    true);
        }
    }

    private enum ContactPhase
    {
        Enter,
        Stay,
        Exit
    }
}
