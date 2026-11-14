using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Scene;

namespace Wildlings;

/// <summary>
/// Companion positioning for non-controlled party members (GDD chapter 18).
/// Holds role spacing from the active leader, backs off when a threat closes in,
/// and faces whatever the shared combat component is shooting at.
/// </summary>
public sealed class WildlingCompanion3D : Component
{
    /// <summary>Scene object name that owns TeamManager3D.</summary>
    public string TeamObjectName { get; set; } = "Wildling Manager";

    public float FollowDistance { get; set; } = 4.5f;

    public float StopDistance { get; set; } = 3.2f;

    public float RetreatDistance { get; set; } = 2.6f;

    public float StrafeOffset { get; set; } = 1.6f;

    protected override void OnUpdate()
    {
        GameObject? self = GameObject;
        Scene? scene = self?.Scene;
        if (self == null || scene == null || self.GetComponent<HealthComponent>() is { IsDead: true })
        {
            return;
        }

        TeamManager3D? team = string.IsNullOrWhiteSpace(TeamObjectName)
            ? null
            : scene.FindGameObject(TeamObjectName)?.GetComponent<TeamManager3D>();

        // Follows the trainer on foot, and the fighting creature during combat.
        GameObject? leader = team?.Leader;
        if (leader == null || ReferenceEquals(leader, self))
        {
            return;
        }

        WildlingCombat3D? combat = self.GetComponent<WildlingCombat3D>();
        WildlingStats3D? stats = self.GetComponent<WildlingStats3D>();
        float range = stats?.Definition.AttackRange ?? 10f;
        GameObject? target = combat is { CombatEnabled: true }
            ? combat.CurrentTarget ?? WildlingCombat3D.FindNearestHostile(self, scene, 16f)
            : null;

        Vector3 position = self.Transform.WorldPosition;
        Vector3 leaderPosition = leader.Transform.WorldPosition;
        Vector3 desired;

        if (target != null)
        {
            Vector3 fromTarget = position - target.Transform.WorldPosition;
            fromTarget.Y = 0f;
            float gap = fromTarget.Length();
            float preferred = MathF.Max(StopDistance, range * .55f);

            desired = gap < preferred && gap > .001f
                ? position + Vector3.Normalize(fromTarget) * MathF.Min(.6f, preferred - gap)
                : position;
        }
        else
        {
            desired = leaderPosition - leader.Transform.Forward * FollowDistance + leader.Transform.Right * StrafeOffset;
        }

        Vector3 delta = desired - position;
        delta.Y = 0f;
        float distance = delta.Length();
        Vector3 move = distance > .12f ? delta / distance : Vector3.Zero;

        if (target != null && Vector3.Distance(position, target.Transform.WorldPosition) < RetreatDistance)
        {
            Vector3 away = position - target.Transform.WorldPosition;
            away.Y = 0f;
            if (away.LengthSquared() > .0001f)
            {
                move = Vector3.Normalize(away);
            }
        }

        if (move.LengthSquared() > .0001f)
        {
            if (self.GetComponent<CharacterController3D>() is { } motor)
            {
                motor.Move(move);
            }
            else
            {
                self.Transform.WorldPosition += move * (stats?.MoveSpeed ?? 4f) * (float)Time.DeltaTime;
            }
        }

        Vector3 facing = target != null
            ? target.Transform.WorldPosition - position
            : move.LengthSquared() > .0001f ? move : leader.Transform.Forward;

        facing.Y = 0f;
        if (facing.LengthSquared() > .0001f)
        {
            FaceDirection(self, facing);
        }
    }

    private static void FaceDirection(GameObject self, Vector3 direction)
    {
        direction = Vector3.Normalize(direction);
        Vector3 euler = self.Transform.EulerAngles;
        euler.Y = MathF.Atan2(-direction.X, -direction.Z) * 180f / MathF.PI;
        self.Transform.EulerAngles = euler;
    }
}
