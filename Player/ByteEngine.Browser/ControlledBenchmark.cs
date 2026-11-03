using System.Numerics;
using System.Runtime.InteropServices.JavaScript;
using ByteEngine.Core.AI;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Navigation;
using ByteEngine.Core.Runtime;
using ByteEngine.Core.VisualLogic;
using KarlsonDemo.Plugin;
public static partial class BrowserGame
{
 [JSExport]
 public static void ConfigureBenchmark(int count,string mode)
 {
  var scene=Loop.Scenes.ActiveScene??throw new InvalidOperationException("Start game first");
  scene.Physics.UseIncrementalQueryIndex=mode!="full-queries";
  Sink.HiddenMeshBudgetBytes=(mode=="small-mesh-cache"?32L:96L)*1024*1024;
  var op=scene.GameObjects.SelectMany(o=>o.Components.OfType<MiningOperation3D>()).Single();op.Enabled=false;op.PersistProgress=false;
  foreach(var o in scene.GameObjects.ToArray()){
   if(o.GetComponent<MiningPauseMenu3D>() is {} pause){pause.PersistSettings=false;if(pause.IsOpen)pause.Resume();pause.ShowMainMenuOnStart=false;pause.Enabled=false;}
   if(o.GetComponent<MiningBusinessUi3D>() is {} ui){ui.Close();ui.Enabled=false;}
   if(o.GetComponent<MiningTrade3D>() is {} trade)trade.PersistProgress=false;
  }
  var player=scene.FindGameObject(op.PlayerObjectId)!;player.GetComponent<HealthComponent>()!.MaxHealth=10000000;player.GetComponent<HealthComponent>()!.Revive();
  if(player.GetComponent<PlayerController3D>() is {} controller)controller.Enabled=false;
  if(player.GetComponent<CameraBoom3D>() is {} boom)boom.Enabled=false;
  if(player.GetComponent<MiningThirdPerson3D>() is {} third)third.Enabled=false;
  player.Transform.WorldPosition=new(0,0,4);var cam=scene.ActiveCamera!;cam.GameObject.SetParent(null,true);cam.Transform.WorldPosition=new(0,16,20);
  Matrix4x4.Invert(Matrix4x4.CreateLookAt(cam.Transform.WorldPosition,new(0,0,-22),Vector3.UnitY),out var pose);cam.Transform.WorldRotation=Quaternion.CreateFromRotationMatrix(pose);cam.FieldOfView=65;
  for(int i=0;i<Math.Clamp(count,0,200);i++){
   var enemy=RuntimeSpawnService.SpawnBlueprint(scene,i%3==0?op.RaiderBlueprint:op.HunterBlueprint,new((i%12-5.5f)*2.2f,0,-10-i/12*3.2f))!;
   op.ConfigureEnemy(enemy,i%3!=0);enemy.GetComponent<AIBehavior3D>()!.TargetObjectId=player.Id;enemy.GetComponent<AIBehavior3D>()!.DecisionInterval=.25f;enemy.GetComponent<SimpleEnemyAI3D>()!.TargetId=player.Id;
  }
  foreach(var o in scene.GameObjects.ToArray()){
   if(o.GetComponent<AIBehavior3D>() is {} scheduledAi)scheduledAi.StaggerDecisions=mode!="synchronized-ai";
   if(o.GetComponent<SkeletalMeshRenderer>() is {} batch)batch.BatchCrowdSubmeshes=mode!="raw-spiders";
   if(mode=="no-ai"){if(o.GetComponent<AIBehavior3D>() is {} ai)ai.Enabled=false;if(o.GetComponent<SimpleEnemyAI3D>() is {} simple)simple.Enabled=false;if(o.GetComponent<NavigationAgent3D>() is {} nav)nav.Enabled=false;}
   if(mode=="frozen"){if(o.GetComponent<MiningSpiderAnimation3D>() is {} driver)driver.Enabled=false;if(o.GetComponent<SkeletalMeshRenderer>() is {} skin)skin.Pause();}
  }
  if(mode=="contact-hitboxes")foreach(var collider in scene.GetComponentsSnapshot<ByteEngine.Core.Characters.Collider3D>())collider.QueryOnly=false;
  if(mode=="contact-movement")foreach(var collider in scene.GetComponentsSnapshot<ByteEngine.Core.Characters.CapsuleCollider3D>())collider.QueryOnly=false;
 }
}
