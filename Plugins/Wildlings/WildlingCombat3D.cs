using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.Animation;
using ByteEngine.Core.Authoring;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.InputSystem;
using ByteEngine.Core.Physics;
using ByteEngine.Core.Scene;

namespace Wildlings;

/// <summary>
/// Creature kit: LMB basic attack, RMB species special, Q utility. The same
/// component runs player input and companion AI so a switch never changes the
/// rules. Damage uses the GDD 0.75 AI factor when AI-controlled.
/// </summary>
public sealed class WildlingCombat3D : Component
{
    public bool AiControlled { get; set; }

    /// <summary>False while exploring: Wildlings follow without attacking.</summary>
    public bool CombatEnabled { get; set; } = true;

    public bool AimAtPointer { get; set; } = true;

    public float AiDamageFactor { get; set; } = .75f;

    public float AiEngageRange { get; set; } = 14f;

    public Vector3 MuzzleOffset { get; set; } = new(0f, .95f, .35f);

    /// <summary>Scene object name that owns TeamManager3D. No raw GUID in the inspector.</summary>
    [SceneObjectPicker]
    public string TeamObjectName { get; set; } = "Wildling Manager";

    /// <summary>Animation profile action played on every shot. Empty disables it.</summary>
    public string AttackActionName { get; set; } = "Attack";

    private float _basicRemaining;
    private float _specialRemaining;
    private float _utilityRemaining;
    private double _scanRemaining;
    private GameObject? _target;

    public float BasicCooldownRemaining => _basicRemaining;

    public float SpecialCooldownRemaining => _specialRemaining;

    public float UtilityCooldownRemaining => _utilityRemaining;

    public GameObject? CurrentTarget => _target;

    private WildlingStats3D? Stats => GameObject?.GetComponent<WildlingStats3D>();

    private WildlingSpecies Species => Stats?.Definition ?? WildlingSpeciesDatabase.Default;

    protected override void OnUpdate()
    {
        GameObject? self = GameObject;
        Scene? scene = self?.Scene;
        if (self == null || scene == null)
        {
            return;
        }

        if (self.GetComponent<HealthComponent>() is { IsDead: true })
        {
            return;
        }

        float delta = (float)Time.DeltaTime;
        _basicRemaining = MathF.Max(0f, _basicRemaining - delta);
        _specialRemaining = MathF.Max(0f, _specialRemaining - delta);
        _utilityRemaining = MathF.Max(0f, _utilityRemaining - delta);

        if (!CombatEnabled)
        {
            return;
        }

        if (AiControlled)
        {
            UpdateAi(self, scene, delta);
        }
        else
        {
            UpdatePlayer(self, scene);
        }
    }

    private void UpdatePlayer(GameObject self, Scene scene)
    {
        if (!Input.IsGameInputCaptured)
        {
            return;
        }

        WildlingSpecies species = Species;
        Camera3D? camera = scene.ActiveCamera;

        bool basic = InputActions.IsDown("Fire") || Input.IsMouseButtonDown(MouseButton.Left);
        if (basic && _basicRemaining <= 0f)
        {
            FireBasic(self, species, Aim(self, scene, camera, null));
        }

        if (Input.IsMouseButtonPressed(MouseButton.Right) && _specialRemaining <= 0f)
        {
            FireSpecial(self, scene, species, Aim(self, scene, camera, null));
        }

        if (Input.IsKeyPressed(Key.Q) && _utilityRemaining <= 0f)
        {
            FireUtility(self, scene, species);
        }
    }

    private void UpdateAi(GameObject self, Scene scene, float delta)
    {
        _scanRemaining -= delta;
        if (_scanRemaining <= 0.0 || _target == null || IsDead(_target))
        {
            _scanRemaining = .2;
            _target = FindNearestHostile(self, scene, AiEngageRange);
        }

        if (_target == null)
        {
            return;
        }

        WildlingSpecies species = Species;
        float distance = Vector3.Distance(self.Transform.WorldPosition, _target.Transform.WorldPosition);
        if (distance > species.AttackRange)
        {
            return;
        }

        Vector3 aim = DirectionTo(self, _target);
        if (_basicRemaining <= 0f)
        {
            FireBasic(self, species, aim);
        }
        else if (_specialRemaining <= 0f)
        {
            FireSpecial(self, scene, species, aim);
        }
    }

