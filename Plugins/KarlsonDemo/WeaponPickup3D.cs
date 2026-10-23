using System.ComponentModel;
using System.Numerics;
using ByteEngine.Core.Scene;
using Component = ByteEngine.Core.Scene.Component;
namespace KarlsonDemo.Plugin;

/// <summary>World representation stays separate from the animated first-person rig. Events own interaction.</summary>
public sealed class WeaponPickup3D : Component
{
    [SceneObjectReference(typeof(WeaponInstance3D)),DisplayName("Weapon Rig"),Category("Pickup"),Description("Any scene object with Weapon Definition and Weapon Instance. Leave empty to use those components on this pickup object. No inventory parenting is required.")]
    public Guid WeaponObjectId { get; set; }
    [Category("Pickup"),Description("For a self-contained world gun, disable its rigid body and colliders while carried and restore them on drop.")]
    public bool SuspendPhysicsWhenCarried { get; set; } = true;
    [Category("Pickup"),Description("Horizontal interaction range in metres. Pickup origin height is checked separately.")] public float PickupRadius { get; set; } = 2.5f;
    [Category("Pickup"),Description("Maximum vertical separation from the collector; prevents picking through floors.")] public float MaximumHeightDifference { get; set; } = 2;
    [Category("Live State")] public string BindingError => Weapon != null ? "" : WeaponObjectId == Guid.Empty ? "Add Weapon Definition and Weapon Instance here, or choose a linked Weapon Rig." : "The linked Weapon Rig is missing or has no Weapon Instance.";
    [Category("Pickup"),Description("Height of the pickup origin above the hit ground when dropped. Increase for meshes whose origin is at their centre so the model stays above the floor.")]
    public float DropGroundClearance { get; set; } = .3f;
    public WeaponInstance3D? Weapon => (WeaponObjectId==Guid.Empty?GameObject:GameObject.Scene?.FindGameObject(WeaponObjectId))?.GetComponent<WeaponInstance3D>();
    public bool Available => GameObject.ActiveInHierarchy && Enabled && Weapon is {Carried:false};
    public float HorizontalDistanceTo(FpsGunLab3D equipment)
    {
        var delta = Transform.WorldPosition-equipment.Transform.WorldPosition; delta.Y=0; return delta.Length();
    }
    public bool CanCollect(FpsGunLab3D equipment) => Available && Weapon is {} weapon && equipment.CanPickUp(weapon) &&
        HorizontalDistanceTo(equipment) <= WeaponDefinition3D.Bound(PickupRadius,2.5f,.1f,20) &&
        Math.Abs(Transform.WorldPosition.Y-equipment.Transform.WorldPosition.Y) <= WeaponDefinition3D.Bound(MaximumHeightDifference,2,.1f,20);
    public string GetCollectionStatus(FpsGunLab3D equipment)
    {
        if(BindingError.Length>0)return BindingError;
        if(!Enabled||!GameObject.ActiveInHierarchy)return "Pickup disabled";
        if(Weapon!.Carried)return "Already carried";
        if(equipment.InventoryFull)return "Carry limit reached";
        if(!equipment.CanPickUp(Weapon))return "Weapon slot already carried or weapon belongs to another scene";
        if(HorizontalDistanceTo(equipment)>WeaponDefinition3D.Bound(PickupRadius,2.5f,.1f,20))return "Out of range";
        if(Math.Abs(Transform.WorldPosition.Y-equipment.Transform.WorldPosition.Y)>WeaponDefinition3D.Bound(MaximumHeightDifference,2,.1f,20))return "Too far above / below player";
        return "Ready";
    }
    protected override void OnUpdate(){if(Weapon is {Carried:true} weapon&&weapon.GameObject!=GameObject)GameObject.Active=false;}
}
