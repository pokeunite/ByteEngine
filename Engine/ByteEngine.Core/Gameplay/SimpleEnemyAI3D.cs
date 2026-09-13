using System.Numerics;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Scene;
using RuntimeScene = ByteEngine.Core.Scene.Scene;

namespace ByteEngine.Core.Gameplay;

public sealed class SimpleEnemyAI3D : Component
{
    private float _moveSpeed = 3f, _detectionRange = 20f, _attackRange = 1.5f;
    private float _damage = 10f, _attackCooldown = 1f, _stopDistance = 1f, _cooldownRemaining;

    public Guid TargetId { get; set; }
    public Guid TargetTagId { get; set; }
    public string TargetName { get; set; } = string.Empty;
    public float MoveSpeed { get => _moveSpeed; set => _moveSpeed = Safe(value); }
    public float DetectionRange { get => _detectionRange; set => _detectionRange = Safe(value); }
    public float AttackRange { get => _attackRange; set => _attackRange = Safe(value); }
    public float Damage { get => _damage; set => _damage = Safe(value); }
    public float AttackCooldown { get => _attackCooldown; set => _attackCooldown = Safe(value); }
    public float StopDistance { get => _stopDistance; set => _stopDistance = Safe(value); }

    public void ResetCombatState() => _cooldownRemaining = 0f;

    protected override void OnUpdate()
    {
        _cooldownRemaining = Math.Max(0f, _cooldownRemaining - (float)Time.DeltaTime);
        GameObject? enemy = AttachedGameObject;
        RuntimeScene? scene = enemy?.Scene;
        if (enemy == null || scene == null || enemy.GetComponent<HealthComponent>()?.IsDead == true) return;
        GameObject? target = ResolveTarget(scene, enemy);
        HealthComponent? targetHealth = target?.GetComponent<HealthComponent>();
        if (target == null || targetHealth?.IsDead == true) return;
        Vector3 delta = target.Transform.WorldPosition - enemy.Transform.WorldPosition;
        delta.Y = 0f;
        float distance = delta.Length();
        if (distance > DetectionRange) return;
        if (distance > .0001f)
        {
            Vector3 direction = delta / distance;
            Vector3 euler = enemy.Transform.EulerAngles;
            euler.Y = MathF.Atan2(-direction.X, -direction.Z) * 180f / MathF.PI;
            enemy.Transform.EulerAngles = euler;
            if (distance > StopDistance)
            {
                float movement = Math.Min(MoveSpeed * (float)Time.DeltaTime, distance - StopDistance);
                enemy.Transform.WorldPosition += direction * Math.Max(0f, movement);
                distance -= Math.Max(0f, movement);
            }
        }
        if (distance <= AttackRange && targetHealth != null && _cooldownRemaining <= 0f)
        {
            targetHealth.Damage(Damage);
            _cooldownRemaining = AttackCooldown;
        }
    }

    private GameObject? ResolveTarget(RuntimeScene scene, GameObject enemy)
    {
        GameObject? target = TargetId != Guid.Empty ? scene.FindGameObject(TargetId) : null;
        if (target == null && TargetTagId != Guid.Empty) target = scene.FindFirstWithTag(TargetTagId);
        if (target == null && !string.IsNullOrWhiteSpace(TargetName)) target = scene.FindGameObject(TargetName);
        return target ?? scene.GameObjects.FirstOrDefault(candidate => !ReferenceEquals(candidate, enemy) &&
            candidate.ActiveInHierarchy && candidate.GetComponent<CharacterController3D>()?.Enabled == true);
    }

    private static float Safe(float value) => float.IsFinite(value) ? Math.Max(0f, value) : 0f;
}
