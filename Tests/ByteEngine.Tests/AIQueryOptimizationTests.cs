using System.Numerics;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Physics;
using ByteEngine.Core.AI;
namespace ByteEngine.Tests;
internal static class AIQueryOptimizationTests
{
 public static void Run()
 {
  void Check(bool value,string message){if(!value)throw new Exception(message);Console.WriteLine("PASS AI QUERY "+message);}
  var scene=new Scene("query revisions");var parent=scene.CreateGameObject("parent");var obj=scene.CreateGameObject("box");obj.SetParent(parent,false);var box=obj.AddComponent(new BoxCollider3D());
  bool Near()=>scene.Physics.QueryBounds(scene,new(-2),new(2)).Contains(obj);
  Check(Near(),"initial collider is indexed");long refresh=scene.Physics.QueryIndexRefreshes;
  for(int i=0;i<100;i++)Check(Near(),"unchanged query retains collider");
  Check(scene.Physics.QueryIndexRefreshes==refresh&&scene.Physics.QueryIndexCacheHits>=100,"unchanged queries reuse index without gathering colliders");
  long full=scene.Physics.QueryFullIndexRefreshes;
  for(int i=0;i<80;i++){parent.Transform.LocalPosition=new(i%2==0?20:0,0,0);Check(Near()==(i%2!=0),"incremental parent move query remains current");}
  Check(scene.Physics.QueryFullIndexRefreshes==full&&scene.Physics.QueryIncrementalBoundsUpdates>=80,"moving collider refits its leaves without regathering scenery");
  var comparison=new Scene("incremental query comparison");var objects=new List<GameObject>();var rng=new Random(4021);
  for(int i=0;i<160;i++){var item=comparison.CreateGameObject("Query box "+i);item.Transform.LocalPosition=new(rng.Next(-40,40),0,rng.Next(-40,40));item.AddComponent(new BoxCollider3D());objects.Add(item);}
  var reference=new PhysicsWorld3D{UseCachedQueryIndex=false};
  for(int step=0;step<30;step++){
   for(int i=0;i<20;i++)objects[i].Transform.LocalPosition+=new Vector3((float)rng.NextDouble()-.5f,0,(float)rng.NextDouble()-.5f)*8;
   var min=new Vector3(rng.Next(-30,10),-2,rng.Next(-30,10));var max=min+new Vector3(25,4,25);
   var actual=comparison.Physics.QueryBounds(comparison,min,max).Select(o=>o.Id).ToHashSet();var expected=reference.QueryBounds(comparison,min,max).Select(o=>o.Id).ToHashSet();
   Check(actual.SetEquals(expected),"incremental multi-object motion matches a fresh full index");
  }
  parent.Transform.LocalPosition=new(20,0,0);Check(!Near(),"parent movement immediately removes stale bounds");
  parent.Transform.LocalPosition=Vector3.Zero;Check(Near(),"parent teleport immediately updates descendants");
  box.Enabled=false;Check(!Near(),"disabled collider disappears immediately");box.Enabled=true;Check(Near(),"enabled collider returns immediately");
  parent.Active=false;Check(!Near(),"inactive parent removes collider");parent.Active=true;Check(Near(),"reactivated parent returns collider");
  obj.Transform.LocalPosition=new(4,0,0);Check(!Near(),"separated box excluded");box.Size=new(10);Check(Near(),"resized collider enters query immediately");box.Size=Vector3.One;box.Center=new(-4,0,0);Check(Near(),"center edit updates bounds immediately");
  var snapshot=scene.Physics.QueryBounds(scene,new(-10),new(10));scene.Physics.QueryBounds(scene,new(500),new(501));Check(snapshot.Contains(obj),"nested query does not overwrite caller snapshot");
  var other=scene.CreateGameObject("other");other.Transform.LocalPosition=new(100,0,0);var before=scene.Physics.QueryIndexRefreshes;other.AddComponent(new BoxCollider3D());scene.Physics.QueryBounds(scene,new(99,-2,-2),new(101,2,2));Check(scene.Physics.QueryIndexRefreshes>before,"adding collider invalidates membership");
  scene.DestroyGameObject(obj);Check(!scene.Physics.QueryBounds(scene,new(-10),new(10)).Contains(obj),"destroyed collider does not remain indexed");
  var meshObject=scene.CreateGameObject("mesh");var mesh=meshObject.AddComponent(new MeshCollider3D{Vertices=[new(-1,0,-1),new(1,0,-1),new(0,0,1)],Triangles=[0,1,2]});
  Check(scene.Physics.QueryBounds(scene,new(-2),new(2)).Contains(meshObject),"custom mesh enters index");long customStable=scene.Physics.QueryIndexRefreshes;scene.Physics.QueryBounds(scene,new(-2),new(2));Check(scene.Physics.QueryIndexRefreshes==customStable,"unchanged custom geometry does not force full collider gather");
  mesh.Vertices=[new(100,0,0),new(102,0,0),new(100,0,2)];Check(!scene.Physics.QueryBounds(scene,new(-2),new(2)).Contains(meshObject),"custom geometry edit immediately refreshes bounds");
  // Unrelated visual motion must not force a collider gather.
  var visual=scene.CreateGameObject("visual");scene.Physics.QueryBounds(scene,new(-2),new(2));long stable=scene.Physics.QueryIndexRefreshes;visual.Transform.LocalPosition=new(1,2,3);scene.Physics.QueryBounds(scene,new(-2),new(2));Check(scene.Physics.QueryIndexRefreshes==stable,"visual-only motion retains physics index");
  var unrelated=visual.AddComponent(new ByteEngine.Core.Graphics.ThreeD.MeshRenderer());unrelated.Enabled=false;unrelated.Enabled=true;visual.Active=false;visual.Active=true;visual.RemoveComponent(unrelated);visual.SetParent(parent,false);scene.DestroyGameObject(visual);
  var freshVisual=scene.CreateGameObject("fresh visual");freshVisual.AddComponent(new ByteEngine.Core.Graphics.UiWidget());scene.Physics.QueryBounds(scene,new(-2),new(2));
  Check(scene.Physics.QueryIndexRefreshes==stable,"visual/UI membership, enabled, active, parenting and destruction retain physics index");
  var newCollider=freshVisual.AddComponent(new BoxCollider3D());Check(scene.Physics.QueryBounds(scene,new(-2),new(2)).Contains(freshVisual),"adding collider after visual-only changes refreshes membership");
  freshVisual.Active=false;Check(!scene.Physics.QueryBounds(scene,new(-2),new(2)).Contains(freshVisual),"new collider active state invalidates immediately");freshVisual.Active=true;
  Check(scene.Physics.QueryBounds(scene,new(-2),new(2)).Contains(freshVisual),"new collider reactivation returns immediately");freshVisual.RemoveComponent(newCollider);Check(!scene.Physics.QueryBounds(scene,new(-2),new(2)).Contains(freshVisual),"removing collider refreshes membership");
  var perception=new Scene("perception");var actor=perception.CreateGameObject("listener");var ai=actor.AddComponent(new AIBehavior3D{PerceptionMemory=true,FindNearestCharacter=false,DetectionRange=10,HearingRange=10});ai.SetDefinition(new(){Root=new(){Kind=BehaviorNodeKind.Wait,Duration=10}});
  AIWorld.ReportNoise(perception,new(1000,0,0),1000);ai.Tick(.11f);Check(perception.Physics.QueryCalls==0,"out-of-hearing-range noise is rejected before occlusion raycast");
  ai.PerceptionMemory=false;ai.FindNearestCharacter=true;var target=perception.CreateGameObject("target");var controller=target.AddComponent(new CharacterController3D());target.Transform.LocalPosition=new(0,0,-2);ai.Tick(.11f);Check(ai.Target==target,"new character enters cached target registry");controller.Enabled=false;ai.Tick(.11f);Check(ai.Target==null,"disabled character is not acquired");controller.Enabled=true;target.Active=false;ai.Tick(.11f);Check(ai.Target==null,"inactive character is not acquired");target.Active=true;ai.Tick(.11f);Check(ai.Target==target,"reactivated character can be acquired");target.RemoveComponent(controller);ai.Tick(.11f);Check(ai.Target==null,"removed character leaves cached target registry");
  scene.Physics.Reset();Check(scene.Physics.QueryCalls==0,"reset clears query metrics and indexes");
  DecisionPhases(Check);
 }
 static void DecisionPhases(Action<bool,string> check)
 {
  (int Max,long Total) Run(bool stagger)
  {
   var scene=new Scene("Decision phases");var agents=new List<AIBehavior3D>();
   for(int i=1;i<=100;i++)
   {
    var id=new Guid(i*65536/101,0,0,new byte[8]);var actor=new GameObject(id,"Scheduled actor");scene.AddGameObject(actor);
    var ai=actor.AddComponent(new AIBehavior3D{FindNearestCharacter=false,DecisionInterval=.16f,StaggerDecisions=stagger});ai.SetDefinition(new(){Root=new(){Kind=BehaviorNodeKind.Signal,Key="tick"}});agents.Add(ai);
   }
   int max=0;for(int frame=0;frame<160;frame++){int calls=0;foreach(var ai in agents){long before=ai.SignalSequence;ai.Tick(.01f);calls+=(int)(ai.SignalSequence-before);}max=Math.Max(max,calls);}
   long total=agents.Sum(ai=>ai.SignalSequence);
   foreach(var ai in agents){long before=ai.SignalSequence;ai.SetMemory("Wake",ByteEngine.Core.Variables.VariableValue.FromBoolean(true));ai.Tick(.001f);check(ai.SignalSequence==before+1,"explicit memory decision bypasses phase waiting");}
   return(max,total);
  }
  var spread=Run(true);var together=Run(false);
  check(spread.Total==together.Total&&spread.Total==1000,"staggered decisions preserve per-agent decision frequency");
  check(spread.Max<=10&&together.Max==100,"100 agents spread across frames instead of one synchronized decision spike");
 }
}
