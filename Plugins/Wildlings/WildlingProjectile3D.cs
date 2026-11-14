using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Physics;
using ByteEngine.Core.Scene;

namespace Wildlings;

/// <summary>
/// Creature projectile with optional splash, Burning, Root and chain jumps.
/// Kept in the plugin (instead of the engine's generic Projectile3D) because a
/// creature kit needs effect payloads on impact, not just raw hit damage.
/// </summary>
public sealed class WildlingProjectile3D : Component
{
    private bool _stopped;
    private GameObject? _owner;
    private int _ownerLayer = 2;

    public Vector3 Velocity { get; set; }

    public float Damage { get; set; } = 10f;

    public float Radius { get; set; } = .18f;

    /// <summary>Friendly-fire guard: the layer of the creature that fired.</summary>
    private int OwnerLayer => _ownerLayer;

    /// <summary>Runtime owner used for self-hit avoidance and friendly fire. Never serialized.</summary>
    public void SetOwner(GameObject owner)
    {
        _owner = owner;
        _ownerLayer = owner.Layer;
    }

    /// <summary>Fluent owner assignment so the projectile is configured in one initializer.</summary>
    public WildlingProjectile3D WithOwner(GameObject owner)
    {
        SetOwner(owner);
        return this;
    }

    public float SplashRadius { get; set; }

    public float SplashDamage { get; set; }

    public float RootSeconds { get; set; }

    public float BurnDamagePerSecond { get; set; }

    public float BurnSeconds { get; set; }

    public int ChainJumps { get; set; }

    public float ChainRange { get; set; } = 3f;

    public float ChainDamage { get; set; }

    protected override void OnUpdate()
    {
        if (AttachedGameObject?.Scene?.FixedSimulation != true)
        {
            Simulate((float)Time.DeltaTime);
        }
    }

    protected override void OnFixedUpdate() => Simulate((float)Time.FixedDeltaTime);

    private void Simulate(float delta)
    {
        if (_stopped || Velocity.LengthSquared() <= .000001f)
        {
            return;
        }

        GameObject? projectile = AttachedGameObject;
        Scene? scene = projectile?.Scene;
        if (projectile == null || scene == null)
        {
            return;
        }

        Vector3 displacement = Velocity * delta;
        float distance = displacement.Length();
        if (distance <= .000001f)
        {
            return;
        }

        Vector3 origin = projectile.Transform.WorldPosition;
        Vector3 direction = displacement / distance;
        GameObject? owner = _owner;

        if (GameplayQuery3D.SphereCast(
                scene,
                origin,
                direction,
                Radius,
                out RaycastHit3D hit,
                distance + Radius,
                ignore: owner,
                layerMask: null,
                source: projectile,
                bypassCollisionMatrix: true,
                includeTriggers: false))
        {
            // Allies soak the shot without taking damage; keep flying through them.
            if (hit.GameObject.Layer == OwnerLayer)
            {
                projectile.Transform.WorldPosition = origin + displacement;
                return;
            }

            projectile.Transform.WorldPosition = hit.Point;
            ResolveImpact(scene, projectile, hit.Point, hit.GameObject);
            scene.DestroyGameObject(projectile);
            _stopped = true;
            return;
        }

        projectile.Transform.WorldPosition = origin + displacement;
    }

    private void ResolveImpact(Scene scene, GameObject projectile, Vector3 point, GameObject hitObject)
    {
        GameObject? primary = FindHealthOwner(hitObject);
        if (primary != null)
        {
            ApplyDamage(primary, Damage);
        }

        if (SplashRadius > 0f)
        {
            foreach (OverlapHit3D overlap in GameplayQuery3D.OverlapSphere(
                         scene,
                         point,
                         SplashRadius,
                         ignore: projectile,
                         layerMask: null,
                         source: projectile,
                         bypassCollisionMatrix: true,
                         includeTriggers: false))
            {
                if (overlap.GameObject.Layer == OwnerLayer)
                {
                    continue;
                }

                GameObject? victim = FindHealthOwner(overlap.GameObject);
                if (victim == null || ReferenceEquals(victim, primary))
                {
                    continue;
                }

                ApplyDamage(victim, SplashDamage > 0f ? SplashDamage : Damage);
            }
        }

        if (ChainJumps > 0)
        {
            Chain(scene, projectile, point, primary);
        }
    }

    private void Chain(Scene scene, GameObject projectile, Vector3 point, GameObject? exclude)
    {
        float damage = ChainDamage > 0f ? ChainDamage : Damage * .5f;
        List<GameObject> alreadyHit = new();
        if (exclude != null)
        {
            alreadyHit.Add(exclude);
        }

        Vector3 search = point;

        for (int jump = 0; jump < ChainJumps; jump++)
        {
            GameObject? next = null;
            float best = float.MaxValue;

            foreach (OverlapHit3D overlap in GameplayQuery3D.OverlapSphere(
                         scene,
                         search,
                         ChainRange,
                         ignore: projectile,
                         layerMask: null,
                         source: projectile,
                         bypassCollisionMatrix: true,
                         includeTriggers: false))
            {
                if (overlap.GameObject.Layer == OwnerLayer)
                {
                    continue;
                }

                GameObject? candidate = FindHealthOwner(overlap.GameObject);
                if (candidate == null || alreadyHit.Contains(candidate))
                {
                    continue;
                }

                float distance = Vector3.Distance(search, candidate.Transform.WorldPosition);
                if (distance >= best)
                {
                    continue;
                }

                best = distance;
                next = candidate;
            }

            if (next == null)
            {
                return;
            }

            alreadyHit.Add(next);
            search = next.Transform.WorldPosition;
            ApplyDamage(next, damage);
        }
    }

    private void ApplyDamage(GameObject victim, float amount)
    {
        if (victim.GetComponent<HealthComponent>() is not { } health || health.IsDead)
        {
            return;
        }

        health.Damage(amount);

        if (BurnSeconds > 0f || RootSeconds > 0f)
        {
            WildlingStatus3D status = victim.GetComponent<WildlingStatus3D>() ??
                                      victim.AddComponent(new WildlingStatus3D());

            if (BurnSeconds > 0f)
            {
                status.ApplyBurning(BurnDamagePerSecond, BurnSeconds);
            }

            if (RootSeconds > 0f)
            {
                status.ApplyRoot(RootSeconds);
            }
        }
    }

    private static GameObject? FindHealthOwner(GameObject gameObject)
    {
        for (GameObject? current = gameObject; current != null; current = current.Parent)
        {
            if (current.GetComponent<HealthComponent>() != null)
            {
                return current;
            }
        }

        return null;
    }
}
