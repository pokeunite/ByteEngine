using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.Vfx;
using ByteEngine.Core.Animation;
using ByteEngine.Core.Classification;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Physics;
using ByteEngine.Core.Scene;

namespace KarlsonDemo.Plugin;

/// <summary>Per-object ammunition and state; reused for every weapon archetype.</summary>
public sealed class WeaponInstance3D : Component
{
    public int CurrentAmmo { get; set; } = 12;
    public int ReserveAmmo { get; set; } = 60;
    public int RandomSeed { get; set; } = 17;
    public WeaponDefinition3D? Definition => GameObject.GetComponent<WeaponDefinition3D>();
    public bool Reloading => _reloadRemaining > 0;
    public bool Equipped { get; private set; }
    public int ShotsFired { get; private set; }
    public int HitCount { get; private set; }
    public bool FiredThisFrame { get; private set; }
    public bool HitThisFrame { get; private set; }
    public bool ReloadCompletedThisFrame { get; private set; }
    public bool EquippedThisFrame { get; private set; }
    public bool ReloadStartedThisFrame { get; private set; }
    public bool ReloadCancelledThisFrame { get; private set; }
    public bool ReloadWasEmpty { get; private set; }
    public long EquipSequence { get; private set; }
    public long ReloadStartSequence { get; private set; }
    public long ReloadCancelSequence { get; private set; }
    public long ReloadCompleteSequence { get; private set; }
    public long HitSequence { get; private set; }
    public bool LastShotWasHitscan { get; private set; }
    private readonly List<WeaponShotHit> _shotHits = [];
    /// <summary>Actual pellet hits from the last accepted shot, nearest first; a miss clears the list.</summary>
    public IReadOnlyList<WeaponShotHit> ShotHits => _shotHits;
    public float CooldownRemaining => _cooldown;
    public bool CanFire => Equipped && _cooldown <= 0 &&
        Definition is {} d && (!Reloading || (d.ReloadMode == WeaponReloadMode.PerRound && d.AllowReloadInterrupt)) && CurrentAmmo >= Math.Clamp(d.AmmoPerShot, 1, 10000);
    public event Action<WeaponInstance3D>? Fired;
    public event Action<WeaponInstance3D>? ReloadCompleted;
    public event Action<RaycastHit3D>? Hit;
    private float _cooldown, _reloadRemaining;
    private Random? _random;
    private VfxPlayer? _muzzleFlash;

