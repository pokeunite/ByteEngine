using System.Numerics;

using ByteEngine.Core.Scene;

namespace ByteEngine.Core.Characters;

public sealed class CharacterController3D
    : Component
{
    private Vector3 _moveInput;

    private bool _jumpQueued;

    private bool _wasGrounded;

    private float _timeSinceGrounded =
        float.PositiveInfinity;

    private float _jumpBufferRemaining;

    public bool IsGrounded { get; private set; }

    public bool IsFalling =>
        !IsGrounded &&
        VerticalVelocity < 0.0f;

    public bool IsMoving =>
        new Vector2(
            Velocity.X,
            Velocity.Z
        ).LengthSquared() >
        0.0001f;

    public bool JustLanded { get; private set; }

    public Vector3 Velocity { get; private set; }

    public float Speed =>
        new Vector2(
            Velocity.X,
            Velocity.Z
        ).Length();

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

        /*
         * Movement stays horizontal.
         *
         * This prevents a tilted character from attempting
         * to move into the floor or into the air.
         */
        forward.Y =
            0.0f;

        if (forward.LengthSquared() >
            0.0001f)
        {
            forward =
                Vector3.Normalize(
                    forward
                );
        }

        Move(
            forward *
            amount
        );
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
                    right
                );
        }

        Move(
            right *
            amount
        );
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
            velocity;
    }

    public void AddImpulse(
        Vector3 impulse)
    {
        Velocity +=
            impulse;
    }

    protected override void OnUpdate()
    {
        float deltaTime =
            (float)Time.DeltaTime;

        JustLanded =
            false;

        DetectGround();

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

        Vector3 target =
            _moveInput;

        target.Y =
            0.0f;

        if (target.LengthSquared() >
            1.0f)
        {
            target =
                Vector3.Normalize(
                    target
                );
        }

        target *=
            MoveSpeed;

        float control =
            IsGrounded
                ? 1.0f
                : AirControl;

        float rate =
            target.LengthSquared() >
            0.001f
                ? Acceleration
                : Deceleration;

        Velocity =
            new Vector3(
                Approach(
                    Velocity.X,
                    target.X,
                    rate *
                    control *
                    deltaTime
                ),

                Velocity.Y,

                Approach(
                    Velocity.Z,
                    target.Z,
                    rate *
                    control *
                    deltaTime
                )
            );

        TryConsumeJump();

        if (!IsGrounded)
        {
            Velocity +=
                Vector3.UnitY *
                (-Gravity *
                 deltaTime);
        }

        Transform.WorldPosition +=
            Velocity *
            deltaTime;

        /*
         * Check again after movement.
         *
         * This prevents a fast downward step from passing
         * through the grounding distance during one frame.
         */
        if (Velocity.Y <=
            0.0f)
        {
            DetectGround();

            if (IsGrounded)
            {
                _timeSinceGrounded =
                    0.0f;

                TryConsumeJump();
            }
        }

        _jumpBufferRemaining =
            Math.Max(
                0.0f,
                _jumpBufferRemaining -
                deltaTime);

        _moveInput =
            Vector3.Zero;

        _jumpQueued =
            false;

        _wasGrounded =
            IsGrounded;
    }

    private void DetectGround()
    {
        GroundObject =
            null;

        GroundNormal =
            Vector3.UnitY;

        float bestHeight =
            float.NegativeInfinity;

        Vector3 bestNormal =
            Vector3.UnitY;

        ByteEngine.Core.Scene.Scene? scene =
            GameObject.Scene;

        if (scene ==
            null)
        {
            IsGrounded =
                false;

            return;
        }

        float feetOffset =
            GetFeetOffset();

        float feetY =
            Transform.WorldPosition.Y -
            feetOffset;

        foreach (GameObject item
                 in scene.GameObjects)
        {
            if (ReferenceEquals(
                    item,
                    GameObject))
            {
                continue;
            }

            GroundSurface? ground =
                item.GetComponent<GroundSurface>();

            if (ground?.Walkable !=
                true)
            {
                continue;
            }

            if (!TryGetGroundContact(
                    item,
                    Transform.WorldPosition.X,
                    Transform.WorldPosition.Z,
                    out float surfaceHeight,
                    out Vector3 surfaceNormal))
            {
                continue;
            }

            float slope =
                MathF.Acos(
                    Math.Clamp(
                        Vector3.Dot(
                            surfaceNormal,
                            Vector3.UnitY),
                        -1.0f,
                        1.0f)) *
                180.0f /
                MathF.PI;

            if (slope >
                MaxSlope)
            {
                continue;
            }

            /*
             * Temporary v0.7 grounding.
             *
             * Physics3D will later replace this with a proper
             * capsule/shape cast.
             */
            float distance =
                feetY -
                surfaceHeight;

            bool closeEnough =
                distance <=
                GroundDistance &&
                distance >=
                -(GroundDistance +
                  StepHeight);

            if (!closeEnough)
            {
                continue;
            }

            if (surfaceHeight >
                bestHeight)
            {
                bestHeight =
                    surfaceHeight;

                GroundObject =
                    item;

                bestNormal =
                    surfaceNormal;
            }
        }

        bool grounded =
            GroundObject !=
            null &&
            Velocity.Y <=
            0.0f;

        IsGrounded =
            grounded;

        GroundNormal =
            grounded
                ? bestNormal
                : Vector3.UnitY;

        if (IsGrounded &&
            SnapToGround)
        {
            float targetOriginY =
                bestHeight +
                feetOffset;

            Transform.WorldPosition =
                new Vector3(
                    Transform.WorldPosition.X,
                    targetOriginY,
                    Transform.WorldPosition.Z
                );

            Velocity =
                new Vector3(
                    Velocity.X,
                    0.0f,
                    Velocity.Z
                );
        }

        JustLanded =
            IsGrounded &&
            !_wasGrounded;
    }

    private float GetFeetOffset()
    {
        Collider3D? collider =
            GameObject.Components
                .OfType<Collider3D>()
                .FirstOrDefault(
                    component =>
                        component.Enabled &&
                        !component.IsTrigger
                );

        if (collider ==
            null)
        {
            /*
             * No collider means the GameObject origin is
             * treated as its feet.
             *
             * Characters should normally have a CapsuleCollider3D
             * and simple test objects should have BoxCollider3D.
             */
            return 0.0f;
        }

        float halfHeight =
            collider.Size.Y *
            0.5f *
            MathF.Abs(
                Transform.WorldScale.Y
            );

        float centerOffset =
            collider.Center.Y *
            Transform.WorldScale.Y;

        /*
         * Origin -> bottom of collider.
         */
        return halfHeight -
               centerOffset;
    }

    private static bool TryGetGroundContact(
        GameObject groundObject,
        float worldX,
        float worldZ,
        out float surfaceHeight,
        out Vector3 surfaceNormal)
    {
        surfaceHeight =
            0.0f;

        surfaceNormal =
            groundObject.Transform.Up;

        if (surfaceNormal.Y <
            0.0f)
        {
            surfaceNormal =
                -surfaceNormal;
        }

        if (MathF.Abs(
                surfaceNormal.Y) <
            0.0001f)
        {
            return false;
        }

        Collider3D? collider =
            groundObject.Components
                .OfType<Collider3D>()
                .FirstOrDefault(
                    component =>
                        component.Enabled &&
                        !component.IsTrigger
                );

        Vector3 localSurfacePoint =
            collider ==
                null
                ? Vector3.Zero
                : collider.Center +
                  Vector3.UnitY *
                  collider.Size.Y *
                  0.5f;

        Vector3 worldSurfacePoint =
            Vector3.Transform(
                localSurfacePoint,
                groundObject.Transform.WorldMatrix);

        surfaceHeight =
            worldSurfacePoint.Y -
            (
                surfaceNormal.X *
                (worldX -
                 worldSurfacePoint.X) +
                surfaceNormal.Z *
                (worldZ -
                 worldSurfacePoint.Z)
            ) /
            surfaceNormal.Y;

        if (collider ==
            null)
        {
            return true;
        }

        if (!Matrix4x4.Invert(
                groundObject.Transform.WorldMatrix,
                out Matrix4x4 inverse))
        {
            return false;
        }

        Vector3 localContact =
            Vector3.Transform(
                new Vector3(
                    worldX,
                    surfaceHeight,
                    worldZ),
                inverse);

        Vector3 halfSize =
            collider.Size *
            0.5f;

        const float boundsTolerance =
            0.001f;

        return
            MathF.Abs(
                localContact.X -
                collider.Center.X) <=
            halfSize.X +
            boundsTolerance &&
            MathF.Abs(
                localContact.Z -
                collider.Center.Z) <=
            halfSize.Z +
            boundsTolerance;
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
                JumpForce,
                Velocity.Z);

        IsGrounded =
            false;

        GroundObject =
            null;

        GroundNormal =
            Vector3.UnitY;

        _timeSinceGrounded =
            float.PositiveInfinity;

        _jumpQueued =
            false;

        _jumpBufferRemaining =
            0.0f;
    }

    private static float Approach(
        float current,
        float target,
        float delta)
    {
        return current <
               target
            ? Math.Min(
                current +
                delta,
                target
            )
            : Math.Max(
                current -
                delta,
                target
            );
    }
}
