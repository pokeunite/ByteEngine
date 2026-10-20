using ByteEngine.Core.Scene;
using ByteEngine.Core.Animation;
using ByteEngine.Core.Assets;

namespace KarlsonDemo.Plugin;

public enum WeaponShotMode { Hitscan, Projectile }
public enum WeaponTriggerMode { SemiAutomatic, Automatic }
public enum WeaponReloadMode { Magazine, PerRound }
public enum WeaponMuzzleSource { Automatic, AnimationSocket, ModelBone, LocalOffset }

/// <summary>Author on a weapon Blueprint. Contains tuning, never remaining ammunition.</summary>
public sealed class WeaponDefinition3D : Component
{
    public string DisplayName { get; set; } = "Pistol";
    public int Slot { get; set; }
    public WeaponShotMode ShotMode { get; set; }
    public WeaponTriggerMode TriggerMode { get; set; }
    public WeaponReloadMode ReloadMode { get; set; }
    public int MagazineCapacity { get; set; } = 12;
    public int ReserveCapacity { get; set; } = 120;
    public int AmmoPerShot { get; set; } = 1;
    public bool InfiniteReserve { get; set; }
    public float RoundsPerMinute { get; set; } = 270;
    public float DamagePerPellet { get; set; } = 25;
    public int PelletsPerShot { get; set; } = 1;
    public float SpreadDegrees { get; set; }
    public float Range { get; set; } = 80;
    public int HitMaskBits { get; set; } = -1;
    public float HitImpulse { get; set; } = 3;
    public float ReloadSeconds { get; set; } = 1.6f;
    public float EmptyReloadSeconds { get; set; } = 1.6f;
    public bool AllowReloadInterrupt { get; set; } = true;
    public float ReloadSpeedMultiplier { get; set; } = 1;
    public bool MuzzleFlashEnabled { get; set; } = true;
    public AssetReference MuzzleFlashEffect { get; set; } = AssetReference.Empty;
    public float MuzzleFlashSize { get; set; } = .6f;
    public float EquipSeconds { get; set; } = .45f;
    public float ProjectileSpeed { get; set; } = 30;
    public float ProjectileLifetime { get; set; } = 5;
    public int MaxActiveProjectiles { get; set; } = 128;
    public AssetReference ProjectileBlueprint { get; set; } = AssetReference.Empty;
    public WeaponMuzzleSource MuzzleSource { get; set; }
    public string MuzzleSocket { get; set; } = "WeaponMuzzle";
    public string MuzzleBone { get; set; } = "MuzzleSocket";
    public System.Numerics.Vector3 MuzzleBoneOffset { get; set; }
    public System.Numerics.Vector3 MuzzleLocalOffset { get; set; }
    public bool ShowShotTrace { get; set; } = true;
    public bool ShowCameraAimRay { get; set; }
    public float TraceSeconds { get; set; } = .3f;
    public float TraceWidth { get; set; } = .012f;
    public float ProjectileTrailLength { get; set; } = 1.4f;
    public float MuzzleLightIntensity { get; set; } = 2.5f;
    public float MuzzleLightRange { get; set; } = 3f;

    internal static float Bound(float value, float fallback, float min, float max) =>
        Math.Clamp(float.IsFinite(value) ? value : fallback, min, max);
}
