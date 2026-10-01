using System.Numerics;
using ByteEngine.Core.Animation;
using ByteEngine.Core.Audio;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Physics;
using ByteEngine.Core.Scene;

namespace ByteEngine.Core.Gameplay;

/// <summary>
/// Bounded, lightweight death physics for a skinned character. The owner's
/// Rigidbody handles translation/collision; the skeletal renderer simulates
/// damped angular springs for its joints. No extra scene objects or per-bone
/// rigid bodies are created. Add beside Health on the character root.
/// </summary>
public sealed class SkeletalRagdoll3D : Component
{
    private static readonly LinkedList<SkeletalRagdoll3D> ActiveCorpses = new();
    private HealthComponent? _health;
    private SkeletalMeshRenderer? _renderer;
    private bool _originalDestroyOnDeath;
    private bool _dead;
    private bool _knockedDown;
    private bool _recovering;
    private float _age;
    private float _knockdownAge;
    private float _recoveryAge;
    private float _airborneImpactEnergy;
    private int _chainBudget;
    private float _lastHitStrength = 1f;
    private Vector3 _impactDirection;

    public float CorpseLifetime { get; set; } = 2.5f;
    public int MaxActiveCorpses { get; set; } = 4;
    public float ImpulseStrength { get; set; } = 1.5f;
    public float HitReactionStrength { get; set; } = 1f;
    public float KnockdownDamageThreshold { get; set; } = 8f;
    public float KnockdownDuration { get; set; } = 1.4f;
    public float RecoveryDuration { get; set; } = .55f;
    public float LaunchSpeed { get; set; } = 6f;
    public float ChainImpactSpeed { get; set; } = 2f;
    public bool IsRagdolling => _dead && _renderer?.LightRagdollActive == true;
    public bool IsKnockedDown => _knockedDown;

    /// <summary>Optional directional hit, usable by projectiles or Event actions.</summary>
    public void ApplyImpact(Vector3 worldDirection, float strength)
    {
        if (!float.IsFinite(strength) || strength <= 0f) return;
        if (float.IsFinite(worldDirection.X) && float.IsFinite(worldDirection.Y) &&
            float.IsFinite(worldDirection.Z) && worldDirection.LengthSquared() > 1e-6f)
            _impactDirection = Vector3.Normalize(worldDirection);
        _lastHitStrength = Math.Clamp(strength, .1f, 3f);
        if (!_dead) _renderer?.ApplyHitReaction(strength * Math.Max(HitReactionStrength, 0f));
    }

    protected override void OnStart()
    {
        GameObject? owner = AttachedGameObject;
        if (owner == null) return;
        _health = owner.GetComponent<HealthComponent>();
        _renderer = FindRenderer(owner);
        if (_health == null || _renderer == null || !_renderer.ResolveRuntimeResources())
            return;
        _originalDestroyOnDeath = _health.DestroyOnDeath;
        _health.DestroyOnDeath = false;
        _health.Damaged += OnDamaged;
        _health.Died += OnDied;
        _impactDirection = -owner.Transform.Forward;
        if (_health.IsDead) OnDied(_health);
    }

    protected override void OnUpdate()
    {
        if (_knockedDown && !_dead)
        {
            _airborneImpactEnergy *= MathF.Exp(-1.8f * Math.Max((float)Time.DeltaTime, 0f));
            _knockdownAge += Math.Max((float)Time.DeltaTime, 0f);
            if (!_recovering && _knockdownAge >= Math.Max(KnockdownDuration, .1f))
            {
                _renderer?.EndLightRagdoll(RecoveryDuration);
                if (GameObject.GetComponent<AnimationController>() is { } animation)
                    animation.Enabled = true;
                _recovering = true;
                _recoveryAge = 0f;
            }
            if (_recovering)
            {
                _recoveryAge += Math.Max((float)Time.DeltaTime, 0f);
                if (_recoveryAge >= Math.Max(RecoveryDuration, .05f))
                {
                    _knockedDown = false;
                    _recovering = false;
                    _chainBudget = 0;
                    if (GameObject.GetComponent<SimpleEnemyAI3D>() is { } ai)
                        ai.Enabled = true;
                }
            }
        }
        if (!_dead) return;
        _airborneImpactEnergy *= MathF.Exp(-1.8f * Math.Max((float)Time.DeltaTime, 0f));
        _age += Math.Max((float)Time.DeltaTime, 0f);
        if (_age >= Math.Max(CorpseLifetime, 0f) && AttachedGameObject?.Scene is { } scene)
            scene.DestroyGameObject(GameObject);
    }

    protected override void OnStop() => Release();
    protected override void OnDestroy() => Release();

    protected override void OnCollisionEnter(PhysicsContact3D contact)
    {
        if ((!_knockedDown && !_dead) || _chainBudget <= 0) return;
        SkeletalRagdoll3D? other = FindRagdoll(contact.Other);
        if (other == null || ReferenceEquals(other, this) || other._dead || other._knockedDown)
            return;
        Vector3 velocity = GameObject.GetComponent<Rigidbody3D>()?.Velocity ?? Vector3.Zero;
        float speed = Math.Max(velocity.Length(), _airborneImpactEnergy);
        if (speed < Math.Max(ChainImpactSpeed, 0f)) return;
        Vector3 direction = speed > .001f ? velocity / speed : -contact.Normal;
        other.KnockDown(direction, Math.Clamp(speed, 1f, 12f), _chainBudget - 1);
        _chainBudget = 0; // One transfer per airborne character, avoiding contact storms.
    }

