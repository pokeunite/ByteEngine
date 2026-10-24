using ByteEngine.Core.Scene;

namespace ByteEngine.Core.Gameplay;

public sealed class HealthComponent : Component
{
    private float _maxHealth = 100f;
    private float _currentHealth = 100f;
    private bool _deathTriggered;

    public event Action<HealthComponent>? Died;
    public event Action<HealthComponent, float>? Damaged;

    public float MaxHealth
    {
        get => _maxHealth;
        set
        {
            _maxHealth = Safe(value);
            _currentHealth = Math.Clamp(_currentHealth, 0f, _maxHealth);
        }
    }

    public float CurrentHealth
    {
        get => _currentHealth;
        set => _currentHealth = Math.Clamp(Safe(value), 0f, MaxHealth);
    }

    public bool Invulnerable { get; set; }
    public float IncomingDamageMultiplier { get; set; } = 1f;
    public bool DestroyOnDeath { get; set; }
    public bool IsDead => CurrentHealth <= 0f;
    public float HealthPercent => MaxHealth <= 0f ? 0f : CurrentHealth / MaxHealth;

    [System.ComponentModel.Browsable(false)] public System.Numerics.Vector3 LastDamageDirection { get; private set; }
    public float KnockbackMultiplier { get; set; } = 1;
    [System.ComponentModel.Browsable(false)] public CombatHitKind LastHitKind {get;private set;}
    public void Damage(float amount) => Damage(amount, System.Numerics.Vector3.Zero);
    public void Damage(float amount, System.Numerics.Vector3 direction) => DamageHit(amount,direction,false);
    public void DamageHit(float amount,System.Numerics.Vector3 direction,bool weakPoint)
    {
        if (Invulnerable || IsDead || !float.IsFinite(amount) || amount <= 0f) return;
        LastDamageDirection = direction;
        LastHitKind=CombatHitKind.Normal;
        if(AttachedGameObject?.GetComponent<CombatArmour3D>() is {Enabled:true} armour){amount=armour.ResolveDamage(amount,weakPoint,out var kind);LastHitKind=kind;}
        float before = CurrentHealth;
        CurrentHealth -= amount * (float.IsFinite(IncomingDamageMultiplier) ? Math.Max(0, IncomingDamageMultiplier) : 1);
        Damaged?.Invoke(this, before - CurrentHealth);
        TriggerDeath();
    }

    public void Heal(float amount)
    {
        if (IsDead || !float.IsFinite(amount) || amount <= 0f) return;
        CurrentHealth += amount;
    }

    public void Kill()
    {
        CurrentHealth = 0f;
        TriggerDeath();
    }

    /// <summary>Explicitly begins another life, including after death or pool reuse.</summary>
    public void Revive(float health = -1f)
    {
        _deathTriggered = false;
        CurrentHealth = health < 0 ? MaxHealth : health;
        TriggerDeath();
    }

    protected override void OnStart() => TriggerDeath();
    protected override void OnUpdate() => TriggerDeath();

    private void TriggerDeath()
    {
        if (!IsDead || _deathTriggered) return;
        _deathTriggered = true;
        Died?.Invoke(this);
        GameObject? owner = AttachedGameObject;
        if (DestroyOnDeath && owner?.Scene != null) owner.Scene.DestroyGameObject(owner);
    }

    private static float Safe(float value) => float.IsFinite(value) ? Math.Max(0f, value) : 0f;
}
