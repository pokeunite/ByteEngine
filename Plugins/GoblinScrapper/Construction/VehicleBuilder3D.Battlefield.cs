using System.Numerics;
using System.Text.Json;
using ByteEngine.Core;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;
namespace GoblinScrapper.Construction;
public sealed partial class VehicleBuilder3D
{
 public bool BattlefieldEnabled {get;set;}
 public int BattlefieldEnemyCount {get;set;}=18;
 public int BattleKills {get;private set;}
 public int BattleSevered {get;private set;}
 public bool BattleWon {get;private set;}
 public bool BattleLost {get;private set;}
 public bool BattleRunning=>_battleActive;
 private bool _battleActive;private float _battleTime,_battleHull=100;
 private UiText? _battleText;private readonly List<BattleGoblin> _goblins=new();
 private readonly List<(int Id,GameObject[] Parts,float Age)> _battleCorpses=new();
 private sealed record ChunkDefinition(string Name,string Bone,Vector3 Centre,Vector3 Size,Vector3 Pivot);
 private ChunkDefinition[] _chunkDefinitions=[];
 private sealed class BattleGoblin{public required int Id;public required GameObject Visual;public required SkeletalMeshRenderer Rig;public float AttackCooldown;public bool Dead;}
 private void StartBattlefield()
 {
  if(!BattlefieldEnabled)return;
  var json=JsonDocument.Parse(File.ReadAllText(Path.Combine(ProjectRoot,"Assets/Battlefield/goblin-parts.json")));
  Vector3 V(JsonElement e)=>new(e[0].GetSingle(),e[1].GetSingle(),e[2].GetSingle());
  _chunkDefinitions=json.RootElement.EnumerateArray().Select(e=>new ChunkDefinition(e.GetProperty("name").GetString()!,e.GetProperty("bone").GetString()!,V(e.GetProperty("centre")),V(e.GetProperty("size")),V(e.GetProperty("pivot")))).ToArray();json.Dispose();
  _battleText=Text("Battle objective","BUILD A MACHINE / B TO DEPLOY",new(18,64),22);_battleText.Color=new(.95f,.9f,.65f,1);
  foreach(int side in new[]{-1,1}){
   Box("Battlefield boundary",new(side*28,1,-10),new(1,2,40),new(.23f,.25f,.20f,1));
   for(int i=0;i<3;i++){
    Box("Red camp post",new(side*(5+i*3),1.2f,-23),new(.16f,2.4f,.16f),new(.22f,.15f,.08f,1));
    Box("Red faction pennant",new(side*(5+i*3)+.4f,1.9f,-23),new(.8f,.55f,.04f),new(.62f,.055f,.025f,1));
   }
  }
  Box("Battlefield far boundary",new(0,1,-29),new(56,2,1),new(.23f,.25f,.20f,1));
  foreach(var p in new[]{new Vector3(-11,.5f,-11),new Vector3(11,.5f,-17)}){
   Box("Battlefield cover",p,new(3,1,2),new(.3f,.28f,.21f,1));_obstacles.Add((new(p.X,p.Z),new(1.5f,1)));
  }
  Box("Green deployment stripe",new(0,.007f,-4),new(12,.012f,.12f),new(.35f,.52f,.18f,1));
  Box("Red camp stripe",new(0,.007f,-21),new(20,.012f,.12f),new(.55f,.12f,.05f,1));
 }
 private void DeployBattlefield()
 {
  if(!BattlefieldEnabled||_contraption==null)return;
  ClearBattlefield();_battleActive=true;BattleWon=BattleLost=false;BattleKills=BattleSevered=0;_battleTime=0;_battleHull=100;
  _contraption.AddObstacle(new(-28,1,-10),new(1,2,40));_contraption.AddObstacle(new(28,1,-10),new(1,2,40));_contraption.AddObstacle(new(0,1,-29),new(56,2,1));
  int count=Math.Clamp(BattlefieldEnemyCount,1,24);
  for(int i=0;i<count;i++){
   var visual=CreateModelVisual(GameObject.Scene!,Assets!,"Assets/Battlefield/red-goblin.glb",null!,"Red goblin #"+i);
   visual.Transform.WorldPosition=new((i%6-2.5f)*2.4f,0,-9-(i/6)*4);var rig=visual.GetComponent<SkeletalMeshRenderer>()!;rig.ResolveRuntimeResources();rig.Play("Idle",true);
   _goblins.Add(new(){Id=i,Visual=visual,Rig=rig});
  }
  _message="Clear the red camp. B returns to workshop; your build is kept.";
 }
 private void ClearBattlefield()
 {
  foreach(var goblin in _goblins)if(goblin.Visual.Scene!=null)goblin.Visual.Scene.DestroyGameObject(goblin.Visual);_goblins.Clear();
  foreach(var corpse in _battleCorpses){_contraption?.RemoveGoblinRagdoll(corpse.Id);foreach(var part in corpse.Parts)if(part.Scene!=null)part.Scene.DestroyGameObject(part);}_battleCorpses.Clear();_battleActive=false;
 }
 private void KillBattleGoblin(BattleGoblin goblin,MachineStrike strike)
 {
  goblin.Dead=true;BattleKills++;var world=goblin.Visual.Transform.WorldMatrix;var chunks=new GoblinChunk[_chunkDefinitions.Length];var visuals=new GameObject[chunks.Length];
  for(int i=0;i<chunks.Length;i++){
   var d=_chunkDefinitions[i];var deformation=Matrix4x4.Identity;
   if(goblin.Rig.TryGetBoneModelMatrix(d.Bone,out var current)&&goblin.Rig.ResolvedModel?.Skeleton?.Bones.FirstOrDefault(b=>b.Name==d.Bone) is {} bone)deformation=bone.BindPose*current;
   var matrix=deformation*world;Matrix4x4.Decompose(matrix,out _,out var rotation,out _);
   chunks[i]=new(d.Name,Vector3.Transform(d.Centre,matrix),d.Size,Vector3.Transform(d.Pivot,matrix),rotation);
   visuals[i]=CreateModelVisual(GameObject.Scene!,Assets!,"Assets/Battlefield/goblin-"+d.Name+".glb",null!,"Ragdoll "+goblin.Id+" "+d.Name);
  }
  int sever=-1;if(strike.Sharp||strike.Velocity.Length()>7){sever=Enumerable.Range(1,chunks.Length-1).OrderBy(i=>Vector3.DistanceSquared(chunks[i].Centre,strike.Point)).First();BattleSevered++;}
  _contraption!.SpawnGoblinRagdoll(goblin.Id,chunks,strike.Velocity,sever);goblin.Visual.Active=false;_battleCorpses.Add((goblin.Id,visuals,0));
  while(_battleCorpses.Count>10){var oldest=_battleCorpses[0];_contraption.RemoveGoblinRagdoll(oldest.Id);foreach(var v in oldest.Parts)GameObject.Scene!.DestroyGameObject(v);_battleCorpses.RemoveAt(0);}
  if(BattleKills==_goblins.Count)BattleWon=true;
 }
 private void TickBattlefield(float dt)
 {
  if(_battleText!=null){_battleText.GameObject.Active=BattlefieldEnabled;_battleText.Text=Building?"BUILD A MACHINE / B TO DEPLOY":BattleWon?$"RED CAMP CLEARED  /  {BattleKills} KILLS  /  B: REBUILD AND RETRY":BattleLost?"MACHINE OVERRUN / B: RETURN AND REBUILD":$"RED CAMP  {BattleKills}/{_goblins.Count}   HULL {(int)_battleHull}%   {Math.Max(0,120-(int)_battleTime)}s";}
  if(!_battleActive||Building||_contraption==null)return;
  if(!BattleWon&&!BattleLost){_battleTime+=dt;if(_battleTime>=120||_battleHull<=0)BattleLost=true;}
  var player=_contraption.Pose(0).Position;
  foreach(var goblin in _goblins.Where(g=>!g.Dead)){
   var position=goblin.Visual.Transform.WorldPosition;
   if(!BattleLost&&(_contraption.StrikeGoblin(position,dt)??_contraption.ProjectileStrikeGoblin(position,dt)) is {} hit){KillBattleGoblin(goblin,hit);continue;}
   if(BattleWon||BattleLost)continue;
   var delta=player-position;delta.Y=0;float distance=delta.Length();goblin.AttackCooldown-=dt;
   if(distance<16&&distance>1.8f){var direction=delta/distance;position+=direction*Math.Min(1.5f*dt,Math.Max(0,distance-1.8f));goblin.Visual.Transform.WorldPosition=position;goblin.Visual.Transform.WorldRotation=Quaternion.CreateFromAxisAngle(Vector3.UnitY,MathF.Atan2(direction.X,direction.Z));if(goblin.Rig.CurrentAnimation!="Walk")goblin.Rig.Play("Walk",true);}
   else if(distance<=1.8f&&goblin.AttackCooldown<=0){goblin.Rig.Play("Attack",false);goblin.AttackCooldown=1.5f;_battleHull=Math.Max(0,_battleHull-5);}
   else if(distance>=16&&goblin.Rig.CurrentAnimation!="Idle")goblin.Rig.Play("Idle",true);
  }
  for(int i=_battleCorpses.Count-1;i>=0;i--){var corpse=_battleCorpses[i];for(int part=0;part<corpse.Parts.Length;part++){var pose=_contraption.GoblinChunkPose(corpse.Id,part);corpse.Parts[part].Transform.WorldPosition=pose.Position;corpse.Parts[part].Transform.WorldRotation=pose.Rotation;}
   corpse.Age+=dt;if(corpse.Age>12){_contraption.RemoveGoblinRagdoll(corpse.Id);foreach(var v in corpse.Parts)GameObject.Scene!.DestroyGameObject(v);_battleCorpses.RemoveAt(i);}else _battleCorpses[i]=corpse;
  }
 }
 public void RestartBattle(){if(!Building){ReturnToBuild();BeginDriving();}}
}
