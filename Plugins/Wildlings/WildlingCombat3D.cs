using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.Animation;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Vfx;
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

    /// <summary>Effect that rides the shot: a looping local-space emitter, so it travels with the bolt.</summary>
    public VfxPreset ProjectileVfxPreset { get; set; } = VfxPreset.Fireball;

    public bool ProjectileVfxEnabled { get; set; } = true;

    /// <summary>Scale multiplier for the projectile effect (1 = preset size).</summary>
    public float ProjectileVfxSize { get; set; } = 1.8f;

    public float ProjectileVfxIntensity { get; set; } = 1.2f;

    /// <summary>Tints the shot effect with the species projectile colour.</summary>
    public bool TintProjectileVfx { get; set; } = true;

    /// <summary>One-shot burst at the muzzle so the shot visibly leaves the creature.</summary>
    public bool MuzzleFlashEnabled { get; set; } = true;

    public float MuzzleFlashSize { get; set; } = 1.2f;

    /// <summary>Emissive bolt material multiplier; keeps the core readable against the glow.</summary>
    public float ProjectileEmissionIntensity { get; set; } = 2.2f;

    /// <summary>Authored .bvfx that rides the bolt. Empty falls back to <see cref="ProjectileVfxPreset"/>.</summary>
    public AssetReference ProjectileEffect { get; set; } = AssetReference.Empty;

    /// <summary>Authored .bvfx muzzle burst. Empty falls back to the MuzzleFlash preset.</summary>
    public AssetReference MuzzleEffect { get; set; } = AssetReference.Empty;

    /// <summary>Authored .bvfx impact burst, handed to each spawned bolt.</summary>
    public AssetReference ImpactEffect { get; set; } = AssetReference.Empty;

    /// <summary>Scene object name that owns TeamManager3D. No raw GUID in the inspector.</summary>
    [SceneObjectPicker]
    public string TeamObjectName { get; set; } = "Wildling Manager";

    /// <summary>Animation profile action played on every shot. Empty disables it.</summary>
    public string AttackActionName { get; set; } = "Attack";

    /// <summary>
    /// Keep locomotion on the body while firing: the attack action is skipped whenever the
    /// creature is walking or running, so the run clip never gets replaced by a shot pose.
    /// The action still plays when standing still.
    /// </summary>
    public bool SkipAttackAnimationWhileMoving { get; set; } = true;

    /// <summary>
    /// Animation profile action for the melee thrust. Falls back to
    /// <see cref="AttackActionName"/> when the creature has no such action.
    /// </summary>
    public string MeleeActionName { get; set; } = "Tackle";

    /// <summary>
    /// Turn the body toward the shot direction before firing, so the projectile
    /// leaves along the facing the player actually sees.
    /// </summary>
    public bool FaceAimOnAttack { get; set; } = true;

    /// <summary>Forward lunge of the melee thrust, in metres.</summary>
    public float MeleeLungeDistance { get; set; } = 1.4f;

    /// <summary>Melee damage as a multiple of the species basic damage.</summary>
    public float MeleeDamageScale { get; set; } = 1.8f;

    /// <summary>Radius of the melee sweep.</summary>
    public float MeleeRadius { get; set; } = .55f;

    public float MeleeCooldown { get; set; } = 1f;

    /// <summary>Distance at which companion AI prefers the melee thrust.</summary>
    public float AiMeleeRange { get; set; } = 2.2f;

    /// <summary>
    /// Aim the muzzle at the world point under the crosshair instead of firing along
    /// the camera's raw ray. The camera sits high and behind, so raw-ray shots dive
    /// into the floor a few metres ahead.
    /// </summary>
    public bool AimAtCrosshair { get; set; } = true;

    /// <summary>How far the crosshair ray looks for a point to aim at.</summary>
    public float AimRange { get; set; } = 60f;

    /// <summary>
    /// Survivors default: pick the nearest hostile in range, turn to it and fire on its
    /// own cooldown without any input. Manual/pointer fire is the optional path.
    /// </summary>
    public bool AutoTarget { get; set; }

    /// <summary>Range at which <see cref="AutoTarget"/> engages, in metres.</summary>
    public float AutoTargetRange { get; set; } = 12f;

    /// <summary>
    /// Fire automatically from code. Turn this off when an Event Sheet rule owns the decision
    /// (<c>creature.hasTarget</c> → <c>creature.fireBasic</c>); target tracking still runs so
    /// the condition, the facing and the aim keep working.
    /// </summary>
    public bool AutoFireFromCode { get; set; } = true;

    /// <summary>Run-upgrade damage multiplier applied to every attack.</summary>
    public float DamageMultiplier { get; set; } = 1f;

    /// <summary>Run-upgrade fire-interval multiplier (0.9 = 10% faster).</summary>
    public float CooldownMultiplier { get; set; } = 1f;

    /// <summary>Seconds the melee lunge takes. Longer reads as a thrust, shorter as a shove.</summary>
    public float MeleeThrustSeconds { get; set; } = .22f;

    /// <summary>
    /// Seconds the creature pauses — and turns to face the aim — before a projectile
    /// leaves. A shot starting from a wrong facing still fires; it just spends this
    /// beat squaring up first.
    /// </summary>
    public float ShotPauseSeconds { get; set; } = .18f;

    /// <summary>
    /// Park the player controller for <see cref="ShotPauseSeconds"/> while a ranged shot winds up.
    /// Off by default: with auto-fire it stops the creature between shots. Melee keeps its lunge park.
    /// </summary>
    public bool PauseInputWhileFiring { get; set; }

    private float _basicRemaining;
    private float _specialRemaining;
    private float _utilityRemaining;
    private float _meleeRemaining;
    private int _pendingShot;
    private float _pendingShotRemaining;
    private PlayerController3D? _parkedController;
    private float _parkedRemaining;
    private double _scanRemaining;
    private GameObject? _target;

    public float BasicCooldownRemaining => _basicRemaining;

    public float SpecialCooldownRemaining => _specialRemaining;

    public float UtilityCooldownRemaining => _utilityRemaining;

    public float MeleeCooldownRemaining => _meleeRemaining;

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
        _meleeRemaining = MathF.Max(0f, _meleeRemaining - delta);

        // Release a shot that spent its wind-up: re-aim at the moment it leaves so a
        // moving target is tracked, then spawn the projectile.
        if (_pendingShot != 0)
        {
            _pendingShotRemaining -= delta;
            if (_pendingShotRemaining <= 0f)
            {
                int kind = _pendingShot;
                _pendingShot = 0;

                if (CombatEnabled && self.GetComponent<HealthComponent>() is not { IsDead: true })
                {
                    Vector3 direction = Aim(self, scene, scene.ActiveCamera, null);
                    FaceAttackDirection(self, direction);

                    if (kind == 1)
                    {
                        FireBasic(self, Species, direction);
                    }
                    else
                    {
                        FireSpecial(self, scene, Species, direction);
                    }
                }
            }
        }

        if (_parkedController != null)
        {
            _parkedRemaining -= delta;
            if (_parkedRemaining <= 0f)
            {
                // Only restore input if this creature is still the one being driven;
                // a switch during the thrust hands control elsewhere.
                if (_parkedController.GameObject.GetComponent<WildlingCombat3D>() is { AiControlled: false })
                {
                    _parkedController.Enabled = true;
                }

                _parkedController = null;
            }
        }

        if (!CombatEnabled)
        {
            return;
        }

        // Player attacks are wired in the Event Sheet (Assets/Logic/WildlingTeamRules):
        // mouse/key conditions call TryFireBasic / TryFireSpecial / TryMelee. No input
        // map is hard-coded here any more.
        // Survivors rule: one creature, automatic targeting and fire. This runs before
        // the AI branch so a player-controlled creature still shoots on its own.
        if (AutoTarget && !AiControlled && CombatEnabled)
        {
            UpdateAutoTarget(self, scene);
        }

        if (AiControlled)
        {
            UpdateAi(self, scene, delta);
        }
    }

    /// <summary>
    /// Picks the nearest hostile in range, turns the body to it and fires on the basic
    /// cooldown. No input, no crosshair: this is the default survivors attack.
    /// </summary>
    private void UpdateAutoTarget(GameObject self, Scene scene)
    {
        GameObject? target = FindNearestHostile(self, scene, MathF.Max(1f, AutoTargetRange));
        _target = target != null && !IsDead(target) ? target : null;

        if (target == null)
        {
            return;
        }

        Vector3 direction = DirectionTo(self, target);
        FaceAttackDirection(self, direction);

        if (!AutoFireFromCode)
        {
            return;
        }

        bool busy = _basicRemaining > 0f || _pendingShot != 0 || _parkedController != null;
        if (!busy)
        {
            FireBasic(self, Species, direction);
        }
    }

    // ---- Callable entry points (Event Sheet actions) ----

    /// <summary>Basic ranged attack toward the current aim. Returns false when not ready.</summary>
    public bool TryFireBasic()
    {
        GameObject? self = GameObject;
        Scene? scene = self?.Scene;
        if (self == null || scene == null || !CombatEnabled || _basicRemaining > 0f || _pendingShot != 0)
        {
            return false;
        }

        BeginQueuedShot(1, self, scene, Species.BasicInterval);
        return true;
    }

    /// <summary>Species special toward the current aim. Returns false when not ready.</summary>
    public bool TryFireSpecial()
    {
        GameObject? self = GameObject;
        Scene? scene = self?.Scene;
        if (self == null || scene == null || !CombatEnabled || _specialRemaining > 0f || _pendingShot != 0)
        {
            return false;
        }

        BeginQueuedShot(2, self, scene, Species.SpecialCooldown);
        return true;
    }

    /// <summary>
    /// Starts a shot: turn to the aim, play the attack action, pause movement for
    /// <see cref="ShotPauseSeconds"/>, then let OnUpdate release the projectile.
    /// </summary>
    private void BeginQueuedShot(int kind, GameObject self, Scene scene, float cooldown)
    {
        Vector3 direction = Aim(self, scene, scene.ActiveCamera, null);
        float pause = MathF.Max(.02f, ShotPauseSeconds);

        FaceAttackDirection(self, direction);
        PlayAttackAnimation(self);

        if (PauseInputWhileFiring)
        {
            ParkPlayerInput(self, pause);
        }

        _pendingShot = kind;
        _pendingShotRemaining = pause;

        if (kind == 1)
        {
            _basicRemaining = cooldown;
        }
        else
        {
            _specialRemaining = cooldown;
        }
    }

    /// <summary>Melee thrust toward the current aim. Returns false when not ready.</summary>
    public bool TryMelee()
    {
        GameObject? self = GameObject;
        Scene? scene = self?.Scene;
        if (self == null || scene == null || !CombatEnabled || _meleeRemaining > 0f)
        {
            return false;
        }

        MeleeAttack(self, scene, Species, Aim(self, scene, scene.ActiveCamera, null));
        return true;
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

        // Close enough to shove: the thrust hits harder than a bolt at this range.
        if (distance <= MathF.Max(.6f, AiMeleeRange))
        {
            if (_meleeRemaining <= 0f)
            {
                MeleeAttack(self, scene, Species, aim);
            }

            return;
        }

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
        FaceAttackDirection(self, direction);

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

        _basicRemaining = species.BasicInterval * Math.Clamp(CooldownMultiplier, .1f, 3f);
        PlayAttackAnimation(self);
    }

    public void FireSpecial(GameObject self, Scene scene, WildlingSpecies species, Vector3 direction)
    {
        float factor = AiControlled ? AiDamageFactor : 1f;
        float damage = species.SpecialDamage * factor;
        FaceAttackDirection(self, direction);

        Shot shot = species.Element switch
        {
            WildlingElement.Fire => new Shot(direction, damage, 16f, .3f, species.SpecialRadius, damage * .6f, 0f, 3f, 3f, 0, 0f),
            WildlingElement.Grass => new Shot(direction, damage, 15f, .26f, species.SpecialRadius, damage * .5f, 1.6f, 0f, 0f, 0, 0f),
            WildlingElement.Electric => new Shot(direction, damage, 22f, .22f, 0f, 0f, 0f, 0f, 0f, 2, damage * .58f),
            _ => new Shot(direction, damage, 18f, .24f, 0f, 0f, 0f, 0f, 0f, 0, 0f)
        };

        FireShot(self, species, shot);
        _specialRemaining = species.SpecialCooldown * Math.Clamp(CooldownMultiplier, .1f, 3f);
        PlayAttackAnimation(self);
    }

    private void PlayAttackAnimation(GameObject self)
    {
        if (string.IsNullOrWhiteSpace(AttackActionName))
        {
            return;
        }

        if (SkipAttackAnimationWhileMoving && IsMoving(self))
        {
            return;
        }

        self.GetComponent<AnimationController>()?.PlayNamedAction(AttackActionName, retrigger: true);
    }

    /// <summary>
    /// Tackle thrust: face the attack direction, play the melee action, lunge forward
    /// and sweep the enemies the lunge passed through. The Tackle clip belongs here,
    /// not in the jump slot.
    /// </summary>
    public void MeleeAttack(GameObject self, Scene scene, WildlingSpecies species, Vector3 direction)
    {
        float factor = AiControlled ? AiDamageFactor : 1f;

        Vector3 flat = direction;
        flat.Y = 0f;
        if (flat.LengthSquared() <= .0001f)
        {
            flat = self.Transform.Forward;
            flat.Y = 0f;
        }

        flat = flat.LengthSquared() > .0001f ? Vector3.Normalize(flat) : -Vector3.UnitZ;
        FaceAttackDirection(self, flat);
        PlayMeleeAnimation(self);

        Vector3 origin = self.Transform.WorldPosition;
        float lunge = MathF.Max(0f, MeleeLungeDistance);
        float thrustSeconds = MathF.Max(.05f, MeleeThrustSeconds);

        // Momentum shift: the lunge owns movement for its duration instead of snapping
        // the transform, and the player controller is parked so held movement input
        // cannot drag the creature sideways mid-thrust.
        if (lunge > .001f)
        {
            if (self.GetComponent<CharacterController3D>() is { } motor)
            {
                motor.BeginBurstMotion(flat * (lunge / thrustSeconds), thrustSeconds);
                ParkPlayerInput(self, thrustSeconds);
            }
            else
            {
                self.Transform.WorldPosition = origin + flat * lunge;
            }
        }

        float damage = species.BasicDamage * MathF.Max(.1f, MeleeDamageScale) * factor;

        // The hit volume follows the intended thrust path, not the frame-by-frame
        // position, so the sweep does not depend on burst timing.
        foreach (OverlapHit3D overlap in GameplayQuery3D.OverlapCapsule(
                     scene,
                     origin + Vector3.UnitY * .6f,
                     origin + flat * lunge + Vector3.UnitY * .6f,
                     MathF.Max(.15f, MeleeRadius),
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
            victim?.GetComponent<HealthComponent>()?.Damage(damage * MathF.Max(0f, DamageMultiplier));
        }

        _meleeRemaining = MathF.Max(.05f, MeleeCooldown);
    }

    /// <summary>True while the creature is walking or running, i.e. locomotion owns the body.</summary>
    private static bool IsMoving(GameObject self)
    {
        if (self.GetComponent<AnimationController>() is { } animation)
        {
            return animation.LocomotionSpeed > animation.MoveThreshold;
        }

        return self.GetComponent<CharacterController3D>() is { } controller && controller.Speed > .05f;
    }

    /// <summary>Melee action, falling back to the ranged attack action.</summary>
    private void PlayMeleeAnimation(GameObject self)
    {
        AnimationController? animations = self.GetComponent<AnimationController>();
        if (animations == null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(MeleeActionName) &&
            animations.PlayNamedAction(MeleeActionName, retrigger: true))
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(AttackActionName))
        {
            animations.PlayNamedAction(AttackActionName, retrigger: true);
        }
    }

    /// <summary>
    /// Parks the player controller for the thrust so held movement input cannot fight
    /// the burst. The fight timer in OnUpdate restores it.
    /// </summary>
    private void ParkPlayerInput(GameObject self, float seconds)
    {
        if (AiControlled)
        {
            return;
        }

        if (self.GetComponent<PlayerController3D>() is not { Enabled: true } controller)
        {
            return;
        }

        controller.Enabled = false;
        _parkedController = controller;
        _parkedRemaining = MathF.Max(_parkedRemaining, seconds);
    }

    /// <summary>
    /// Turns the body toward the attack so the muzzle, the model and the projectile
    /// all point the same way. Yaw only; the aim keeps its pitch.
    /// </summary>
    private void FaceAttackDirection(GameObject self, Vector3 direction)
    {
        if (!FaceAimOnAttack)
        {
            return;
        }

        Vector3 flat = direction;
        flat.Y = 0f;
        if (flat.LengthSquared() <= .0001f)
        {
            return;
        }

        flat = Vector3.Normalize(flat);
        float yaw = MathF.Atan2(-flat.X, -flat.Z) * 180f / MathF.PI;
        self.Transform.EulerAngles = new Vector3(0f, yaw, 0f);
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

        // An authored effect (e.g. the fireball flipbook) *is* the visual. Keeping the opaque
        // sphere would sit inside the billboard and hide its centre, so it is only added when
        // the shot falls back to a preset.
        bool effectIsVisual = ProjectileVfxEnabled && !ProjectileEffect.IsEmpty;

        if (!effectIsVisual)
        {
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
                    Metallic = 0f,
                    EmissionEnabled = ProjectileEmissionIntensity > 0f,
                    EmissionColor = new Vector3(species.ProjectileColor.X, species.ProjectileColor.Y, species.ProjectileColor.Z),
                    EmissionIntensity = MathF.Max(0f, ProjectileEmissionIntensity)
                }
            });
        }

        if (ProjectileVfxEnabled)
        {
            projectile.AddComponent(new VfxPlayer
            {
                Effect = ProjectileEffect,
                Preset = ProjectileVfxPreset,
                PlayOnStart = true,
                Size = MathF.Max(.05f, ProjectileVfxSize),
                Intensity = MathF.Max(0f, ProjectileVfxIntensity),
                Tint = TintProjectileVfx ? species.ProjectileColor : Vector4.One
            });
        }

        if (MuzzleFlashEnabled)
        {
            SpawnMuzzleFlash(scene, muzzle, direction, species);
        }

        projectile.AddComponent(new WildlingProjectile3D
        {
            Velocity = direction * shot.Speed,
            Damage = shot.Damage * MathF.Max(0f, DamageMultiplier),
            Radius = shot.Radius,
            SplashRadius = shot.SplashRadius,
            SplashDamage = shot.SplashDamage,
            RootSeconds = shot.RootSeconds,
            BurnDamagePerSecond = shot.BurnDps,
            BurnSeconds = shot.BurnSeconds,
            ChainJumps = shot.ChainJumps,
            ChainRange = 3f,
            ChainDamage = shot.ChainDamage,
            ImpactPreset = VfxPreset.Impact,
            ImpactEffect = ImpactEffect,
            ImpactEnabled = ProjectileVfxEnabled,
            ImpactSize = MathF.Max(.2f, ProjectileVfxSize),
            ImpactTint = species.ProjectileColor
        }.WithOwner(self));

        projectile.AddComponent(new LifetimeComponent { LifetimeSeconds = 5f });
    }

    /// <summary>Short-lived muzzle burst aimed along the shot.</summary>
    private void SpawnMuzzleFlash(Scene scene, Vector3 muzzle, Vector3 direction, WildlingSpecies species)
    {
        GameObject flash = scene.CreateGameObject(species.DisplayName + " muzzle");
        flash.Layer = 5;
        flash.Transform.WorldPosition = muzzle;

        float pitch = MathF.Asin(Math.Clamp(direction.Y, -1f, 1f));
        float yaw = MathF.Atan2(-direction.X, -direction.Z);
        flash.Transform.WorldRotation = Quaternion.CreateFromYawPitchRoll(yaw, pitch, 0f);

        flash.AddComponent(new VfxPlayer
        {
            Effect = MuzzleEffect,
            Preset = VfxPreset.MuzzleFlash,
            PlayOnStart = true,
            Size = MathF.Max(.1f, MuzzleFlashSize),
            Tint = species.ProjectileColor,
            DestroyWhenFinished = true
        });

        flash.AddComponent(new LifetimeComponent { LifetimeSeconds = .6f });
    }

    private Vector3 Aim(GameObject self, Scene scene, Camera3D? camera, GameObject? target)
    {
        if (target != null)
        {
            return DirectionTo(self, target);
        }

        // Survivors: when auto-targeting is on, the tracked hostile is the aim. This keeps
        // Event Sheet calls (creature.fireBasic) pointed at the target the rule reacted to.
        if (AutoTarget && _target != null && !IsDead(_target))
        {
            return DirectionTo(self, _target);
        }

        if (camera == null)
        {
            return self.Transform.Forward;
        }

        Vector3 aim;
        if (AimAtPointer)
        {
            float aspect = Input.GameViewSize.X / Math.Max(Input.GameViewSize.Y, 1f);
            aim = PlayerShooter3D.CalculateAimDirection(self, camera, scene, aspect, true);
        }
        else
        {
            // The reticle sits at screen centre plus the team's crosshair offset, so the
            // shot ray passes through exactly where the reticle is drawn.
            (Vector3 _, Vector3 rayDirection) = camera.ScreenPointToRay(
                CrosshairViewPoint(),
                Input.GameViewSize.X / MathF.Max(Input.GameViewSize.Y, 1f));
            aim = rayDirection;
        }

        if (aim.LengthSquared() <= .000001f)
        {
            return self.Transform.Forward;
        }

        return AimAtCrosshair ? AimAtCrosshairPoint(self, scene, camera, aim) : aim;
    }

    /// <summary>Normalised view point of the reticle: screen centre plus the team offset.</summary>
    private Vector2 CrosshairViewPoint()
    {
        Vector2 offset = Vector2.Zero;

        if (!string.IsNullOrWhiteSpace(TeamObjectName) &&
            GameObject?.Scene?.FindGameObject(TeamObjectName)?.GetComponent<TeamManager3D>() is { } team)
        {
            offset = team.CrosshairOffset;
        }

        Vector2 size = Input.GameViewSize;
        return new Vector2(
            Math.Clamp(.5f + offset.X / MathF.Max(1f, size.X), 0f, 1f),
            Math.Clamp(.5f + offset.Y / MathF.Max(1f, size.Y), 0f, 1f));
    }

    /// <summary>
    /// Casts the camera ray through the crosshair to find what the reticle covers,
    /// then aims the muzzle at that point. Firing the raw camera ray instead throws
    /// bolts into the ground because the ray starts above and behind the creature.
    /// </summary>
    private Vector3 AimAtCrosshairPoint(GameObject self, Scene scene, Camera3D camera, Vector3 rayDirection)
    {
        Vector3 origin = camera.Transform.WorldPosition;
        float range = MathF.Max(1f, AimRange);
        Vector3 point = origin + rayDirection * range;

        if (GameplayQuery3D.Raycast(
                scene,
                origin,
                rayDirection,
                out RaycastHit3D hit,
                range,
                ignore: self,
                layerMask: null,
                source: self,
                bypassCollisionMatrix: true,
                includeTriggers: false))
        {
            point = hit.Point;
        }

        Vector3 muzzle = self.Transform.WorldPosition + Vector3.Transform(MuzzleOffset, self.Transform.WorldRotation);
        Vector3 delta = point - muzzle;
        return delta.LengthSquared() > .000001f ? Vector3.Normalize(delta) : rayDirection;
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