    public void FireBasic(GameObject self, WildlingSpecies species, Vector3 direction)
    {
        float factor = AiControlled ? AiDamageFactor : 1f;
        FireShot(self, species, new Shot(
            Direction: direction,
            Damage: species.BasicDamage * factor,
            Speed: species.ProjectileSpeed,
            Radius: .16f,
            SplashRadius: 0f,
            SplashDamage: 0f,
            RootSeconds: 0f,
            BurnDps: 0f,
            BurnSeconds: 0f,
            ChainJumps: 0,
            ChainDamage: 0f));

        _basicRemaining = species.BasicInterval;
        PlayAttackAnimation(self);
    }

    public void FireSpecial(GameObject self, Scene scene, WildlingSpecies species, Vector3 direction)
    {
        float factor = AiControlled ? AiDamageFactor : 1f;
        float damage = species.SpecialDamage * factor;

        Shot shot = species.Element switch
        {
            WildlingElement.Fire => new Shot(direction, damage, 16f, .3f, species.SpecialRadius, damage * .6f, 0f, 3f, 3f, 0, 0f),
            WildlingElement.Grass => new Shot(direction, damage, 15f, .26f, species.SpecialRadius, damage * .5f, 1.6f, 0f, 0f, 0, 0f),
            WildlingElement.Electric => new Shot(direction, damage, 22f, .22f, 0f, 0f, 0f, 0f, 0f, 2, damage * .58f),
            _ => new Shot(direction, damage, 18f, .24f, 0f, 0f, 0f, 0f, 0f, 0, 0f)
        };

        FireShot(self, species, shot);
        _specialRemaining = species.SpecialCooldown;
        PlayAttackAnimation(self);
    }

    private void PlayAttackAnimation(GameObject self)
    {
        if (string.IsNullOrWhiteSpace(AttackActionName))
        {
            return;
        }

        self.GetComponent<AnimationController>()?.PlayNamedAction(AttackActionName, retrigger: true);
    }

    public void FireUtility(GameObject self, Scene scene, WildlingSpecies species)
    {
        float factor = AiControlled ? AiDamageFactor : 1f;

        switch (species.Element)
        {
            case WildlingElement.Grass:
                HealLowestAlly(self, scene, 35f);
                break;

            case WildlingElement.Fire:
                Dash(self, scene, 3.2f, 20f * factor, 1.1f);
                break;

            case WildlingElement.Electric:
                Dash(self, scene, 3f, 10f * factor, .9f);
                break;

            default:
                Dash(self, scene, 2.5f, 12f * factor, .9f);
                break;
        }

        _utilityRemaining = species.UtilityCooldown;
    }

    private void FireShot(GameObject self, WildlingSpecies species, in Shot shot)
    {
        Scene? scene = self.Scene;
        if (scene == null || shot.Direction.LengthSquared() <= .000001f)
        {
            return;
        }

        Vector3 direction = Vector3.Normalize(shot.Direction);
        Vector3 muzzle = self.Transform.WorldPosition + Vector3.Transform(MuzzleOffset, self.Transform.WorldRotation);

        GameObject projectile = scene.CreateGameObject(species.DisplayName + " shot");
        projectile.Layer = self.Layer == 3 ? 5 : 4;
        projectile.Transform.WorldPosition = muzzle;
        projectile.Transform.WorldScale = Vector3.One * MathF.Max(.14f, shot.Radius * 2f);

        projectile.AddComponent(new MeshRenderer
        {
            Primitive = PrimitiveMeshType.Sphere,
            UsePrimitive = true,
            CastShadows = false,
            ReceiveShadows = false,
            FrustumCulling = false,
            Material = new Material
            {
                BaseColor = species.ProjectileColor,
                Roughness = .35f,
                Metallic = 0f
            }
        });

        projectile.AddComponent(new WildlingProjectile3D
        {
            Velocity = direction * shot.Speed,
            Damage = shot.Damage,
            Radius = shot.Radius,
            SplashRadius = shot.SplashRadius,
            SplashDamage = shot.SplashDamage,
            RootSeconds = shot.RootSeconds,
            BurnDamagePerSecond = shot.BurnDps,
            BurnSeconds = shot.BurnSeconds,
            ChainJumps = shot.ChainJumps,
            ChainRange = 3f,
            ChainDamage = shot.ChainDamage
        }.WithOwner(self));

        projectile.AddComponent(new LifetimeComponent { LifetimeSeconds = 5f });
    }

