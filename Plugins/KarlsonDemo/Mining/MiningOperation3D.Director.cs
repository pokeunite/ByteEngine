using System.ComponentModel;
using System.Numerics;
using ByteEngine.Core.AI;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Navigation;
using ByteEngine.Core.Scene;
namespace KarlsonDemo.Plugin;
public sealed partial class MiningOperation3D
{
 [Category("Wave director")]public bool StagedSpawning{get;set;}=true;
 [Category("Wave director")]public float InitialWaveFraction{get;set;}=2f/3;
 [Category("Wave director")]public int ReinforcementBatch{get;set;}=4;
 [Category("Wave director")]public int KillsBeforeReinforcement{get;set;}=3;
 [Category("Wave director")]public float MinimumReinforcementGap{get;set;}=3;
 [Category("Wave director")]public float PressureReliefHealthFraction{get;set;}=.3f;
 [Category("Wave director")]public float PressureReliefDelayMultiplier{get;set;}=2.5f;
 [Category("Wave director")]public float FastClearWindowSeconds{get;set;}=8;
 [Category("Wave director")]public float FastSpawnIntervalMultiplier{get;set;}=.55f;
 [Category("Wave recovery")]public bool RecoverStrandedEnemies{get;set;}=true;
 [Category("Wave recovery")]public float StrandedSeconds{get;set;}=22;
 [Category("Wave recovery")]public float FinalEnemyMarkerDelay{get;set;}=5;
 [Category("Wave recovery")]public float FinalEnemyMarkerHeight{get;set;}=1.8f;
 [Category("Wave recovery")]public float FinalEnemyMarkerSize{get;set;}=16;
 [Browsable(false)]public int ReleasedEnemies{get;private set;}
 [Browsable(false)]public int StrandedEnemiesRecovered{get;private set;}
 [Browsable(false)]public string LastEnemyRecovery{get;private set;}="";
 float reinforcementClock,killWindow,watchdogClock,finalEnemyClock;int killsAtRelease;bool fastClearing;
 [Browsable(false)]public float FinalEnemyMarkerSeconds=>finalEnemyClock;
 [Browsable(false)]public IReadOnlyList<GameObject> OperationEnemies=>_enemies;
 readonly Dictionary<Guid,(Vector3 Position,float Still)> enemyProgress=[];
 void ResetDirector()
 {
  ReleasedEnemies=StagedSpawning?Math.Clamp((int)Math.Ceiling(EnemiesScheduled*Math.Clamp(InitialWaveFraction,.1f,1)),1,EnemiesScheduled):EnemiesScheduled;
  reinforcementClock=killWindow=watchdogClock=finalEnemyClock=0;killsAtRelease=0;fastClearing=false;enemyProgress.Clear();
 }
 void TickDirector(float delta)
 {
  reinforcementClock+=delta;killWindow+=delta;
  int alive=AliveEnemies,kills=Math.Max(0,EnemiesSpawned-alive);
  float relief=Math.Clamp(PressureReliefHealthFraction,0,1);
  bool struggling=Player?.GetComponent<HealthComponent>()?.HealthPercent<relief||DrillHealthFraction<relief;
  float gap=Math.Max(.25f,MinimumReinforcementGap)*(struggling?Math.Clamp(PressureReliefDelayMultiplier,1,5):1);
  if(ReleasedEnemies<EnemiesScheduled&&reinforcementClock>=gap&&(kills-killsAtRelease>=Math.Max(1,KillsBeforeReinforcement)||alive==0&&EnemiesSpawned>=ReleasedEnemies))
  {
   fastClearing=!struggling&&kills-killsAtRelease>=Math.Max(1,KillsBeforeReinforcement)&&killWindow<Math.Max(1,FastClearWindowSeconds);
   ReleasedEnemies=Math.Min(EnemiesScheduled,ReleasedEnemies+Math.Clamp(ReinforcementBatch,1,24));killsAtRelease=kills;reinforcementClock=killWindow=0;
  }
  finalEnemyClock=EnemiesSpawned==EnemiesScheduled&&alive is >0 and <=2?finalEnemyClock+delta:0;
  if(!RecoverStrandedEnemies||(watchdogClock+=delta)<1)return;float elapsed=watchdogClock;watchdogClock=0;
  foreach(var enemy in _enemies)
  {
   if(enemy.Scene!=GameObject.Scene||enemy.GetComponent<HealthComponent>() is not {IsDead:false})continue;
   var position=enemy.Transform.WorldPosition;var prior=enemyProgress.GetValueOrDefault(enemy.Id,(position,0f));
   bool finite=float.IsFinite(position.X)&&float.IsFinite(position.Y)&&float.IsFinite(position.Z);
   float still=finite&&Vector3.DistanceSquared(position,prior.Item1)<.16f?prior.Item2+elapsed:0;
   enemyProgress[enemy.Id]=(position,still);
   var target=GameObject.Scene!.FindGameObject(enemy.GetComponent<SimpleEnemyAI3D>()?.TargetId??DrillObjectId);
   var points=SpawnPoints();if(points.Length==0)continue;
   bool invalid=!finite||position.Y<points.Min(p=>p.Transform.WorldPosition.Y)-.35f||position.Y>points.Max(p=>p.Transform.WorldPosition.Y)+30;
   bool far=target!=null&&Vector3.DistanceSquared(position,target.Transform.WorldPosition)>9;
   bool blocked=enemy.GetComponent<NavigationAgent3D>()?.Status==NavigationAgentStatus.Unreachable&&target!=null&&Vector3.DistanceSquared(position,target.Transform.WorldPosition)>9;
   if(!invalid&&enemy.ActiveInHierarchy&&!((far||blocked)&&still>=(alive<=2?6:Math.Max(5,StrandedSeconds))))continue;
   // Re-enter from an authored front spawn, never from beside the player.
   var regionId=enemy.GetComponent<NavigationAgent3D>()?.RegionObject??Guid.Empty;
   var region=GameObject.Scene.GetComponentsSnapshot<NavigationRegion3D>().FirstOrDefault(r=>r.Enabled&&r.Ready&&(regionId==Guid.Empty||r.GameObject.Id==regionId));
   bool Reachable(Vector3 start)
   {
    if(region?.Ready!=true||target==null)return true;
    // Machine origins sit inside their obstacle footprint. Validate a route to
    // an outside approach point, rather than rejecting every drill-bound path.
    var bounds=MiningCampGeometry.Transform(MiningCampGeometry.LocalBounds(target),target.Transform.WorldMatrix);
    var end=Vector3.Clamp(start,bounds.Minimum,bounds.Maximum);var outward=start-end;outward.Y=0;
    if(outward.LengthSquared()>.001f)end+=Vector3.Normalize(outward)*.8f;end.Y=start.Y;
    return region.FindPath(start,end,4000).Count>0;
   }
   var spawn=points.FirstOrDefault(p=>(Player==null||Vector3.DistanceSquared(p.Transform.WorldPosition,Player.Transform.WorldPosition)>144)&&Reachable(p.Transform.WorldPosition));
   if(spawn==null){LastEnemyRecovery="No safe reachable recovery spawn; final enemy remains marked.";continue;}
   bool wasInactive=!enemy.ActiveInHierarchy;if(wasInactive)enemy.SetParent(null,true);
   enemy.Active=true;if(!TryChooseSpacedSpawn(points,out var recovered))continue;enemy.Transform.WorldPosition=recovered;
   if(enemy.GetComponent<NavigationAgent3D>() is {} nav){nav.Enabled=true;nav.SetDestination(target?.Transform.WorldPosition??spawn.Transform.WorldPosition);}
   if(enemy.GetComponent<AIBehavior3D>() is {} ai)ai.Enabled=true;
   if(enemy.GetComponent<SimpleEnemyAI3D>() is {} combat)combat.Enabled=true;
   enemyProgress[enemy.Id]=(enemy.Transform.WorldPosition,0);StrandedEnemiesRecovered++;LastEnemyRecovery=$"Recovered {enemy.Name}: {(invalid?"outside arena":wasInactive?"inactive":"stalled")}.";
  }
 }
}
