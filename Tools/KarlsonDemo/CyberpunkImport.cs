using System.Numerics;
using System.Text.Json;
using ByteEngine.Core.Animation;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Blueprints;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;
using KarlsonDemo.Plugin;
namespace KarlsonDemoTool;
internal static class CyberpunkImport
{
    const string Source="C:/Users/codex/Downloads/byte engine files/Cyberpunk Weapons v3";
    internal static void Run()
    {
        string backup=Program.Root+"/.byteengine/Backups/Before Cyberpunk v3";Directory.CreateDirectory(backup);
        Backup("Scenes/Main.bytescene");
        foreach(var name in Program.Names)
        {
            Backup("Assets/Animation/"+name+".byteanim");Backup("Assets/Models/Karlson_"+name+".glb");
            File.Copy(Source+"/Cyberpunk_"+name+".glb",Program.Root+"/Assets/Models/Cyberpunk_"+name+".glb",true);
            File.Copy(Source+"/Cyberpunk_"+name+"_Prop.glb",Program.Root+"/Assets/Models/Cyberpunk_"+name+"_Prop.glb",true);
        }
        foreach(string path in new[]{Program.Root+"/Plugins/bytebard.karlsondemo.byteplugin",Program.Root+"/PluginLibrary/KarlsonDemo.byteplugin","PluginLibrary/KarlsonDemo.byteplugin","Dist/ByteEngine/PluginLibrary/KarlsonDemo.byteplugin"})File.Copy("artifacts/plugins/KarlsonDemo.byteplugin",path,true);
        foreach(var file in Directory.GetFiles("Plugins/KarlsonDemo","*",SearchOption.TopDirectoryOnly))File.Copy(file,Program.Root+"/PluginSource/KarlsonDemo/"+Path.GetFileName(file),true);
        using var runtime=new DemoRuntime();runtime.Database.Scan();var profiles=new Dictionary<string,AssetReference>();var models=new Dictionary<string,AssetReference>();var durations=new Dictionary<string,Dictionary<string,float>>();var reports=new List<object>();
        foreach(var name in Program.Names)
        {
            var reference=runtime.Database.ResolveReference("Assets/Models/Cyberpunk_"+name+".glb");models[name]=reference;var model=runtime.Assets.LoadModel(reference);
            if(model.Skeleton==null||!new[]{"Idle","Fire","Reload","ReloadEmpty","Equip"}.All(clip=>model.Animations.Any(a=>a.Name==clip)))throw new Exception("Missing cyberpunk rig/clip: "+name);
            durations[name]=model.Animations.ToDictionary(a=>a.Name,a=>a.Duration);
            var profile=new AnimationProfile{Name="Cyberpunk "+name,Rig=new(){Type=AnimationRigType.Generic,ReferenceModel=reference},Locomotion=new(){Idle="Idle",Walk="Idle",Run="Idle",Jump="Idle",Fall="Idle",Land="Idle",TransitionDuration=0}};
            foreach(string clip in new[]{"Fire","Reload","ReloadEmpty","Equip"})profile.Actions.Add(new(){Name=clip,Clip=clip,BlendIn=clip.StartsWith("Reload")?0:.012f,BlendOut=0,Priority=clip.StartsWith("Reload")?20:10,Interruptible=true});
            string profilePath="Assets/Animation/Cyberpunk_"+name+".byteanim";AnimationProfileSerializer.Save(Program.Root+"/"+profilePath,profile);runtime.Database.Scan();profiles[name]=runtime.Database.ResolveReference(profilePath);
            var frame=model.Skeleton.Bones.Single(b=>b.Name=="Frame");var muzzle=model.Skeleton.Bones.Single(b=>b.Name=="MuzzleSocket");Matrix4x4.Invert(frame.BindPose,out var frameBind);Matrix4x4.Invert(muzzle.BindPose,out var muzzleBind);Matrix4x4.Decompose(frameBind,out _,out var rotation,out _);
            var sockets=ModelSocketMetadataStore.Load(Program.Root,model.Guid).Select(s=>s.Clone()).ToList();var socket=sockets.FirstOrDefault(s=>s.Name=="WeaponMuzzle");if(socket==null){socket=new(){Name="WeaponMuzzle"};sockets.Add(socket);}socket.BoneName="Frame";socket.PositionOffset=Vector3.Transform(muzzleBind.Translation-frameBind.Translation,Quaternion.Inverse(rotation));ModelSocketMetadataStore.Save(Program.Root,model.Guid,sockets);model.ReplaceSockets(sockets);
            reports.Add(new{name,triangles=model.Meshes.Sum(m=>m.Indices.Length/3),bones=model.Skeleton.Bones.Count,clips=durations[name],socket=socket.PositionOffset});
        }
        var scene=runtime.Serializer.Load(Program.Root+"/Scenes/Main.bytescene");
        foreach(var obj in scene.GameObjects.ToArray())
        {
            var renderer=obj.GetComponent<SkeletalMeshRenderer>();if(renderer==null)continue;
            string? name=Program.Names.FirstOrDefault(n=>obj.Name.Contains(n,StringComparison.OrdinalIgnoreCase));if(name==null)continue;
            bool weapon=obj.GetComponent<WeaponInstance3D>()!=null;
            renderer.Model=weapon?models[name]:runtime.Database.ResolveReference("Assets/Models/Cyberpunk_"+name+"_Prop.glb");renderer.SkeletonKey="";renderer.PlayOnStart=weapon;renderer.DefaultAnimation=weapon?"Idle":"";
            if(!weapon)
            {
                var reference=renderer.Model;var prop=runtime.Assets.LoadModel(reference);obj.RemoveComponent(renderer);
                obj.AddComponent(new ModelHierarchyInstance { Model=reference });
                foreach(var mesh in prop.Meshes)
                {
                    var child=scene.CreateGameObject(name+" display mesh "+mesh.Name);child.SetParent(obj,false);
                    child.AddComponent(new MeshRenderer { MeshReference=new ModelMeshReference(reference,mesh.Key),Material=string.IsNullOrEmpty(mesh.MaterialKey)?new Material():runtime.Assets.GetModelMaterial(reference,mesh.MaterialKey).Clone(),MaterialReference=string.IsNullOrEmpty(mesh.MaterialKey)?null:new ModelMaterialReference(reference,mesh.MaterialKey) });
                }
                continue;
            }
            if(obj.GetComponent<AnimationController>() is {} controller)controller.AnimationProfile=profiles[name];
            if(obj.GetComponent<WeaponDefinition3D>() is {} d)
            {
                d.ReloadSeconds=durations[name]["Reload"];d.EmptyReloadSeconds=durations[name]["ReloadEmpty"];d.EmptyReloadAnimation="ReloadEmpty";d.EquipSeconds=durations[name]["Equip"];
                d.MuzzleSource=WeaponMuzzleSource.AnimationSocket;d.MuzzleSocket="WeaponMuzzle";
                obj.Transform.LocalPosition=name=="Pistol"?new(.14f,-.015f,-.50f):new(.18f,-.045f,-.72f);
                obj.Transform.LocalRotation=Quaternion.CreateFromYawPitchRoll(MathF.PI+.08f,name=="Pistol"?-.085f:-.14f,0);
            }
        }
        foreach(var meshRenderer in scene.GameObjects.Select(o=>o.GetComponent<MeshRenderer>()).OfType<MeshRenderer>())
            if(meshRenderer.MaterialReference is {} materialReference && materialReference.Model.CachedProjectPath?.Contains("Cyberpunk_")==true)
                meshRenderer.Material=runtime.Assets.GetModelMaterial(materialReference.Model,materialReference.SubAssetKey).Clone();
        runtime.Serializer.Save(scene,Program.Root+"/Scenes/Main.bytescene");var serialized=runtime.Serializer.Serialize(scene);
        foreach(var obj in scene.GameObjects.Where(o=>o.GetComponent<WeaponDefinition3D>()!=null))
        {
            var data=serialized.GameObjects.Single(o=>o.Id==obj.Id);data.ParentId=null;var blueprint=new BlueprintDefinition{Name="Cyberpunk "+obj.GetComponent<WeaponDefinition3D>()!.DisplayName,Root=data};new BlueprintSerializer().Save(blueprint,Program.Root+"/Assets/Blueprints/"+blueprint.Name+".byteblueprint");
        }
        var player=scene.GameObjects.Single(o=>o.GetComponent<FpsGunLab3D>()!=null);var descendants=scene.GameObjects.Where(o=>IsUnder(o,player)).ToArray();
        serialized=runtime.Serializer.Serialize(scene);
        var character=new BlueprintDefinition{Name="FPS Character with cyberpunk guns and arms",Type=BlueprintType.Character,Root=serialized.GameObjects.Single(o=>o.Id==player.Id),Children=serialized.GameObjects.Where(o=>descendants.Any(d=>d.Id==o.Id)&&o.Id!=player.Id).ToList()};
        Backup("Assets/Blueprints/FPS Character.byteblueprint");new BlueprintSerializer().Save(character,Program.Root+"/Assets/Blueprints/FPS Character.byteblueprint");
        runtime.Database.Scan();File.WriteAllText(Program.Root+"/Preview/cyberpunk-import.json",JsonSerializer.Serialize(reports,new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine("CYBERPUNK IMPORT PASS");
        bool IsUnder(GameObject obj,GameObject root){for(GameObject? current=obj;current!=null;current=current.Parent)if(current==root)return true;return false;}
        void Backup(string path){string target=backup+"/"+path.Replace('/','_');if(File.Exists(Program.Root+"/"+path)&&!File.Exists(target))File.Copy(Program.Root+"/"+path,target);}
    }
}
