using System.Runtime.CompilerServices;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Variables;
using ByteEngine.Core.VisualLogic;
using ByteEngine.Core.Plugins;

namespace KarlsonDemo.Plugin;

/// <summary>Public weapon data for event-authored HUDs, bars, audio and game rules. Never writes UI.</summary>
internal static class WeaponReadoutEvents
{
    private sealed record Readout(Guid Weapon,string Name,int Ammo,int Capacity,int Reserve,int Slot,bool Reloading,bool InfiniteReserve);
    private sealed class Observer {public Readout? Last;public EventExecutionContext? Context;public bool Changed;}
    private static readonly ConditionalWeakTable<VisualInstruction,ConditionalWeakTable<GameObject,Observer>> Observers=new();
    private static readonly string[] Fields=["Weapon name","Magazine ammo","Magazine capacity","Reserve ammo","Reserve capacity","Ammo per shot","Equipped slot","Reloading","Can fire","Can reload","Reload seconds remaining","Reload progress","Cooldown seconds remaining","Damage per pellet","Rounds per minute","Range","Spread degrees","Pellets per shot","Infinite reserve","Shot mode","Trigger mode","Reload mode","Weapon shots fired","Weapon hits","Base inaccuracy degrees","Current inaccuracy degrees","Accuracy bloom","Recoil pitch","Recoil yaw"];
    internal static void Register(ByteEnginePluginContext context)
    {
        var target=new VisualArgumentDefinition("target","Weapon or equipment object",VariableValue.FromString("Self")){IsObjectReference=true};
        context.RegisterCondition(new(){Id="bytebard.karlsondemo.hudChanged",Category="Weapons",MenuGroup="Weapon Equipment",DisplayName="Weapon HUD values changed",SentenceTemplate="{target}: weapon name or ammo display changed",Arguments=[target],Evaluate=(i,c)=>
        {
            var weapon=WeaponEvents.ResolveWeapon(i,c);var definition=weapon?.Definition;
            var current=new Readout(weapon?.GameObject.Id??Guid.Empty,definition?.DisplayName??"",weapon?.CurrentAmmo??0,definition?.MagazineCapacity??0,weapon?.ReserveAmmo??0,definition?.Slot??-1,weapon?.Reloading==true,definition?.InfiniteReserve==true);
            var observer=Observers.GetOrCreateValue(i).GetOrCreateValue(c.Self);
            if(ReferenceEquals(observer.Context,c)&&observer.Last==current)return observer.Changed;
            observer.Context=c;observer.Changed=observer.Last!=current;observer.Last=current;return observer.Changed;
        }});
        context.RegisterAction(new(){Id="bytebard.karlsondemo.readValue",Category="Weapons",MenuGroup="Weapon Instance",DisplayName="Read weapon value into variable",Arguments=[target,
            new("field","Weapon value",VariableValue.FromString("Magazine ammo")){Choices=Fields},
            new("scope","Variable scope",VariableValue.FromString("Self")){Choices=["Self","Scene","Global","Object"]},
            new("destination","Variable owner (Object scope)",VariableValue.FromString("Self")){IsObjectReference=true},
            new("variable","Variable name",VariableValue.FromString("WeaponValue"))],Execute=(i,c)=>
        {
            string name=EventValueResolver.GetString(i,"variable",c,"WeaponValue");if(string.IsNullOrWhiteSpace(name))throw new ArgumentException("Choose an output variable name.");
            var store=EventValueResolver.GetString(i,"scope",c,"Self") switch
            {"Scene"=>c.Scene.Variables,"Global"=>c.Globals,"Object"=>WeaponEvents.ResolveObject(i,c,"destination")?.Variables??throw new InvalidOperationException("Variable owner was not found."),_=>c.Self.Variables};
            store.Set(name,Read(WeaponEvents.ResolveWeapon(i,c),EventValueResolver.GetString(i,"field",c,"Magazine ammo")));
        }});
    }
    private static VariableValue Read(WeaponInstance3D? weapon,string field)
    {
        if(weapon?.Definition is not {} d)return VariableValue.FromNull();
        object value=field switch
        {
            "Weapon name"=>d.DisplayName,"Magazine ammo"=>weapon.CurrentAmmo,"Magazine capacity"=>d.MagazineCapacity,
            "Reserve ammo"=>weapon.ReserveAmmo,"Reserve capacity"=>d.ReserveCapacity,"Ammo per shot"=>d.AmmoPerShot,"Equipped slot"=>d.Slot,
            "Reloading"=>weapon.Reloading,"Can fire"=>weapon.CanFire,"Can reload"=>weapon.CanReload,
            "Reload seconds remaining"=>weapon.ReloadRemainingSeconds,"Reload progress"=>weapon.ReloadProgress,"Cooldown seconds remaining"=>weapon.CooldownRemaining,
            "Damage per pellet"=>d.DamagePerPellet,"Rounds per minute"=>d.RoundsPerMinute,"Range"=>d.Range,"Spread degrees"=>d.SpreadDegrees,
            "Pellets per shot"=>d.PelletsPerShot,"Infinite reserve"=>d.InfiniteReserve,"Shot mode"=>d.ShotMode.ToString(),
            "Base inaccuracy degrees"=>d.AccuracyDegrees,"Current inaccuracy degrees"=>weapon.CurrentInaccuracyDegrees,"Accuracy bloom"=>weapon.AccuracyBloom,"Recoil pitch"=>weapon.RecoilAngles.X,"Recoil yaw"=>weapon.RecoilAngles.Y,
            "Trigger mode"=>d.TriggerMode.ToString(),"Reload mode"=>d.ReloadMode.ToString(),"Weapon shots fired"=>weapon.ShotsFired,"Weapon hits"=>weapon.HitCount,
            _=>throw new ArgumentException("Unknown weapon value: "+field)
        };
        return VariableJson.FromObject(value);
    }
}
