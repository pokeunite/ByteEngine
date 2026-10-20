using System.Numerics;
using System.Text.Json;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Animation;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Runtime;
using ByteEngine.Core.Blueprints;
using ByteEngine.Core.Scene;

// Offline native asset/scene authoring. No gameplay code is installed in the project.
internal static class SketchfabAuthor
{
 const string Root="C:/Users/codex/Documents/ultra violence";
 public static void Run(GameProjectRuntime runtime)
 {
  var scene=runtime.LoadStartupScene();
  foreach(var obj in scene.GameObjects.Where(o=>o.Name.Contains("Infima")||o.Name.Contains("display",StringComparison.OrdinalIgnoreCase)||o.Name.EndsWith(" stand")).ToArray())scene.DestroyGameObject(obj);
  var rigs=scene.GameObjects.Where(o=>o.Components.Any(c=>c.GetType().Name=="WeaponDefinition3D")).ToArray();
  var report=new List<object>();
  string[] files=["pistol","saps-12","ak-47"],names=["Pistol","SPAS-12","AK-47"];
  for(int slot=0;slot<3;slot++)
  {
   var rig=rigs.Single(o=>(int)o.Components.Single(c=>c.GetType().Name=="WeaponDefinition3D").GetType().GetProperty("Slot")!.GetValue(o.Components.Single(c=>c.GetType().Name=="WeaponDefinition3D"))! ==slot);
   foreach(var child in rig.Children.ToArray())scene.DestroyGameObject(child);
   string path="Assets/Sketchfab/fps_character_animation_pack_"+files[slot]+".glb";
   var reference=runtime.Database.ResolveReference(path);var model=runtime.Assets.LoadModel(reference);
   string Clip(string suffix)=>model.Animations.Single(a=>a.Name.EndsWith(suffix,StringComparison.Ordinal)&&!a.Name.Contains("Aim")&&!a.Name.Contains("Accion")).Name;
   string idle=Clip("_Idle"),fire=Clip("_Fire"),reload=Clip("_Reload"),equip=Clip("_Equip"),walk=Clip("_Walk"),run=Clip("_Run");
   var profile=new AnimationProfile{Name=names[slot]+" original Sketchfab rig",Rig=new(){Type=AnimationRigType.Generic,ReferenceModel=reference},Locomotion=new(){Idle=idle,Walk=walk,Run=run,Jump=idle,Fall=idle,Land=idle,TransitionDuration=.06f}};
   foreach(var (action,clip) in new[]{("Equip",equip),("Fire",fire),("Reload",reload)})profile.Actions.Add(new(){Name=action,Clip=clip,BlendIn=.02f,BlendOut=.05f,Priority=action=="Reload"?20:10,Interruptible=true});
   string profilePath="Assets/Animation/Sketchfab "+names[slot]+".byteanim";
   AnimationProfileSerializer.Save(Root+"/"+profilePath,profile);
   using(var db=new AssetDatabase(Root,["Assets","Scenes"]))db.Scan();runtime.Database.Scan();
   rig.Name=names[slot]+" view model";rig.Active=slot==2;
   // The three downloads have different authored scene scales/camera heights.
   // Place the complete source rig; do not alter its bones or animation tracks.
   rig.Transform.LocalPosition=new(0,slot==0?-1.8f:slot==1?-1.65f:-1.6f,0);
   rig.Transform.LocalRotation=Quaternion.Identity;rig.Transform.LocalScale=new(slot==0?.4f:slot==1?.45f:1f);
   var renderer=rig.GetComponent<SkeletalMeshRenderer>()!;renderer.Model=reference;renderer.SkeletonKey="";renderer.MaterialKeys=[];renderer.MaterialAssetSlots=[];renderer.DefaultAnimation=idle;renderer.PlayOnStart=true;renderer.Loop=true;
   rig.GetComponent<AnimationController>()!.AnimationProfile=runtime.Database.ResolveReference(profilePath);
   var d=rig.Components.Single(c=>c.GetType().Name=="WeaponDefinition3D");var instance=rig.Components.Single(c=>c.GetType().Name=="WeaponInstance3D");
   void Set(string name,object value)=>d.GetType().GetProperty(name)!.SetValue(d,value);
   void EnumSet(string name,string value)=>Set(name,Enum.Parse(d.GetType().GetProperty(name)!.PropertyType,value));
   Set("DisplayName",names[slot]);Set("MagazineCapacity",slot==0?12:slot==1?8:30);Set("ReserveCapacity",240);Set("ReloadSeconds",model.Animations.Single(a=>a.Name==reload).Duration);Set("EmptyReloadSeconds",model.Animations.Single(a=>a.Name==reload).Duration);Set("EquipSeconds",model.Animations.Single(a=>a.Name==equip).Duration);
   EnumSet("TriggerMode",slot==2?"Automatic":"SemiAutomatic");EnumSet("ReloadMode","Magazine");EnumSet("ShotMode","Hitscan");EnumSet("MuzzleSource","LocalOffset");Set("MuzzleLocalOffset",slot==0?new Vector3(.3f,4.2f,-1.9f):slot==1?new Vector3(.25f,3.5f,-2.1f):new Vector3(.13f,1.45f,-.84f));Set("ShowShotTrace",false);
   instance.GetType().GetProperty("CurrentAmmo")!.SetValue(instance,slot==0?12:slot==1?8:30);instance.GetType().GetProperty("ReserveAmmo")!.SetValue(instance,90);
   report.Add(new{name=names[slot],bones=model.Skeleton!.Bones.Count,meshes=model.Meshes.Count,triangles=model.Meshes.Sum(m=>m.Indices.Length/3),clips=model.Animations.Select(a=>new{a.Name,a.Duration})});
  }
  foreach(var extra in rigs.Where(o=>(int)o.Components.Single(c=>c.GetType().Name=="WeaponDefinition3D").GetType().GetProperty("Slot")!.GetValue(o.Components.Single(c=>c.GetType().Name=="WeaponDefinition3D"))! >2))scene.DestroyGameObject(extra);
  foreach(var text in scene.GameObjects.Select(o=>o.GetComponent<UiText>()).OfType<UiText>())if(text.Text.Contains("WASD"))text.Text="WASD MOVE / MOUSE LOOK / SPACE JUMP / 1 PISTOL / 2 SPAS-12 / 3 AK-47 / LMB FIRE / R RELOAD";
  runtime.Serializer.Save(scene,Root+"/Scenes/Weapons Range.bytescene");
  var data=runtime.Serializer.Serialize(scene);var player=scene.FindGameObject("FPS Character")!;
  bool Under(GameObject obj){for(GameObject? current=obj;current!=null;current=current.Parent)if(current==player)return true;return false;}
  var ids=scene.GameObjects.Where(Under).Select(o=>o.Id).ToHashSet();new BlueprintSerializer().Save(new(){Name="Ultra Violence Sketchfab FPS",Type=BlueprintType.Character,Root=data.GameObjects.Single(o=>o.Id==player.Id),Children=data.GameObjects.Where(o=>ids.Contains(o.Id)&&o.Id!=player.Id).ToList()},Root+"/Assets/Blueprints/Ultra Violence FPS.byteblueprint");
  using(var db=new AssetDatabase(Root,["Assets","Scenes"]))db.Scan();
  File.WriteAllText(Root+"/Preview/Sketchfab imports.json",JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));
  Console.WriteLine("ONLY THREE ORIGINAL SKETCHFAB WEAPON RIGS AUTHORED");
 }
}
