using ByteEngine.Core;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Scene;

namespace KarlsonDemo.Plugin;

/// <summary>Equipment orchestration only. Weapons are discovered by component, not names or types.</summary>
public sealed class FpsGunLab3D : Component
{
    public Guid CameraObjectId { get; set; }
    public Guid InventoryRootId { get; set; }
    public Guid StatusTextObjectId { get; set; }
    public bool UseBuiltInFireInput { get; set; } = true;
    public int StartingSlot { get; set; }
    public int SelectedSlot { get; private set; }
    public int ShotsFired { get; private set; }
    public WeaponInstance3D? SelectedWeapon => _weapons.FirstOrDefault(w => w.Definition?.Slot == SelectedSlot);
    public int Ammo => SelectedWeapon?.CurrentAmmo ?? 0;
    public int ReserveAmmo => SelectedWeapon?.ReserveAmmo ?? 0;
    public bool Reloading => SelectedWeapon?.Reloading == true;
    public bool CanFire => SelectedWeapon?.CanFire == true;
    public override int UpdateOrder => -1100;
    private GameObject? _eye;
    private WeaponInstance3D[] _weapons = [];
    private bool _ready;
    private float _hud;

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
        _hud -= dt;
        if (_hud <= 0)
        {
            _hud = .05f;
            if (GameObject.Scene?.FindGameObject(StatusTextObjectId)?.GetComponent<UiText>() is {} text && SelectedWeapon?.Definition is {} d)
                text.Text = $"{d.DisplayName}   {Ammo}/{d.MagazineCapacity}   RESERVE {ReserveAmmo}   {d.ShotMode}   {(Reloading ? "RELOADING" : "")}";
        }
    }
}
