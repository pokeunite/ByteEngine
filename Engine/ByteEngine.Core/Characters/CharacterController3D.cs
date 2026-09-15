using System.Numerics;

using ByteEngine.Core.Classification;
using ByteEngine.Core.Physics;
using ByteEngine.Core.Scene;

namespace ByteEngine.Core.Characters;

/// <summary>
/// Kinematic capsule-based character motor.
///
/// v0.10-B replaces the legacy ground-height snapping implementation with
/// shape queries against ByteEngine's collision system. The controller remains
/// the sole owner of character movement while an optional Rigidbody3D is kept
/// kinematic for physical interaction with dynamic bodies.
/// </summary>
public sealed class CharacterController3D
    : Component
{
    private const float MinimumSkinWidth =
        0.005f;

    private const float MaximumSkinWidth =
        0.05f;

    private const int MaximumSlideIterations =
        4;

    private const float PushImpulseFactor =
        0.18f;

    private Vector3 _moveInput;

    private bool _jumpQueued;

    private bool _wasGrounded;

    private float _timeSinceGrounded =
        float.PositiveInfinity;

    private float _jumpBufferRemaining;

    private Vector3 _groundLastPosition;

    private bool _hasGroundAnchor;

    private Rigidbody3D? _attachedRigidbody;

    private RigidbodyBodyType3D _authoredBodyType;

    private bool _overrodeBodyType;

    public LayerMask GroundCollisionMask { get; set; } =
        LayerMask.All;

    public bool IsGrounded { get; private set; }

    public bool IsFalling =>
        !IsGrounded &&
        VerticalVelocity <
            0.0f;

    public bool IsMoving =>
        new Vector2(
            Velocity.X,
            Velocity.Z).LengthSquared() >
        0.0001f;

    public bool JustLanded { get; private set; }

    public Vector3 Velocity { get; private set; }

    public float Speed =>
        new Vector2(
            Velocity.X,
            Velocity.Z).Length();

    public float VerticalVelocity =>
        Velocity.Y;

    public Vector3 GroundNormal { get; private set; } =
        Vector3.UnitY;

    public GameObject? GroundObject { get; private set; }

    public float MoveSpeed { get; set; } =
        5.0f;

    public float Acceleration { get; set; } =
        30.0f;

    public float Deceleration { get; set; } =
        35.0f;

    public float AirControl { get; set; } =
        0.35f;

    public float JumpForce { get; set; } =
        7.0f;

    public float Gravity { get; set; } =
        20.0f;

    /// <summary>
    /// Ground probe/snap distance below the capsule.
    /// </summary>
    public float GroundDistance { get; set; } =
        0.15f;

    public float MaxSlope { get; set; } =
        50.0f;

    public float StepHeight { get; set; } =
        0.3f;

    public float CoyoteTime { get; set; } =
        0.1f;

    public float JumpBuffer { get; set; } =
        0.1f;

    public bool SnapToGround { get; set; } =
        true;

    protected override void OnStart()
    {
        EnsureKinematicRigidbody();
    }

    protected override void OnStop()
    {
        if (_attachedRigidbody !=
                null &&
            _overrodeBodyType)
        {
            _attachedRigidbody.BodyType =
                _authoredBodyType;

            _attachedRigidbody.Velocity =
                Vector3.Zero;
        }

        _attachedRigidbody =
            null;

        _overrodeBodyType =
            false;

        _hasGroundAnchor =
            false;
    }

    public void Move(
        Vector3 direction)
    {
        _moveInput +=
            direction;
    }

    public void MoveForward(
        float amount = 1.0f)
    {
        Vector3 forward =
            Transform.Forward;

        forward.Y =
            0.0f;

        if (forward.LengthSquared() >
            0.0001f)
        {
            forward =
                Vector3.Normalize(
                    forward);
        }

        Move(
            forward *
            amount);
    }

    public void MoveRight(
        float amount = 1.0f)
    {
        Vector3 right =
            Transform.Right;

        right.Y =
            0.0f;

        if (right.LengthSquared() >
            0.0001f)
        {
            right =
                Vector3.Normalize(
                    right);
        }

        Move(
            right *
            amount);
    }

    public void Jump()
    {
        _jumpQueued =
            true;

        _jumpBufferRemaining =
            Math.Max(
                JumpBuffer,
                0.0f);
    }

    public void SetVelocity(
        Vector3 velocity)
    {
        Velocity =
            Finite(
                velocity);
    }

    public void AddImpulse(
        Vector3 impulse)
    {
        Velocity +=
            Finite(
                impulse);
    }

    protected override void OnUpdate()
    {
        float deltaTime =
            Math.Clamp(
                (float)Time.DeltaTime,
                0.0f,
                0.1f);

        if (deltaTime <=
            0.0f)
        {
            ClearFrameInput();

            return;
        }

        EnsureKinematicRigidbody();

        ApplyMovingPlatformDelta();

        JustLanded =
            false;

        ProbeGround(
            allowSnap:
                SnapToGround &&
                Velocity.Y <=
                    0.0f);

        if (IsGrounded)
        {
            _timeSinceGrounded =
                0.0f;
        }
        else
        {
            _timeSinceGrounded +=
                deltaTime;
        }

        UpdateHorizontalVelocity(
            deltaTime);

        TryConsumeJump();

        if (!IsGrounded)
        {
            Velocity =
                new Vector3(
                    Velocity.X,
                    Velocity.Y -
                        Math.Max(
                            Gravity,
                            0.0f) *
                        deltaTime,
                    Velocity.Z);
        }
        else if (Velocity.Y <
                 0.0f)
        {
            Velocity =
                new Vector3(
                    Velocity.X,
                    0.0f,
                    Velocity.Z);
        }

        Vector3 horizontalDisplacement =
            new(
                Velocity.X *
                    deltaTime,
                0.0f,
                Velocity.Z *
                    deltaTime);

        MoveHorizontal(
            horizontalDisplacement);

        Vector3 verticalDisplacement =
            Vector3.UnitY *
            (
                Velocity.Y *
                deltaTime
            );

        MoveVertical(
            verticalDisplacement);

        if (Velocity.Y <=
            0.0f)
        {
            ProbeGround(
                allowSnap:
                    SnapToGround);
        }

        JustLanded =
            IsGrounded &&
            !_wasGrounded;

        if (IsGrounded)
        {
            _timeSinceGrounded =
                0.0f;

            TryConsumeJump();
        }

        _jumpBufferRemaining =
            Math.Max(
                0.0f,
                _jumpBufferRemaining -
                deltaTime);

        UpdateGroundAnchor();

        if (_attachedRigidbody !=
            null)
        {
            /*
             * Kinematic bodies are not integrated by PhysicsWorld3D, but
             * carrying the motor velocity lets dynamic contact response know
             * how fast the character is moving.
             */
            _attachedRigidbody.Velocity =
                Velocity;
        }

        ClearFrameInput();

        _wasGrounded =
            IsGrounded;
    }

    private void UpdateHorizontalVelocity(
        float deltaTime)
    {
        Vector3 target =
            _moveInput;

        target.Y =
            0.0f;

        if (target.LengthSquared() >
            1.0f)
        {
            target =
                Vector3.Normalize(
                    target);
        }

        target *=
            Math.Max(
                MoveSpeed,
                0.0f);

        float control =
            IsGrounded
                ? 1.0f
                : Math.Clamp(
                    AirControl,
                    0.0f,
                    1.0f);

        float rate =
            target.LengthSquared() >
            0.001f
                ? Math.Max(
                    Acceleration,
                    0.0f)
                : Math.Max(
                    Deceleration,
                    0.0f);

        Velocity =
            new Vector3(
                Approach(
                    Velocity.X,
                    target.X,
                    rate *
                    control *
                    deltaTime),
                Velocity.Y,
                Approach(
                    Velocity.Z,
                    target.Z,
                    rate *
                    control *
                    deltaTime));
    }

    private void MoveHorizontal(
        Vector3 displacement)
    {
        if (displacement.LengthSquared() <=
            0.0000001f)
        {
            return;
        }

        Vector3 startingPosition =
            Transform.WorldPosition;

        if (IsGrounded &&
            StepHeight >
                0.0f &&
            CapsuleCastCurrent(
                displacement,
                displacement.Length() +
                    SkinWidth(),
                out RaycastHit3D initialHit) &&
            IsStepCandidate(
                initialHit))
        {
            if (TryStep(
                    displacement))
            {
                return;
            }

            /*
             * TryStep rolls back when it cannot find a valid landing.
             */
            Transform.WorldPosition =
                startingPosition;
        }

        Vector3 remaining =
            displacement;

        for (int iteration =
                 0;
             iteration <
             MaximumSlideIterations;
             iteration++)
        {
            float distance =
                remaining.Length();

            if (distance <=
                0.00001f)
            {
                break;
            }

            Vector3 direction =
                remaining /
                distance;

            if (!CapsuleCastCurrent(
                    direction,
                    distance +
                        SkinWidth(),
                    out RaycastHit3D hit))
            {
                Transform.WorldPosition +=
                    remaining;

                break;
            }

            float travel =
                Math.Clamp(
                    hit.Distance -
                        SkinWidth(),
                    0.0f,
                    distance);

            if (travel >
                0.0f)
            {
                Transform.WorldPosition +=
                    direction *
                    travel;
            }

            PushDynamicBody(
                hit);

            Vector3 leftover =
                remaining -
                direction *
                travel;

            float intoSurface =
                Vector3.Dot(
                    leftover,
                    hit.Normal);

            if (intoSurface <
                0.0f)
            {
                leftover -=
                    hit.Normal *
                    intoSurface;
            }

            if (!IsWalkableNormal(
                    hit.Normal) &&
                leftover.Y >
                    0.0f)
            {
                leftover.Y =
                    0.0f;
            }

            remaining =
                leftover *
                0.999f;

            if (travel <=
                    0.00001f &&
                remaining.LengthSquared() >=
                    displacement.LengthSquared() *
                    0.999f)
            {
                break;
            }
        }
    }

    private void MoveVertical(
        Vector3 displacement)
    {
        float distance =
            displacement.Length();

        if (distance <=
            0.00001f)
        {
            return;
        }

        Vector3 direction =
            displacement /
            distance;

        if (!CapsuleCastCurrent(
                direction,
                distance +
                    SkinWidth(),
                out RaycastHit3D hit))
        {
            Transform.WorldPosition +=
                displacement;

            ClearGroundIfRising();

            return;
        }

        float travel =
            Math.Clamp(
                hit.Distance -
                    SkinWidth(),
                0.0f,
                distance);

        if (travel >
            0.0f)
        {
            Transform.WorldPosition +=
                direction *
                travel;
        }

        if (direction.Y >
            0.0f)
        {
            Velocity =
                new Vector3(
                    Velocity.X,
                    0.0f,
                    Velocity.Z);

            ClearGround();

            return;
        }

        if (IsWalkableSurface(
                hit))
        {
            SetGround(
                hit);

            Velocity =
                new Vector3(
                    Velocity.X,
                    0.0f,
                    Velocity.Z);

            return;
        }

        /*
         * A downward movement can strike the side of a steep slope. Remove
         * only the velocity component directed into that surface so the motor
         * can continue sliding naturally.
         */
        float intoSurface =
            Vector3.Dot(
                Velocity,
                hit.Normal);

        if (intoSurface <
            0.0f)
        {
            Velocity -=
                hit.Normal *
                intoSurface;
        }

        ClearGround();
    }

    private bool TryStep(
        Vector3 horizontalDisplacement)
    {
        float horizontalDistance =
            horizontalDisplacement.Length();

        if (horizontalDistance <=
                0.00001f ||
            StepHeight <=
                0.0f)
        {
            return false;
        }

        Vector3 originalPosition =
            Transform.WorldPosition;

        float step =
            Math.Max(
                StepHeight,
                0.0f);

        Transform.WorldPosition +=
            Vector3.UnitY *
            step;

        if (HasBlockingOverlap())
        {
            Transform.WorldPosition =
                originalPosition;

            return false;
        }

        Vector3 direction =
            horizontalDisplacement /
            horizontalDistance;

        if (CapsuleCastCurrent(
                direction,
                horizontalDistance +
                    SkinWidth(),
                out _))
        {
            Transform.WorldPosition =
                originalPosition;

            return false;
        }

        Transform.WorldPosition +=
            horizontalDisplacement;

        float downDistance =
            step +
            Math.Max(
                GroundDistance,
                0.0f) +
            SkinWidth();

        if (!CapsuleCastCurrent(
                -Vector3.UnitY,
                downDistance,
                out RaycastHit3D groundHit) ||
            !IsWalkableSurface(
                groundHit))
        {
            Transform.WorldPosition =
                originalPosition;

            return false;
        }

        float landingTravel =
            Math.Max(
                groundHit.Distance -
                    SkinWidth(),
                0.0f);

        Transform.WorldPosition -=
            Vector3.UnitY *
            landingTravel;

        SetGround(
            groundHit);

        Velocity =
            new Vector3(
                Velocity.X,
                0.0f,
                Velocity.Z);

        return true;
    }

    private void ProbeGround(
        bool allowSnap)
    {
        if (Velocity.Y >
            0.01f)
        {
            ClearGround();

            return;
        }

        float maximumDistance =
            Math.Max(
                GroundDistance,
                0.0f) +
            SkinWidth();

        if (!CapsuleCastCurrent(
                -Vector3.UnitY,
                maximumDistance,
                out RaycastHit3D hit) ||
            !IsWalkableSurface(
                hit))
        {
            ClearGround();

            return;
        }

        SetGround(
            hit);

        if (allowSnap)
        {
            float travel =
                Math.Max(
                    hit.Distance -
                        SkinWidth(),
                    0.0f);

            if (travel >
                0.0f)
            {
                Transform.WorldPosition -=
                    Vector3.UnitY *
                    travel;
            }
        }

        if (Velocity.Y <
            0.0f)
        {
            Velocity =
                new Vector3(
                    Velocity.X,
                    0.0f,
                    Velocity.Z);
        }
    }

    private bool CapsuleCastCurrent(
        Vector3 direction,
        float maxDistance,
        out RaycastHit3D hit)
    {
        if (!TryGetCapsuleGeometry(
                out CapsuleGeometry shape) ||
            GameObject.Scene is not
                { } scene)
        {
            hit =
                default;

            return false;
        }

        return
            GameplayQuery3D.CapsuleCast(
                scene,
                shape.PointA,
                shape.PointB,
                direction,
                Math.Max(
                    shape.Radius -
                        SkinWidth(),
                    MinimumSkinWidth),
                out hit,
                maxDistance,
                GameObject,
                GroundCollisionMask,
                GameObject,
                false,
                false);
    }

    private bool HasBlockingOverlap()
    {
        if (!TryGetCapsuleGeometry(
                out CapsuleGeometry shape) ||
            GameObject.Scene is not
                { } scene)
        {
            return false;
        }

        return
            GameplayQuery3D.OverlapCapsule(
                scene,
                shape.PointA,
                shape.PointB,
                Math.Max(
                    shape.Radius -
                        SkinWidth(),
                    MinimumSkinWidth),
                GameObject,
                GroundCollisionMask,
                GameObject,
                false,
                false).Count >
            0;
    }

    private bool TryGetCapsuleGeometry(
        out CapsuleGeometry geometry)
    {
        Collider3D? collider =
            FindCharacterCollider();

        if (collider is
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

            geometry =
                new CapsuleGeometry(
                    center -
                        up *
                        halfSegment,
                    center +
                        up *
                        halfSegment,
                    radius);

            return true;
        }

        if (collider is
            BoxCollider3D box)
        {
            Vector3 scale =
                Vector3.Abs(
                    box.Transform.WorldScale);

            Vector3 size =
                Vector3.Abs(
                    box.Size) *
                scale;

            float radius =
                Math.Max(
                    size.X,
                    size.Z) *
                0.5f;

            float halfSegment =
                Math.Max(
                    0.0f,
                    size.Y *
                        0.5f -
                    radius);

            Vector3 center =
                Vector3.Transform(
                    box.Center,
                    box.Transform.WorldMatrix);

            Vector3 up =
                SafeNormalize(
                    box.Transform.Up,
                    Vector3.UnitY);

            geometry =
                new CapsuleGeometry(
                    center -
                        up *
                        halfSegment,
                    center +
                        up *
                        halfSegment,
                    Math.Max(
                        radius,
                        0.05f));

            return true;
        }

        geometry =
            new CapsuleGeometry(
                Transform.WorldPosition -
                    Vector3.UnitY *
                    0.55f,
                Transform.WorldPosition +
                    Vector3.UnitY *
                    0.55f,
                0.35f);

        return true;
    }

    private Collider3D? FindCharacterCollider()
    {
        Collider3D? local =
            GameObject.Components
                .OfType<Collider3D>()
                .FirstOrDefault(
                    component =>
                        component.Enabled &&
                        !component.IsTrigger);

        if (local !=
            null)
        {
            return local;
        }

        foreach (GameObject child
                 in Descendants(
                     GameObject))
        {
            Collider3D? childCollider =
                child.Components
                    .OfType<Collider3D>()
                    .FirstOrDefault(
                        component =>
                            component.Enabled &&
                            !component.IsTrigger);

            if (childCollider !=
                null)
            {
                return childCollider;
            }
        }

        return null;
    }

    private bool IsWalkableSurface(
        RaycastHit3D hit)
    {
        GroundSurface? surface =
            hit.GameObject.GetComponent<GroundSurface>();

        if (surface?.Walkable ==
            false)
        {
            return false;
        }

        return
            IsWalkableNormal(
                hit.Normal);
    }

    private bool IsWalkableNormal(
        Vector3 normal)
    {
        normal =
            SafeNormalize(
                normal,
                Vector3.UnitY);

        float maximumSlope =
            Math.Clamp(
                MaxSlope,
                0.0f,
                89.9f);

        float minimumUp =
            MathF.Cos(
                maximumSlope *
                MathF.PI /
                180.0f);

        return
            Vector3.Dot(
                normal,
                Vector3.UnitY) >=
            minimumUp;
    }

    private bool IsStepCandidate(
        RaycastHit3D hit)
    {
        if (hit.IsDefault())
        {
            return false;
        }

        float up =
            Vector3.Dot(
                hit.Normal,
                Vector3.UnitY);

        /*
         * Walkable surfaces are slopes rather than step faces.
         * A step candidate is predominantly vertical.
         */
        return
            up <
            MathF.Cos(
                Math.Clamp(
                    MaxSlope,
                    0.0f,
                    89.9f) *
                MathF.PI /
                180.0f);
    }

    private void PushDynamicBody(
        RaycastHit3D hit)
    {
        Rigidbody3D? body =
            FindRigidbodyInParents(
                hit.GameObject);

        if (body ==
                null ||
            !body.Enabled ||
            body.BodyType !=
                RigidbodyBodyType3D.Dynamic)
        {
            return;
        }

        Vector3 horizontalVelocity =
            new(
                Velocity.X,
                0.0f,
                Velocity.Z);

        float speedIntoBody =
            -Vector3.Dot(
                horizontalVelocity,
                hit.Normal);

        if (speedIntoBody <=
            0.0f)
        {
            return;
        }

        float effectiveMass =
            Math.Clamp(
                body.Mass,
                0.25f,
                8.0f);

        body.AddImpulse(
            -hit.Normal *
            speedIntoBody *
            effectiveMass *
            PushImpulseFactor);
    }

    private static Rigidbody3D? FindRigidbodyInParents(
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
                null)
            {
                return body;
            }
        }

        return null;
    }

    private void ApplyMovingPlatformDelta()
    {
        if (!IsGrounded ||
            GroundObject ==
                null ||
            !_hasGroundAnchor)
        {
            return;
        }

        Vector3 currentGroundPosition =
            GroundObject.Transform.WorldPosition;

        Vector3 delta =
            currentGroundPosition -
            _groundLastPosition;

        if (delta.LengthSquared() >
            0.0f)
        {
            Transform.WorldPosition +=
                delta;
        }
    }

    private void UpdateGroundAnchor()
    {
        if (!IsGrounded ||
            GroundObject ==
                null)
        {
            _hasGroundAnchor =
                false;

            return;
        }

        _groundLastPosition =
            GroundObject.Transform.WorldPosition;

        _hasGroundAnchor =
            true;
    }

    private void SetGround(
        RaycastHit3D hit)
    {
        IsGrounded =
            true;

        GroundObject =
            hit.GameObject;

        GroundNormal =
            SafeNormalize(
                hit.Normal,
                Vector3.UnitY);
    }

    private void ClearGround()
    {
        IsGrounded =
            false;

        GroundObject =
            null;

        GroundNormal =
            Vector3.UnitY;

        _hasGroundAnchor =
            false;
    }

    private void ClearGroundIfRising()
    {
        if (Velocity.Y >
            0.0f)
        {
            ClearGround();
        }
    }

    private void TryConsumeJump()
    {
        bool hasBufferedJump =
            _jumpQueued ||
            _jumpBufferRemaining >
                0.0f;

        bool canJump =
            IsGrounded ||
            _timeSinceGrounded <=
                Math.Max(
                    CoyoteTime,
                    0.0f);

        if (!hasBufferedJump ||
            !canJump)
        {
            return;
        }

        Velocity =
            new Vector3(
                Velocity.X,
                Math.Max(
                    JumpForce,
                    0.0f),
                Velocity.Z);

        ClearGround();

        _timeSinceGrounded =
            float.PositiveInfinity;

        _jumpQueued =
            false;

        _jumpBufferRemaining =
            0.0f;
    }

    private void EnsureKinematicRigidbody()
    {
        Rigidbody3D? body =
            GameObject.GetComponent<Rigidbody3D>();

        if (!ReferenceEquals(
                body,
                _attachedRigidbody))
        {
            _attachedRigidbody =
                body;

            _overrodeBodyType =
                false;

            if (body !=
                null)
            {
                _authoredBodyType =
                    body.BodyType;
            }
        }

        if (body ==
            null)
        {
            return;
        }

        if (body.BodyType !=
            RigidbodyBodyType3D.Kinematic)
        {
            if (!_overrodeBodyType)
            {
                _authoredBodyType =
                    body.BodyType;

                _overrodeBodyType =
                    true;
            }

            body.BodyType =
                RigidbodyBodyType3D.Kinematic;
        }

        /*
         * CharacterController3D owns movement. The body exists only so the
         * physics world can treat the character as a moving infinite-mass body
         * when interacting with dynamic rigidbodies.
         */
        body.ClearForces();
    }

    private float SkinWidth()
    {
        if (!TryGetCapsuleGeometry(
                out CapsuleGeometry geometry))
        {
            return 0.02f;
        }

        return
            Math.Clamp(
                geometry.Radius *
                    0.06f,
                MinimumSkinWidth,
                MaximumSkinWidth);
    }

    private void ClearFrameInput()
    {
        _moveInput =
            Vector3.Zero;

        _jumpQueued =
            false;
    }

    private static IEnumerable<GameObject> Descendants(
        GameObject root)
    {
        foreach (GameObject child
                 in root.Children)
        {
            yield return
                child;

            foreach (GameObject descendant
                     in Descendants(
                         child))
            {
                yield return
                    descendant;
            }
        }
    }

    private static Vector3 Finite(
        Vector3 value)
    {
        return
            new Vector3(
                float.IsFinite(
                    value.X)
                    ? value.X
                    : 0.0f,
                float.IsFinite(
                    value.Y)
                    ? value.Y
                    : 0.0f,
                float.IsFinite(
                    value.Z)
                    ? value.Z
                    : 0.0f);
    }

    private static Vector3 SafeNormalize(
        Vector3 value,
        Vector3 fallback)
    {
        return
            value.LengthSquared() >
            0.000001f
                ? Vector3.Normalize(
                    value)
                : fallback;
    }

    private static float Approach(
        float current,
        float target,
        float delta)
    {
        return
            current <
            target
                ? Math.Min(
                    current +
                        delta,
                    target)
                : Math.Max(
                    current -
                        delta,
                    target);
    }

    private readonly record struct CapsuleGeometry(
        Vector3 PointA,
        Vector3 PointB,
        float Radius);
}

internal static class RaycastHit3DExtensions
{
    public static bool IsDefault(
        this RaycastHit3D hit)
    {
        return
            hit.GameObject ==
            null;
    }
}
