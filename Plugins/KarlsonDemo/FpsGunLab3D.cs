using ByteEngine.Core;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Scene;
using System.ComponentModel;
using Component = ByteEngine.Core.Scene.Component;

namespace KarlsonDemo.Plugin;

/// <summary>Equipment orchestration only. Weapons are discovered by component, not names or types.</summary>
public sealed class FpsGunLab3D : Component
{
    [SceneObjectReference(typeof(Camera3D)),DisplayName("View Camera"),Category("Bindings"),Description("Pick the camera used to aim weapon shots.")]
    public Guid CameraObjectId { get; set; }
    [SceneObjectReference,DisplayName("Weapon Root"),Category("Bindings"),Description("Weapons with Weapon Instance below this object form the equipment list. Applied when Play starts.")]
    public Guid InventoryRootId { get; set; }
    [Browsable(false)] // Serialized compatibility only. Presentation belongs to event sheets.
    public Guid StatusTextObjectId { get; set; }
    [Category("Input"),Description("Disable when an event sheet handles fire input.")]
    public bool UseBuiltInFireInput { get; set; } = true;
    [Category("Starting Equipment"),Description("Weapon Definition slot selected when Play starts. Slots are zero based.")]
    public int StartingSlot { get; set; }
    [DisplayName("Equipped Slot")]
    public int SelectedSlot { get; private set; }
    public int ShotsFired { get; private set; }
    public WeaponInstance3D? SelectedWeapon => _weapons.FirstOrDefault(w => w.Definition?.Slot == SelectedSlot);
    [DisplayName("Magazine Ammo")]
    public int Ammo => SelectedWeapon?.CurrentAmmo ?? 0;
    public int ReserveAmmo => SelectedWeapon?.ReserveAmmo ?? 0;
    public bool Reloading => SelectedWeapon?.Reloading == true;
    public bool CanFire => SelectedWeapon?.CanFire == true;
    public bool HasWeapon => SelectedWeapon != null;
    public int WeaponCount => _weapons.Length;
    public string WeaponName => SelectedWeapon?.Definition?.DisplayName ?? "";
    public int MagazineCapacity => SelectedWeapon?.Definition?.MagazineCapacity ?? 0;
    public int ReserveCapacity => SelectedWeapon?.Definition?.ReserveCapacity ?? 0;
    public int AmmoPerShot => SelectedWeapon?.Definition?.AmmoPerShot ?? 0;
    public bool InfiniteReserve => SelectedWeapon?.Definition?.InfiniteReserve == true;
    public bool CanReload => SelectedWeapon?.CanReload == true;
    public float ReloadRemainingSeconds => SelectedWeapon?.ReloadRemainingSeconds ?? 0;
    public float ReloadProgress => SelectedWeapon?.ReloadProgress ?? 0;
    public float CooldownRemaining => SelectedWeapon?.CooldownRemaining ?? 0;
    public float DamagePerPellet => SelectedWeapon?.Definition?.DamagePerPellet ?? 0;
    public float RoundsPerMinute => SelectedWeapon?.Definition?.RoundsPerMinute ?? 0;
    public float Range => SelectedWeapon?.Definition?.Range ?? 0;
    public float SpreadDegrees => SelectedWeapon?.Definition?.SpreadDegrees ?? 0;
    public int PelletsPerShot => SelectedWeapon?.Definition?.PelletsPerShot ?? 0;
    public string ShotMode => SelectedWeapon?.Definition?.ShotMode.ToString() ?? "";
    public string TriggerMode => SelectedWeapon?.Definition?.TriggerMode.ToString() ?? "";
    public string ReloadMode => SelectedWeapon?.Definition?.ReloadMode.ToString() ?? "";
    public override int UpdateOrder => -1100;
    private GameObject? _eye;
    private WeaponInstance3D[] _weapons = [];
    private bool _ready;

    protected override void OnStart()
    {
        _eye = GameObject.Scene?.FindGameObject(CameraObjectId);
        var root = GameObject.Scene?.FindGameObject(InventoryRootId) ?? _eye;
        _weapons = root == null ? [] : Descendants(root).Select(o => o.GetComponent<WeaponInstance3D>())
            .OfType<WeaponInstance3D>().Where(w => w.Definition != null).ToArray();
        // Let every rig initialize before hiding unequipped presentation on the first update.
        foreach (var weapon in _weapons) weapon.GameObject.Active = true;
    }
    private static IEnumerable<GameObject> Descendants(GameObject root)
    {
        foreach (var child in root.Children)
        { yield return child; foreach (var item in Descendants(child)) yield return item; }
    }
    public void Select(int slot)
    {
        var next = _weapons.FirstOrDefault(w => w.Definition?.Slot == slot);
        if (next == null || (_ready && next == SelectedWeapon && next.Equipped)) return;
        SelectedWeapon?.Equip(false); SelectedSlot = slot;
        foreach (var weapon in _weapons) weapon.GameObject.Active = weapon == next;
        next.Equip(true);
    }
    public bool Fire()
    {
        _eye = GameObject.Scene?.FindGameObject(CameraObjectId);
        if (_eye == null || SelectedWeapon is not {} weapon) return false;
        GameObject.GetComponent<FirstPersonCamera3D>()?.SnapToView();
        if (!weapon.Fire(_eye.Transform.WorldPosition, _eye.Transform.Forward, GameObject)) return false;
        ShotsFired++; return true;
    }
    public bool Reload() => SelectedWeapon?.Reload() == true;
    public void CancelReload() => SelectedWeapon?.CancelReload();
    protected override void OnUpdate()
    {
        float dt = (float)Time.DeltaTime;
        foreach (var weapon in _weapons) weapon.Tick(dt);
        if (!_ready) { Select(StartingSlot); _ready = true; }
        if (UseBuiltInFireInput && Input.IsGameInputCaptured && Input.IsMouseButtonDown(MouseButton.Left) &&
            (SelectedWeapon?.Definition?.TriggerMode == WeaponTriggerMode.Automatic || Input.IsMouseButtonPressed(MouseButton.Left))) Fire();
    }
}
