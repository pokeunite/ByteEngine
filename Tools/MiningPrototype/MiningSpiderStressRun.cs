using System.Numerics;
using System.Text.Json;
using ByteEngine.Core;
using ByteEngine.Core.AI;
using ByteEngine.Core.Animation;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Runtime;
using ByteEngine.Core.Scene;
using ByteEngine.Core.VisualLogic;
using KarlsonDemo.Plugin;
using OpenTK.Graphics.OpenGL4;
internal sealed class MiningSpiderStressRun
{
 readonly List<double> frames=[],render=[],physics=[],updates=[];readonly Scene scene;readonly RenderWorld? renderWorld;int tick,attachmentCount;long allocated;
 public MiningSpiderStressRun(Scene scene,MiningOperation3D op,bool optimized,int requestedCount=120,RenderWorld? world=null,bool profiling=true,bool batchCrowds=true)
 {
  this.scene=scene;renderWorld=world;op.Enabled=false;var player=scene.FindGameObject(op.PlayerObjectId)!;
  player.GetComponent<HealthComponent>()!.MaxHealth=10000000;player.GetComponent<HealthComponent>()!.Revive();player.GetComponent<PlayerController3D>()!.Enabled=false;player.GetComponent<CameraBoom3D>()!.Enabled=false;player.GetComponent<MiningThirdPerson3D>()!.Enabled=false;
  player.Transform.WorldPosition=new(0,0,4);var cam=scene.ActiveCamera!;cam.GameObject.SetParent(null,true);cam.Transform.WorldPosition=new(0,16,20);Matrix4x4.Invert(Matrix4x4.CreateLookAt(cam.Transform.WorldPosition,new(0,0,-22),Vector3.UnitY),out var pose);cam.Transform.WorldRotation=Quaternion.CreateFromRotationMatrix(pose);cam.FieldOfView=65;
  for(int i=0;i<requestedCount;i++){var enemy=RuntimeSpawnService.SpawnBlueprint(scene,i%3==0?op.RaiderBlueprint:op.HunterBlueprint,new((i%12-5.5f)*2.2f,0,-10-i/12*3.2f))??throw new Exception("Stress spawn failed");op.ConfigureEnemy(enemy,i%3!=0);var ai=enemy.GetComponent<AIBehavior3D>()!;ai.TargetObjectId=player.Id;if(optimized)ai.DecisionInterval=.25f;enemy.GetComponent<SimpleEnemyAI3D>()!.TargetId=player.Id;}
  foreach(var enemy in scene.GameObjects.Where(o=>o.GetComponent<MiningSpiderAnimation3D>()!=null).ToArray())
  {
   var renderer=enemy.GetComponent<MiningSpiderAnimation3D>()!.Renderer!;renderer.BatchCrowdSubmeshes=batchCrowds;var model=renderer.ResolvedModel!;const string socket="BenchmarkAttachment";
   if(model.FindSocket(socket)==null)model.ReplaceSockets(model.Sockets.Append(new SkeletalSocketDefinition{Name=socket,BoneName=model.Skeleton!.Bones[Math.Min(1,model.Skeleton.Bones.Count-1)].Name}));
   var prop=scene.CreateGameObject("Benchmark skeletal attachment");prop.Transform.LocalScale=new(.08f);prop.AddComponent(new MeshRenderer{UsePrimitive=true,CastShadows=false});
   if(!SkeletalAttachmentService.AttachToSocket(prop,renderer.GameObject,socket))throw new Exception("Benchmark socket attachment failed");attachmentCount++;
  }
  scene.ComponentProfiler.Enabled=profiling;scene.ComponentProfiler.AccumulateFrames=true;Console.WriteLine($"STRESS: {requestedCount} fully animated, navigating spiders with socket attachments in the real scene; 1280x720, VSync off.");
 }
 public bool Tick(double renderMs,string suffix)
 {
  if(++tick==61){scene.ComponentProfiler.ClearSamples();allocated=GC.GetTotalAllocatedBytes();}
  if(tick>60){frames.Add(Time.UnscaledDeltaTime*1000);render.Add(renderMs);physics.Add(scene.LastFrameMetrics.PhysicsMs);updates.Add(scene.LastFrameMetrics.UpdateMs);}
  if(tick<240)return false;
  var sorted=frames.Order().ToArray();int count=scene.GameObjects.SelectMany(o=>o.Components).OfType<MiningSpiderAnimation3D>().Count();
  var result=new{crowdBatches=scene.GameObjects.SelectMany(o=>o.Components.OfType<SkeletalMeshRenderer>()).Count(r=>r.BatchCrowdSubmeshes),cachedQueries=scene.Physics.UseCachedQueryIndex,parallelCulling=JobRunner.ParallelExecutionEnabled,profiling=scene.ComponentProfiler.Enabled,spiders=count,attachments=attachmentCount,width=1280,height=720,frames=frames.Count,meanMs=frames.Average(),fps=1000/frames.Average(),medianMs=sorted[sorted.Length/2],p95Ms=sorted[(int)(sorted.Length*.95)],renderMeanMs=render.Average(),physicsMeanMs=physics.Average(),updateMeanMs=updates.Average(),allocatedBytes=GC.GetTotalAllocatedBytes()-allocated,queryCalls=scene.Physics.QueryCalls,queryIndexRefreshes=scene.Physics.QueryIndexRefreshes,queryCacheHits=scene.Physics.QueryIndexCacheHits,physicsGatherMs=scene.Physics.LastColliderGatherMs,physicsIndexMs=scene.Physics.LastIndexUpdateMs,physicsCandidates=scene.Physics.LastCandidateCount,animationMs=scene.LastFrameMetrics.AnimationMs,animatedComponents=scene.LastFrameMetrics.AnimatedComponents,renderStats=renderWorld?.LastStats,mainTriangles=renderWorld?.MainTriangles,instancedObjects=renderWorld?.InstancedObjects,occlusionMs=renderWorld?.OcclusionMilliseconds,occludedMeshes=renderWorld?.OccludedMeshes,gpu=GL.GetString(StringName.Renderer),components=scene.ComponentProfiler.Snapshot(),systems=scene.ComponentProfiler.SystemSnapshot()};
  Directory.CreateDirectory("artifacts");File.WriteAllText("artifacts/spider-stress-"+count+"-"+suffix+(scene.ComponentProfiler.Enabled?"":"-unprofiled")+(JobRunner.ParallelExecutionEnabled?"":"-serial")+".json",JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine($"STRESS RESULT {result.fps:0.0} FPS / {result.meanMs:0.0} ms / p95 {result.p95Ms:0.0} ms / {count} spiders");return true;
 }
}


