using System.Numerics;
using System.Reflection;
using ByteEngine.Core;
using ByteEngine.Core.AI;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Navigation;
using ByteEngine.Core.Physics;
using ByteEngine.Core.Scene;
using KarlsonDemo.Plugin;
internal static class MiningCanyonVerify
{
 static void Check(bool ok,string text){if(!ok)throw new Exception(text);Console.WriteLine("PASS CANYON "+text);}
 public static void Run(Scene scene,MiningOperation3D op)
 {
  var hash=System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(Program.Root+"/Scenes/Main.bytescene"));
  var root=scene.FindGameObject("Alien canyon environment")!;
  bool Under(GameObject o){for(var p=o;p!=null;p=p.Parent)if(p==root)return true;return false;}
  var meshes=scene.GameObjects.Where(Under).SelectMany(o=>o.Components.OfType<MeshRenderer>()).ToArray();
  Check(meshes.Length>600&&meshes.All(m=>m.MeshReference!=null),"authored rock meshes resolve from reusable model assets");
  Check(meshes.Count(m=>m.Material.BaseColor.X<.15f&&m.Material.BaseColor.Z<.2f)>400,"basalt materials retain their dark violet colours");
  var floor=scene.FindGameObject("Alien worksite floor")!;Check(floor.Transform.WorldScale.X==84&&floor.Transform.WorldScale.Z==168,"floor measures 84 by 168 metres");
  var player=scene.FindGameObject(op.PlayerObjectId)!;var drill=scene.FindGameObject(op.DrillObjectId)!;
  player.GetComponent<HealthComponent>()!.MaxHealth=100000;player.GetComponent<HealthComponent>()!.CurrentHealth=100000;op.BaseDrillHealth=100000;
  var host=new SceneManager();host.LoadScene(scene);
  var clock=typeof(Time).GetMethod("Update",BindingFlags.NonPublic|BindingFlags.Static)!;var update=typeof(Scene).GetMethod("UpdateInternal",BindingFlags.NonPublic|BindingFlags.Instance)!;
  void Frame(){clock.Invoke(null,[.05d]);update.Invoke(scene,null);}
  Frame();Frame();
  var region=scene.GameObjects.SelectMany(o=>o.Components.OfType<NavigationRegion3D>()).Single();Check(region.Ready&&region.WalkableCells>15000,"larger navigation grid bakes with rock obstacles");
  foreach(var point in scene.GameObjects.SelectMany(o=>o.Components.OfType<MiningSpawnPoint3D>()))
  {
   var start=point.Transform.WorldPosition;Check(Vector3.Distance(start,drill.Transform.WorldPosition)>105,"spawn lies over 105 metres in front of drill");
   Check(region.FindPath(start,drill.Transform.WorldPosition+new Vector3(0,0,5),20000).Count>0,"distant entrance has a path to drill");
   Check(region.FindPath(start,player.Transform.WorldPosition,20000).Count>0,"distant entrance has a path to player");
  }
  int probes=0;
  for(float z=-132;z<=24;z+=2)foreach(var side in new[]{-1,1})
  {var origin=new Vector3(side<0?-21:41,1,z);Check(GameplayQuery3D.Raycast(scene,origin,new Vector3(side,0,0),out var hit,20,includeTriggers:false)&&Under(hit.GameObject),"side boundary sealed at "+z+" / "+side);probes++;}
  for(float x=-24;x<=44;x+=2){Check(GameplayQuery3D.Raycast(scene,new(x,1,20),Vector3.UnitZ,out var hit,20,includeTriggers:false)&&Under(hit.GameObject),"rear boundary sealed at "+x);probes++;}
  Console.WriteLine("BOUNDARY PROBES "+probes);
  op.ExtractionSeconds=2;op.SpawnInterval=.15f;Check(op.StartOperation(),"operation starts from enlarged camp");
  bool playerAttack=false,drillAttack=false,moving=false;int maxAlive=0;float minZ=0;
  for(int i=0;i<1700;i++)
  {
   Frame();maxAlive=Math.Max(maxAlive,op.AliveEnemies);
   foreach(var enemy in op.SpawnedEnemies.Where(e=>e.Scene==scene))
   {
    var ai=enemy.GetComponent<AIBehavior3D>()!;if(ai.LastError.Length!=0)throw new Exception(ai.LastError);
    if(enemy.GetComponent<NavigationAgent3D>() is {Moving:true})moving=true;
    minZ=Math.Min(minZ,enemy.Transform.WorldPosition.Z);
    if(enemy.GetComponent<SimpleEnemyAI3D>()?.AttackPhase=="Windup"){if(ai.TargetObjectId==player.Id)playerAttack=true;if(ai.TargetObjectId==drill.Id)drillAttack=true;}
   }
   if(playerAttack&&drillAttack&&player.GetComponent<HealthComponent>()!.CurrentHealth<100000&&drill.GetComponent<HealthComponent>()!.CurrentHealth<100000)break;
  }
  Check(moving&&minZ< -100,"aliens enter from the distant approach and navigate");
  foreach(var enemy in op.SpawnedEnemies){var nav=enemy.GetComponent<NavigationAgent3D>();Console.WriteLine($"ENEMY {enemy.Name} {enemy.Transform.WorldPosition} {nav?.Status}");}
  Check(playerAttack&&drillAttack,"both roles traverse the canyon and reach attack range");
  Check(player.GetComponent<HealthComponent>()!.CurrentHealth<100000&&drill.GetComponent<HealthComponent>()!.CurrentHealth<100000,"both targets take damage after the longer approach");
  Check(maxAlive<=op.MaximumAlive,"larger arena respects enemy budget");
  foreach(var enemy in op.SpawnedEnemies.ToArray())enemy.GetComponent<HealthComponent>()?.Kill();Frame();Check(op.Phase==MiningPhase.Preparation,"clearing the wave returns to preparation");
  Check(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(Program.Root+"/Scenes/Main.bytescene")).SequenceEqual(hash),"verification preserves authored scene");host.UnloadScene();Console.WriteLine("CANYON NATIVE VERIFICATION COMPLETE");
 }
}
