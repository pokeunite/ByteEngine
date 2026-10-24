using ByteEngine.Core;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Animation;
using ByteEngine.Core.Physics;
using ByteEngine.Core.Characters;
using System.Numerics;
using System.ComponentModel;
using Component = ByteEngine.Core.Scene.Component;

namespace KarlsonDemo.Plugin;

public enum WeaponAimMode { ViewCamera, TopDownCursor }

/// <summary>Equipment orchestration only. Weapons are discovered by component, not names or types.</summary>
public sealed class FpsGunLab3D : Component, ICharacterDebugSource
{
    [Category("Live State"), Description("Result of the last pickup action. Bind this to your own UI text through an Event Sheet.")]
    public string LastPickupResult { get; private set; } = "";
    public bool TryPickUpNearest(bool equip = true)
    {
        var candidates = GameObject.Scene?.GameObjects.Select(o => o.GetComponent<WeaponPickup3D>())
            .OfType<WeaponPickup3D>().Where(p => p.Enabled && p.GameObject.ActiveInHierarchy && p.Weapon?.Carried != true)
            .OrderBy(p => p.HorizontalDistanceTo(this)).ToArray() ?? [];
        var item = candidates.FirstOrDefault(p => p.CanCollect(this)) ?? candidates.FirstOrDefault();
        if (item == null) { LastPickupResult = "No world weapons found"; return false; }
        return PickUp(item, equip);
    }
    [Category("Aiming"), Description("View Camera for FPS; Top Down Cursor fires from the weapon muzzle toward the native Top Down Controller's cursor point.")]
    public WeaponAimMode AimMode { get; set; }
    [SceneObjectReference(typeof(AnimationController)), DisplayName("Character Animation Controller"), Category("Character Animation"),
     Description("Player object containing the Animation Controller. Empty uses this equipment object's controller. Weapon Definition selects its named locomotion blendspace.")]
    public Guid AnimationControllerObjectId { get; set; }
    [Category("Character Animation"), Description("Blendspace used with no equipped weapon. Empty restores the Animation Profile's default.")]
    [AnimationBlendSpacePicker]
    public string UnarmedBlendSpace { get; set; } = string.Empty;
    [Category("Character Animation"), DisplayName("Locomotion Binding Error")] public string LocomotionBindingError { get; private set; } = string.Empty;
    private AnimationController? _locomotionController;
    private string? _appliedLocomotion;
    [Category("Character Animation")] public string WeaponAttachmentError { get; private set; } = string.Empty;

    public string GetCharacterDebugText()
    {
        var pickups=GameObject.Scene?.GameObjects.Select(o=>o.GetComponent<WeaponPickup3D>()).OfType<WeaponPickup3D>()
            .Where(p=>p.Weapon?.Carried!=true).OrderBy(p=>p.HorizontalDistanceTo(this)).ToArray() ?? [];
        var nearest=pickups.FirstOrDefault();var ready=pickups.FirstOrDefault(p=>p.CanCollect(this));
        string inventory=$"Weapons: {WeaponCount}/{(CarryLimit>0?CarryLimit.ToString():"unlimited")} | Equipped: {(HasWeapon?WeaponName:"Unarmed")}";
        if(nearest==null)return inventory+"\nPickup: no world weapons";
        string name=nearest.Weapon?.Definition?.DisplayName ?? nearest.GameObject.Name;
        string status=$"Pickup: {name} {nearest.HorizontalDistanceTo(this):F2}m / {nearest.PickupRadius:F1}m | {nearest.GetCollectionStatus(this)}";
        if(ready!=null&&ready!=nearest)status+=$" | Ready: {ready.Weapon?.Definition?.DisplayName}";
        if(ready!=null)status+=$"\nPickup ready: {ready.Weapon?.Definition?.DisplayName}";
        return inventory+"\n"+status+(LastPickupResult.Length>0?"\nLast pickup: "+LastPickupResult:"");
    }

