using ByteEngine.Core.Scene;
using ByteEngine.Core.Animation;
using ByteEngine.Core.Assets;

namespace KarlsonDemo.Plugin;

public enum WeaponShotMode { Hitscan, Projectile }
public enum WeaponTriggerMode { SemiAutomatic, Automatic }
public enum WeaponReloadMode { Magazine, PerRound }
public enum WeaponMuzzleSource { Automatic, AnimationSocket, ModelBone, LocalOffset, NamedModelPoint, SceneObject }

/// <summary>Author on a weapon Blueprint. Contains tuning, never remaining ammunition.</summary>
public sealed class WeaponDefinition3D : Component
{
    [System.ComponentModel.Category("Character Animation"), System.ComponentModel.DisplayName("Preview Character"),
     System.ComponentModel.Description("Optional character for the socket and blendspace pickers when authoring a world weapon. Equipping still uses the collecting player's Character Animation Controller.")]
    [SceneObjectReference(typeof(AnimationController))]
    public Guid AnimationControllerObjectId { get; set; }

    [System.ComponentModel.Category("Character Animation"),
     System.ComponentModel.Description("Character model socket for this weapon, for example PistolGrip, RifleGrip or ShotgunGrip. Runtime snaps the model root directly to this socket, matching Socket Preview. Empty keeps legacy parenting.")]
    [AnimationSocketPicker]
    public string GripSocket { get; set; } = string.Empty;
    [System.ComponentModel.Category("Character Animation"),
     System.ComponentModel.Description("Named blendspace in the player's Animation Profile (for example One Hand or Two Hand). Empty uses unarmed/profile locomotion. Applied only when equipped, not when stored in inventory.")]
    [AnimationBlendSpacePicker]
    public string LocomotionBlendSpace { get; set; } = string.Empty;
    public string DisplayName { get; set; } = "Pistol";
    public int Slot { get; set; }
    public WeaponShotMode ShotMode { get; set; }
    public WeaponTriggerMode TriggerMode { get; set; }
    public WeaponReloadMode ReloadMode { get; set; }
    public int MagazineCapacity { get; set; } = 12;
    public int ReserveCapacity { get; set; } = 120;
    public int AmmoPerShot { get; set; } = 1;
    public bool InfiniteReserve { get; set; }
    [System.ComponentModel.Category("Ammunition"),System.ComponentModel.Description("Automatically reload an equipped empty magazine once its final shot cooldown ends, if reserve ammo is available.")]
    public bool AutoReload { get; set; }
    public float RoundsPerMinute { get; set; } = 270;
    public float DamagePerPellet { get; set; } = 25;
    public int PelletsPerShot { get; set; } = 1;
    public float SpreadDegrees { get; set; }
    [System.ComponentModel.Category("Accuracy"),System.ComponentModel.DisplayName("Base Inaccuracy (degrees)")]
    public float AccuracyDegrees { get; set; }
    [System.ComponentModel.Category("Accuracy")] public float MovingInaccuracyDegrees { get; set; }
    [System.ComponentModel.Category("Accuracy")] public float BloomPerShotDegrees { get; set; }
    [System.ComponentModel.Category("Accuracy")] public float MaximumBloomDegrees { get; set; } = 4;
    [System.ComponentModel.Category("Accuracy")] public float AccuracyRecoveryDegreesPerSecond { get; set; } = 8;
    [System.ComponentModel.Category("Accuracy")] public float AccuracyRecoveryDelay { get; set; } = .12f;
    [System.ComponentModel.Category("Recoil")] public float RecoilPitchPerShot { get; set; }
    [System.ComponentModel.Category("Recoil")] public float RecoilYawPerShot { get; set; }
    [System.ComponentModel.Category("Recoil")] public float MaximumRecoilDegrees { get; set; } = 6;
    [System.ComponentModel.Category("Recoil")] public float RecoilRecoveryDegreesPerSecond { get; set; } = 12;
    [System.ComponentModel.Category("Recoil")] public float RecoilRecoveryDelay { get; set; } = .12f;
    [System.ComponentModel.Category("HUD")] public AssetReference HudIcon { get; set; } = AssetReference.Empty;
    [System.ComponentModel.Category("AI Hearing"),System.ComponentModel.Description("Audible gunshot radius in metres. Zero is silent; hearing still respects the listener settings.")]
    public float NoiseRadius { get; set; } = 24;
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
    [System.ComponentModel.Category("Projectiles")] public float ProjectileRadius {get;set;}=.025f;
    [System.ComponentModel.Category("Projectiles")] public float ProjectileVisualSize {get;set;}=.06f;
    [System.ComponentModel.Category("Projectile trail")] public System.Numerics.Vector4 TrailCoreColor {get;set;}=new(1,.95f,.7f,1);
    [System.ComponentModel.Category("Projectile trail")] public System.Numerics.Vector4 TrailGlowColor {get;set;}=new(1,.55f,.12f,.55f);
    [System.ComponentModel.Category("Projectile trail")] public float TrailGlowScale {get;set;}=3;
    [System.ComponentModel.Category("Projectile trail")] public float TrailBrightness {get;set;}=1.5f;
    [System.ComponentModel.Category("Projectile trail"),System.ComponentModel.Description("Short fade at impact for built-in bullets. Custom projectile blueprints retain their own destruction behaviour.")]
    public float TrailImpactFadeSeconds {get;set;}=.07f;
    public float ProjectileLifetime { get; set; } = 5;
    public int MaxActiveProjectiles { get; set; } = 128;
    public AssetReference ProjectileBlueprint { get; set; } = AssetReference.Empty;
    [System.ComponentModel.Category("Muzzle"),System.ComponentModel.Description("Named Model Point uses a point placed visually in the model editor. Scene Object uses any scene Empty.")]
    public WeaponMuzzleSource MuzzleSource { get; set; }
    [SceneObjectReference,System.ComponentModel.Category("Muzzle"),System.ComponentModel.DisplayName("Muzzle Object"),System.ComponentModel.Description("Optional scene Empty at the barrel tip. Takes priority when assigned; parent it under the gun so it follows movement.")]
    public Guid MuzzleObjectId { get; set; }
    [ModelAttachmentPointPicker,System.ComponentModel.Category("Muzzle"),System.ComponentModel.DisplayName("Muzzle Point")]
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
