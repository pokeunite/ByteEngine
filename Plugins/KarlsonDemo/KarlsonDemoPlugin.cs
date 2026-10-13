using ByteEngine.Core.Plugins;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Variables;
using ByteEngine.Core.VisualLogic;
namespace KarlsonDemo.Plugin;
public sealed class KarlsonDemoPlugin : IByteEnginePlugin
{
    public void Register(ByteEnginePluginContext context)
    {
        context.RegisterSimpleComponent<FpsGunLab3D>("FpsGunLab3D", new("Weapon Equipment", "Weapons", "Equip any number of independently configured weapon instances; optional input and HUD bindings.", "fps inventory guns"));
        context.RegisterSimpleComponent<WeaponDefinition3D>("WeaponDefinition3D", new("Weapon Definition", "Weapons", "Editable tuning and presentation settings. Author variants as weapon Blueprints.", "ammo hitscan projectile pistol spread"));
        context.RegisterSimpleComponent<WeaponInstance3D>("WeaponInstance3D", new("Weapon Instance", "Weapons", "Independent current and reserve ammo; shared behavior for every weapon definition.", "ammo state reload"));
        WeaponEvents.Register(context);
    }
}
internal static class WeaponEvents
{
    const string Prefix = "bytebard.karlsondemo.";
    internal static void Register(ByteEnginePluginContext c)
    {
        VisualArgumentDefinition Arg(string key, string label, VariableValue value) => new(key, label, value);
        var target = Arg("target", "Equipment object (Self, name or id:GUID)", VariableValue.FromString("Self"));
        var slot = Arg("slot", "Slot (zero based)", VariableValue.FromNumber(0));
        var enabled = Arg("enabled", "Enabled", VariableValue.FromBoolean(true));
        float Num(VisualInstruction i, EventExecutionContext e, string key) => (float)EventValueResolver.GetNumber(i, key, e);
        bool Bool(VisualInstruction i, EventExecutionContext e, string key) => EventValueResolver.GetBoolean(i, key, e);
        void Action(string id, string name, Action<FpsGunLab3D, VisualInstruction, EventExecutionContext> run, params VisualArgumentDefinition[] args) =>
            c.RegisterAction(new() { Id = Prefix + id, Category = "Weapons", DisplayName = name, TargetComponent = "FpsGunLab3D", Arguments = [target, ..args],
                Execute = (i,e) => { if (Resolve(i,e) is {} equipment) run(equipment,i,e); } });
        void Condition(string id, string name, Func<FpsGunLab3D, VisualInstruction, EventExecutionContext, bool> test, params VisualArgumentDefinition[] args) =>
            c.RegisterCondition(new() { Id = Prefix + id, Category = "Weapons", DisplayName = name, TargetComponent = "FpsGunLab3D", Arguments = [target, ..args],
                Evaluate = (i,e) => Resolve(i,e) is {} equipment && test(equipment,i,e) });
        Action("equip", "Equip weapon slot", (w,i,e) => w.Select((int)Num(i,e,"slot")), slot);
        Action("fire", "Fire equipped weapon", (w,i,e) => w.Fire());
        Action("reload", "Reload equipped weapon", (w,i,e) => w.Reload());
        Action("cancelReload", "Cancel reload", (w,i,e) => w.CancelReload());
        Action("setAmmo", "Set magazine and reserve ammo", (w,i,e) => w.SelectedWeapon?.SetAmmo((int)Num(i,e,"current"),(int)Num(i,e,"reserve")),
            Arg("current","Current magazine ammo",VariableValue.FromNumber(12)), Arg("reserve","Reserve ammo",VariableValue.FromNumber(60)));
        Action("addReserve", "Add reserve ammo", (w,i,e) => w.SelectedWeapon?.AddReserve((int)Num(i,e,"amount")), Arg("amount","Ammo amount",VariableValue.FromNumber(12)));
        Action("capacity", "Set magazine capacity", (w,i,e) => { if (w.SelectedWeapon?.Definition is {} d) { d.MagazineCapacity = Math.Clamp((int)Num(i,e,"capacity"),1,10000); w.SelectedWeapon.ClampAmmo(); } }, Arg("capacity","Magazine capacity",VariableValue.FromNumber(12)));
        Action("shotMode", "Use projectile / hitscan mode", (w,i,e) => { if(w.SelectedWeapon?.Definition is {} d) d.ShotMode = Bool(i,e,"projectile") ? WeaponShotMode.Projectile : WeaponShotMode.Hitscan; }, Arg("projectile","Projectile (off = hitscan)",VariableValue.FromBoolean(false)));
        Action("trace", "Show shot traces", (w,i,e) => { if(w.SelectedWeapon?.Definition is {} d) { d.ShowShotTrace = Bool(i,e,"enabled"); d.TraceSeconds = Num(i,e,"seconds"); } }, enabled, Arg("seconds","Trace duration seconds",VariableValue.FromNumber(.3)));
        Action("input", "Use built-in fire input", (w,i,e) => w.UseBuiltInFireInput = Bool(i,e,"enabled"), enabled);
        Action("readAmmo", "Copy ammo to object variables", (w,i,e) => {
            w.GameObject.Variables.Set(Name(i,e,"currentVariable","Ammo"),VariableValue.FromNumber(w.Ammo));
            w.GameObject.Variables.Set(Name(i,e,"reserveVariable","Reserve"),VariableValue.FromNumber(w.ReserveAmmo));
        }, Arg("currentVariable","Magazine variable name",VariableValue.FromString("Ammo")), Arg("reserveVariable","Reserve variable name",VariableValue.FromString("Reserve")));
        Action("tuneShot", "Set damage, fire rate and spread", (w,i,e) => { if(w.SelectedWeapon?.Definition is {} d) { d.DamagePerPellet=Num(i,e,"damage");d.RoundsPerMinute=Num(i,e,"rpm");d.SpreadDegrees=Num(i,e,"spread");d.PelletsPerShot=Math.Clamp((int)Num(i,e,"pellets"),1,32); } },
            Arg("damage","Damage per pellet",VariableValue.FromNumber(25)),Arg("rpm","Rounds per minute",VariableValue.FromNumber(270)),Arg("spread","Spread cone degrees",VariableValue.FromNumber(0)),Arg("pellets","Pellets per shot",VariableValue.FromNumber(1)));
        Condition("fired", "Weapon fired this frame", (w,i,e) => w.SelectedWeapon?.FiredThisFrame==true);
        Condition("hit", "Hitscan hit this frame", (w,i,e) => w.SelectedWeapon?.HitThisFrame==true);
        Condition("reloadComplete", "Reload completed this frame", (w,i,e) => w.SelectedWeapon?.ReloadCompletedThisFrame==true);
        Condition("canFire", "Equipped weapon can fire", (w,i,e) => w.CanFire);
        Condition("reloading", "Equipped weapon is reloading", (w,i,e) => w.Reloading);
        Condition("ammoAtLeast", "Magazine ammo is at least", (w,i,e) => w.Ammo >= Num(i,e,"amount"),Arg("amount","Ammo amount",VariableValue.FromNumber(1)));
        Condition("slot", "Weapon slot is equipped", (w,i,e) => w.SelectedSlot == (int)Num(i,e,"slot"),slot);
        // Keep old serialized events loadable, without prescribing the number of new slots.
        for(int n=0;n<3;n++) { int captured=n; Action("select"+n,"Legacy select slot "+n,(w,i,e)=>w.Select(captured)); }
    }
    private static string Name(VisualInstruction i, EventExecutionContext e, string key, string fallback)
    { string value=EventValueResolver.GetString(i,key,e,fallback);return string.IsNullOrWhiteSpace(value)?fallback:value; }
    private static FpsGunLab3D? Resolve(VisualInstruction i, EventExecutionContext e)
    {
        string token = EventValueResolver.GetString(i,"target",e,"Self");
        GameObject? obj = token.Equals("Self",StringComparison.OrdinalIgnoreCase) ? e.Self :
            token.StartsWith("id:") && Guid.TryParse(token[3..],out var id) ? e.Scene.FindGameObject(id) : e.Scene.FindGameObject(token);
        var result = obj?.GetComponent<FpsGunLab3D>();
        if(result == null) e.WarningSink?.Invoke("Weapons: equipment target was not found: "+token);
        return result;
    }
}