    public bool RefreshWeaponAttachment()
    {
        string socket = SelectedWeapon?.Definition?.GripSocket ?? string.Empty;
        WeaponAttachmentError = string.Empty;
        if (SelectedWeapon is not {} weapon || string.IsNullOrWhiteSpace(socket)) return true;
        var target = AnimationControllerObjectId == Guid.Empty ? GameObject : GameObject.Scene?.FindGameObject(AnimationControllerObjectId);
        target?.GetComponent<AnimationController>()?.RefreshRenderers();
        if (target != null && SkeletalAttachmentService.AttachToSocket(weapon.GameObject, target, socket,
                AttachmentTransformRule.SnapToTarget, AttachmentTransformRule.SnapToTarget, AttachmentTransformRule.SnapToTarget)) return true;
        WeaponAttachmentError = $"Socket '{socket}' was not found on the selected character model.";
        return false;
    }

    public bool RefreshWeaponLocomotion()
    {
        var controller = (AnimationControllerObjectId == Guid.Empty ? GameObject : GameObject.Scene?.FindGameObject(AnimationControllerObjectId))?.GetComponent<AnimationController>();
        if (_locomotionController != null && _locomotionController != controller)
        {
            _locomotionController.SetLocomotionBlendSpace(string.Empty);
            _locomotionController = null;
            _appliedLocomotion = null;
        }
        string desired = SelectedWeapon?.Definition?.LocomotionBlendSpace ?? string.Empty;
        if (string.IsNullOrWhiteSpace(desired)) desired = UnarmedBlendSpace;
        if (controller == null)
        {
            LocomotionBindingError = string.IsNullOrWhiteSpace(desired) ? string.Empty : "Choose a Character Animation Controller on Weapon Equipment.";
            return string.IsNullOrWhiteSpace(desired);
        }
        if (controller == _locomotionController && desired == _appliedLocomotion) return true;
        if (!controller.SetLocomotionBlendSpace(desired))
        {
            // Never leave the previous gun's stance active after selecting an invalid binding.
            controller.SetLocomotionBlendSpace(string.Empty);
            LocomotionBindingError = $"Blendspace '{desired}' is missing or empty in the player's Animation Profile.";
            _appliedLocomotion = null;
            return false;
        }
        _locomotionController = controller;
        _appliedLocomotion = desired;
        LocomotionBindingError = string.Empty;
        return true;
    }
    [SceneObjectReference(typeof(Camera3D)),DisplayName("View Camera"),Category("Bindings"),Description("Pick the camera used to aim weapon shots.")]
    public Guid CameraObjectId { get; set; }
    [SceneObjectReference,DisplayName("Weapon Root"),Category("Bindings"),Description("Optional root for preloaded starting equipment. World pickups may reference any weapon in this scene; parenting is not required.")]
    public Guid InventoryRootId { get; set; }
    [Browsable(false)] // Serialized compatibility only. Presentation belongs to event sheets.
    public Guid StatusTextObjectId { get; set; }
    [Category("Input"),Description("Disable when an event sheet handles fire input.")]
    public bool UseBuiltInFireInput { get; set; } = true;
    [Category("Starting Equipment"),Description("Weapon Definition slot selected when Play starts. Slots are zero based.")]
    public int StartingSlot { get; set; }
    [Category("Starting Equipment"),Description("Start unarmed and acquire weapons through event-sheet pickup actions.")]
    public bool StartWithNoWeapons { get; set; }
    [Category("Starting Equipment"),DisplayName("Default Starting Weapon"),SceneObjectReference(typeof(WeaponInstance3D)),Description("Choose any scene gun, regardless of parenting or pickup range. Turn Start With No Weapons off. This gun is carried and equipped at startup; drop restores its world scale and physics.")]
    public Guid DefaultWeaponObjectId { get; set; }
    [Category("Inventory"),Description("Maximum carried weapons. Zero means unlimited. Starting weapons also obey this limit.")]
    public int CarryLimit { get; set; }
    [Category("Inventory"), Description("Horizontal distance in front of the player when dropping a weapon. Top-down drops use the character's facing, not the downward camera angle.")]
    public float DropDistance { get; set; } = 1.5f;
    [DisplayName("Equipped Slot")]
    public int SelectedSlot { get; private set; } = -1;
    public int ShotsFired { get; private set; }
    public WeaponInstance3D? SelectedWeapon => _weapons.FirstOrDefault(w => w.Definition?.Slot == SelectedSlot);
    [DisplayName("Magazine Ammo")]
    public int Ammo => SelectedWeapon?.CurrentAmmo ?? 0;
    public int ReserveAmmo => SelectedWeapon?.ReserveAmmo ?? 0;
    public bool Reloading => SelectedWeapon?.Reloading == true;
    public bool CanFire => SelectedWeapon?.CanFire == true;
    public bool HasWeapon => SelectedWeapon != null;
    public int WeaponCount => _weapons.Length;
    [Browsable(false)] public IReadOnlyList<WeaponInstance3D> CarriedWeapons => _weapons;
    public int SelectedInventoryIndex => Array.IndexOf(_weapons,SelectedWeapon);
    public bool InventoryFull => CarryLimit > 0 && WeaponCount >= CarryLimit;
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
    public float InaccuracyDegrees => SelectedWeapon?.CurrentInaccuracyDegrees ?? 0;
    public float AccuracyBloom => SelectedWeapon?.AccuracyBloom ?? 0;
    public Vector2 RecoilAngles => SelectedWeapon?.RecoilAngles ?? Vector2.Zero;
    public int PelletsPerShot => SelectedWeapon?.Definition?.PelletsPerShot ?? 0;
    public string ShotMode => SelectedWeapon?.Definition?.ShotMode.ToString() ?? "";
    public string TriggerMode => SelectedWeapon?.Definition?.TriggerMode.ToString() ?? "";
    public string ReloadMode => SelectedWeapon?.Definition?.ReloadMode.ToString() ?? "";
    public override int UpdateOrder => -1100;
    private GameObject? _eye;
    private WeaponInstance3D[] _weapons = [];
    private WeaponInstance3D[] _available = [];
    private bool _ready;
    private readonly Dictionary<WeaponInstance3D,(Vector3 Scale,Quaternion Rotation)> _worldPickupTransforms=[];
    private readonly Dictionary<WeaponInstance3D,List<(Component Component,bool Enabled)>> _suspendedWorldPhysics=[];

