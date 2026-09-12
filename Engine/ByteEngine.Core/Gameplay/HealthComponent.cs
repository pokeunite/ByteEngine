using ByteEngine.Core.Scene;

namespace ByteEngine.Core.Gameplay;

public sealed class HealthComponent : Component
{
    private float _maxHealth = 100f;
    private float _currentHealth = 100f;
    private bool _deathTriggered;

    public event Action<HealthComponent>? Died;

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
    public bool DestroyOnDeath { get; set; }
    public bool IsDead => CurrentHealth <= 0f;
    public float HealthPercent => MaxHealth <= 0f ? 0f : CurrentHealth / MaxHealth;

    public void Damage(float amount)
    {
        if (Invulnerable || IsDead || !float.IsFinite(amount) || amount <= 0f) return;
        CurrentHealth -= amount;
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
