using System.ComponentModel;
using System.Numerics;
using ByteEngine.Core.Scene;
using Component = ByteEngine.Core.Scene.Component;
namespace KarlsonDemo.Plugin;

/// <summary>World representation stays separate from the animated first-person rig. Events own interaction.</summary>
public sealed class WeaponPickup3D : Component
{
    [SceneObjectReference(typeof(WeaponInstance3D)),DisplayName("Weapon Rig"),Category("Pickup"),Description("Weapon Instance under the equipment's Weapon Root. This world object can use any mesh; do not put it beneath the hidden rig.")]
    public Guid WeaponObjectId { get; set; }
    [Category("Pickup")] public float PickupRadius { get; set; } = 2.5f;
    public WeaponInstance3D? Weapon => GameObject.Scene?.FindGameObject(WeaponObjectId)?.GetComponent<WeaponInstance3D>();
    public bool Available => GameObject.ActiveInHierarchy && Enabled && Weapon is {Carried:false};
    public bool CanCollect(FpsGunLab3D equipment) => Available && Weapon is {} weapon && equipment.CanPickUp(weapon) && Vector3.Distance(Transform.WorldPosition,equipment.Transform.WorldPosition)<=Math.Clamp(PickupRadius,.1f,20);
    protected override void OnUpdate(){if(Weapon?.Carried==true)GameObject.Active=false;}
}
