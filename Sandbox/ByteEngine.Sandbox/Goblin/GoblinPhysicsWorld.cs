using System.Numerics;
using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.CollisionDetection;
using BepuPhysics.Constraints;
using BepuUtilities;
using BepuUtilities.Memory;
using ByteEngine.Core.Construction;
using ByteEngine.Core.Scene;

namespace ByteEngine.Sandbox.Goblin;

/// <summary>Vehicle-only joint simulation. Infantry remains outside BEPU.</summary>
public sealed class GoblinPhysicsWorld : IAssemblyConstraintAdapter, IDisposable
{
    private readonly BufferPool _pool = new();
    private readonly Simulation _simulation;
    private readonly Dictionary<Guid, BodyHandle> _bodies = new();
    private readonly Dictionary<Guid, BodyInertia> _inertias = new();
    private readonly Dictionary<Guid, GameObject> _visuals = new();
    private readonly Dictionary<Guid, AssemblyLink> _definitions = new();
    private readonly Dictionary<Guid, ConstraintHandle> _constraints = new();
    private readonly Dictionary<Guid, ConstraintHandle> _motors = new();
    private readonly Dictionary<Guid, Vector3> _motorAxes = new();
    private readonly Dictionary<ulong, int> _connectedPairs = new();
    private readonly HashSet<Guid> _frozen = new();
    private float _accumulator;
    private float _lastStep = 1f / 60f;
    private bool _disposed;

    public GoblinPhysicsWorld()
    {
        _simulation = Simulation.Create(_pool, new Contacts(_connectedPairs),
            new Gravity(new Vector3(0, -9.81f, 0)), new SolveDescription(8, 2));
        _simulation.Statics.Add(new StaticDescription(new Vector3(0, -.5f, 0),
            _simulation.Shapes.Add(new Box(100, 1, 100))));
    }

    public void AddBox(Guid id, GameObject visual, Vector3 size, float mass)
    {
        if (id == Guid.Empty || _bodies.ContainsKey(id) || mass <= 0)
            throw new ArgumentException("Invalid part body.", nameof(id));
        var shape = new Box(size.X, size.Y, size.Z);
        BodyInertia inertia = shape.ComputeInertia(mass);
        BodyHandle handle = _simulation.Bodies.Add(BodyDescription.CreateDynamic(
            new RigidPose(visual.Transform.WorldPosition, visual.Transform.WorldRotation),
            inertia, _simulation.Shapes.Add(shape), .01f));
        _bodies.Add(id, handle);
        _inertias.Add(id, inertia);
        _visuals.Add(id, visual);
    }

