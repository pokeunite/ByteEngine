using System.Numerics;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Scene;
using RuntimeScene = ByteEngine.Core.Scene.Scene;

namespace ByteEngine.Core.Gameplay;

public enum SimpleEnemyAIState
{
    Idle,
    Chase,
    Attack
}

public sealed partial class SimpleEnemyAI3D : Component
{
    private float _moveSpeed = 3f, _detectionRange = 20f, _attackRange = 1.5f;
    private float _damage = 10f, _attackCooldown = 1f, _stopDistance = 1f, _cooldownRemaining;

    public SimpleEnemyAIState State { get; private set; } = SimpleEnemyAIState.Idle;
    public bool IsMoving { get; private set; }
    public bool IsAttacking => State == SimpleEnemyAIState.Attack;
    public bool AttackFiredThisFrame { get; private set; }

    [SceneObjectReference,System.ComponentModel.DisplayName("Target Object")]
    public Guid TargetId { get; set; }
    /// <summary>Disable when attack event sheets apply damage or fire projectiles.</summary>
    [System.ComponentModel.Description("Disable when attack events apply damage or launch projectiles. The attack signal and cooldown still run.")]
    public bool AutomaticDamage {get;set;}=true;
    public Guid TargetTagId { get; set; }
    public string TargetName { get; set; } = string.Empty;
    public float MoveSpeed { get => _moveSpeed; set => _moveSpeed = Safe(value); }
    public float DetectionRange { get => _detectionRange; set => _detectionRange = Safe(value); }
    public float AttackRange { get => _attackRange; set => _attackRange = Safe(value); }
    public float Damage { get => _damage; set => _damage = Safe(value); }
    public float AttackCooldown { get => _attackCooldown; set => _attackCooldown = Safe(value); }
    public float StopDistance { get => _stopDistance; set => _stopDistance = Safe(value); }

    [System.ComponentModel.Category("Behaviour"), System.ComponentModel.Description("AI Behaviour drives this combat executor. Its normal autonomous update is disabled; existing attack, dodge, cover and boss settings are retained.")]
    public bool BehaviorControlled { get; set; }
    public void TickBehaviorCombat(float delta)
    {
        if (BehaviorControlled && Enabled) Simulate(delta);
    }
    public void ResetCombatState() => _cooldownRemaining = 0f;

    protected override void OnStart(){ObserveTacticHealth();PrewarmAttackEffects();}
    protected override void OnUpdate(){if(AttachedGameObject?.Scene?.FixedSimulation!=true){if(!BehaviorControlled)Simulate((float)Time.DeltaTime);}}
    protected override void OnFixedUpdate(){if(!BehaviorControlled)Simulate((float)Time.FixedDeltaTime);}
    private void Simulate(float stepDelta)
    {
        State = SimpleEnemyAIState.Idle;
        IsMoving = false;
        AttackFiredThisFrame = false;
        _cooldownRemaining = Math.Max(0f, _cooldownRemaining - stepDelta);
        GameObject? enemy = AttachedGameObject;
        RuntimeScene? scene = enemy?.Scene;
        if (enemy == null || scene == null) return;
        if (enemy.GetComponent<HealthComponent>()?.IsDead == true) { ClearWarning(); return; }
        GameObject? target = ResolveTarget(scene, enemy);
        HealthComponent? targetHealth = target?.GetComponent<HealthComponent>();
        if (target == null || targetHealth?.IsDead == true) { ClearWarning(); return; }
        if (AttackStyle != EnemyAttackStyle.Contact) { SimulatePattern(target, stepDelta); return; }
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
                float movement = Math.Min(MoveSpeed * stepDelta, distance - StopDistance);
                IsMoving = movement > 0f;
                if(UseNavigation)Navigate(target.Transform.WorldPosition,stepDelta);
                else enemy.Transform.WorldPosition += direction * Math.Max(0f, movement);
                distance -= Math.Max(0f, movement);
            }
        }
        State = distance <= AttackRange ? SimpleEnemyAIState.Attack : SimpleEnemyAIState.Chase;
        if (State == SimpleEnemyAIState.Attack && targetHealth != null && _cooldownRemaining <= 0f)
        {
            float healthBefore = targetHealth.CurrentHealth;
            if(AutomaticDamage)targetHealth.Damage(Damage);
            AttackFiredThisFrame = !AutomaticDamage||targetHealth.CurrentHealth < healthBefore;
            _cooldownRemaining = AttackCooldown;
        }
    }

    protected override void OnStop()
    {
        ResetAttackPattern();
        UnsubscribeTactics();
        State = SimpleEnemyAIState.Idle;
        IsMoving = false;
        AttackFiredThisFrame = false;
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