    public bool KnockDown(Vector3 worldDirection, float speed, int chainBudget = 2)
    {
        if (_dead || _knockedDown || _renderer?.BeginLightRagdoll(speed / 6f) != true)
            return false;
        _knockedDown = true;
        _recovering = false;
        _knockdownAge = 0f;
        _chainBudget = Math.Clamp(chainBudget, 0, 3);
        _airborneImpactEnergy = Math.Clamp(speed, 0f, 20f);
        if (GameObject.GetComponent<SimpleEnemyAI3D>() is { } ai) ai.Enabled = false;
        if (GameObject.GetComponent<AnimationController>() is { } animation) animation.Enabled = false;
        if (GameObject.GetComponent<Rigidbody3D>() is { } body)
        {
            Vector3 horizontal = new(worldDirection.X, 0f, worldDirection.Z);
            if (horizontal.LengthSquared() < .0001f) horizontal = -GameObject.Transform.Forward;
            horizontal = Vector3.Normalize(horizontal);
            body.BodyType = RigidbodyBodyType3D.Dynamic;
            body.UseGravity = true;
            body.Velocity = new Vector3(0f, body.Velocity.Y, 0f);
            body.AddImpulse((horizontal + Vector3.UnitY * .42f) * body.Mass *
                Math.Clamp(speed, 0f, 20f));
        }
        return true;
    }

    private void OnDamaged(HealthComponent health, float amount)
    {
        if (_dead) return;
        _lastHitStrength = Math.Clamp(amount / Math.Max(health.MaxHealth * .25f, 1f), .1f, 3f);
        if (!health.IsDead && amount >= Math.Max(KnockdownDamageThreshold, 0f))
            KnockDown(_impactDirection, Math.Max(LaunchSpeed, 0f) * _lastHitStrength);
        else _renderer?.ApplyHitReaction(amount * Math.Max(HitReactionStrength, 0f));
    }

    private void OnDied(HealthComponent health)
    {
        if (_dead) return;
        GameObject? owner = AttachedGameObject;
        if (owner?.Scene == null || _renderer?.BeginLightRagdoll(_lastHitStrength) != true)
        {
            owner?.Scene?.DestroyGameObject(owner);
            return;
        }
        _dead = true;
        _knockedDown = false;
        _recovering = false;
        _chainBudget = Math.Max(_chainBudget, 1);
        _airborneImpactEnergy = Math.Max(_airborneImpactEnergy, Math.Max(ImpulseStrength, 0f) * 3f);
        if (owner.GetComponent<AudioSource3D>() is { } audio)
        {
            audio.Stop();
            audio.Enabled = false;
        }
        if (owner.GetComponent<SimpleEnemyAI3D>() is { } ai) ai.Enabled = false;
        if (owner.GetComponent<AnimationController>() is { } animation) animation.Enabled = false;
        if (owner.GetComponent<Rigidbody3D>() is { } body)
        {
            body.BodyType = RigidbodyBodyType3D.Dynamic;
            body.UseGravity = true;
            body.AddImpulse(_impactDirection * body.Mass * Math.Max(ImpulseStrength, 0f));
        }
        ActiveCorpses.AddLast(this);
        TrimCorpses(owner.Scene);
    }

    private void TrimCorpses(Scene.Scene scene)
    {
        int limit = Math.Clamp(MaxActiveCorpses, 1, 32);
        while (ActiveCorpses.Count(item => item.AttachedGameObject?.Scene == scene) > limit)
        {
            SkeletalRagdoll3D? oldest = ActiveCorpses.FirstOrDefault(item =>
                item.AttachedGameObject?.Scene == scene);
            if (oldest == null) break;
            ActiveCorpses.Remove(oldest);
            if (oldest.AttachedGameObject is { } objectToRemove)
                scene.DestroyGameObject(objectToRemove);
        }
    }

    private void Release()
    {
        ActiveCorpses.Remove(this);
        if (_health != null)
        {
            _health.Damaged -= OnDamaged;
            _health.Died -= OnDied;
            if (!_dead) _health.DestroyOnDeath = _originalDestroyOnDeath;
        }
        _health = null;
        _renderer = null;
    }

    private static SkeletalMeshRenderer? FindRenderer(GameObject node)
    {
        SkeletalMeshRenderer? renderer = node.GetComponent<SkeletalMeshRenderer>();
        if (renderer != null) return renderer;
        foreach (GameObject child in node.Children)
            if (FindRenderer(child) is { } nested) return nested;
        return null;
    }

    private static SkeletalRagdoll3D? FindRagdoll(GameObject node)
    {
        for (GameObject? current = node; current != null; current = current.Parent)
            if (current.GetComponent<SkeletalRagdoll3D>() is { } ragdoll) return ragdoll;
        return null;
    }
}