    protected override void OnStart()
    {
        _eye = GameObject.Scene?.FindGameObject(CameraObjectId);
        var root = GameObject.Scene?.FindGameObject(InventoryRootId) ?? _eye;
        var defaultWeapon=GameObject.Scene?.FindGameObject(DefaultWeaponObjectId)?.GetComponent<WeaponInstance3D>();
        _available = (root == null ? Enumerable.Empty<WeaponInstance3D>() : Descendants(root).Select(o=>o.GetComponent<WeaponInstance3D>()).OfType<WeaponInstance3D>())
            .Concat(defaultWeapon==null?[]:new[]{defaultWeapon}).Where(w=>w.Definition!=null&&w.InventoryOwner==null).Distinct().ToArray();
        var starting = StartWithNoWeapons ? [] : _available.Where(w=>w.InventoryOwner==null&&(w==defaultWeapon||w.StartInInventory)).OrderBy(w=>w==defaultWeapon?0:w.Definition!.Slot==StartingSlot?1:2).ThenBy(w=>w.Definition!.Slot).GroupBy(w=>w.Definition!.Slot).Select(g=>g.First()).ToArray();
        _weapons = (CarryLimit>0 ? starting.Take(CarryLimit) : starting).ToArray();
        foreach(var weapon in _available)weapon.InventoryOwner=_weapons.Contains(weapon)?this:null;
        foreach(var weapon in _weapons)PrepareWorldWeaponForCarry(weapon);
        // Let every rig initialize before hiding unequipped presentation on the first update.
        foreach (var weapon in _available) weapon.GameObject.Active = true;
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
        RefreshWeaponLocomotion();
        RefreshWeaponAttachment();
    }
    public bool Owns(WeaponInstance3D weapon) => _weapons.Contains(weapon);
    public void SelectInventoryIndex(int index) { if(index>=0&&index<_weapons.Length)Select(_weapons[index].Definition!.Slot); }
    public bool CanPickUp(WeaponInstance3D weapon) => weapon.GameObject.Scene == GameObject.Scene && weapon.Definition != null && weapon.InventoryOwner==null && !InventoryFull && !_weapons.Any(w=>w.Definition?.Slot==weapon.Definition?.Slot);
    public bool PickUp(WeaponPickup3D? pickup,bool equip=true)
    {
        if(pickup==null){LastPickupResult="Selected world pickup was not found";return false;}
        if(!pickup.CanCollect(this)||pickup.Weapon is not {} weapon)
        { LastPickupResult = pickup.GetCollectionStatus(this); return false; }
        return GiveWeapon(weapon,equip);
    }
    /// <summary>Event-driven starting/loadout grants; intentionally bypasses pickup distance, never carry limits or ownership.</summary>
    public bool GiveWeapon(WeaponInstance3D? weapon,bool equip=true)
    {
        if(weapon==null||!CanPickUp(weapon)){LastPickupResult="Cannot give weapon: check ownership, carry limit and unique weapon slot";return false;}
        PrepareWorldWeaponForCarry(weapon);
        var pickup=GameObject.Scene?.GameObjects.Select(o=>o.GetComponent<WeaponPickup3D>()).FirstOrDefault(p=>p?.Weapon==weapon);
        _weapons=[.._weapons,weapon];weapon.InventoryOwner=this;
        if(pickup!=null&&pickup.GameObject!=weapon.GameObject)pickup.GameObject.Active=false;
        if(equip||SelectedWeapon==null)Select(weapon.Definition!.Slot);else weapon.GameObject.Active=false;
        LastPickupResult = "Picked up " + weapon.Definition!.DisplayName;
        return true;
    }
    private void PrepareWorldWeaponForCarry(WeaponInstance3D weapon)
    {
        var pickup=GameObject.Scene?.GameObjects.Select(o=>o.GetComponent<WeaponPickup3D>()).FirstOrDefault(p=>p?.Weapon==weapon);
        if(pickup!=null&&pickup.GameObject==weapon.GameObject)
        {
            _worldPickupTransforms[weapon]=(weapon.Transform.WorldScale,weapon.Transform.WorldRotation);
            if(pickup.SuspendPhysicsWhenCarried)
            {
                var states=new[]{weapon.GameObject}.Concat(Descendants(weapon.GameObject)).SelectMany(o=>o.Components)
                    .Where(c=>c is Collider3D or Rigidbody3D).Select(c=>(Component:c,Enabled:c.Enabled)).ToList();
                _suspendedWorldPhysics[weapon]=states;
                foreach(var state in states)state.Component.Enabled=false;
            }
        }
    }
    public bool Drop(WeaponInstance3D? weapon=null)
    {
        weapon??=SelectedWeapon;if(weapon==null||!Owns(weapon))return false;
        var pickup=GameObject.Scene?.GameObjects.Select(o=>o.GetComponent<WeaponPickup3D>()).FirstOrDefault(p=>p?.Weapon==weapon);
        if(pickup==null)return false;
        bool selected=weapon==SelectedWeapon;weapon.Equip(false);weapon.InventoryOwner=null;weapon.GameObject.Active=false;
        _weapons=_weapons.Where(w=>w!=weapon).ToArray();
        Vector3 forward = AimMode == WeaponAimMode.TopDownCursor ? Transform.Forward : (_eye?.Transform.Forward ?? Transform.Forward);
        forward.Y = 0;
        if (!float.IsFinite(forward.LengthSquared()) || forward.LengthSquared() < 1e-6f)
        {
            forward = Transform.Forward; forward.Y = 0;
        }
        forward = forward.LengthSquared() > 1e-6f ? Vector3.Normalize(forward) : -Vector3.UnitZ;
        float distance = WeaponDefinition3D.Bound(DropDistance, 1.5f, .25f, 5f);
        float clearance = WeaponDefinition3D.Bound(pickup.DropGroundClearance, .3f, .01f, 2f);
        Vector3 position = Transform.WorldPosition + forward * distance;
        if (GameObject.Scene is {} scene && GameplayQuery3D.Raycast(scene, position + Vector3.UnitY * 2,
                -Vector3.UnitY, out var ground, 6, GameObject, includeTriggers: false) && ground.Normal.Y > .2f)
            position.Y = ground.Point.Y + clearance;
        else
            position.Y = Transform.WorldPosition.Y + clearance;
        if(pickup.GameObject==weapon.GameObject)
        {
            SkeletalAttachmentService.Detach(weapon.GameObject,keepWorldTransform:true);
            weapon.GameObject.SetParent(null,worldPositionStays:true);
            var restored=_worldPickupTransforms.GetValueOrDefault(weapon,(Vector3.One,Quaternion.Identity));
            weapon.Transform.WorldScale=restored.Item1;weapon.Transform.WorldRotation=restored.Item2;
        }
        pickup.Transform.WorldPosition = position;
        if(_suspendedWorldPhysics.Remove(weapon,out var physics))
            foreach(var state in physics)
            {
                if(state.Component is Rigidbody3D body){body.Velocity=Vector3.Zero;body.AngularVelocity=Vector3.Zero;}
                state.Component.Enabled=state.Enabled;
            }
        pickup.GameObject.Active=true;
        if(selected){SelectedSlot=-1;if(_weapons.FirstOrDefault() is {} next)Select(next.Definition!.Slot);else RefreshWeaponLocomotion();}
        return true;
    }
    public bool Fire()
    {
        _eye = GameObject.Scene?.FindGameObject(CameraObjectId);
        if (SelectedWeapon is not {} weapon) return false;
        System.Numerics.Vector3 origin, direction;
        if (AimMode == WeaponAimMode.TopDownCursor)
        {
            origin = weapon.TryGetMuzzleWorldPosition(out var muzzle) ? muzzle : Transform.WorldPosition + System.Numerics.Vector3.UnitY;
            var aim = GameObject.GetComponent<TopDownController3D>();
            direction = aim?.HasCursorAim == true ? aim.AimWorldPoint - origin : Transform.Forward;
        }
        else
        {
            if (_eye == null) return false;
            GameObject.GetComponent<FirstPersonCamera3D>()?.SnapToView();
            origin = _eye.Transform.WorldPosition; direction = _eye.Transform.Forward;
        }
        if (!weapon.Fire(origin, direction, GameObject)) return false;
        ShotsFired++; return true;
    }
    public bool Reload() => SelectedWeapon?.Reload() == true;
    protected override void OnDestroy()
    {
        _locomotionController?.SetLocomotionBlendSpace(string.Empty);
        _locomotionController = null;
        _appliedLocomotion = null;
    }
    public void CancelReload() => SelectedWeapon?.CancelReload();
    protected override void OnUpdate()
    {
        float dt = (float)Time.DeltaTime;
        foreach (var weapon in _weapons) weapon.Tick(dt);
        if (!_ready) { foreach(var weapon in _available)if(Owns(weapon)||weapon.GameObject.GetComponent<WeaponPickup3D>()==null)weapon.GameObject.Active=false;var preferred=_weapons.FirstOrDefault(w=>w.GameObject.Id==DefaultWeaponObjectId);Select(preferred?.Definition?.Slot??(_weapons.Any(w=>w.Definition?.Slot==StartingSlot)?StartingSlot:_weapons.FirstOrDefault()?.Definition?.Slot??-1)); RefreshWeaponLocomotion(); _ready = true; }
        if (UseBuiltInFireInput && Input.IsGameInputCaptured && Input.IsMouseButtonDown(MouseButton.Left) &&
            (SelectedWeapon?.Definition?.TriggerMode == WeaponTriggerMode.Automatic || Input.IsMouseButtonPressed(MouseButton.Left))) Fire();
    }
}
