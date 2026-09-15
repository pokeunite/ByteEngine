using ByteEngine.Core.Animation;
using ByteEngine.Core.Audio;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Physics;

namespace ByteEngine.Editor;

internal readonly record struct PropertyRange(float Speed, float Minimum, float Maximum);

/// <summary>Authoring ranges migrated from the former Inspector widgets; shared by every context.</summary>
internal static class ComponentPropertyConstraints
{
    private static readonly Dictionary<(Type, string), PropertyRange> Ranges = new()
    {
        [(typeof(Camera2D), "Zoom")] = new(.01f, .01f, 100f),
        [(typeof(Camera3D), "FieldOfView")] = new(.25f, 1f, 179f),
        [(typeof(Camera3D), "NearClip")] = new(.01f, .001f, 100f),
        [(typeof(Camera3D), "FarClip")] = new(1f, 1f, 100000f),
        [(typeof(DirectionalLight), "Intensity")] = new(.02f, 0f, 100f),
        [(typeof(DirectionalLight), "AmbientIntensity")] = new(.01f, 0f, 1f),

        [(typeof(AudioSource3D), nameof(AudioSource3D.Volume))] = new(.01f, 0f, 4f),
        [(typeof(AudioSource3D), nameof(AudioSource3D.Pitch))] = new(.01f, .25f, 4f),
        [(typeof(AudioSource3D), nameof(AudioSource3D.MinDistance))] = new(.05f, .001f, 100000f),
        [(typeof(AudioSource3D), nameof(AudioSource3D.MaxDistance))] = new(.25f, .001f, 1000000f),
        [(typeof(AudioSource3D), nameof(AudioSource3D.RolloffFactor))] = new(.01f, 0f, 100f),
        [(typeof(AudioListener3D), nameof(AudioListener3D.Volume))] = new(.01f, 0f, 4f),

        [(typeof(Rigidbody3D), nameof(Rigidbody3D.Mass))] = new(.05f, .0001f, 100000f),
        [(typeof(Rigidbody3D), nameof(Rigidbody3D.GravityScale))] = new(.05f, -16f, 16f),
        [(typeof(Rigidbody3D), nameof(Rigidbody3D.LinearDamping))] = new(.01f, 0f, 100f),
        [(typeof(Rigidbody3D), nameof(Rigidbody3D.Restitution))] = new(.01f, 0f, 1f),
        [(typeof(Rigidbody3D), nameof(Rigidbody3D.Friction))] = new(.01f, 0f, 4f),

        [(typeof(SkyEnvironment), nameof(SkyEnvironment.Exposure))] = new(.02f, 0f, 16f),
        [(typeof(GroundSurface), "Friction")] = new(.02f, 0f, 10f),
        [(typeof(CapsuleCollider3D), "Radius")] = new(.01f, .001f, 1000f),
        [(typeof(CapsuleCollider3D), "Height")] = new(.02f, .002f, 1000f),
        [(typeof(CharacterController3D), "MoveSpeed")] = new(.05f, 0f, 1000f),
        [(typeof(CharacterController3D), "Acceleration")] = new(.1f, 0f, 1000f),
        [(typeof(CharacterController3D), "Deceleration")] = new(.1f, 0f, 1000f),
        [(typeof(CharacterController3D), "AirControl")] = new(.01f, 0f, 1f),
        [(typeof(CharacterController3D), "JumpForce")] = new(.05f, 0f, 1000f),
        [(typeof(CharacterController3D), "Gravity")] = new(.1f, 0f, 1000f),
        [(typeof(CharacterController3D), "GroundDistance")] = new(.01f, 0f, 100f),
        [(typeof(CharacterController3D), "MaxSlope")] = new(.25f, 0f, 90f),
        [(typeof(CharacterController3D), "StepHeight")] = new(.01f, 0f, 100f),
        [(typeof(CharacterController3D), "CoyoteTime")] = new(.01f, 0f, 10f),
        [(typeof(CharacterController3D), "JumpBuffer")] = new(.01f, 0f, 10f),
        [(typeof(HealthComponent), "MaxHealth")] = new(.5f, 0f, 100000f),
        [(typeof(HealthComponent), "CurrentHealth")] = new(.5f, 0f, 100000f),
        [(typeof(LifetimeComponent), "LifetimeSeconds")] = new(.05f, 0f, 100000f),
        [(typeof(Projectile3D), "Damage")] = new(.25f, 0f, 100000f),
        [(typeof(Projectile3D), "Radius")] = new(.01f, 0f, 10000f),
        [(typeof(ProjectileLauncher3D), "ProjectileSpeed")] = new(.25f, 0f, 100000f),
        [(typeof(ProjectileLauncher3D), "Damage")] = new(.25f, 0f, 100000f),
        [(typeof(ProjectileLauncher3D), "FireCooldown")] = new(.01f, 0f, 10000f),
        [(typeof(SimpleEnemyAI3D), "MoveSpeed")] = new(.05f, 0f, 10000f),
        [(typeof(SimpleEnemyAI3D), "DetectionRange")] = new(.1f, 0f, 100000f),
        [(typeof(SimpleEnemyAI3D), "AttackRange")] = new(.05f, 0f, 100000f),
        [(typeof(SimpleEnemyAI3D), "Damage")] = new(.25f, 0f, 100000f),
        [(typeof(SimpleEnemyAI3D), "AttackCooldown")] = new(.01f, 0f, 10000f),
        [(typeof(SimpleEnemyAI3D), "StopDistance")] = new(.05f, 0f, 100000f),
        [(typeof(PlayerController3D), "TurnSpeed")] = new(5f, 0f, 3600f),
        [(typeof(PlayerController3D), "ControlYaw")] = new(.25f, -180f, 180f),
        [(typeof(PlayerController3D), "ControlPitch")] = new(.25f, -89f, 89f),
        [(typeof(ThirdPersonCamera3D), "Distance")] = new(.05f, 0f, 10000f),
        [(typeof(ThirdPersonCamera3D), "Height")] = new(.05f, -10000f, 10000f),
        [(typeof(ThirdPersonCamera3D), "LookAtHeight")] = new(.05f, -10000f, 10000f),
        [(typeof(ThirdPersonCamera3D), "FollowSmoothing")] = new(.1f, 0f, 1000f),
        [(typeof(ThirdPersonCamera3D), "Yaw")] = new(.25f, -100000f, 100000f),
        [(typeof(ThirdPersonCamera3D), "Pitch")] = new(.25f, -89f, 89f),
        [(typeof(ThirdPersonCamera3D), "MinPitch")] = new(.25f, -89f, 89f),
        [(typeof(ThirdPersonCamera3D), "MaxPitch")] = new(.25f, -89f, 89f),
        [(typeof(ThirdPersonCamera3D), "MouseSensitivity")] = new(.01f, 0f, 10f),
        [(typeof(ThirdPersonCamera3D), "ShoulderOffset")] = new(.02f, -100f, 100f),
        [(typeof(CameraBoom3D), "ArmLength")] = new(.05f, 0f, 1000f),
        [(typeof(CameraBoom3D), "PivotHeight")] = new(.02f, -100f, 100f),
        [(typeof(CameraBoom3D), "Yaw")] = new(.25f, -100000f, 100000f),
        [(typeof(CameraBoom3D), "Pitch")] = new(.25f, -89f, 89f),
        [(typeof(CameraBoom3D), "MinPitch")] = new(.25f, -89f, 89f),
        [(typeof(CameraBoom3D), "MaxPitch")] = new(.25f, -89f, 89f),
        [(typeof(CameraBoom3D), "MouseSensitivityX")] = new(.005f, 0f, 10f),
        [(typeof(CameraBoom3D), "MouseSensitivityY")] = new(.005f, 0f, 10f),
        [(typeof(CameraBoom3D), "PositionSmoothness")] = new(.1f, 0f, 1000f),
        [(typeof(CameraBoom3D), "RotationSmoothness")] = new(.1f, 0f, 1000f),
        [(typeof(CameraBoom3D), "ShoulderOffset")] = new(.02f, -100f, 100f),
        [(typeof(CameraBoom3D), "CollisionRadius")] = new(.01f, 0f, 10f),
        [(typeof(CameraBoom3D), "CollisionReturnSpeed")] = new(.1f, 0f, 1000f),
        [(typeof(CameraBoom3D), "MaximumLagDistance")] = new(.05f, 0f, 100f),
        [(typeof(CameraBoom3D), "MaxLagTimeStep")] = new(.001f, .001f, .1f),
        [(typeof(AnimationController), "RunThreshold")] = new(.05f, 0f, 1000f),
    };

    public static PropertyRange Get(Type type, string name) =>
        Ranges.TryGetValue((type, name), out var range)
            ? range
            : new(.05f, -float.MaxValue, float.MaxValue);
}
