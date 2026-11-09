using System.Numerics;
using System.Reflection;
using ByteEngine.Core;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Navigation;
using ByteEngine.Core.Runtime;
using ByteEngine.Core.Scene;
using KarlsonDemo.Plugin;
internal static class MiningDecoratedCampVerify
{
 static void Check(bool ok,string label){if(!ok)throw new Exception(label);Console.WriteLine("PASS DECORATED CAMP "+label);}
 public static void Run(Scene scene,MiningOperation3D op)
 {
  var root=scene.FindGameObject("Approved decorated camp")!;Check(root!=null&&root.Children.Count==241,"all 240 prop instances and the shared paving collision are authored");
  Check(scene.GameObjects.Where(o=>o.IsDescendantOf(root)).SelectMany(o=>o.Components.OfType<MiningCampObject3D>()).All(m=>!m.AllowRelocation),"decorations are excluded from selection, labels and saved rearrangements");
  var host=new SceneManager();host.LoadScene(scene);var player=scene.FindGameObject(op.PlayerObjectId)!;var ui=scene.GetComponentsSnapshot<MiningBusinessUi3D>().Single();
  foreach(var obj in new[]{scene.FindGameObject(ui.AmmoMachineObjectId)!,scene.FindGameObject("Company tent")!})Check(MathF.Abs(Quaternion.Dot(obj.Transform.WorldRotation,Quaternion.Identity))>.999f,"machine faces the open battlefield: "+obj.Name);
  typeof(Time).GetMethod("Update",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,[.05d]);typeof(Scene).GetMethod("UpdateInternal",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(scene,null);
  var region=scene.GetComponentsSnapshot<NavigationRegion3D>().Single();var target=scene.FindGameObject(op.DrillObjectId)!;
  foreach(var spawn in scene.GetComponentsSnapshot<MiningSpawnPoint3D>())Check(region.FindPath(spawn.Transform.WorldPosition,target.Transform.WorldPosition+new Vector3(0,0,-4),30000).Count>0,"spawn has a route to the drill approach: "+spawn.GameObject.Name);
  var distance=typeof(MiningCampObject3D).Assembly.GetType("KarlsonDemo.Plugin.MiningCampGeometry")!.GetMethod("SurfaceDistance",BindingFlags.Static|BindingFlags.NonPublic)!;
  foreach(var pair in new[]{(op.DrillObjectId,MiningScreen.Drill),(ui.RefineryMachineObjectId,MiningScreen.Refinery),(ui.AmmoMachineObjectId,MiningScreen.AmmoPress),(ui.TerminalObjectId,MiningScreen.Company)}){
   var machine=scene.FindGameObject(pair.Item1)!;bool reachable=false;Vector3 candidate=default;
   for(int radius=1;radius<=14&&!reachable;radius++)for(int i=0;i<32&&!reachable;i++){
    float angle=i*MathF.Tau/32;var p=machine.Transform.WorldPosition+new Vector3(MathF.Cos(angle)*radius,.15f,MathF.Sin(angle)*radius);float near=pair.Item2==MiningScreen.Company?Vector3.Distance(p,machine.Transform.WorldPosition):(float)distance.Invoke(null,[machine,p])!;if(near>ui.InteractionDistance-.1f)continue;
    if(region.FindPath(player.Transform.WorldPosition,p,30000).Count==0)continue;candidate=p;reachable=true;}
   Check(reachable,"machine has a reachable interaction area: "+pair.Item2);player.Transform.WorldPosition=candidate;typeof(MiningBusinessUi3D).GetMethod("OnUpdate",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(ui,null);Check(ui.NearestScreen==pair.Item2,"correct nearby interaction resolves: "+pair.Item2);
  }
  host.UnloadScene();
 }
}
