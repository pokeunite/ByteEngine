using System.Numerics;
using ByteEngine.Core.AI;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Scene;
using ByteEngine.Core.VisualLogic;
using ByteEngine.Core.Navigation;
using KarlsonDemo.Plugin;
internal static class MiningHunterSpacingCheck
{
 public static void Run(Scene scene,MiningOperation3D op)
 {
  op.Enabled=false;var player=scene.FindGameObject(op.PlayerObjectId)!;
  player.Transform.WorldPosition=new(0,0,4);var hp=player.GetComponent<HealthComponent>()!;hp.MaxHealth=10000;hp.Revive();
  if(player.GetComponent<PlayerController3D>() is {} controller)controller.Enabled=false;
  var enemy=RuntimeSpawnService.SpawnBlueprint(scene,op.HunterBlueprint,new(0,0,2.5f))!;op.ConfigureEnemy(enemy,true);
  var regionObject=scene.CreateGameObject("Hunter spacing test navigation");regionObject.Transform.WorldPosition=new(-5,0,-1);
  var region=regionObject.AddComponent(new NavigationRegion3D{Width=20,Height=20,CellSize=.5f,ObstacleLayerMask=0,BakeOnStart=false});region.Bake();
  var agent=enemy.GetComponent<NavigationAgent3D>()!;agent.RegionObject=regionObject.Id;agent.CollisionAvoidance=false; // Isolate the authored sequence from camp machinery; blocked retreats are bounded by Timeout.
  var ai=enemy.GetComponent<AIBehavior3D>()!;ai.TargetObjectId=player.Id;ai.DecisionInterval=.05f;
  int hits=0,retreatFrames=0,waitFrames=0;float maxSeparation=0;bool approachedAgain=false;
  hp.Damaged+=(_,_)=>hits++;
  for(int i=0;i<300;i++){
   ai.Tick(.05f);float distance=Vector3.Distance(enemy.Transform.WorldPosition,player.Transform.WorldPosition);
   if(ai.CurrentTask=="Step back after attacking"){retreatFrames++;maxSeparation=Math.Max(maxSeparation,distance);}
   if(ai.CurrentTask=="Brief opening before pursuing again")waitFrames++;
   if(retreatFrames>0&&ai.CurrentTask=="Chase the operator")approachedAgain=true;
   if(hits>=2&&approachedAgain&&waitFrames>0)break;
  }
  if(hits<2||retreatFrames==0||waitFrames==0||maxSeparation<2.4f||!approachedAgain)
   throw new Exception($"Hunter spacing failed: hits={hits},retreat={retreatFrames},gap={maxSeparation},pause={waitFrames},pursuit={approachedAgain},task={ai.CurrentTask},error={ai.LastError}");
  Console.WriteLine($"PASS hunter: {hits} attacks, backs off to {maxSeparation:0.00} m, pauses and pursues again; editable authored AI sequence");
 }
}
