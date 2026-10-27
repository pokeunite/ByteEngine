using System.Numerics;
using System.Reflection;
using ByteEngine.Core;
using ByteEngine.Core.AI;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Navigation;
using ByteEngine.Core.Runtime;
using ByteEngine.Core.Scene;
using KarlsonDemo.Plugin;
internal static class MiningAmbushCheck
{
 public static void Author(Scene scene,GameProjectRuntime runtime)
 {
  string backup=Program.Root+"/.byteengine/Backups/curiosity-ambush-"+DateTime.Now.ToString("yyyyMMdd-HHmmss");Directory.CreateDirectory(backup);File.Copy(Program.Root+"/Scenes/Main.bytescene",backup+"/Main.bytescene");
  var op=scene.GameObjects.SelectMany(o=>o.Components.OfType<MiningOperation3D>()).Single();var root=scene.FindGameObject("Far canyon spider nest")??scene.CreateGameObject("Far canyon spider nest");root.Transform.WorldPosition=new(10,0,-131);
  var trap=root.GetComponent<MiningCuriosityAmbush3D>()??root.AddComponent(new MiningCuriosityAmbush3D());trap.OperationObjectId=op.GameObject.Id;trap.PlayerObjectId=op.PlayerObjectId;trap.EnemyBlueprint=op.HunterBlueprint;trap.EnemyCount=50;trap.SpawnInterval=.12f;trap.TriggerSize=new(70,8,12);trap.MinimumSpawnRadius=9;trap.MaximumSpawnRadius=18;
  runtime.Serializer.Save(scene,Program.Root+"/Scenes/Main.bytescene");Console.WriteLine("INSTALLED far canyon one-shot 50 spider nest; trigger begins at Z -125. "+backup);
 }
 static void Check(bool ok,string text){if(!ok)throw new Exception(text);Console.WriteLine("PASS AMBUSH "+text);}
 public static void Verify(Scene scene,MiningOperation3D op)
 {
  var hash=System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(Program.Root+"/Scenes/Main.bytescene"));var host=new SceneManager();host.LoadScene(scene);
  var player=scene.FindGameObject(op.PlayerObjectId)!;var health=player.GetComponent<HealthComponent>()!;health.MaxHealth=100000;health.CurrentHealth=100000;
  var trap=scene.GameObjects.SelectMany(o=>o.Components.OfType<MiningCuriosityAmbush3D>()).Single();
  var clock=typeof(Time).GetMethod("Update",BindingFlags.NonPublic|BindingFlags.Static)!;var update=typeof(Scene).GetMethod("UpdateInternal",BindingFlags.NonPublic|BindingFlags.Instance)!;
  void Frame(){clock.Invoke(null,[.05d]);update.Invoke(scene,null);}
  for(int i=0;i<20;i++)Frame();Check(!trap.Triggered&&op.RoamingEnemiesAlive==0,"camp stays safe before crossing the far-end trigger");
  player.Transform.WorldPosition=new(10,0,-124);Frame();Check(!trap.Triggered,"approaching trigger does not fire early");
  player.Transform.WorldPosition=new(10,0,-130);for(int i=0;i<180;i++)Frame();
  Check(trap.Triggered&&!trap.Spawning&&trap.SpawnedCount==50&&op.RoamingEnemiesAlive==50,"entering far end releases exactly 50 live spiders");
  Check(op.Phase==MiningPhase.Preparation&&op.EnemiesSpawned==0,"surprise does not consume or complete a drill contract");
  foreach(var enemy in op.SpawnedEnemies){var ai=enemy.GetComponent<AIBehavior3D>()!;Check(ai.TargetObjectId==player.Id&&ai.LastError.Length==0,"spider targets player with valid behaviour");}
  var starts=op.SpawnedEnemies.ToDictionary(e=>e.Id,e=>e.Transform.WorldPosition);for(int i=0;i<100;i++)Frame();Check(op.SpawnedEnemies.Any(e=>Vector3.Distance(starts[e.Id],e.Transform.WorldPosition)>1),"released spiders move through navigation");
  var sentry=scene.CreateGameObject("Ambush defence check");sentry.Transform.WorldPosition=player.Transform.WorldPosition+new Vector3(3,0,0);var defence=sentry.AddComponent(new MiningDefense3D{OperationObjectId=op.GameObject.Id,WorkshopObjectId=scene.GameObjects.SelectMany(o=>o.Components.OfType<MiningTrade3D>()).Single().GameObject.Id,Kind=MiningDefenseKind.Sentry,BaseDamage=.001f,Range=40,BaseInterval=.1f});for(int i=0;i<40;i++)Frame();Check(defence.ShotsFired>0,"placed defences can fire at roaming spiders between drill rounds");scene.DestroyGameObject(sentry);
  player.Transform.WorldPosition=new(10,0,-110);Frame();player.Transform.WorldPosition=new(10,0,-130);Frame();Check(trap.SpawnedCount==50&&!trap.Activate(),"leaving and re-entering cannot repeat the ambush");
  Check(op.StartOperation()&&op.RoamingEnemiesAlive==50,"starting drill cannot erase the punishment spiders");
  health.Kill();Frame();Check(op.Phase==MiningPhase.Failed&&op.RoamingEnemiesAlive==0,"operator death cleans the swarm and uses normal recovery");
  op.Retry();Frame();Check(!health.IsDead&&trap.SpawnedCount==50&&!trap.Spawning,"recovery does not release another fifty");
  Check(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(Program.Root+"/Scenes/Main.bytescene")).SequenceEqual(hash),"native check preserves authored scene and disables business persistence");host.UnloadScene();Console.WriteLine("AMBUSH NATIVE VERIFICATION COMPLETE");
 }
}
