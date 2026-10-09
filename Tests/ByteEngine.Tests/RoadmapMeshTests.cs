using System.Numerics;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Physics;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Serialization;
namespace ByteEngine.Tests;
internal static class RoadmapMeshTests
{
 static void Check(bool value,string message){if(!value)throw new Exception(message);}
 internal static MeshCollider3D Cube(float half=.5f)
 {
  var points=new List<Vector3>();for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)points.Add(new(x*half,y*half,z*half));
  return new MeshCollider3D{Mode=MeshCollisionMode.Convex,Vertices=points.ToArray(),Triangles=[0,1,3,0,3,2,4,6,7,4,7,5,0,4,5,0,5,1,2,3,7,2,7,6,0,2,6,0,6,4,1,5,7,1,7,3]};
 }
 public static void Run(string root)
 {
  var scene=new Scene("Triangle mesh floor");var ground=scene.CreateGameObject("floor");var mesh=ground.AddComponent(new MeshCollider3D{Vertices=[new(-10,0,-10),new(10,0,-10),new(10,0,10),new(-10,0,10)],Triangles=[0,1,2,0,2,3]});
  Check(mesh.Geometry!=null&&mesh.GeometryStatus.Contains("2 triangles"),"Triangle geometry invalid");
  Check(GameplayQuery3D.Raycast(scene,new(1,5,2),-Vector3.UnitY,out var ray,10)&&Math.Abs(ray.Distance-5)<.001f&&ray.Collider==mesh,"Triangle raycast");
  Check(GameplayQuery3D.SphereCast(scene,new(1,5,2),-Vector3.UnitY,.5f,out var sphere,10)&&Math.Abs(sphere.Distance-4.5f)<.001f,"Triangle sphere cast");
  var box=scene.CreateGameObject("box");box.Transform.LocalPosition=new(2,3,1);box.AddComponent(new BoxCollider3D());var body=box.AddComponent(new Rigidbody3D{SimulateRotation=true});for(int i=0;i<360;i++)scene.Physics.Step(scene,1f/60);Check(Math.Abs(box.Transform.WorldPosition.Y-.5f)<.03f&&box.Transform.WorldPosition.X>1.9f&&body.IsSleeping,"Box cannot rest stably on triangle mesh "+box.Transform.WorldPosition);
  var capsule=scene.CreateGameObject("capsule");capsule.Transform.LocalPosition=new(-2,4,0);capsule.AddComponent(new CapsuleCollider3D());capsule.AddComponent(new Rigidbody3D());for(int i=0;i<360;i++)scene.Physics.Step(scene,1f/60);Check(Math.Abs(capsule.Transform.WorldPosition.Y)<.04f,"Capsule cannot rest on mesh "+capsule.Transform.WorldPosition);
  ground.Transform.LocalPosition=new(0,1,0);Check(GameplayQuery3D.Raycast(scene,new(8,5,8),-Vector3.UnitY,out ray,10)&&Math.Abs(ray.Distance-4)<.001f,"Moved mesh leaves stale bounds/tree");
  mesh.Center=new(0,1,0);Check(GameplayQuery3D.Raycast(scene,new(8,5,8),-Vector3.UnitY,out ray,10)&&Math.Abs(ray.Distance-3)<.001f,"Edited mesh center leaves stale geometry");
  var hullScene=new Scene("Convex body");var floor=hullScene.CreateGameObject("Box floor");floor.Transform.LocalPosition=new(0,-.5f,0);floor.AddComponent(new BoxCollider3D{Size=new(20,1,20)});var hull=hullScene.CreateGameObject("hull");hull.Transform.LocalPosition=new(0,3,0);var convex=hull.AddComponent(Cube());hull.AddComponent(new Rigidbody3D{SimulateRotation=true});Check(convex.Geometry is {ValidConvex:true},"Closed cube rejected as convex");for(int i=0;i<360;i++)hullScene.Physics.Step(hullScene,1f/60);Check(Math.Abs(hull.Transform.WorldPosition.Y-.5f)<.05f,"Convex body falls through box floor "+hull.Transform.WorldPosition);
  var invalid=hullScene.CreateGameObject("Open hull").AddComponent(new MeshCollider3D{Mode=MeshCollisionMode.Convex,Vertices=[Vector3.Zero,Vector3.UnitX,Vector3.UnitZ],Triangles=[0,1,2]});Check(invalid.Geometry==null,"Open triangle accepted as dynamic convex hull");
  var ccdScene=new Scene("Mesh CCD");var wall=ccdScene.CreateGameObject("wall");wall.AddComponent(new MeshCollider3D{Vertices=[new(0,-10,-10),new(0,10,-10),new(0,10,10),new(0,-10,10)],Triangles=[0,1,2,0,2,3]});var bullet=ccdScene.CreateGameObject("bullet");bullet.Transform.LocalPosition=new(-5,0,0);bullet.AddComponent(new BoxCollider3D{Size=new(.2f)});var rb=bullet.AddComponent(new Rigidbody3D{UseGravity=false,Velocity=new(1000,0,0),ContinuousCollision=true,LinearDamping=0});ccdScene.Physics.Step(ccdScene,1f/60);Check(bullet.Transform.WorldPosition.X<0&&rb.Velocity.X<1,"CCD tunnels through triangle wall");
  using var database=new ByteEngine.Core.Assets.AssetDatabase(root,["Assets"]);using var assets=new ByteEngine.Core.Assets.AssetManager(database);var serializer=new SceneSerializer(new ComponentSerializer(root,database,assets));var clone=serializer.Deserialize(serializer.Serialize(scene));var clonedMesh=clone.GameObjects.Single(o=>o.Name=="floor").GetComponent<MeshCollider3D>();Check(clonedMesh?.Geometry!=null&&clonedMesh.Vertices.Length==4,"Mesh geometry scene roundtrip");
  Console.WriteLine("PASS mesh ray/sphere queries, moved mesh index, angular box/capsule resting contacts, validated convex body, invalid hull rejection, thin mesh CCD and serialization");
 }
}
