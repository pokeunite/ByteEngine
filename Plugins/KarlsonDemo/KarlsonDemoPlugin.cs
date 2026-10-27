using ByteEngine.Core.Plugins;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Variables;
using ByteEngine.Core.VisualLogic;
namespace KarlsonDemo.Plugin;
public sealed class KarlsonDemoPlugin : IByteEnginePlugin
{
    public void Register(ByteEnginePluginContext context)
    {
        context.RegisterSimpleComponent<MiningPauseMenu3D>("MiningPauseMenu3D",new("Mining Pause Menu","Mining","Escape pause menu with persistent sound, key bindings and graphics settings.","pause settings controls graphics audio"));
        context.RegisterSimpleComponent<FpsGunLab3D>("FpsGunLab3D", new("Weapon Equipment", "Weapons", "Choose the view camera and weapon root. Input, animations and HUD text are authored in Event Sheets. Live values are available as component references.", "fps inventory guns ammo hud"));
        context.RegisterSimpleComponent<WeaponDefinition3D>("WeaponDefinition3D", new("Weapon Definition", "Weapons", "Damage, ammo and timing. Equip/fire/reload animations are authored in Event Sheets.", "ammo hitscan projectile pistol spread"));
        context.RegisterSimpleComponent<WeaponInstance3D>("WeaponInstance3D", new("Weapon Instance", "Weapons", "Ammo state and weapon events. Use Weapon shot hit in an Event Sheet to access Last Ray Hit.", "ammo state reload raycast hit"));
        context.RegisterSimpleComponent<MilkDepotCourse3D>("MilkDepotCourse3D", new("Milk Depot Course", "Karlson", "Editable targets, milk goal, checkpoints and fall recovery.", "level objective checkpoint"));
        context.RegisterSimpleComponent<WeaponAmmoPickup3D>("WeaponAmmoPickup3D", new("Weapon Ammo Pickup", "Weapons", "Add reserve ammunition to the equipped weapon when approached.", "ammo pickup"));
        context.RegisterSimpleComponent<BulletTrail3D>("BulletTrail3D",new("Bullet Trail","Weapons","Tapered additive bullet streak with editable core/glow colours and depth testing.","projectile tracer trail"));
        context.RegisterSimpleComponent<MiningOperation3D>("MiningOperation3D",new("Mining Operation","Mining","Finite extraction, wave scheduling, processing, market prices and saved company upgrades. Bind controls through Event Sheets.","drill mining waves business ore"));
        context.RegisterSimpleComponent<MiningSpawnPoint3D>("MiningSpawnPoint3D",new("Mining Enemy Spawn Point","Mining","Choose the mining operation that owns this spawn point.","alien wave spawn"));
        context.RegisterSimpleComponent<MiningCuriosityAmbush3D>("MiningCuriosityAmbush3D",new("Curiosity Spider Ambush","Mining","A one-shot nest at the far end of the canyon. Editable trigger volume, enemy blueprint, count and spawn timing.","spider nest ambush trigger"));
        context.RegisterSimpleComponent<MiningHud3D>("MiningHud3D",new("Mining HUD","Mining","Presentation of editable operation state. Buttons are connected by Event Sheets.","shop mining hud"));
        context.RegisterSimpleComponent<MiningRigVisual3D>("MiningRigVisual3D",new("Mining Drill Visual","Mining","Rotate an authored drill part only while extracting.","drill animation spin"));
        context.RegisterSimpleComponent<MiningEnemyEvasion3D>("MiningEnemyEvasion3D",new("Mining Enemy Evasion","Mining","Delayed occasional sidesteps when fired upon; editable probability and cooldown.","enemy dodge evade bullets"));
        context.RegisterSimpleComponent<MiningAudio3D>("MiningAudio3D",new("Mining Audio","Mining","Editable selected sound assets, machine loops, combat cues and bounded spatial voices.","sound audio machines combat interface"));
        MiningEvents.Register(context);
        context.RegisterSimpleComponent<MiningEffects3D>("MiningEffects3D",new("Mining Effects Library","Mining","Editable muzzle, tracer, explosion and machine VFX assets with bounded prewarmed playback.","effects explosion dust smoke tracer"));
        context.RegisterSimpleComponent<MiningMachineVfx3D>("MiningMachineVfx3D",new("Mining Machine Effects","Mining","Dust, exhaust and small material effects linked to machine operation and delivery markers.","drill refinery press dust steam"));
        context.RegisterSimpleComponent<MiningSpiderAnimation3D>("MiningSpiderAnimation3D",new("Animated Mining Spider","Mining","Fitted spider model with editable idle, walk, jump, attack and death clips. AI owns damage and navigation.","spider animation death walk rig"));
        context.RegisterSimpleComponent<MiningCampLayout3D>("MiningCampLayout3D",new("Camp Arrangement","Mining","Move and rotate machines and placed defences between operations. Tab opens the camp view.","camp arrange relocate move"));
        context.RegisterSimpleComponent<MiningCampObject3D>("MiningCampObject3D",new("Movable Camp Object","Mining","Allow this machine and all its children to move together in camp arrangement.","camp machine reposition"));
        context.RegisterSimpleComponent<MiningThirdPerson3D>("MiningThirdPerson3D",new("Third Person Combat View","Mining","Camera-relative combat, right-mouse zoom and centre crosshair. Uses the native player controller and collision boom.","third person aiming camera crosshair"));
        context.RegisterSimpleComponent<MiningMachineAnimation3D>("MiningMachineAnimation3D",new("Mining Machine Animation","Mining","Play the imported machine rig while its operation is active.","drill press refinery animation"));
        context.RegisterSimpleComponent<MiningAmmoBucket3D>("MiningAmmoBucket3D",new("Ammo Collection Bucket","Mining","Eject produced rounds and collect them by walking to the bucket.","ammo press pickup bucket"));
        context.RegisterSimpleComponent<MiningRefineryOutput3D>("MiningRefineryOutput3D",new("Refinery Output Conveyor and Bin","Mining","Carry completed refined cargo along model markers into a bounded ingot pile. The pile follows actual stock and market sales.","refinery ingot conveyor bin pile"));
        context.RegisterSimpleComponent<MiningDefensePlanner3D>("MiningDefensePlanner3D",new("Defence Placement","Mining","Place researched company defences with B and left mouse.","turret mine mortar placement"));
        context.RegisterSimpleComponent<MiningDefense3D>("MiningDefense3D",new("Company Defence","Mining","Configurable sentry, rotary gun, mortar or proximity mine.","turret mine mortar"));
        context.RegisterSimpleComponent<MiningTrade3D>("MiningTrade3D",new("Mining Workshop and Market","Mining","Editable ammo production, separate upgrades and saved commodity price history.","market ammo production"));
        context.RegisterSimpleComponent<MiningBusinessUi3D>("MiningBusinessUi3D",new("Mining Business Interface","Mining","Scalable combat HUD, contextual machine panels and market. Native buttons are wired through Event Sheets.","ui mining shop market"));
        WeaponEvents.Register(context);
        WeaponReadoutEvents.Register(context);
        WeaponInventoryEvents.Register(context);
        WeaponHudEvents.Register(context);
        context.RegisterSimpleComponent<WeaponInventoryHud3D>("WeaponInventoryHud3D",new("Weapon Inventory HUD","Weapons","Native UI templates driven by the Refresh carried weapon HUD Event Sheet action. Shows only carried weapons and magazine reserve markers.","inventory hud clips"));
        context.RegisterSimpleComponent<WeaponRecoilVisual3D>("WeaponRecoilVisual3D",new("Weapon Recoil Visual","Weapons","Editable model kick. Select a weapon model child; socket transforms stay untouched.","recoil kick accuracy"));
        context.RegisterSimpleComponent<WeaponPickup3D>("WeaponPickup3D",new("Weapon Pickup","Weapons","World weapon pickup: use a linked scene rig anywhere, or put Weapon Definition and Weapon Instance on this object. Events control pickup and drop.","gun pickup drop inventory"));
    }
}
internal static class WeaponEvents
{
    const string Prefix = "bytebard.karlsondemo.";
    internal static void Register(ByteEnginePluginContext c)
    {
        VisualArgumentDefinition Arg(string key, string label, VariableValue value) => new(key, label, value);
        var target = Arg("target", "Weapon or equipment object", VariableValue.FromString("Self")) with { IsObjectReference = true };
        var slot = Arg("slot", "Slot (zero based)", VariableValue.FromNumber(0));
        var enabled = Arg("enabled", "Enabled", VariableValue.FromBoolean(true));
        float Num(VisualInstruction i, EventExecutionContext e, string key) => (float)EventValueResolver.GetNumber(i, key, e);
        bool Bool(VisualInstruction i, EventExecutionContext e, string key) => EventValueResolver.GetBoolean(i, key, e);
        string? Sentence(string id) => id switch
        {
            "equip" => "{target}: equip weapon slot {slot}",
            "fire" => "{target}: fire equipped weapon",
            "reload" => "{target}: reload equipped weapon",
            "cancelReload" => "{target}: cancel reload",
            "inputActive" => "Game input is active",
            "automatic" => "Equipped weapon is automatic",
            "semiAutomatic" => "Equipped weapon is single-shot",
            "canFire" => "Equipped weapon can fire",
            "fired" => "{target}: weapon fired",
            "equipped" => "{target}: weapon equipped",
            "reloading" => "{target}: weapon is reloading",
            "reloadStarted" => "{target}: reload started",
            "reloadComplete" => "{target}: reload completed",
            _ => null
        };
        void Action(string id, string name, Action<FpsGunLab3D, VisualInstruction, EventExecutionContext> run, params VisualArgumentDefinition[] args) =>
            c.RegisterAction(new() { Id = Prefix + id, Category = "Weapons", DisplayName = name, SentenceTemplate = Sentence(id), MenuGroup = "Weapon Equipment", TargetComponent = "FpsGunLab3D", Arguments = [target, ..args],
                Execute = (i,e) => { if (Resolve(i,e) is {} equipment) run(equipment,i,e); } });
        void Condition(string id, string name, Func<FpsGunLab3D, VisualInstruction, EventExecutionContext, bool> test, params VisualArgumentDefinition[] args) =>
            c.RegisterCondition(new() { Id = Prefix + id, Category = "Weapons", DisplayName = name, SentenceTemplate = Sentence(id), MenuGroup = "Weapon Equipment", TargetComponent = "FpsGunLab3D", Arguments = [target, ..args],
                Evaluate = (i,e) => Resolve(i,e) is {} equipment && test(equipment,i,e) });
        void WeaponCondition(string id,string name,Func<WeaponInstance3D,VisualInstruction,EventExecutionContext,bool> test,params VisualArgumentDefinition[] args)=>
            c.RegisterCondition(new(){Id=Prefix+id,Category="Weapons",DisplayName=name,SentenceTemplate=Sentence(id),MenuGroup="Weapon Instance",Arguments=[target,..args],Evaluate=(i,e)=>ResolveWeapon(i,e) is {} weapon&&test(weapon,i,e)});
        void WeaponAction(string id,string name,Action<WeaponInstance3D,VisualInstruction,EventExecutionContext> run,params VisualArgumentDefinition[] args)=>
            c.RegisterAction(new(){Id=Prefix+id,Category="Weapons",DisplayName=name,MenuGroup="Weapon Instance",Arguments=[target,..args],Execute=(i,e)=>{if(ResolveWeapon(i,e) is {} weapon)run(weapon,i,e);}});
        Action("equip", "Equip weapon slot", (w,i,e) => w.Select((int)Num(i,e,"slot")), slot);
        Action("fire", "Fire equipped weapon", (w,i,e) => {var weapon=w.SelectedWeapon;bool fired=w.Fire();WeaponEventBridge.Publish(e,weapon,accepted:fired);});
        Action("reload", "Reload equipped weapon", (w,i,e) => w.Reload());
        Action("cancelReload", "Cancel reload", (w,i,e) => w.CancelReload());
        WeaponAction("autoReload", "Automatically reload when empty", (w,i,e)=>{if(w.Definition is {} d)d.AutoReload=Bool(i,e,"enabled");},enabled);
        WeaponAction("setAmmo", "Set magazine and reserve ammo", (w,i,e) => w.SetAmmo((int)Num(i,e,"current"),(int)Num(i,e,"reserve")),
            Arg("current","Current magazine ammo",VariableValue.FromNumber(12)), Arg("reserve","Reserve ammo",VariableValue.FromNumber(60)));
        WeaponAction("addReserve", "Add reserve ammo", (w,i,e) => w.AddReserve((int)Num(i,e,"amount")), Arg("amount","Ammo amount",VariableValue.FromNumber(12)));
        WeaponAction("capacity", "Set magazine capacity", (w,i,e) => { if (w.Definition is {} d) { d.MagazineCapacity = Math.Clamp((int)Num(i,e,"capacity"),1,10000); w.ClampAmmo(); } }, Arg("capacity","Magazine capacity",VariableValue.FromNumber(12)));
        WeaponAction("shotMode", "Use projectile / hitscan mode", (w,i,e) => { if(w.Definition is {} d) d.ShotMode = Bool(i,e,"projectile") ? WeaponShotMode.Projectile : WeaponShotMode.Hitscan; }, Arg("projectile","Projectile (off = hitscan)",VariableValue.FromBoolean(false)));
        WeaponAction("trace", "Show shot traces", (w,i,e) => { if(w.Definition is {} d) { d.ShowShotTrace = Bool(i,e,"enabled"); d.TraceSeconds = Num(i,e,"seconds"); } }, enabled, Arg("seconds","Trace duration seconds",VariableValue.FromNumber(.3)));
        Action("input", "Use built-in fire input", (w,i,e) => w.UseBuiltInFireInput = Bool(i,e,"enabled"), enabled);
        Action("readAmmo", "Copy ammo to object variables", (w,i,e) => {
            w.GameObject.Variables.Set(Name(i,e,"currentVariable","Ammo"),VariableValue.FromNumber(w.Ammo));
            w.GameObject.Variables.Set(Name(i,e,"reserveVariable","Reserve"),VariableValue.FromNumber(w.ReserveAmmo));
        }, Arg("currentVariable","Magazine variable name",VariableValue.FromString("Ammo")), Arg("reserveVariable","Reserve variable name",VariableValue.FromString("Reserve")));
        WeaponAction("tuneShot", "Set damage, fire rate and spread", (w,i,e) => { if(w.Definition is {} d) { d.DamagePerPellet=Num(i,e,"damage");d.RoundsPerMinute=Num(i,e,"rpm");d.SpreadDegrees=Num(i,e,"spread");d.PelletsPerShot=Math.Clamp((int)Num(i,e,"pellets"),1,32); } },
            Arg("damage","Damage per pellet",VariableValue.FromNumber(25)),Arg("rpm","Rounds per minute",VariableValue.FromNumber(270)),Arg("spread","Spread cone degrees",VariableValue.FromNumber(0)),Arg("pellets","Pellets per shot",VariableValue.FromNumber(1)));
        WeaponAction("reloadTiming", "Set reload timing", (w,i,e) => { if(w.Definition is {} d) { d.ReloadSeconds=Num(i,e,"seconds"); d.EmptyReloadSeconds=Num(i,e,"emptySeconds"); d.ReloadSpeedMultiplier=Num(i,e,"speed"); d.AllowReloadInterrupt=Bool(i,e,"interrupt"); } },
            Arg("seconds","Reload seconds (per shell for shotgun)",VariableValue.FromNumber(1)), Arg("emptySeconds","Empty reload seconds",VariableValue.FromNumber(1.6)), Arg("speed","Reload speed multiplier",VariableValue.FromNumber(1)), Arg("interrupt","Allow per-shell reload interruption",VariableValue.FromBoolean(true)));
        WeaponAction("muzzleFlash", "Set muzzle flash", (w,i,e) => { if(w.Definition is {} d) { d.MuzzleFlashEnabled=Bool(i,e,"enabled"); d.MuzzleFlashSize=Num(i,e,"size"); } }, enabled, Arg("size","Flash size",VariableValue.FromNumber(.6)));
        WeaponAction("projectileTuning", "Set projectile speed and trail", (w,i,e)=>{if(w.Definition is {} d){d.ProjectileSpeed=Num(i,e,"speed");d.ProjectileTrailLength=Num(i,e,"length");d.TraceWidth=Num(i,e,"width");d.ShowShotTrace=Bool(i,e,"enabled");}},
            Arg("speed","Projectile speed",VariableValue.FromNumber(140)),Arg("length","Trail length",VariableValue.FromNumber(1.4)),Arg("width","Trail width",VariableValue.FromNumber(.018)),enabled);
        WeaponAction("muzzleLight", "Set muzzle light pulse", (w,i,e)=>{if(w.Definition is {} d){d.MuzzleLightIntensity=Num(i,e,"intensity");d.MuzzleLightRange=Num(i,e,"range");}},Arg("intensity","Light intensity",VariableValue.FromNumber(2.5)),Arg("range","Light range",VariableValue.FromNumber(3)));
        WeaponAction("trailAppearance", "Set bullet trail appearance", (w,i,e)=>{if(w.Definition is {} d){var color=EventValueResolver.GetVector3(i,"color",e,new(1,.55f,.12f));d.TrailGlowColor=new(color,Math.Clamp(Num(i,e,"opacity"),0,1));d.TrailGlowScale=Num(i,e,"glow");d.TrailImpactFadeSeconds=Num(i,e,"fade");}},
            Arg("color","Glow RGB",VariableValue.FromVector3(new(1,.55f,.12f))),Arg("opacity","Glow opacity",VariableValue.FromNumber(.55)),Arg("glow","Glow width multiplier",VariableValue.FromNumber(3)),Arg("fade","Impact fade seconds",VariableValue.FromNumber(.07)));
        WeaponCondition("canReload","Weapon can reload",(w,i,e)=>w.CanReload);
        WeaponCondition("magazineEmpty","Magazine is empty",(w,i,e)=>w.CurrentAmmo<=0);
        WeaponCondition("reserveAtLeast","Reserve ammo is at least",(w,i,e)=>w.ReserveAmmo>=Num(i,e,"amount"),Arg("amount","Ammo amount",VariableValue.FromNumber(1)));
        WeaponCondition("fired", "Weapon fired", (w,i,e) => {bool fired=WeaponEventBridge.Pulse(i,e,w,"fired");if(fired)WeaponEventBridge.Publish(e,w);return fired;});
        WeaponCondition("hit", "Weapon shot hit", (w,i,e) =>
        {
            if (!WeaponEventBridge.Pulse(i,e,w,"hit")) return false;
            if (Bool(i,e,"checkLastRayHit"))
            {
                var selected = ResolveObject(i,e,"hitObject");
                for (int index=0; index<w.ShotHits.Count; index++)
                    if (selected != null && w.ShotHits[index].Hit.GameObject == selected)
                    { WeaponEventBridge.Publish(e,w,index); return true; }
                WeaponEventBridge.Publish(e,w,accepted:false);
                return false;
            }
            WeaponEventBridge.Publish(e,w);
            return e.LastRaycastHit.HasValue;
        }, Arg("checkLastRayHit","Last Ray Hit - check object",VariableValue.FromBoolean(false)),
           Arg("hitObject","Hit object",VariableValue.FromString("Self")) with { IsObjectReference = true });
        WeaponCondition("missed", "Hitscan shot missed", (w,i,e) => {bool fired=WeaponEventBridge.Pulse(i,e,w,"fired");if(fired)WeaponEventBridge.Publish(e,w);return fired&&w.LastShotWasHitscan&&w.ShotHits.Count==0;});
        WeaponCondition("equipped", "Weapon equipped", (w,i,e) => WeaponEventBridge.Pulse(i,e,w,"equipped"));
        WeaponCondition("reloadStarted", "Reload started", (w,i,e) => WeaponEventBridge.Pulse(i,e,w,"reloadStarted"));
        WeaponCondition("reloadCancelled", "Reload cancelled", (w,i,e) => WeaponEventBridge.Pulse(i,e,w,"reloadCancelled"));
        WeaponCondition("reloadComplete", "Reload completed", (w,i,e) => WeaponEventBridge.Pulse(i,e,w,"reloadComplete"));
        WeaponCondition("canFire", "Equipped weapon can fire", (w,i,e) => w.CanFire);
        WeaponCondition("reloading", "Weapon is reloading", (w,i,e) => w.Reloading);
        WeaponCondition("emptyReload", "Current reload started empty", (w,i,e) => w.Reloading&&w.ReloadWasEmpty);
        WeaponCondition("automatic", "Weapon uses automatic trigger", (w,i,e) => w.Definition?.TriggerMode==WeaponTriggerMode.Automatic);
        WeaponCondition("semiAutomatic", "Weapon uses single-shot trigger", (w,i,e) => w.Definition?.TriggerMode==WeaponTriggerMode.SemiAutomatic);
        WeaponCondition("ammoAtLeast", "Magazine ammo is at least", (w,i,e) => w.CurrentAmmo >= Num(i,e,"amount"),Arg("amount","Ammo amount",VariableValue.FromNumber(1)));
        c.RegisterAction(new(){Id=Prefix+"useShotResult",Category="Weapons",DisplayName="Use weapon shot result as Last Ray Hit",MenuGroup="Weapon Instance",Arguments=[target,Arg("hitIndex","Hit index (nearest first, zero based)",VariableValue.FromNumber(0))],Execute=(i,e)=>WeaponEventBridge.Publish(e,ResolveWeapon(i,e),(int)Num(i,e,"hitIndex"))});
        Condition("slot", "Weapon slot is equipped", (w,i,e) => w.SelectedSlot == (int)Num(i,e,"slot"),slot);
        Condition("inputActive", "Game input is active", (w,i,e) => ByteEngine.Core.InputSystem.InputActions.GameplayEnabled);
        // Keep old serialized events loadable, without prescribing the number of new slots.
        for(int n=0;n<3;n++) { int captured=n; Action("select"+n,"Legacy select slot "+n,(w,i,e)=>w.Select(captured)); }
    }
    private static string Name(VisualInstruction i, EventExecutionContext e, string key, string fallback)
    { string value=EventValueResolver.GetString(i,key,e,fallback);return string.IsNullOrWhiteSpace(value)?fallback:value; }
    internal static GameObject? ResolveObject(VisualInstruction i,EventExecutionContext e,string key="target")
    {
        string token = EventValueResolver.GetString(i,key,e,"Self");
        if(token.Equals("Last Ray Hit",StringComparison.OrdinalIgnoreCase))return e.LastRaycastHit?.GameObject;
        if(token.Equals("Picked",StringComparison.OrdinalIgnoreCase)||token.StartsWith("Picked:",StringComparison.OrdinalIgnoreCase))return e.PickedObject;
        return string.IsNullOrWhiteSpace(token)||token.Equals("Self",StringComparison.OrdinalIgnoreCase) ? e.Self :
            token.StartsWith("id:") && Guid.TryParse(token[3..],out var id) ? e.Scene.FindGameObject(id) : e.Scene.FindGameObject(token);
    }
    internal static WeaponInstance3D? ResolveWeapon(VisualInstruction i,EventExecutionContext e)
    {
        var obj=ResolveObject(i,e);return obj?.GetComponent<WeaponInstance3D>()??FindEquipment(obj)?.SelectedWeapon;
    }
    private static FpsGunLab3D? FindEquipment(GameObject? obj)
    {
        for(var current=obj;current!=null;current=current.Parent)if(current.GetComponent<FpsGunLab3D>() is {} equipment)return equipment;
        return null;
    }
    private static FpsGunLab3D? Resolve(VisualInstruction i, EventExecutionContext e)
    {
        var result=FindEquipment(ResolveObject(i,e));
        string token=EventValueResolver.GetString(i,"target",e,"Self");
        if(result == null) e.WarningSink?.Invoke("Weapons: equipment target was not found: "+token);
        return result;
    }
}
