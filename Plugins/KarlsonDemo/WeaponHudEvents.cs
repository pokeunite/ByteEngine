using System.Numerics;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Plugins;
using ByteEngine.Core.Variables;
using ByteEngine.Core.VisualLogic;
namespace KarlsonDemo.Plugin;
internal static class WeaponHudEvents
{
    internal static void Register(ByteEnginePluginContext context)
    {
        var target=new VisualArgumentDefinition("target","Weapon or equipment",VariableValue.FromString("Self")){IsObjectReference=true};
        VisualArgumentDefinition Obj(string key,string name)=>new(key,name,VariableValue.FromString("Self")){IsObjectReference=true};
        context.RegisterAction(new(){Id="bytebard.karlsondemo.writeHudText",Category="Weapons",MenuGroup="Weapon HUD",DisplayName="Write formatted weapon HUD text",Arguments=[target,Obj("text","Text object"),new("format","Format ({name}, {ammo}, {reserve}, {capacity}, {state})",VariableValue.FromString("{ammo} / {reserve}")),new("empty","When unarmed",VariableValue.FromString("-- / --"))],Execute=(i,c)=>{
            if(WeaponEvents.ResolveObject(i,c,"text")?.GetComponent<UiText>() is not {} text)return;
            var weapon=WeaponEvents.ResolveWeapon(i,c);text.LocalizationKey="";
            text.Text=weapon?.Definition is {} d?EventValueResolver.GetString(i,"format",c,"{ammo} / {reserve}").Replace("{name}",d.DisplayName).Replace("{ammo}",weapon.CurrentAmmo.ToString()).Replace("{reserve}",d.InfiniteReserve?"∞":weapon.ReserveAmmo.ToString()).Replace("{capacity}",d.MagazineCapacity.ToString()).Replace("{state}",weapon.Reloading?"RELOADING":""):EventValueResolver.GetString(i,"empty",c,"-- / --");
        }});
        context.RegisterAction(new(){Id="bytebard.karlsondemo.updateSlotHud",Category="Weapons",MenuGroup="Weapon HUD",DisplayName="Update weapon slot icon and selection",Arguments=[target,Obj("weapon","World weapon object"),Obj("image","Slot image"),new("selectedColor","Selected RGB",VariableValue.FromVector3(new Vector3(1,.76f,.4f)))],Execute=(i,c)=>{
            var owner=WeaponEvents.ResolveObject(i,c)?.GetComponent<FpsGunLab3D>();var gun=WeaponEvents.ResolveObject(i,c,"weapon")?.GetComponent<WeaponInstance3D>();
            if(owner==null||gun==null||WeaponEvents.ResolveObject(i,c,"image")?.GetComponent<UiWidget>() is not {} image)return;
            if(gun.Definition?.HudIcon.IsEmpty==false)image.ImageReference=gun.Definition.HudIcon;
            image.Color=owner.SelectedWeapon==gun?new Vector4(EventValueResolver.GetVector3(i,"selectedColor",c,new(1,.76f,.4f)),1):owner.Owns(gun)?new(.72f,.8f,.8f,.8f):new(.3f,.36f,.4f,.55f);
        }});
        context.RegisterAction(new(){Id="bytebard.karlsondemo.tuneAccuracy",Category="Weapons",MenuGroup="Weapon Definition",DisplayName="Set weapon accuracy and recoil",Arguments=[target,new("accuracy","Base inaccuracy degrees",VariableValue.FromNumber(.3)),new("moving","Movement penalty degrees",VariableValue.FromNumber(1)),new("bloom","Bloom per shot degrees",VariableValue.FromNumber(.3)),new("pitch","Recoil pitch per shot",VariableValue.FromNumber(.8)),new("yaw","Recoil yaw per shot",VariableValue.FromNumber(.3))],Execute=(i,c)=>{
            if(WeaponEvents.ResolveWeapon(i,c)?.Definition is {} d){float N(string k)=>(float)EventValueResolver.GetNumber(i,k,c);d.AccuracyDegrees=N("accuracy");d.MovingInaccuracyDegrees=N("moving");d.BloomPerShotDegrees=N("bloom");d.RecoilPitchPerShot=N("pitch");d.RecoilYawPerShot=N("yaw");}
        }});
    }
}
