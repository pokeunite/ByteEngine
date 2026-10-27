using System.ComponentModel;
using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.AI;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Navigation;
using ByteEngine.Core.Runtime;
using ByteEngine.Core.Scene;
using ByteEngine.Core.VisualLogic;
using Component=ByteEngine.Core.Scene.Component;
namespace KarlsonDemo.Plugin;
/// <summary>An authored one-shot ambush, independent of the drill's finite wave quota.</summary>
public sealed class MiningCuriosityAmbush3D:Component
{
 [SceneObjectReference,Category("Bindings")] public Guid OperationObjectId{get;set;}
 [SceneObjectReference,Category("Bindings")] public Guid PlayerObjectId{get;set;}
 [Category("Enemies")] public AssetReference EnemyBlueprint{get;set;}=AssetReference.Empty;
 [Category("Trigger")] public Vector3 TriggerSize{get;set;}=new(70,8,12);
 [Category("Enemies")] public int EnemyCount{get;set;}=50;
 [Category("Enemies")] public float SpawnInterval{get;set;}=.12f;
 [Category("Enemies")] public float MinimumSpawnRadius{get;set;}=9;
 [Category("Enemies")] public float MaximumSpawnRadius{get;set;}=18;
 [Category("Enemies")] public float DetectionRange{get;set;}=200;
 [Category("Live state")] public bool Triggered{get;private set;}
 [Category("Live state")] public int SpawnedCount{get;private set;}
 [Category("Live state")] public bool Spawning{get;private set;}
 [Category("Live state")] public string Status{get;private set;}="Waiting for a curious visitor.";
 Vector3 _centre;float _clock;int _goal;
 MiningOperation3D? Operation=>AttachedGameObject?.Scene?.FindGameObject(OperationObjectId)?.GetComponent<MiningOperation3D>();
 GameObject? Player=>AttachedGameObject?.Scene?.FindGameObject(PlayerObjectId);
 protected override void OnUpdate()=>Tick((float)Time.DeltaTime);
 protected override void OnStop()=>Spawning=false;
 public bool PlayerInside()
 {if(Player is not {} p||!Matrix4x4.Invert(Transform.WorldMatrix,out var inverse))return false;var at=Vector3.Abs(Vector3.Transform(p.Transform.WorldPosition,inverse));var half=Vector3.Abs(TriggerSize)*.5f;return at.X<=half.X&&at.Y<=half.Y&&at.Z<=half.Z;}
 public bool Activate()
 {
  if(Triggered||Player is not {} player||player.GetComponent<HealthComponent>() is not {IsDead:false}||Operation is not {} op||op.Phase==MiningPhase.Failed||EnemyBlueprint.IsEmpty||!RuntimeSpawnService.IsBlueprintSpawnerConfigured)return false;
  Triggered=true;Spawning=true;SpawnedCount=0;_clock=0;_goal=Math.Clamp(EnemyCount,1,100);_centre=player.Transform.WorldPosition;Status="You disturbed the nest.";return true;
 }
 public void Reset(){Spawning=false;Triggered=false;SpawnedCount=0;Status="Waiting for a curious visitor.";}
 public void Cancel(){Spawning=false;Status="Ambush stopped.";}
 public void Tick(float delta)
 {
  if(!float.IsFinite(delta)||delta<=0||AttachedGameObject?.Scene is not {} scene)return;
  if(!Triggered&&PlayerInside())Activate();if(!Spawning)return;
  if(Player?.GetComponent<HealthComponent>() is not {IsDead:false}||Operation is not {} op||op.Phase==MiningPhase.Failed){Cancel();return;}
  _clock-=Math.Min(delta,.25f);if(_clock>0)return;
  var region=scene.GameObjects.SelectMany(o=>o.Components.OfType<NavigationRegion3D>()).FirstOrDefault(r=>r.Ready);
  Vector3? chosen=null;float min=Math.Clamp(MinimumSpawnRadius,2,100),max=Math.Clamp(MaximumSpawnRadius,min,100);
  for(int attempt=0;attempt<32;attempt++)
  {
   float angle=(SpawnedCount*2.399963f+attempt*.73f);float radius=min+(max-min)*((SpawnedCount*17+attempt*7)%31)/30f;var p=_centre+new Vector3(MathF.Cos(angle)*radius,0,MathF.Sin(angle)*radius);p.Y=0;
   if(region?.Grid is {} grid){int x=(int)MathF.Floor((p.X-grid.Origin.X)/grid.CellSize),z=(int)MathF.Floor((p.Z-grid.Origin.Y)/grid.CellSize);if(!grid.IsWalkable(x,z))continue;p.Y=grid.HeightAt(x,z,0);}
   if(region!=null&&region.FindPath(p,Player.Transform.WorldPosition,20000).Count==0)continue;chosen=p;break;
  }
  if(chosen==null){Status="Waiting for a reachable spawn position.";_clock=.25f;return;}
  var enemy=RuntimeSpawnService.SpawnBlueprint(scene,EnemyBlueprint,chosen.Value);
  if(enemy?.GetComponent<AIBehavior3D>() is not {} ai||enemy.GetComponent<SimpleEnemyAI3D>() is not {} combat||enemy.GetComponent<HealthComponent>()==null)
  {if(enemy!=null)scene.DestroyGameObject(enemy);Status="Enemy blueprint needs health, combat and AI behaviour.";Spawning=false;return;}
  enemy.Name="Curiosity spider "+(SpawnedCount+1);ai.TargetObjectId=PlayerObjectId;ai.FindNearestCharacter=false;ai.DetectionRange=Math.Max(20,DetectionRange);combat.TargetId=PlayerObjectId;
  if(enemy.GetComponent<NavigationAgent3D>() is {} nav)nav.MaximumVisited=20000;
  op.TrackRoamingEnemy(enemy);SpawnedCount++;_clock=Math.Max(.03f,SpawnInterval);
  if(SpawnedCount>=_goal){Spawning=false;Status=$"Nest disturbed: {SpawnedCount} spiders released.";}
 }
}
