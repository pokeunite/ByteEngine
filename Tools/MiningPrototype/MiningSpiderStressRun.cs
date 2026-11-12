using ByteEngine.Core.Graphics;
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
 ReplayCrowd? replay;
 Func<int,Vector3,GameObject>? spawn;
 BinaryReader? reader;
 BinaryWriter? writer;
 readonly Stack<GameObject>[] reserves = [new(), new()];
 bool pooled, hybrid, liveProbe;
 HealthComponent? probeTarget;
 double maxPromotionFrameMs;
 bool lastFramePromoted;
 readonly bool trace = Environment.GetEnvironmentVariable("BYTEENGINE_TRACE_ACTIVATION")=="1";
 readonly List<object> activationTrace = new();
 long frameStart, activationTicks;
 double replayMs;
 int framePromotions;
 TimeSpan previousPause,previousRegistry;

 int returned;
 void Park(int i,GameObject actor)
 {
  actor.Active=false;
  reserves[ReplayCrowd.Kind(i)].Push(actor);
 }
 GameObject Rent(int i,Vector3 position,float health)
 {
  var actor=reserves[ReplayCrowd.Kind(i)].Pop();
  actor.Transform.WorldPosition=position;
  actor.GetComponent<HealthComponent>()!.Revive(health);
  long start=System.Diagnostics.Stopwatch.GetTimestamp();
  actor.Active=true;
  activationTicks+=System.Diagnostics.Stopwatch.GetTimestamp()-start;
  return actor;
 }
 double spawnMs, peakPromotionMs;
 int promotions;
 readonly bool capturePasses = Environment.GetEnvironmentVariable("BYTEENGINE_STRESS_PASSES") == "1";
 readonly List<RenderPassMeasurement> passSamples = new();
 readonly List<double> frames=[],render=[],physics=[],updates=[],animations=[];readonly Scene scene;readonly RenderWorld? renderWorld;int tick,attachmentCount;long allocated;
 public MiningSpiderStressRun(Scene scene,MiningOperation3D op,bool optimized,int requestedCount=120,RenderWorld? world=null,bool profiling=true,bool batchCrowds=true,string mode="baseline")
 {
  if(capturePasses) foreach(var sky in scene.GetComponentsSnapshot<SkyEnvironment>()) sky.ProfileGraphicsGpu=true;
  this.scene=scene;renderWorld=world;if(world!=null)world.AdaptiveOcclusion=mode=="adaptive-open";op.Enabled=false;var player=scene.FindGameObject(op.PlayerObjectId)!;
  player.GetComponent<HealthComponent>()!.MaxHealth=10000000;player.GetComponent<HealthComponent>()!.Revive();player.GetComponent<PlayerController3D>()!.Enabled=false;player.GetComponent<CameraBoom3D>()!.Enabled=false;player.GetComponent<MiningThirdPerson3D>()!.Enabled=false;
  player.Transform.WorldPosition=new(0,0,4);var cam=scene.ActiveCamera!;cam.GameObject.SetParent(null,true);cam.Transform.WorldPosition=new(0,16,20);Matrix4x4.Invert(Matrix4x4.CreateLookAt(cam.Transform.WorldPosition,new(0,0,-22),Vector3.UnitY),out var pose);cam.Transform.WorldRotation=Quaternion.CreateFromRotationMatrix(pose);cam.FieldOfView=65;
  var spawnTimer=System.Diagnostics.Stopwatch.StartNew();
  bool recording=mode=="record-paths";
  pooled=mode=="replay-pool";hybrid=pooled||mode=="replay-hybrid";liveProbe=mode is "live-reuse" or "live-fresh";
  bool comparing=hybrid||mode=="replay-full";
  if(recording||comparing){
   replay=scene.CreateGameObject("Crowd representation benchmark").AddComponent(new ReplayCrowd());
   replay.Actors=new GameObject?[requestedCount];replay.Positions=new Vector3[requestedCount];replay.Health=new float[requestedCount];replay.Targets=Enumerable.Repeat(player.Id,requestedCount).ToArray();
   if(recording)writer=new BinaryWriter(File.Create("paths.bin"));else reader=new BinaryReader(File.OpenRead("paths.bin"));
  }
  spawn=(i,pos)=>{
   var enemy=RuntimeSpawnService.SpawnBlueprint(scene,i%3==0?op.RaiderBlueprint:op.HunterBlueprint,pos)??throw new Exception("Stress spawn failed");
   op.ConfigureEnemy(enemy,i%3!=0);
   var ai=enemy.GetComponent<AIBehavior3D>()!;ai.TargetObjectId=replay?.Targets[i]??player.Id;if(optimized)ai.DecisionInterval=.25f;
   var simple=enemy.GetComponent<SimpleEnemyAI3D>()!;simple.TargetId=ai.TargetObjectId;
   if(comparing){ai.Enabled=false;simple.Enabled=false;if(enemy.GetComponent<ByteEngine.Core.Navigation.NavigationAgent3D>() is {} nav)nav.Enabled=false;}
   return enemy;
  };
  float[] hpByKind=new float[2];
  for(int i=0;i<requestedCount;i++){
   var pos=new Vector3((i%12-5.5f)*2.2f,0,-10-i/12*3.2f);
   if(replay!=null)replay.Positions[i]=pos;
   if(!hybrid||i<2||Vector3.DistanceSquared(pos,player.Transform.WorldPosition)<225){
    var actor=spawn(liveProbe?1:i,pos);
    if(replay!=null){replay.Actors[i]=actor;hpByKind[ReplayCrowd.Kind(i)]=actor.GetComponent<HealthComponent>()!.CurrentHealth;if(hybrid)replay.Capture(ReplayCrowd.Kind(i),actor);}
   }
   if(replay!=null)replay.Health[i]=hpByKind[ReplayCrowd.Kind(i)];
  }
  if(pooled){
   for(int i=0;i<requestedCount;i++)
    if(replay!.Actors[i]==null)Park(i,spawn(i,new(0,0,-100)));
  }
  if(liveProbe){
   var actor=scene.GetComponentsSnapshot<MiningSpiderAnimation3D>().Single().GameObject;
   var hp=actor.GetComponent<HealthComponent>()!;
   if(mode=="live-reuse"){Park(1,actor);actor=Rent(1,player.Transform.WorldPosition+new Vector3(0,0,-1),hp.MaxHealth);}
   else actor.Transform.WorldPosition=player.Transform.WorldPosition+new Vector3(0,0,-1);
   float initial=hp.CurrentHealth;hp.Damage(1);if(hp.CurrentHealth>=initial)throw new Exception("Damage failed");
   probeTarget=player.GetComponent<HealthComponent>();
  }
  spawnMs=spawnTimer.Elapsed.TotalMilliseconds;
  foreach(var enemy in scene.GameObjects.Where(o=>o.GetComponent<MiningSpiderAnimation3D>()!=null).ToArray())
  {
   var renderer=enemy.GetComponent<MiningSpiderAnimation3D>()!.Renderer!;renderer.DistantCrowdGeometry=mode is "geometry-lod" or "shadow-lod" or "crowd-no-shadow" or "adaptive-open";renderer.DistantCrowdShadows=mode is "shadow-lod" or "adaptive-open";renderer.CastShadows=mode!="crowd-no-shadow";renderer.CrowdSamplingLod=mode=="crowd-lod";renderer.BatchCrowdSubmeshes=batchCrowds;var model=renderer.ResolvedModel!;const string socket="BenchmarkAttachment";
   if(model.FindSocket(socket)==null)model.ReplaceSockets(model.Sockets.Append(new SkeletalSocketDefinition{Name=socket,BoneName=model.Skeleton!.Bones[Math.Min(1,model.Skeleton.Bones.Count-1)].Name}));
   if(comparing||recording)continue;
   var prop=scene.CreateGameObject("Benchmark skeletal attachment");prop.Transform.LocalScale=new(.08f);prop.AddComponent(new MeshRenderer{UsePrimitive=true,CastShadows=false});
   if(!SkeletalAttachmentService.AttachToSocket(prop,renderer.GameObject,socket))throw new Exception("Benchmark socket attachment failed");attachmentCount++;
  }
  foreach(var o in scene.GameObjects.ToArray()){
   if(mode=="no-ai"){if(o.GetComponent<AIBehavior3D>() is {} ai)ai.Enabled=false;if(o.GetComponent<SimpleEnemyAI3D>() is {} simple)simple.Enabled=false;if(o.GetComponent<ByteEngine.Core.Navigation.NavigationAgent3D>() is {} nav)nav.Enabled=false;}
   if(mode=="frozen"){if(o.GetComponent<MiningSpiderAnimation3D>() is {} driver)driver.Enabled=false;if(o.GetComponent<SkeletalMeshRenderer>() is {} skin)skin.Pause();}
   if(mode=="no-shadows"){if(o.GetComponent<DirectionalLight>() is {} sun)sun.CastShadows=false;if(o.GetComponent<PointLight>() is {} lamp)lamp.CastShadows=false;}
   if(mode=="hidden-spiders"&&o.GetComponent<MiningSpiderAnimation3D>()?.Renderer is {} sk)sk.Visible=false;
  }
  scene.ComponentProfiler.Enabled=profiling;scene.ComponentProfiler.AccumulateFrames=true;Console.WriteLine($"STRESS: {requestedCount} fully animated, navigating spiders with socket attachments in the real scene; 1280x720, VSync off.");
 }
 public bool Tick(double renderMs,string suffix)
 {
  if(trace){
   long now=System.Diagnostics.Stopwatch.GetTimestamp();
   var pause=GC.GetTotalPauseDuration();
   var registry=scene.PhysicsRegistryRebuildTime;
   if(frameStart!=0)activationTrace.Add(new {tick,framePromotions,wallMs=System.Diagnostics.Stopwatch.GetElapsedTime(frameStart,now).TotalMilliseconds,
    replayMs,activeSetterMs=activationTicks*1000d/System.Diagnostics.Stopwatch.Frequency,
    renderMs,swapMs=ByteEngineApplication.LastSwapMilliseconds,scene.LastFrameMetrics,
    registryMs=(registry-previousRegistry).TotalMilliseconds,scene.Physics.LastColliderGatherMs,
    gcPauseMs=(pause-previousPause).TotalMilliseconds});
   previousRegistry=registry;previousPause=pause;frameStart=now;activationTicks=0;
  }
  if(lastFramePromoted)maxPromotionFrameMs=Math.Max(maxPromotionFrameMs,Time.UnscaledDeltaTime*1000);
  if(liveProbe && probeTarget!.CurrentHealth<probeTarget.MaxHealth){
   Console.WriteLine($"LIVE PASS target damaged by frame {tick}, HP {probeTarget.CurrentHealth}");return true;
  }
  if(replay!=null){
   var target=scene.FindGameObject(replay.Targets[0])!.Transform.WorldPosition;
   var promotionTimer=System.Diagnostics.Stopwatch.StartNew();int before=promotions;
   for(int i=0;i<replay.Actors.Length;i++){
    if(writer!=null){var pos=replay.Actors[i]!.Transform.WorldPosition;writer.Write(pos.X);writer.Write(pos.Y);writer.Write(pos.Z);continue;}
    var point=new Vector3(reader!.ReadSingle(),reader.ReadSingle(),reader.ReadSingle());replay.Positions[i]=point;
    if(pooled && replay.Actors[i] is {} retiring && Vector3.DistanceSquared(point,target)>441){
     replay.Health[i]=retiring.GetComponent<HealthComponent>()!.CurrentHealth;
     Park(i,retiring);replay.Actors[i]=null;returned++;
    }
    if(hybrid&&replay.Actors[i]==null&&Vector3.DistanceSquared(point,target)<225){
     var actor=pooled?Rent(i,point,replay.Health[i]):spawn!(i,point);actor.GetComponent<HealthComponent>()!.CurrentHealth=replay.Health[i];replay.Actors[i]=actor;promotions++;
    }
    if(replay.Actors[i] is {} active)active.Transform.WorldPosition=point;
   }
   replayMs=promotionTimer.Elapsed.TotalMilliseconds;framePromotions=promotions-before;
   lastFramePromoted=before!=promotions;
   if(before!=promotions)peakPromotionMs=Math.Max(peakPromotionMs,promotionTimer.Elapsed.TotalMilliseconds);
  }
  if(++tick==61){scene.ComponentProfiler.ClearSamples();allocated=GC.GetTotalAllocatedBytes();}
  if(capturePasses && tick>60) passSamples.AddRange(GraphicsDiagnostics.Passes);
  if(tick>60){frames.Add(Time.UnscaledDeltaTime*1000);render.Add(renderMs);physics.Add(scene.LastFrameMetrics.PhysicsMs);updates.Add(scene.LastFrameMetrics.UpdateMs);animations.Add(scene.LastFrameMetrics.AnimationMs);}
  if(tick<240)return false;
  if(liveProbe)throw new Exception("Enemy never damaged target");
  writer?.Dispose();reader?.Dispose();
  var sorted=frames.Order().ToArray();int count=scene.GameObjects.SelectMany(o=>o.Components).OfType<MiningSpiderAnimation3D>().Count();
  var result=new{activationTrace,logicalCount=replay?.Actors.Length??count,spawnMs,promotions,returned,maxPromotionFrameMs,peakPromotionMs,passSamples,crowdBatches=scene.GameObjects.SelectMany(o=>o.Components.OfType<SkeletalMeshRenderer>()).Count(r=>r.BatchCrowdSubmeshes),cachedQueries=scene.Physics.UseCachedQueryIndex,parallelCulling=JobRunner.ParallelExecutionEnabled,profiling=scene.ComponentProfiler.Enabled,spiders=count,attachments=attachmentCount,width=1280,height=720,frames=frames.Count,meanMs=frames.Average(),fps=1000/frames.Average(),medianMs=sorted[sorted.Length/2],p95Ms=sorted[(int)(sorted.Length*.95)],renderMeanMs=render.Average(),physicsMeanMs=physics.Average(),updateMeanMs=updates.Average(),allocatedBytes=GC.GetTotalAllocatedBytes()-allocated,queryCalls=scene.Physics.QueryCalls,queryIndexRefreshes=scene.Physics.QueryIndexRefreshes,queryCacheHits=scene.Physics.QueryIndexCacheHits,physicsGatherMs=scene.Physics.LastColliderGatherMs,physicsIndexMs=scene.Physics.LastIndexUpdateMs,physicsCandidates=scene.Physics.LastCandidateCount,animationMeanMs=animations.Average(),animationMs=scene.LastFrameMetrics.AnimationMs,animatedComponents=scene.LastFrameMetrics.AnimatedComponents,renderStats=renderWorld?.LastStats,mainTriangles=renderWorld?.MainTriangles,instancedObjects=renderWorld?.InstancedObjects,occlusionMs=renderWorld?.OcclusionMilliseconds,occludedMeshes=renderWorld?.OccludedMeshes,gpu=GL.GetString(StringName.Renderer),components=scene.ComponentProfiler.Snapshot(),systems=scene.ComponentProfiler.SystemSnapshot()};
  Directory.CreateDirectory("artifacts");File.WriteAllText("artifacts/spider-stress-"+count+"-"+suffix+(scene.ComponentProfiler.Enabled?"":"-unprofiled")+(JobRunner.ParallelExecutionEnabled?"":"-serial")+".json",JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine($"STRESS RESULT {result.fps:0.0} FPS / {result.meanMs:0.0} ms / p95 {result.p95Ms:0.0} ms / {count} spiders");return true;
 }
}