    private Vector3 Aim(GameObject self, Scene scene, Camera3D? camera, GameObject? target)
    {
        if (target != null)
        {
            return DirectionTo(self, target);
        }

        if (camera == null)
        {
            return self.Transform.Forward;
        }

        float aspect = Input.GameViewSize.X / Math.Max(Input.GameViewSize.Y, 1f);
        Vector3 aim = PlayerShooter3D.CalculateAimDirection(self, camera, scene, aspect, AimAtPointer);
        return aim.LengthSquared() > .000001f ? aim : self.Transform.Forward;
    }

    private Vector3 DirectionTo(GameObject self, GameObject target)
    {
        Vector3 muzzle = self.Transform.WorldPosition + Vector3.Transform(MuzzleOffset, self.Transform.WorldRotation);
        Vector3 delta = target.Transform.WorldPosition + Vector3.UnitY * .9f - muzzle;
        return delta.LengthSquared() > .000001f ? Vector3.Normalize(delta) : self.Transform.Forward;
    }

    private void Dash(GameObject self, Scene scene, float distance, float damage, float radius)
    {
        Vector3 direction = self.Transform.Forward;
        direction.Y = 0f;
        direction = direction.LengthSquared() > .0001f ? Vector3.Normalize(direction) : -Vector3.UnitZ;

        Vector3 origin = self.Transform.WorldPosition;
        float travel = distance;

        if (GameplayQuery3D.SphereCast(
                scene,
                origin + Vector3.UnitY * .6f,
                direction,
                .32f,
                out RaycastHit3D hit,
                distance + .32f,
                ignore: self,
                layerMask: null,
                source: self,
                bypassCollisionMatrix: false,
                includeTriggers: false))
        {
            travel = MathF.Max(0f, hit.Distance - .35f);
        }

        Vector3 destination = origin + direction * travel;
        self.Transform.WorldPosition = destination;

        foreach (OverlapHit3D overlap in GameplayQuery3D.OverlapCapsule(
                     scene,
                     origin + Vector3.UnitY * .6f,
                     destination + Vector3.UnitY * .6f,
                     radius,
                     ignore: self,
                     layerMask: null,
                     source: self,
                     bypassCollisionMatrix: true,
                     includeTriggers: false))
        {
            if (overlap.GameObject.Layer == self.Layer)
            {
                continue;
            }

            GameObject? victim = FindHealthOwner(overlap.GameObject);
            victim?.GetComponent<HealthComponent>()?.Damage(damage);
        }
    }

    private void HealLowestAlly(GameObject self, Scene scene, float amount)
    {
        GameObject? best = null;
        float bestPercent = 1f;

        foreach (GameObject member in Party(self))
        {
            if (member.GetComponent<HealthComponent>() is not { IsDead: false } health)
            {
                continue;
            }

            if (Vector3.Distance(self.Transform.WorldPosition, member.Transform.WorldPosition) > 10f)
            {
                continue;
            }

            if (health.HealthPercent >= bestPercent)
            {
                continue;
            }

            bestPercent = health.HealthPercent;
            best = member;
        }

        best?.GetComponent<HealthComponent>()?.Heal(amount);
    }

    private IEnumerable<GameObject> Party(GameObject self)
    {
        TeamManager3D? team = string.IsNullOrWhiteSpace(TeamObjectName)
            ? null
            : self.Scene?.FindGameObject(TeamObjectName)?.GetComponent<TeamManager3D>();

        if (team == null)
        {
            yield return self;
            yield break;
        }

        foreach (GameObject member in team.Members())
        {
            yield return member;
        }
    }

    public static GameObject? FindNearestHostile(GameObject self, Scene scene, float range)
    {
        GameObject? best = null;
        float bestDistance = range;

        foreach (GameObject candidate in scene.GameObjects)
        {
            if (candidate.Layer == self.Layer)
            {
                continue;
            }

            if (candidate.GetComponent<HealthComponent>() is not { IsDead: false })
            {
                continue;
            }

            float distance = Vector3.Distance(self.Transform.WorldPosition, candidate.Transform.WorldPosition);
            if (distance >= bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            best = candidate;
        }

        return best;
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

    private static bool IsDead(GameObject gameObject) =>
        gameObject.GetComponent<HealthComponent>() is { IsDead: true };

    private readonly record struct Shot(
        Vector3 Direction,
        float Damage,
        float Speed,
        float Radius,
        float SplashRadius,
        float SplashDamage,
        float RootSeconds,
        float BurnDps,
        float BurnSeconds,
        int ChainJumps,
        float ChainDamage);
}

