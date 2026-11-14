using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Scene;

namespace Wildlings;

/// <summary>
/// Keeps the trainer at a safe distance behind the fighting creature.
///
/// Out of combat the player drives the trainer directly, so this component does
/// nothing. During combat TeamManager3D takes input and the camera away from the
/// trainer, and this follower walks it after the active Wildling.
/// </summary>
public sealed class TrainerFollower3D : Component
{
    /// <summary>Scene object name that owns TeamManager3D.</summary>
    public string TeamObjectName { get; set; } = "Wildling Manager";

    /// <summary>Distance the trainer tries to keep from the fighting creature.</summary>
    public float FollowDistance { get; set; } = 5.5f;

    public float StopDistance { get; set; } = 3.8f;

    public float MoveSpeed { get; set; } = 5.4f;

    protected override void OnUpdate()
    {
        GameObject? trainer = GameObject;
        Scene? scene = trainer?.Scene;
        if (trainer == null || scene == null)
        {
            return;
        }

        TeamManager3D? team = string.IsNullOrWhiteSpace(TeamObjectName)
            ? null
            : scene.FindGameObject(TeamObjectName)?.GetComponent<TeamManager3D>();

        // No team, or exploring: the player is driving the trainer.
        if (team == null || !team.InCombat)
        {
            return;
        }

        GameObject? leader = team.ActiveMember;
        if (leader == null || ReferenceEquals(leader, trainer))
        {
            return;
        }

        Vector3 delta = leader.Transform.WorldPosition - trainer.Transform.WorldPosition;
        delta.Y = 0f;
        float distance = delta.Length();
        if (distance <= StopDistance || distance < 0.0001f)
        {
            return;
        }

        Vector3 direction = delta / distance;

        if (trainer.GetComponent<CharacterController3D>() is { } motor)
        {
            motor.Move(direction);
        }
        else
        {
            float step = MathF.Min(MoveSpeed * (float)Time.DeltaTime, MathF.Max(0f, distance - StopDistance));
            if (step > 0f)
            {
                trainer.Transform.WorldPosition += direction * step;
            }
        }

        Vector3 euler = trainer.Transform.EulerAngles;
        euler.Y = MathF.Atan2(-direction.X, -direction.Z) * 180f / MathF.PI;
        trainer.Transform.EulerAngles = euler;
    }
}
