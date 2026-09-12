using ByteEngine.Core.Scene;

namespace ByteEngine.Core.Gameplay;

public sealed class LifetimeComponent : Component
{
    private float _lifetimeSeconds = 5f;
    private bool _expired;

    public float LifetimeSeconds
    {
        get => _lifetimeSeconds;
        set => _lifetimeSeconds = float.IsFinite(value) ? Math.Max(0f, value) : 0f;
    }

    public float RemainingSeconds { get; private set; }

    protected override void OnStart()
    {
        RemainingSeconds = LifetimeSeconds;
        Expire();
    }

    protected override void OnUpdate()
    {
        if (_expired) return;
        RemainingSeconds = Math.Max(0f, RemainingSeconds - (float)Time.DeltaTime);
        Expire();
    }

    private void Expire()
    {
        if (_expired || RemainingSeconds > 0f) return;
        _expired = true;
        GameObject? owner = AttachedGameObject;
        owner?.Scene?.DestroyGameObject(owner);
    }
}
