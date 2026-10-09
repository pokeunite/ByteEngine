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
/// Iterative linear/angular contacts, reusable spatial trees, sleeping and
/// opt-in swept collision protection. Specialized vehicle physics remains separate.
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

    // Reuse snapshots across steps; retain component order and all compound colliders.
    private readonly List<Rigidbody3D> _stepBodies = new();
    private readonly Dictionary<Rigidbody3D,Vector3> _framePositions=new();
    private readonly List<ColliderEntry> _stepColliders = new();

    private readonly List<int> _sweepOrder = new();
    private readonly List<(int First, int Second)> _candidatePairs = new();
    public int LastCandidateCount { get; private set; }
    public double LastColliderGatherMs {get;private set;}
    public double LastIndexUpdateMs {get;private set;}
    public double LastCandidateQueryMs {get;private set;}
    public int LastColliderCount { get; private set; }

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

    public int SolverIterations { get; set; } = 8;

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

        LastCandidateCount = 0;LastColliderGatherMs=LastIndexUpdateMs=LastCandidateQueryMs=0;
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

        _stepBodies.Clear();
        foreach (GameObject gameObject in scene.GameObjects)
        {
            if (!gameObject.ActiveInHierarchy) continue;
            foreach (Component component in gameObject.Components)
                if (component is Rigidbody3D { Enabled: true } body)
                    _stepBodies.Add(body);
        }
        List<Rigidbody3D> bodies = _stepBodies;
        _framePositions.Clear();foreach(var body in bodies)_framePositions[body]=body.Transform.WorldPosition;
        var joints=scene.GameObjects.Where(o=>o.ActiveInHierarchy).SelectMany(o=>o.Components.Where(c=>c.Enabled).OfType<IPhysicsConstraint3D>()).ToArray();

        _frameContacts.Clear();

        for (int step =
                 0;
             step <
             substeps;
             step++)
        {
            foreach (Rigidbody3D body
                     in bodies)
            {
                Vector3 before = body.Transform.WorldPosition;
                body.Integrate(stepDelta, Gravity);
                if (body.ContinuousCollision && !body.IsSleeping) SweepBody(scene, body, before);
            }

            for (int iteration = 0; iteration < Math.Clamp(SolverIterations, 1, 32); iteration++)
            {
                SolveContacts(scene);
                foreach (var joint in joints) joint.Solve(stepDelta);
                if (_frameContacts.Count==0 && joints.Length==0) break;
            }
        }

        var quietIslands=QuietContactIslands(bodies);
        var supportedBodies=new HashSet<Rigidbody3D>();foreach(var pair in _frameContacts.Values)if(!pair.IsTrigger){if(FindBodyForCollider(pair.A) is {} a)supportedBodies.Add(a);if(FindBodyForCollider(pair.B) is {} b)supportedBodies.Add(b);}
        foreach (Rigidbody3D body
                 in bodies)
        {
            bool supported = supportedBodies.Contains(body);
            body.UpdateSleep(safeDelta, supported, supported && quietIslands.Contains(body));
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
    }

    private HashSet<Rigidbody3D> QuietContactIslands(List<Rigidbody3D> bodies)
    {
        var parents=bodies.Where(b=>b.BodyType==RigidbodyBodyType3D.Dynamic).ToDictionary(b=>b,b=>b);
        Rigidbody3D Root(Rigidbody3D b){while(parents[b]!=b){parents[b]=parents[parents[b]];b=parents[b];}return b;}
        foreach(var contact in _frameContacts.Values)
        {if(contact.IsTrigger)continue;var a=FindBodyForCollider(contact.A);var b=FindBodyForCollider(contact.B);if(a!=null&&b!=null&&parents.ContainsKey(a)&&parents.ContainsKey(b))parents[Root(a)]=Root(b);}
        var noisy=new HashSet<Rigidbody3D>();
        foreach(var body in parents.Keys)
            if(!body.AllowSleep||body.AngularVelocity.LengthSquared()>.0025f||Vector3.DistanceSquared(body.Transform.WorldPosition,_framePositions[body])>.00000025f)noisy.Add(Root(body));
        return parents.Keys.Where(b=>!noisy.Contains(Root(b))).ToHashSet();
    }

    private static int ComponentOrdinal(GameObject obj,Component component)
    {for(int i=0;i<obj.Components.Count;i++)if(ReferenceEquals(obj.Components[i],component))return i;return -1;}

    private void SweepBody(RuntimeScene scene, Rigidbody3D body, Vector3 before)
    {
        Vector3 after = body.Transform.WorldPosition;
        Vector3 movement = after - before;
        float length = movement.Length();
        if (length < .00001f) return;
        body.Transform.WorldPosition = before;
        float allowed = length;
        foreach (var obj in scene.GameObjects)
            if (ReferenceEquals(FindBodyForCollider(obj), body))
                foreach (var collider in obj.Components.OfType<Collider3D>())
                {
                    if (!collider.Enabled || collider.IsTrigger) continue;
                    var bounds = CalculateBounds(collider);
                    Vector3 half = (bounds.Maximum - bounds.Minimum) * .5f;
                    // Capsule endpoints preserve the complete long shape. Boxes use a conservative enclosing sphere,
                    // so CCD cannot miss an outer corner; elongated boxes may stop before exact contact.
                    Vector3 start=(bounds.Minimum+bounds.Maximum)*.5f,end=start;
                    float radius=half.Length();
                    if(collider is CapsuleCollider3D capsule){var shape=CreateCapsule(capsule);start=shape.A;end=shape.B;radius=shape.Radius;}
                    if (GameplayQuery3D.CapsuleCast(scene, start,end, movement, radius, out var hit,
                        allowed, body.GameObject, source: obj, includeTriggers: false))
                    {
                        allowed = Math.Max(0, hit.Distance - .001f);
                        var pair = new PhysicsContactPair3D(obj, collider, hit.GameObject, hit.Collider, hit.Point, -hit.Normal, 0, false);
                        _frameContacts[ContactKey.Create(pair)] = pair;
                        float incoming = Vector3.Dot(body.Velocity, hit.Normal);
                        if (incoming < 0) body.Velocity -= (1 + body.Restitution) * incoming * hit.Normal;
                    }
                }
        body.Transform.WorldPosition = before + movement * (allowed / length);
    }

    public void Reset()
    {
        _stepBodies.Clear();
        _framePositions.Clear();_staticIndex.Clear();_movingIndex.Clear();
        _stepColliders.Clear();
        _previousContacts.Clear();
        _frameContacts.Clear();
    }

    private void SolveContacts(
        RuntimeScene scene)
    {
        long gatherStart=System.Diagnostics.Stopwatch.GetTimestamp();
        _stepColliders.Clear();
        foreach (GameObject gameObject in scene.GameObjects)
        {
            if (!gameObject.ActiveInHierarchy) continue;
            foreach (Component component in gameObject.Components)
                if (component is Collider3D { Enabled: true } collider)
                    _stepColliders.Add(new ColliderEntry(gameObject, collider,
                        FindBodyForCollider(gameObject), CalculateBounds(collider)));
        }
        LastColliderGatherMs+=System.Diagnostics.Stopwatch.GetElapsedTime(gatherStart).TotalMilliseconds;
        List<ColliderEntry> colliders = _stepColliders;

        BuildCandidatePairs(colliders);
        foreach (var candidate in _candidatePairs)
        {
                ColliderEntry first = colliders[candidate.First];
                ColliderEntry second = colliders[candidate.Second];
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

                if (!trigger)
                {
                    ResolveContact(
                        first,
                        second,
                        normalFromFirstToSecond,
                        penetration, point);
                }
        }
    }

    private readonly BoundsTree _staticIndex = new();
    private readonly BoundsTree _movingIndex = new();
    private readonly List<int> _queryResults = new();
    public int StaticIndexRebuilds => _staticIndex.Rebuilds;
    public int MovingIndexRebuilds => _movingIndex.Rebuilds;
    public int MovingIndexRefits => _movingIndex.Refits;

    private void BuildCandidatePairs(List<ColliderEntry> colliders)
    {
        _candidatePairs.Clear();
        LastColliderCount = colliders.Count;
        long indexStart=System.Diagnostics.Stopwatch.GetTimestamp();
        _staticIndex.Update(colliders, moving: false);
        _movingIndex.Update(colliders, moving: true);
        LastIndexUpdateMs+=System.Diagnostics.Stopwatch.GetElapsedTime(indexStart).TotalMilliseconds;
        long queryStart=System.Diagnostics.Stopwatch.GetTimestamp();
        for (int a = 0; a < colliders.Count; a++)
        {
            _queryResults.Clear();
            _staticIndex.Query(colliders[a].Bounds, _queryResults);
            _movingIndex.Query(colliders[a].Bounds, _queryResults);
            foreach (int b in _queryResults)
                if (b > a) _candidatePairs.Add((a, b));
        }
        _candidatePairs.Sort((a, b) => a.First != b.First ? a.First.CompareTo(b.First) : a.Second.CompareTo(b.Second));
        LastCandidateCount += _candidatePairs.Count;
        LastCandidateQueryMs+=System.Diagnostics.Stopwatch.GetElapsedTime(queryStart).TotalMilliseconds;
    }

    /// <summary>Conservative candidates in scene order. Refreshes bounds so editor moves and teleports are immediately visible.</summary>
    public IReadOnlyList<GameObject> QueryBounds(RuntimeScene scene, Vector3 minimum, Vector3 maximum)
    {
        if (!float.IsFinite(minimum.X + minimum.Y + minimum.Z + maximum.X + maximum.Y + maximum.Z))
            return scene.GameObjects;
        var entries = new List<ColliderEntry>();
        foreach (var obj in scene.GameObjects)
            if (obj.ActiveInHierarchy)
                foreach (var collider in obj.Components.OfType<Collider3D>())
                    if (collider.Enabled) entries.Add(new(obj, collider, FindBodyForCollider(obj), CalculateBounds(collider)));
        _staticIndex.Update(entries, false);
        _movingIndex.Update(entries, true);
        var results = new List<int>();
        var bounds = new Bounds3D(Vector3.Min(minimum, maximum), Vector3.Max(minimum, maximum));
        _staticIndex.Query(bounds, results);
        _movingIndex.Query(bounds, results);
        results.Sort();
        return results.Select(i => entries[i].GameObject).Distinct().ToArray();
    }

    // Median BVH: static nodes are retained until membership or bounds change.
    // Moving nodes live in a separate tree so moving one body does not rebuild scenery.
    private sealed class BoundsTree
    {
        private readonly List<(Collider3D Collider, Bounds3D Bounds, int Index)> _leaves = new();
        private Node? _root;
        public int Rebuilds { get; private set; }
        public int Refits { get; private set; }
        private int _refitsSinceBuild;
        public void Clear(){_leaves.Clear();_root=null;Rebuilds=Refits=_refitsSinceBuild=0;}
        private sealed class Node(Bounds3D bounds, int index, int leaf = -1, Node? left = null, Node? right = null)
        {
            public Bounds3D Bounds = bounds;
            public int Index = index;
            public readonly int Leaf = leaf;
            public readonly Node? Left = left, Right = right;
        }
        public void Update(List<ColliderEntry> source, bool moving)
        {
            int count = 0;
            bool changed = false, membershipChanged = false;
            for (int i = 0; i < source.Count; i++)
            {
                var item = source[i];
                bool isMoving = item.Body is { BodyType: not RigidbodyBodyType3D.Static };
                if (isMoving != moving) continue;
                var value = (item.Collider, item.Bounds, i);
                if (count >= _leaves.Count) { _leaves.Add(value); changed = membershipChanged = true; }
                else if (_leaves[count] != value)
                {
                    membershipChanged |= !ReferenceEquals(_leaves[count].Collider,item.Collider) || _leaves[count].Index != i;
                    _leaves[count] = value; changed = true;
                }
                count++;
            }
            if (count < _leaves.Count) { _leaves.RemoveRange(count, _leaves.Count - count); changed = membershipChanged = true; }
            if (!changed) return;
            // Preserve topology during motion; periodically rebalance to avoid a degraded tree.
            if (!membershipChanged && _root != null && _refitsSinceBuild < 60)
            {
                Refit(_root); Refits++; _refitsSinceBuild++; return;
            }
            _refitsSinceBuild = 0;
            var order = Enumerable.Range(0, count).ToArray();
            _root = Build(order, 0, count);
            Rebuilds++;
        }
        private Node? Build(int[] order, int start, int count)
        {
            if (count == 0) return null;
            var bounds = _leaves[order[start]].Bounds;
            for (int i = start + 1; i < start + count; i++)
            {
                var b = _leaves[order[i]].Bounds;
                bounds = new(Vector3.Min(bounds.Minimum, b.Minimum), Vector3.Max(bounds.Maximum, b.Maximum));
            }
            if (count == 1) return new(bounds, _leaves[order[start]].Index, order[start]);
            var span = bounds.Maximum - bounds.Minimum;
            int axis = span.Y > span.X ? 1 : 0;
            if (span.Z > (axis == 0 ? span.X : span.Y)) axis = 2;
            float Center(int index) { var b = _leaves[index].Bounds; var c = b.Minimum + b.Maximum; return axis == 0 ? c.X : axis == 1 ? c.Y : c.Z; }
            Array.Sort(order, start, count, Comparer<int>.Create((a,b) => { int c = Center(a).CompareTo(Center(b)); return c != 0 ? c : a.CompareTo(b); }));
            int half = count / 2;
            return new(bounds, -1, -1, Build(order, start, half), Build(order, start + half, count - half));
        }
        private Bounds3D Refit(Node node)
        {
            if (node.Leaf >= 0) return node.Bounds = _leaves[node.Leaf].Bounds;
            var left = Refit(node.Left!); var right = Refit(node.Right!);
            return node.Bounds = new(Vector3.Min(left.Minimum,right.Minimum),Vector3.Max(left.Maximum,right.Maximum));
        }
        public void Query(Bounds3D bounds, List<int> results) => Visit(_root, bounds, results);
        private static void Visit(Node? node, Bounds3D bounds, List<int> results)
        {
            if (node == null || !node.Bounds.Intersects(bounds)) return;
            if (node.Index >= 0) results.Add(node.Index);
            else { Visit(node.Left, bounds, results); Visit(node.Right, bounds, results); }
        }
    }

    private static void ResolveContact(
        ColliderEntry first,
        ColliderEntry second,
        Vector3 normal,
        float penetration, Vector3 point)
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
            return;
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
            firstBody?.VelocityAt(point) ??
            Vector3.Zero;

        Vector3 secondVelocity =
            secondBody?.VelocityAt(point) ??
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
            return;
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
            (inverseMassSum + (firstBody?.AngularImpulseDenominator(point, normal) ?? 0) +
                (secondBody?.AngularImpulseDenominator(point, normal) ?? 0));

        Vector3 impulse =
            normal *
            impulseMagnitude;

        firstBody?.ApplyContactImpulse(
            impulse, point);

        secondBody?.ApplyContactImpulse(
            -impulse, point);

        /*
         * Coulomb friction from the remaining tangential relative velocity.
         */
        firstVelocity =
            firstBody?.VelocityAt(point) ??
            Vector3.Zero;

        secondVelocity =
            secondBody?.VelocityAt(point) ??
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
            return;
        }

        tangent /=
            MathF.Sqrt(
                tangentLengthSquared);

        float tangentImpulseMagnitude =
            -Vector3.Dot(
                relativeVelocity,
                tangent) /
            (inverseMassSum + (firstBody?.AngularImpulseDenominator(point, tangent) ?? 0) +
                (secondBody?.AngularImpulseDenominator(point, tangent) ?? 0));

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

        firstBody?.ApplyContactImpulse(
            frictionImpulse, point);

        secondBody?.ApplyContactImpulse(
            -frictionImpulse, point);
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
        if (first is MeshCollider3D meshFirst && second is not HeightfieldCollider3D)
            return MeshContacts.Contact(meshFirst,second,out point,out normal,out penetration);
        if(second is MeshCollider3D meshSecond && first is not HeightfieldCollider3D)
        {bool result=MeshContacts.Contact(meshSecond,first,out point,out normal,out penetration);normal=-normal;return result;}
        if (first is HeightfieldCollider3D terrainFirst)
            return TerrainContact(terrainFirst, second, out point, out normal, out penetration);
        if (second is HeightfieldCollider3D terrainSecond)
        {
            bool contact=TerrainContact(terrainSecond, first, out point, out normal, out penetration);
            normal=-normal;return contact;
        }

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

    private static bool TerrainContact(HeightfieldCollider3D terrain, Collider3D collider,
        out Vector3 point, out Vector3 normal, out float penetration)
    {
        point=default;normal=Vector3.UnitY;penetration=0;
        if (!terrain.SupportedTransform) return false;
        if(collider is MeshCollider3D mesh && mesh.Geometry is {Convex:true} geometry)
        {
            bool found=false;foreach(var vertex in geometry.Points)if(terrain.TrySampleWorld(vertex,out var surface,out var up))
            {float depth=Vector3.Dot(surface-vertex,up);if(depth>=0&&(!found||depth>penetration)){found=true;point=surface;normal=up;penetration=depth;}}return found;
        }
        Span<Vector3> samples=stackalloc Vector3[9];int count=0;
        if(collider is BoxCollider3D box)
        {
            var b=CreateBox(box);
            for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)
                samples[count++]=b.Center+x*b.AxisX*b.HalfSize.X+y*b.AxisY*b.HalfSize.Y+z*b.AxisZ*b.HalfSize.Z;
            samples[count++]=b.Center-Vector3.UnitY*(Vector3.Abs(b.AxisX).Y*b.HalfSize.X+Vector3.Abs(b.AxisY).Y*b.HalfSize.Y+Vector3.Abs(b.AxisZ).Y*b.HalfSize.Z);
        }
        else if(collider is CapsuleCollider3D capsule)
        {
            var c=CreateCapsule(capsule);
            samples[count++]=c.A-Vector3.UnitY*c.Radius;samples[count++]=c.B-Vector3.UnitY*c.Radius;
        }
        for(int i=0;i<count;i++)if(terrain.TrySampleWorld(samples[i],out var surface,out var n))
        {
            float depth=Vector3.Dot(surface-samples[i],n);
            if(depth>penetration){penetration=depth;point=surface;normal=n;}
        }
        return penetration>0;
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
        if(collider is MeshCollider3D mesh){var bounds=mesh.WorldBounds;return new Bounds3D(bounds.Minimum,bounds.Maximum);}
        if (collider is HeightfieldCollider3D terrain)
        {
            var bounds=terrain.WorldTerrainBounds;
            return new Bounds3D(bounds.Minimum, bounds.Maximum);
        }
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
                pair.ColliderA.GetType().Name;
            firstType += ":" + ComponentOrdinal(pair.A,pair.ColliderA);

            string secondType =
                pair.ColliderB
                    .GetType()
                    .FullName ??
                pair.ColliderB.GetType().Name;
            secondType += ":" + ComponentOrdinal(pair.B,pair.ColliderB);

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

    private enum ContactPhase
    {
        Enter,
        Stay,
        Exit
    }
}
