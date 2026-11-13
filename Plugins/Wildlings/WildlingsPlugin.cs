using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Plugins;
using ByteEngine.Core.Scene;

namespace Wildlings;

public sealed class WildlingsPlugin : IByteEnginePlugin
{
    public void Register(ByteEnginePluginContext context)
    {
        context.RegisterSimpleComponent<TeamManager3D>(
            "TeamManager3D",
            new ByteEnginePluginComponentMetadata(
                DisplayName: "Team Manager 3D",
                Category: "Wildlings",
                Description: "Owns the three-slot party, hands player input between members and keeps one active camera.",
                SearchKeywords: "wildlings party team switch roster"));

        context.RegisterSimpleComponent<TrainerFollower3D>(
            "TrainerFollower3D",
            new ByteEnginePluginComponentMetadata(
                DisplayName: "Trainer Follower 3D",
                Category: "Wildlings",
                Description: "Noncombat trainer that trails the active party member.",
                SearchKeywords: "wildlings trainer follower companion"));
    }
}

/// <summary>
/// Three-slot party ownership. Exactly one member receives player input and
/// owns the active game camera; the other members run companion AI.
/// </summary>
public sealed class TeamManager3D : Component
{
    public Guid SlotOneId { get; set; }

    public Guid SlotTwoId { get; set; }

    public Guid SlotThreeId { get; set; }

    public int ActiveSlot { get; set; } = 1;

    public float SwitchCooldown { get; set; } = 0.6f;

    private float _cooldownRemaining;

    public GameObject? ActiveMember => Resolve(ActiveSlot);

    protected override void OnStart() => ApplySlot(ActiveSlot);

    protected override void OnUpdate()
    {
        _cooldownRemaining = MathF.Max(0f, _cooldownRemaining - (float)Time.DeltaTime);

        if (Input.IsKeyPressed(Key.D1)) RequestSwitch(1);
        else if (Input.IsKeyPressed(Key.D2)) RequestSwitch(2);
        else if (Input.IsKeyPressed(Key.D3)) RequestSwitch(3);
    }

    /// <summary>Requests a slot change. Repeats inside the throttle are ignored.</summary>
    public void RequestSwitch(int slot)
    {
        if (slot is < 1 or > 3 || slot == ActiveSlot || _cooldownRemaining > 0f) return;

        GameObject? target = Resolve(slot);
        if (target == null) return;
        if (target.GetComponent<HealthComponent>() is { IsDead: true }) return;

        ApplySlot(slot);
        _cooldownRemaining = MathF.Max(0.05f, SwitchCooldown);
    }

    public GameObject? Resolve(int slot)
    {
        Scene? scene = GameObject?.Scene;
        if (scene == null) return null;

        Guid id = slot switch
        {
            1 => SlotOneId,
            2 => SlotTwoId,
            3 => SlotThreeId,
            _ => Guid.Empty
        };

        return id == Guid.Empty ? null : scene.FindGameObject(id);
    }

    private void ApplySlot(int slot)
    {
        Scene? scene = GameObject?.Scene;
        if (scene == null) return;

        for (int index = 1; index <= 3; index++)
        {
            GameObject? member = Resolve(index);
            if (member == null) continue;

            bool controlled = index == slot;
            SetEnabled<PlayerController3D>(member, controlled);
            SetEnabled<PlayerShooter3D>(member, controlled);
            SetEnabled<SimpleEnemyAI3D>(member, !controlled);

            CameraBoom3D? boom = member.GetComponent<CameraBoom3D>();
            if (boom == null) continue;

            boom.Enabled = controlled;
            GameObject? cameraObject = scene.FindGameObject(boom.CameraObjectId);
            if (cameraObject?.GetComponent<Camera3D>() is { } camera)
            {
                camera.ActiveGameCamera = controlled;
            }
        }

        ActiveSlot = slot;
    }

    private static void SetEnabled<T>(GameObject member, bool enabled) where T : Component
    {
        if (member.GetComponent<T>() is { } component) component.Enabled = enabled;
    }
}

/// <summary>
/// Keeps the trainer walking behind whichever party member the player controls.
/// The trainer never attacks and is not targetable in this prototype slice.
/// </summary>
public sealed class TrainerFollower3D : Component
{
    public Guid TeamId { get; set; }

    public float FollowDistance { get; set; } = 6f;

    public float StopDistance { get; set; } = 4.2f;

    public float MoveSpeed { get; set; } = 5.4f;

    protected override void OnUpdate()
    {
        GameObject? trainer = GameObject;
        Scene? scene = trainer?.Scene;
        if (trainer == null || scene == null) return;

        GameObject? leader = TeamId != Guid.Empty
            ? scene.FindGameObject(TeamId)?.GetComponent<TeamManager3D>()?.ActiveMember
            : null;
        if (leader == null || ReferenceEquals(leader, trainer)) return;

        Vector3 delta = leader.Transform.WorldPosition - trainer.Transform.WorldPosition;
        delta.Y = 0f;
        float distance = delta.Length();
        if (distance <= StopDistance || distance < 0.0001f) return;

        Vector3 direction = delta / distance;
        float step = MathF.Min(MoveSpeed * (float)Time.DeltaTime, distance - StopDistance);
        if (step > 0f)
        {
            trainer.Transform.WorldPosition += direction * step;
        }

        Vector3 euler = trainer.Transform.EulerAngles;
        euler.Y = MathF.Atan2(-direction.X, -direction.Z) * 180f / MathF.PI;
        trainer.Transform.EulerAngles = euler;
    }
}
