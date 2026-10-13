using System.Reflection;
using System.Text.Json;
using ByteEngine.Core.Scene;
using ByteEngine.Core.VisualLogic;
using ByteEngine.Core.Variables;
using KarlsonDemo.Plugin;
namespace KarlsonDemoTool;
internal static class DesignCheck
{
    internal static void Run()
    {
        using var runtime=new DemoRuntime();runtime.Database.Scan();var scene=runtime.Serializer.Load(Program.Root+"/Scenes/Main.bytescene");var checks=new List<string>();
        void Check(bool result,string label){if(!result)throw new Exception(label);checks.Add(label);Console.WriteLine("PASS "+label);}
        var weapons=scene.GameObjects.Select(o=>o.GetComponent<WeaponInstance3D>()).OfType<WeaponInstance3D>().ToArray();
        Check(weapons.Length==4,"Four generic weapon instances load");
        Check(weapons[0].Definition!.MagazineCapacity==12&&weapons[3].Definition!.MagazineCapacity==8,"Different pistols serialize different definitions");
        Check(weapons[1].Definition!.PelletsPerShot==8&&weapons[3].Definition!.ShotMode==WeaponShotMode.Projectile,"Shot strategy and spread settings round-trip");
        var w=weapons[0];w.SetAmmo(999,-1);Check(w.CurrentAmmo==12&&w.ReserveAmmo==0,"Ammo changes clamp to valid capacities");
        w.Equip(true);w.SetAmmo(0,0);Check(!w.Reload(),"Empty reserve prevents reload");w.Definition!.InfiniteReserve=true;Check(w.Reload(),"Infinite reserve permits reload");w.CancelReload();Check(!w.Reloading&&w.CurrentAmmo==0,"Reload cancellation does not invent ammo");
        w.SetAmmo(5,20);w.AddReserve(-999);Check(w.CurrentAmmo==5&&w.ReserveAmmo==0&&weapons[3].CurrentAmmo==8,"Ammo pickups and removal remain per instance");
        var registry=VisualLogicRegistry.CreateDefault();var editable=registry.Actions.Where(a=>a.Id.StartsWith("bytebard.karlsondemo.")).ToArray();
        Check(registry.GetArguments("bytebard.karlsondemo.equip").Any(a=>a.Name=="slot"),"Event equip action exposes editable slot argument");
        Check(registry.GetArguments("bytebard.karlsondemo.setAmmo").Select(a=>a.Name).Contains("reserve"),"Event ammo action exposes magazine and reserve values");
        Check(registry.GetArguments("bytebard.karlsondemo.tuneShot").Count==5,"Event tuning action exposes damage, rate, spread and pellet count");
        Check(registry.Conditions.Count(a=>a.Id.StartsWith("bytebard.karlsondemo."))>=7,"Weapon state and frame notifications available as conditions");
        var editorAssembly=typeof(ByteEngine.Editor.EditorApplication).Assembly;
        var renderer=editorAssembly.GetType("ByteEngine.Editor.ComponentPropertyRenderer")!;
        var contextType=editorAssembly.GetType("ByteEngine.Editor.PropertyEditorContext")!;
        var method=renderer.GetMethod("Descriptors",BindingFlags.Static|BindingFlags.Public)!;
        foreach(var type in new[]{typeof(WeaponDefinition3D),typeof(WeaponInstance3D)})
        {
            var descriptors=(System.Collections.IEnumerable)method.Invoke(null,[type,Enum.Parse(contextType,"Scene")])!;
            var names=new List<string>();foreach(var d in descriptors){var property=(PropertyInfo)d.GetType().GetProperty("Property")!.GetValue(d)!;if(property.SetMethod?.IsPublic==true)names.Add(property.Name);}
            Check(type==typeof(WeaponDefinition3D)?names.Contains("MagazineCapacity")&&names.Contains("ShotMode")&&names.Contains("ProjectileBlueprint"):names.Contains("CurrentAmmo")&&names.Contains("ReserveAmmo"),"Native inspector exposes editable settings: "+type.Name);
        }
        // Structural asset validation prevents a repeat of cross-scene export leakage.
        foreach(var name in Program.Names)
        {
            var model=runtime.Assets.LoadModel(runtime.Database.ResolveReference("Assets/Models/Cyberpunk_"+name+".glb"));
            Check(model.Meshes.Sum(m=>m.Indices.Length/3)<15000,"Bounded cyberpunk gun/arms geometry imports: "+name);
            Check(model.Skeleton!.Bones.Count==48,"One complete gun/arms skeleton: "+name);
        }
        File.WriteAllText(Program.Root+"/Preview/design-validation.json",JsonSerializer.Serialize(checks,new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine("DESIGN PASS "+checks.Count);
    }
}
