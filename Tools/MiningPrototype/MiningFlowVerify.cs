using System.Numerics;
using System.Reflection;
using ByteEngine.Core;
using ByteEngine.Core.AI;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Navigation;
using ByteEngine.Core.Runtime;
using ByteEngine.Core.Scene;
using KarlsonDemo.Plugin;
internal static class MiningFlowVerify
{
 public static void Run(Scene scene)
 {
  void Check(bool ok,string label){if(!ok)throw new Exception(label);Console.WriteLine("PASS flow: "+label);}
  var op=scene.GameObjects.SelectMany(o=>o.Components).OfType<MiningOperation3D>().Single();op.PersistProgress=false;foreach(var trade in scene.GameObjects.SelectMany(o=>o.Components).OfType<MiningTrade3D>())trade.PersistProgress=false;
  var host=new SceneManager();host.LoadScene(scene);var region=scene.GameObjects.SelectMany(o=>o.Components).OfType<NavigationRegion3D>().Single();var drill=scene.FindGameObject(op.DrillObjectId)!;
  foreach(var point in scene.GameObjects.SelectMany(o=>o.Components).OfType<MiningSpawnPoint3D>())
  {Check(point.Transform.WorldPosition.Z<-30&&Vector3.Distance(point.Transform.WorldPosition,drill.Transform.WorldPosition)>30,"spawn lane is ahead and distant");Check(region.FindPath(point.Transform.WorldPosition,drill.Transform.WorldPosition+new Vector3(0,0,3),20000).Count>0,"front lane has a navigation route to the camp");}
  Check(op.StartOperation(),"front wave starts");op.Tick(.1f);Check(op.SpawnedEnemies.Count==1,"one enemy arrives from an authored front lane");var enemy=op.SpawnedEnemies[0];var brain=enemy.GetComponent<AIBehavior3D>()!;var start=enemy.Transform.WorldPosition;
  for(int i=0;i<180;i++){typeof(Time).GetMethod("Update",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,[.05d]);typeof(Scene).GetMethod("UpdateInternal",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(scene,null);}
  Check(enemy.Scene!=null&&Vector3.Distance(enemy.Transform.WorldPosition,drill.Transform.WorldPosition)<Vector3.Distance(start,drill.Transform.WorldPosition)-3,"enemy actually advances from the front rather than spawning beside camp");
  Check(brain.LastError.Length==0,"front approach produces no AI error");
  host.UnloadScene();Console.WriteLine("FRONT APPROACH VERIFIED");
 }
}