    public void AddWheel(Guid id, GameObject visual, float radius, float width, float mass)
    {
        if (id == Guid.Empty || _bodies.ContainsKey(id) || radius <= 0 || width <= 0 || mass <= 0)
            throw new ArgumentException("Invalid wheel body.", nameof(id));
        var shape = new Cylinder(radius, width);
        BodyInertia inertia = shape.ComputeInertia(mass);
        Quaternion orientation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, -MathF.PI / 2);
        visual.Transform.WorldRotation = orientation;
        BodyHandle handle = _simulation.Bodies.Add(BodyDescription.CreateDynamic(
            new RigidPose(visual.Transform.WorldPosition, orientation),
            inertia, _simulation.Shapes.Add(shape), .01f));
        _bodies.Add(id, handle);
        _inertias.Add(id, inertia);
        _visuals.Add(id, visual);
    }

    public void AddBall(Guid id, GameObject visual, float radius, float mass,
        Vector3 launchVelocity)
    {
        if (id == Guid.Empty || _bodies.ContainsKey(id) || radius <= 0 || mass <= 0)
            throw new ArgumentException("Invalid projectile body.", nameof(id));
        var shape = new Sphere(radius);
        BodyInertia inertia = shape.ComputeInertia(mass);
        BodyHandle handle = _simulation.Bodies.Add(BodyDescription.CreateDynamic(
            new RigidPose(visual.Transform.WorldPosition, Quaternion.Identity),
            inertia, _simulation.Shapes.Add(shape), .01f));
        _simulation.Bodies[handle].Velocity = new BodyVelocity(launchVelocity);
        _bodies.Add(id, handle);
        _inertias.Add(id, inertia);
        _visuals.Add(id, visual);
    }

    public void RemoveBody(Guid id)
    {
        if (!_bodies.TryGetValue(id, out BodyHandle handle)) return;
        if (_definitions.Values.Any(link => link.PartA == id || link.PartB == id))
            throw new InvalidOperationException("Disconnect a part before removing its body.");
        _bodies.Remove(id);
        _frozen.Remove(id);
        _simulation.Bodies.Remove(handle);
        _inertias.Remove(id);
        _visuals.Remove(id);
    }

    public StaticHandle AddObstacle(Vector3 center, Vector3 size) =>
        _simulation.Statics.Add(new StaticDescription(center,
            _simulation.Shapes.Add(new Box(size.X, size.Y, size.Z))));

    public void RemoveObstacle(StaticHandle handle) => _simulation.Statics.Remove(handle);

    public void Create(AssemblyLink link)
    {
        _definitions.Add(link.Id, link);
        if (_frozen.Count == 0) AddConstraint(link);
    }

    public void Destroy(Guid linkId)
    {
        RemoveConstraint(linkId);
        _definitions.Remove(linkId);
    }

    public void SetBuildingMode(Guid partId, bool frozen)
    {
        if (!_bodies.TryGetValue(partId, out BodyHandle handle))
            throw new KeyNotFoundException("Part has no physics body.");
        if (frozen)
        {
            if (_frozen.Count == 0)
                foreach (Guid id in _constraints.Keys.ToArray()) RemoveConstraint(id);
            if (!_frozen.Add(partId)) return;
            _simulation.Bodies[handle].BecomeKinematic();
            _simulation.Bodies[handle].Velocity = default;
        }
        else if (_frozen.Remove(partId))
        {
            BodyInertia inertia = _inertias[partId];
            _simulation.Bodies[handle].SetLocalInertia(in inertia);
            if (_frozen.Count == 0)
                foreach (AssemblyLink link in _definitions.Values) AddConstraint(link);
        }
    }

    public AssemblyLoad Measure(Guid linkId)
    {
        if (!_constraints.TryGetValue(linkId, out ConstraintHandle handle))
            return default;
        float magnitude = _simulation.Solver.GetAccumulatedImpulseMagnitude(handle);
        return new AssemblyLoad(magnitude / _lastStep, 0);
    }

    public void SetWheelSpeed(float radiansPerSecond)
    {
        foreach ((Guid id, ConstraintHandle motor) in _motors)
        {
            if (!string.Equals(_definitions[id].Channel, "WheelAxle",
                    StringComparison.OrdinalIgnoreCase)) continue;
            SetMotorSpeed(id, motor, radiansPerSecond);
        }
    }

    public void SetSawSpeed(float radiansPerSecond)
    {
        foreach ((Guid id, ConstraintHandle motor) in _motors)
        {
            if (!string.Equals(_definitions[id].Channel, "SawAxle",
                    StringComparison.OrdinalIgnoreCase)) continue;
            SetMotorSpeed(id, motor, radiansPerSecond);
        }
    }

    private void SetMotorSpeed(Guid id, ConstraintHandle motor, float radiansPerSecond)
    {
            _simulation.Solver.ApplyDescription(motor, new AngularAxisMotor
            {
                LocalAxisA = _motorAxes[id],
                TargetVelocity = radiansPerSecond,
                Settings = new MotorSettings(100, .001f)
            });
    }

    public void SetSteering(Guid cockpit, float yawRate)
    {
        BodyReference body = _simulation.Bodies[_bodies[cockpit]];
        BodyVelocity velocity = body.Velocity;
        velocity.Angular.Y = yawRate;
        body.Velocity = velocity;
    }

    public Vector3 Position(Guid partId) => _simulation.Bodies[_bodies[partId]].Pose.Position;
    public Quaternion Orientation(Guid partId) => _simulation.Bodies[_bodies[partId]].Pose.Orientation;
    public Vector3 Velocity(Guid partId) => _simulation.Bodies[_bodies[partId]].Velocity.Linear;

    public void Step(float elapsed)
    {
        if (_frozen.Count != 0 || !float.IsFinite(elapsed) || elapsed <= 0) return;
        _accumulator = Math.Min(_accumulator + elapsed, .1f);
        const float fixedStep = 1f / 60f;
        while (_accumulator >= fixedStep)
        {
            _simulation.Timestep(fixedStep);
            _accumulator -= fixedStep;
            _lastStep = fixedStep;
        }
        foreach ((Guid id, BodyHandle handle) in _bodies)
        {
            RigidPose pose = _simulation.Bodies[handle].Pose;
            _visuals[id].Transform.WorldPosition = pose.Position;
            _visuals[id].Transform.WorldRotation = pose.Orientation;
        }
    }

    private void AddConstraint(AssemblyLink link)
    {
        BodyHandle a = _bodies[link.PartA], b = _bodies[link.PartB];
        RigidPose poseA = _simulation.Bodies[a].Pose;
        RigidPose poseB = _simulation.Bodies[b].Pose;
        Vector3 aPosition = poseA.Position;
        Vector3 bPosition = poseB.Position;
        Quaternion inverseA = Quaternion.Inverse(poseA.Orientation);
        if (link.PartA == link.PartB) throw new InvalidOperationException("Self joint.");
        // WheelAxle rotates, Spring articulates, and structural channels weld.
        bool wheel = string.Equals(link.Channel, "WheelAxle", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(link.Channel, "SawAxle", StringComparison.OrdinalIgnoreCase);
        bool spring = string.Equals(link.Channel, "Spring", StringComparison.OrdinalIgnoreCase);
        if (wheel)
        {
            Vector3 point = bPosition;
            ConstraintHandle hinge = _simulation.Solver.Add(a, b, new Hinge
            {
                LocalOffsetA = Vector3.Transform(point - aPosition, inverseA),
                LocalOffsetB = Vector3.Zero,
                LocalHingeAxisA = Vector3.Transform(Vector3.UnitX, inverseA),
                LocalHingeAxisB = Vector3.UnitY,
                SpringSettings = new SpringSettings(30, 1)
            });
            _constraints.Add(link.Id, hinge);
            Vector3 motorAxis = Vector3.Transform(Vector3.UnitX, inverseA);
            _motorAxes.Add(link.Id, motorAxis);
            _motors.Add(link.Id, _simulation.Solver.Add(a, b, new AngularAxisMotor
            {
                LocalAxisA = motorAxis,
                Settings = new MotorSettings(100, .001f),
                TargetVelocity = 0
            }));
        }
        else if (spring)
        {
            _constraints.Add(link.Id, _simulation.Solver.Add(a, b, new BallSocket
            {
                LocalOffsetA = Vector3.Transform(bPosition - aPosition, inverseA),
                LocalOffsetB = Vector3.Zero,
                SpringSettings = new SpringSettings(5, 1)
            }));
        }
        else
        {
            _constraints.Add(link.Id, _simulation.Solver.Add(a, b, new Weld
            {
                LocalOffset = Vector3.Transform(bPosition - aPosition, inverseA),
                LocalOrientation = Quaternion.Normalize(poseB.Orientation * inverseA),
                SpringSettings = new SpringSettings(30, 1)
            }));
        }
        ulong pair = PairKey(a, b);
        _connectedPairs[pair] = _connectedPairs.GetValueOrDefault(pair) + 1;
    }

    private void RemoveConstraint(Guid id)
    {
        if (_constraints.ContainsKey(id) && _definitions.TryGetValue(id, out AssemblyLink? link))
        {
            ulong pair = PairKey(_bodies[link.PartA], _bodies[link.PartB]);
            int remaining = _connectedPairs[pair] - 1;
            if (remaining == 0) _connectedPairs.Remove(pair);
            else _connectedPairs[pair] = remaining;
        }
        if (_motors.Remove(id, out ConstraintHandle motor))
            _simulation.Solver.Remove(motor);
        _motorAxes.Remove(id);
        if (_constraints.Remove(id, out ConstraintHandle joint))
            _simulation.Solver.Remove(joint);
    }

    private static ulong PairKey(BodyHandle a, BodyHandle b)
    {
        uint low = (uint)Math.Min(a.Value, b.Value);
        uint high = (uint)Math.Max(a.Value, b.Value);
        return ((ulong)low << 32) | high;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _simulation.Dispose();
        _pool.Clear();
        _disposed = true;
    }

    private struct Contacts : INarrowPhaseCallbacks
    {
        private readonly Dictionary<ulong, int> _connectedPairs;
        public Contacts(Dictionary<ulong, int> connectedPairs) => _connectedPairs = connectedPairs;
        public void Initialize(Simulation simulation) { }
        public bool AllowContactGeneration(int workerIndex, CollidableReference a,
            CollidableReference b, ref float speculativeMargin)
        {
            if (a.Mobility != CollidableMobility.Dynamic &&
                b.Mobility != CollidableMobility.Dynamic) return false;
            return a.Mobility == CollidableMobility.Static ||
                   b.Mobility == CollidableMobility.Static ||
                   !_connectedPairs.ContainsKey(PairKey(a.BodyHandle, b.BodyHandle));
        }
        public bool AllowContactGeneration(int workerIndex, CollidablePair pair,
            int childIndexA, int childIndexB) => true;
        public bool ConfigureContactManifold<TManifold>(int workerIndex, CollidablePair pair,
            ref TManifold manifold, out PairMaterialProperties material)
            where TManifold : unmanaged, IContactManifold<TManifold>
        {
            material = new PairMaterialProperties
            {
                FrictionCoefficient = 1.5f,
                MaximumRecoveryVelocity = 2,
                SpringSettings = new SpringSettings(30, 1)
            };
            return true;
        }
        public bool ConfigureContactManifold(int workerIndex, CollidablePair pair,
            int childIndexA, int childIndexB, ref ConvexContactManifold manifold) => true;
        public void Dispose() { }
    }

    private struct Gravity : IPoseIntegratorCallbacks
    {
        private readonly Vector3 _gravity;
        private Vector3Wide _gravityDt;
        public Gravity(Vector3 gravity) => _gravity = gravity;
        public AngularIntegrationMode AngularIntegrationMode => AngularIntegrationMode.Nonconserving;
        public bool AllowSubstepsForUnconstrainedBodies => false;
        public bool IntegrateVelocityForKinematics => false;
        public void Initialize(Simulation simulation) { }
        public void PrepareForIntegration(float dt) => _gravityDt = Vector3Wide.Broadcast(_gravity * dt);
        public void IntegrateVelocity(Vector<int> bodyIndices, Vector3Wide position,
            QuaternionWide orientation, BodyInertiaWide localInertia,
            Vector<int> integrationMask, int workerIndex, Vector<float> dt,
            ref BodyVelocityWide velocity) => velocity.Linear += _gravityDt;
    }
}
