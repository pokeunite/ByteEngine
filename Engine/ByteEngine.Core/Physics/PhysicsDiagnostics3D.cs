using System.Numerics;

namespace ByteEngine.Core.Physics;

/// <summary>
/// Read-only snapshot used by the editor to inspect what the physics solver
/// actually saw during the most recently completed physics frame.
/// </summary>
public sealed record PhysicsObjectDiagnostics3D(
    string SelectedObjectName,
    IReadOnlyList<string> ComponentTypes,
    PhysicsBodyDiagnostics3D? LocalBody,
    PhysicsBodyDiagnostics3D? ResolvedBody,
    IReadOnlyList<PhysicsColliderDiagnostics3D> Colliders,
    IReadOnlyList<PhysicsContactDiagnostics3D> Contacts);

public sealed record PhysicsBodyDiagnostics3D(
    string ObjectName,
    RigidbodyBodyType3D BodyType,
    bool Enabled,
    float Mass,
    bool UseGravity,
    float GravityScale,
    float LinearDamping,
    float Restitution,
    float Friction,
    Vector3 Velocity);

public sealed record PhysicsColliderDiagnostics3D(
    string ObjectName,
    string ColliderType,
    bool Enabled,
    bool IsTrigger,
    string ResolvedBodyObject,
    RigidbodyBodyType3D? ResolvedBodyType,
    float? ResolvedRestitution);

public sealed record PhysicsContactDiagnostics3D(
    string ColliderObject,
    string OtherObject,
    string ColliderType,
    string OtherColliderType,
    bool IsTrigger,
    Vector3 Point,
    Vector3 NormalTowardSelected,
    float Penetration,
    string BodyA,
    string BodyB,
    float RestitutionUsed,
    float RelativeNormalVelocity,
    float NormalImpulseMagnitude,
    bool SolverAppliedImpulse);