    protected override void OnStart() { ClampAmmo(); _random = new Random(RandomSeed); }
    public void ClampAmmo()
    {
        if (Definition is not {} d) return;
        CurrentAmmo = Math.Clamp(CurrentAmmo, 0, Math.Clamp(d.MagazineCapacity, 1, 10000));
        ReserveAmmo = Math.Clamp(ReserveAmmo, 0, Math.Clamp(d.ReserveCapacity, 0, 1000000));
    }
    public void SetAmmo(int magazine, int reserve)
    { CancelReload(); CurrentAmmo = magazine; ReserveAmmo = reserve; ClampAmmo(); }
    public void AddReserve(int amount)
    { ReserveAmmo = (int)Math.Clamp((long)ReserveAmmo + amount, 0, 1000000); ClampAmmo(); }
    public void Equip(bool equipped)
    {
        Equipped = equipped; CancelReload();
        if (equipped && Definition is {} d)
        { _cooldown = WeaponDefinition3D.Bound(d.EquipSeconds, .45f, 0, 10); EquippedThisFrame=true;EquipSequence++; }
    }
    public void CancelReload()
    {
        if(Reloading){ReloadCancelledThisFrame=true;ReloadCancelSequence++;}
        _reloadRemaining = 0;
    }
    public bool Reload()
    {
        if (!Equipped || Reloading || Definition is not {} d) return false;
        ClampAmmo();
        if (CurrentAmmo >= Math.Clamp(d.MagazineCapacity, 1, 10000) || (!d.InfiniteReserve && ReserveAmmo == 0)) return false;
        bool empty = CurrentAmmo == 0;ReloadWasEmpty=empty;
        _reloadRemaining = WeaponDefinition3D.Bound(empty ? d.EmptyReloadSeconds : d.ReloadSeconds, 1.6f, .05f, 30)
            / WeaponDefinition3D.Bound(d.ReloadSpeedMultiplier, 1, .1f, 10);
        ReloadStartedThisFrame=true;ReloadStartSequence++;
        return true;
    }
    internal void Tick(float dt)
    {
        FiredThisFrame = HitThisFrame = ReloadCompletedThisFrame = false;
        EquippedThisFrame=ReloadStartedThisFrame=ReloadCancelledThisFrame=false;
        _cooldown = Math.Max(0, _cooldown - dt);
        if (!Reloading) return;
        _reloadRemaining = Math.Max(0, _reloadRemaining - dt);
        if (_reloadRemaining > 0 || Definition is not {} d) return;
        int needed = Math.Max(0, Math.Clamp(d.MagazineCapacity, 1, 10000) - CurrentAmmo);
        int transfer = d.ReloadMode == WeaponReloadMode.PerRound ? Math.Min(1, needed) : needed;
        if (!d.InfiniteReserve) transfer = Math.Min(transfer, Math.Max(0, ReserveAmmo));
        CurrentAmmo += transfer;
        if (!d.InfiniteReserve) ReserveAmmo -= transfer;
        ClampAmmo(); ReloadCompletedThisFrame = true;ReloadCompleteSequence++; ReloadCompleted?.Invoke(this);
        if (d.ReloadMode == WeaponReloadMode.PerRound) Reload();
    }
    public bool Fire(Vector3 origin, Vector3 direction, GameObject owner)
    {
        ClampAmmo();
        if (!CanFire || Definition is not {} d || GameObject.Scene is not {} scene ||
            !float.IsFinite(direction.LengthSquared()) || direction.LengthSquared() < 1e-6f) return false;
        direction = Vector3.Normalize(direction);
        _random ??= new Random(RandomSeed);
        int count = Math.Clamp(d.PelletsPerShot, 1, 32);
        if (d.ShotMode == WeaponShotMode.Projectile && scene.GameObjects.Count(o =>
                o.GetComponent<Projectile3D>()?.OwnerId == owner.Id) + count > Math.Clamp(d.MaxActiveProjectiles, 1, 512))
            return false;
        if (Reloading) CancelReload();
        _shotHits.Clear();HitThisFrame=false;LastShotWasHitscan=d.ShotMode==WeaponShotMode.Hitscan;
        Vector3 muzzlePosition = TryGetMuzzleWorldPosition(out var resolvedMuzzle) ? resolvedMuzzle : origin;
        float range = WeaponDefinition3D.Bound(d.Range, 80, .1f, 1000);
        var mask = LayerMask.FromLayers(Enumerable.Range(0, 32).Where(bit => (d.HitMaskBits & (1 << bit)) != 0).ToArray());
        for (int n = 0; n < count; n++)
        {
            Vector3 shot = Spread(direction, d.SpreadDegrees);
            Vector3 end = origin + shot * range;
            if (d.ShotMode == WeaponShotMode.Hitscan)
            {
                if (GameplayQuery3D.Raycast(scene, origin, shot, out var hit, range, owner, mask, includeTriggers: false))
                {
                    end = hit.Point; HitCount++; HitThisFrame = true;_shotHits.Add(new(hit,shot)); Hit?.Invoke(hit);
                    for (GameObject? target = hit.GameObject; target != null; target = target.Parent)
                        if (target.GetComponent<HealthComponent>() is {} health)
                        { health.Damage(WeaponDefinition3D.Bound(d.DamagePerPellet, 25, 0, 100000)); break; }
                    hit.GameObject.GetComponent<Rigidbody3D>()?.AddImpulseAtPosition(shot * WeaponDefinition3D.Bound(d.HitImpulse, 3, 0, 1000), end);
                }
            }
            else
            {
                var launcher = owner.GetComponent<ProjectileLauncher3D>() ?? owner.AddComponent(new ProjectileLauncher3D());
                Vector3 muzzle = muzzlePosition;
                // Aim at the camera's first obstruction, but launch from the model socket.
                if (GameplayQuery3D.Raycast(scene, origin, shot, out var aim, range, owner, mask, includeTriggers: false)) end = aim.Point;
                launcher.MuzzleOffset = Vector3.Transform(muzzle, Matrix4x4.Invert(owner.Transform.WorldMatrix, out var inverse) ? inverse : Matrix4x4.Identity);
                launcher.ProjectileBlueprint = d.ProjectileBlueprint;
                launcher.ProjectileSpeed = WeaponDefinition3D.Bound(d.ProjectileSpeed, 30, .1f, 500);
                launcher.Damage = WeaponDefinition3D.Bound(d.DamagePerPellet, 25, 0, 100000);
                launcher.FireCooldown = 0; launcher.ResetCooldown();
                if (launcher.Fire(end - muzzle) is {} projectile)
                {
                    projectile.GetComponent<Projectile3D>()!.CollisionMask = mask;
                    var lifetime = projectile.GetComponent<LifetimeComponent>() ?? projectile.AddComponent(new LifetimeComponent());
                    lifetime.LifetimeSeconds = WeaponDefinition3D.Bound(d.ProjectileLifetime, 5, .1f, 30);
                }
            }
            if (d.ShowShotTrace)
            {
                // The camera ray determines hits; the muzzle tracer makes the shot readable in FPS view.
                Vector3 visualStart = muzzlePosition;
                WeaponTrace.Draw(scene, visualStart, end, d.TraceSeconds, d.TraceWidth,
                    d.ShotMode == WeaponShotMode.Projectile ? new(.05f,.85f,1,1) : new(1,.6f,.06f,1));
                if (d.ShowCameraAimRay)
                    WeaponTrace.Draw(scene, origin + shot * .35f, end, d.TraceSeconds, .002f, new(.95f,.12f,.2f,1));
            }
        }
        CurrentAmmo -= Math.Clamp(d.AmmoPerShot, 1, 10000);
        _cooldown = 60 / WeaponDefinition3D.Bound(d.RoundsPerMinute, 270, 1, 3000);
        _shotHits.Sort((a,b)=>a.Hit.Distance.CompareTo(b.Hit.Distance));if(_shotHits.Count>0)HitSequence++;
        ShotsFired++; FiredThisFrame = true; Flash(TryGetMuzzleWorldPosition(out var flashPosition) ? flashPosition : muzzlePosition, direction); Fired?.Invoke(this); return true;
    }
    private void Flash(Vector3 position, Vector3 direction)
    {
        if (Definition is not { MuzzleFlashEnabled: true } d || GameObject.Scene is not {} scene) return;
        if (_muzzleFlash == null)
        {
            var emitter = scene.CreateGameObject("Weapon muzzle flash");
            emitter.SetParent(GameObject, false);
            _muzzleFlash = emitter.AddComponent(new VfxPlayer { PlayOnStart = false, Preset = VfxPreset.MuzzleFlash, DistanceQuality = false });
        }
        _muzzleFlash.Effect = d.MuzzleFlashEffect;
        _muzzleFlash.Size = WeaponDefinition3D.Bound(d.MuzzleFlashSize, .6f, .05f, 3);
        _muzzleFlash.Transform.WorldPosition = position;
        // VFX muzzle preset emits along local -Z; orient it along the shot.
        var axis = Vector3.Cross(-Vector3.UnitZ, direction);
        var dot = Math.Clamp(Vector3.Dot(-Vector3.UnitZ, direction), -1, 1);
        _muzzleFlash.Transform.WorldRotation = axis.LengthSquared() > 1e-6f
            ? Quaternion.CreateFromAxisAngle(Vector3.Normalize(axis), MathF.Acos(dot))
            : dot > 0 ? Quaternion.Identity : Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI);
        _muzzleFlash.Play(restart: true);
    }
    /// <summary>Uses editor-authored animation-workspace sockets, imported bones, or a local offset.</summary>
    public bool TryGetMuzzleWorldPosition(out Vector3 position)
    {
        position = default;
        if (Definition is not {} d) return false;
        if (d.MuzzleSource is WeaponMuzzleSource.Automatic or WeaponMuzzleSource.AnimationSocket)
        {
            if (SkeletalSocketResolver.TryGetSocketWorldPosition(GameObject, d.MuzzleSocket, out position))
                return IsFinite(position);
            if (d.MuzzleSource == WeaponMuzzleSource.AnimationSocket) return false;
        }
        if (d.MuzzleSource is WeaponMuzzleSource.Automatic or WeaponMuzzleSource.ModelBone)
        {
            if (SkeletalSocketResolver.TryResolveRenderer(GameObject, null, out var renderer) &&
                renderer?.TryGetBoneWorldMatrix(d.MuzzleBone, out var matrix) == true)
            { position = matrix.Translation; return IsFinite(position); }
            if (d.MuzzleSource == WeaponMuzzleSource.ModelBone) return false;
        }
        position = Vector3.Transform(d.MuzzleLocalOffset, Transform.WorldMatrix);
        return IsFinite(position);
    }
    private static bool IsFinite(Vector3 value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);

    private Vector3 Spread(Vector3 forward, float degrees)
    {
        float radius = MathF.Tan(WeaponDefinition3D.Bound(degrees, 0, 0, 45) * MathF.PI / 180);
        if (radius == 0) return forward;
        Vector3 right = Vector3.Normalize(Vector3.Cross(forward, Math.Abs(forward.Y) > .99f ? Vector3.UnitX : Vector3.UnitY));
        Vector3 up = Vector3.Cross(right, forward);
        float r = MathF.Sqrt((float)_random!.NextDouble()) * radius;
        float angle = (float)_random.NextDouble() * MathF.Tau;
        return Vector3.Normalize(forward + right * (r * MathF.Cos(angle)) + up * (r * MathF.Sin(angle)));
    }
}

