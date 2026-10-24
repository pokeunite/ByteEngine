using ByteEngine.Core.Plugins;
using ByteEngine.Core.Variables;
using ByteEngine.Core.VisualLogic;
namespace KarlsonDemo.Plugin;
internal static class WeaponInventoryEvents
{
    internal static void Register(ByteEnginePluginContext context)
    {
        const string prefix="bytebard.karlsondemo.";
        var target=new VisualArgumentDefinition("target","Equipment object",VariableValue.FromString("Self")){IsObjectReference=true};
        var pickup=new VisualArgumentDefinition("pickup","World pickup object (when nearest is off)",VariableValue.FromString("Last Ray Hit")){IsObjectReference=true};
        var nearest=new VisualArgumentDefinition("nearest","Use nearest eligible pickup",VariableValue.FromBoolean(true));
        var weapon=new VisualArgumentDefinition("weapon","Weapon rig object",VariableValue.FromString("Self")){IsObjectReference=true};
        FpsGunLab3D? Equipment(VisualInstruction i,EventExecutionContext e)=>WeaponEvents.ResolveObject(i,e)?.GetComponent<FpsGunLab3D>();
        context.RegisterAction(new(){Id=prefix+"refreshAttachment",Category="Weapons",MenuGroup="Weapon Equipment",DisplayName="Apply equipped weapon socket",Arguments=[target],Execute=(i,e)=>{if(Equipment(i,e) is {} owner&&!owner.RefreshWeaponAttachment())e.WarningSink?.Invoke(owner.WeaponAttachmentError);}});
        context.RegisterAction(new(){Id=prefix+"refreshLocomotion",Category="Weapons",MenuGroup="Weapon Equipment",DisplayName="Apply equipped weapon locomotion",Arguments=[target],Execute=(i,e)=>{if(Equipment(i,e) is {} owner&&!owner.RefreshWeaponLocomotion())e.WarningSink?.Invoke(owner.LocomotionBindingError);}});
        WeaponPickup3D? Pickup(VisualInstruction i,EventExecutionContext e)=>EventValueResolver.GetBoolean(i,"nearest",e,!i.Arguments.ContainsKey("pickup")||EventValueResolver.GetString(i,"pickup",e,"").Equals("Nearest",StringComparison.OrdinalIgnoreCase))&&Equipment(i,e) is {} owner
            ?e.Scene.GameObjects.Select(o=>o.GetComponent<WeaponPickup3D>()).OfType<WeaponPickup3D>().Where(p=>p.CanCollect(owner)).OrderBy(p=>p.HorizontalDistanceTo(owner)).FirstOrDefault()
            :WeaponEvents.ResolveObject(i,e,"pickup")?.GetComponent<WeaponPickup3D>();
        context.RegisterCondition(new(){Id=prefix+"inventoryFull",Category="Weapons",MenuGroup="Weapon Equipment",DisplayName="Weapon inventory is full",Arguments=[target],Evaluate=(i,e)=>Equipment(i,e)?.InventoryFull==true});
        context.RegisterCondition(new(){Id=prefix+"hasWeapon",Category="Weapons",MenuGroup="Weapon Equipment",DisplayName="Has an equipped weapon",Arguments=[target],Evaluate=(i,e)=>Equipment(i,e)?.HasWeapon==true});
        context.RegisterCondition(new(){Id=prefix+"ownsWeapon",Category="Weapons",MenuGroup="Weapon Equipment",DisplayName="Carries weapon",Arguments=[target,weapon],Evaluate=(i,e)=>Equipment(i,e) is {} owner&&WeaponEvents.ResolveObject(i,e,"weapon")?.GetComponent<WeaponInstance3D>() is {} instance&&owner.Owns(instance)});
        context.RegisterCondition(new(){Id=prefix+"canPickUp",Category="Weapons",MenuGroup="Weapon Pickup",DisplayName="Can pick up weapon (range and carry limit)",Arguments=[target,nearest,pickup],Evaluate=(i,e)=>Equipment(i,e) is {} owner&&Pickup(i,e)?.CanCollect(owner)==true});
        context.RegisterAction(new(){Id=prefix+"pickUp",Category="Weapons",MenuGroup="Weapon Pickup",DisplayName="Pick up weapon",Arguments=[target,nearest,pickup,new("equip","Equip after pickup",VariableValue.FromBoolean(true))],Execute=(i,e)=>{
            if(Equipment(i,e) is not {} owner){e.WarningSink?.Invoke("Pickup equipment object was not found.");return;}
            bool equip=EventValueResolver.GetBoolean(i,"equip",e,true);
            bool useNearest=EventValueResolver.GetBoolean(i,"nearest",e,!i.Arguments.ContainsKey("pickup")||EventValueResolver.GetString(i,"pickup",e,"").Equals("Nearest",StringComparison.OrdinalIgnoreCase));
            bool picked=useNearest?owner.TryPickUpNearest(equip):owner.PickUp(Pickup(i,e),equip);
            if(!picked)e.WarningSink?.Invoke("Pickup: "+owner.LastPickupResult);
        }});
        context.RegisterAction(new(){Id=prefix+"giveWeapon",Category="Weapons",MenuGroup="Weapon Equipment",DisplayName="Give starting or reward weapon",Arguments=[target,weapon,new("equip","Equip now",VariableValue.FromBoolean(true))],Execute=(i,e)=>{if(Equipment(i,e) is {} owner&&!owner.GiveWeapon(WeaponEvents.ResolveObject(i,e,"weapon")?.GetComponent<WeaponInstance3D>(),EventValueResolver.GetBoolean(i,"equip",e,true)))e.WarningSink?.Invoke(owner.LastPickupResult);}});
        context.RegisterAction(new(){Id=prefix+"equipInventoryIndex",Category="Weapons",MenuGroup="Weapon Equipment",DisplayName="Equip carried weapon by inventory position",Arguments=[target,new("index","Carried position (zero based)",VariableValue.FromNumber(0))],Execute=(i,e)=>Equipment(i,e)?.SelectInventoryIndex((int)EventValueResolver.GetNumber(i,"index",e,0))});
        context.RegisterAction(new(){Id=prefix+"drop",Category="Weapons",MenuGroup="Weapon Equipment",DisplayName="Drop equipped weapon",Arguments=[target],Execute=(i,e)=>{if(Equipment(i,e) is {} owner&&!owner.Drop())e.WarningSink?.Invoke("Cannot drop: equip a carried weapon and link a Weapon Pickup world object to its rig.");}});
        context.RegisterAction(new(){Id=prefix+"setCarryLimit",Category="Weapons",MenuGroup="Weapon Equipment",DisplayName="Set weapon carry limit",Arguments=[target,new("limit","Maximum weapons (0 = unlimited)",VariableValue.FromNumber(2))],Execute=(i,e)=>{if(Equipment(i,e) is {} owner)owner.CarryLimit=Math.Max(0,(int)EventValueResolver.GetNumber(i,"limit",e,2));}});
    }
}
