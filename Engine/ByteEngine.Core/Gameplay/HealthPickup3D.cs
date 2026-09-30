using ByteEngine.Core.Physics;
using ByteEngine.Core.Scene;

namespace ByteEngine.Core.Gameplay;

/// <summary>Heals a living player once when its trigger is entered.</summary>
public sealed class HealthPickup3D : Component
{
    private bool _consumed;

    public float Amount { get; set; } = 25f;
    public bool RequirePlayerController { get; set; } = true;

    protected override void OnStart() => _consumed = false;

    protected override void OnTriggerEnter(PhysicsContact3D contact)
    {
        if (_consumed || !float.IsFinite(Amount) || Amount <= 0f ||
            RequirePlayerController && contact.Other.GetComponent<PlayerController3D>() == null)
            return;

        HealthComponent? health = contact.Other.GetComponent<HealthComponent>();
        if (health == null || health.IsDead || health.CurrentHealth >= health.MaxHealth)
            return;

        health.Heal(Amount);
        _consumed = true;
        if (GameObject.Scene is { } scene) scene.DestroyGameObject(GameObject);
    }
}