public readonly record struct WeaponShotHit(RaycastHit3D Hit,Vector3 Direction);

internal static class WeaponTrace
{
    internal static void Draw(ByteEngine.Core.Scene.Scene scene, Vector3 start, Vector3 end, float seconds, float width, Vector4 color)
    {
        Vector3 delta = end - start;
        if (delta.LengthSquared() < 1e-6f) return;
        // Bound debug geometry even when a fast weapon is held down.
        if (scene.GameObjects.Count(o => o.Name == "Weapon shot trace") >= 64) return;
        var line = scene.CreateGameObject("Weapon shot trace");
        line.Transform.WorldPosition = (start + end) / 2;
        Vector3 dir = Vector3.Normalize(delta);
        float dot = Math.Clamp(Vector3.Dot(Vector3.UnitZ, dir), -1, 1);
        line.Transform.WorldRotation = dot > .9999f ? Quaternion.Identity : dot < -.9999f ?
            Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI) :
            Quaternion.CreateFromAxisAngle(Vector3.Normalize(Vector3.Cross(Vector3.UnitZ, dir)), MathF.Acos(dot));
        float thickness = WeaponDefinition3D.Bound(width, .012f, .002f, .1f);
        line.Transform.LocalScale = new(thickness, thickness, delta.Length());
        line.AddComponent(new MeshRenderer { UsePrimitive = true, CastShadows = false, ReceiveShadows = false,
            Material = new Material { BaseColor = color, BlendMode = BlendMode3D.Additive,
                DepthTest = false, DepthWriteMode = DepthWriteMode3D.Disabled, CullMode = CullMode3D.None } });
        line.AddComponent(new LifetimeComponent { LifetimeSeconds = WeaponDefinition3D.Bound(seconds, .3f, .05f, 2) });
    }
}
