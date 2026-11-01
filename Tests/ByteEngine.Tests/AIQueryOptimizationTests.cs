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
  var perception=new Scene("perception");var actor=perception.CreateGameObject("listener");var ai=actor.AddComponent(new AIBehavior3D{PerceptionMemory=true,FindNearestCharacter=false,DetectionRange=10,HearingRange=10});ai.SetDefinition(new(){Root=new(){Kind=BehaviorNodeKind.Wait,Duration=10}});
  AIWorld.ReportNoise(perception,new(1000,0,0),1000);ai.Tick(.11f);Check(perception.Physics.QueryCalls==0,"out-of-hearing-range noise is rejected before occlusion raycast");
  ai.PerceptionMemory=false;ai.FindNearestCharacter=true;var target=perception.CreateGameObject("target");var controller=target.AddComponent(new CharacterController3D());target.Transform.LocalPosition=new(0,0,-2);ai.Tick(.11f);Check(ai.Target==target,"new character enters cached target registry");controller.Enabled=false;ai.Tick(.11f);Check(ai.Target==null,"disabled character is not acquired");controller.Enabled=true;target.Active=false;ai.Tick(.11f);Check(ai.Target==null,"inactive character is not acquired");target.Active=true;ai.Tick(.11f);Check(ai.Target==target,"reactivated character can be acquired");target.RemoveComponent(controller);ai.Tick(.11f);Check(ai.Target==null,"removed character leaves cached target registry");
  scene.Physics.Reset();Check(scene.Physics.QueryCalls==0,"reset clears query metrics and indexes");
 }
}
