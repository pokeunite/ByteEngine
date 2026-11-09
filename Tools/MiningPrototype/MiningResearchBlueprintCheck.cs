using System.Numerics;
using System.Reflection;
using System.Text.Json;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Runtime;
using ByteEngine.Core.Scene;
using ByteEngine.Core.VisualLogic;
using KarlsonDemo.Plugin;
internal static class MiningResearchBlueprintCheck
{
 public static void Run(Scene scene)
 {
  foreach(var name in new[]{"ResearchStation","SecurityDrone","FieldBarricade"})
  {
   string path="Assets/Blueprints/Mining/Research/"+name+".byteblueprint";using var meta=JsonDocument.Parse(File.ReadAllText(Program.Root+"/"+path+".meta"));var reference=new AssetReference(meta.RootElement.GetProperty("guid").GetGuid(),path);
   var root=RuntimeSpawnService.SpawnBlueprint(scene,reference,new Vector3(35,0,20))??throw new Exception("Blueprint could not spawn: "+name);
   if(name=="SecurityDrone"){
    var drone=root.GetComponent<MiningSecurityDrone3D>()??throw new Exception("Drone gameplay component did not deserialize");
    typeof(Component).GetMethod("StartInternal",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(drone,null);
    var skin=root.Children.Select(o=>o.GetComponent<SkeletalMeshRenderer>()).OfType<SkeletalMeshRenderer>().Single();
    if(!skin.ResolveRuntimeResources()||!skin.Play("SecurityDrone_Hover",true,0)||drone.BatteryRemaining!=60)throw new Exception("Drone model, clip or battery binding failed");
   }else{
    Console.WriteLine("COMPONENTS "+string.Join(",",root.Components.Select(c=>c.GetType().FullName)));
    if(root.GetComponent<MiningCampObject3D>()==null)throw new Exception("Missing movable camp binding: "+name);
    if(root.GetComponent<BoxCollider3D>()==null)throw new Exception("Missing collision: "+name);
    if(root.GetComponent<SkeletalMeshRenderer>()?.ResolveRuntimeResources()!=true)throw new Exception("Missing model binding: "+name);
   }
   scene.DestroyGameObject(root);Console.WriteLine("PASS project blueprint spawn, component bindings and model: "+name);
  }
 }
}
