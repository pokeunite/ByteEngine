using System.Text.Json.Nodes;
using ByteEngine.Core.Vfx;
namespace KarlsonDemoTool;
internal static class WeaponEffectsAuthor
{
    internal static void Run()
    {
        Directory.CreateDirectory(Program.Root+"/Assets/VFX");
        var effect=VfxPresets.Create(VfxPreset.MuzzleFlash);effect.Name="Weapon muzzle flash";effect.Duration=.12f;effect.ParticleBudget=16;
        var layer=effect.Layers[0];layer.LocalSpace=true;layer.LifetimeVariation=0;layer.MaxParticles=16;layer.EndSize=.02f;layer.EndColor=new(1,.25f,.02f,0);
        VfxEffectSerializer.Save(Program.Root+"/Assets/VFX/Weapon muzzle flash.bvfx",effect);
        using var runtime=new DemoRuntime();runtime.Database.Scan();var reference=runtime.Database.ResolveReference("Assets/VFX/Weapon muzzle flash.bvfx");
        foreach(var path in new[]{Program.Root+"/Scenes/Main.bytescene"}.Concat(Directory.GetFiles(Program.Root+"/Assets/Blueprints","*.byteblueprint")))
        {
            var json=JsonNode.Parse(File.ReadAllText(path))!;bool changed=false;
            void Visit(JsonNode? node)
            {
                if(node is JsonObject obj)
                {
                    if(obj["type"]?.GetValue<string>()=="bytebard.karlsondemo.WeaponDefinition3D")
                    {obj["properties"]!["muzzleFlashEffect"]=new JsonObject{["guid"]=reference.Guid.ToString(),["path"]=reference.ProjectPath};changed=true;}
                    foreach(var child in obj.ToArray())Visit(child.Value);
                }
                else if(node is JsonArray array)foreach(var child in array)Visit(child);
            }
            Visit(json);if(changed)File.WriteAllText(path,json.ToJsonString(new(){WriteIndented=true}));
        }
        Console.WriteLine("Editable native muzzle VFX asset installed.");
    }
}
