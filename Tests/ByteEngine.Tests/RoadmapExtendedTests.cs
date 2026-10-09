using System.Numerics;
using System.Diagnostics;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Physics;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Runtime;
namespace ByteEngine.Tests;
internal static class RoadmapExtendedTests
{
 static void Check(bool value,string message){if(!value)throw new Exception(message);}
 private sealed class TriggerProbe:Component
 {
  public int Enter,Stay,Exit;
  protected override void OnTriggerEnter(PhysicsContact3D contact)=>Enter++;
  protected override void OnTriggerStay(PhysicsContact3D contact)=>Stay++;
  protected override void OnTriggerExit(PhysicsContact3D contact)=>Exit++;
 }
 public static void Run()
 {
  foreach(int count in new[]{100,1000,5000})
  {
   var scene=new Scene("Sparse "+count);for(int i=0;i<count;i++){var obj=scene.CreateGameObject("static");obj.Transform.LocalPosition=new(i*3,0,0);obj.AddComponent(new BoxCollider3D());}
   for(int warmup=0;warmup<100;warmup++)scene.Physics.Step(scene,1f/60);int rebuilds=scene.Physics.StaticIndexRebuilds;
   var clock=Stopwatch.StartNew();for(int i=0;i<60;i++)scene.Physics.Step(scene,1f/60);clock.Stop();
   Check(scene.Physics.StaticIndexRebuilds==rebuilds,"Sparse static tree rebuilt");
   Console.WriteLine($"Sparse {count}: {clock.Elapsed.TotalMilliseconds/60:F2} ms/step, static index reused; {scene.Physics.LastCandidateCount} pairs; gather {scene.Physics.LastColliderGatherMs:F2}ms index {scene.Physics.LastIndexUpdateMs:F2}ms query {scene.Physics.LastCandidateQueryMs:F2}ms");
  }
  var movingScene=new Scene("Moving refit");var movingObjects=new List<GameObject>();
  for(int i=0;i<1000;i++){var obj=movingScene.CreateGameObject("Moving");obj.Transform.LocalPosition=new(i*3,0,0);obj.AddComponent(new BoxCollider3D());obj.AddComponent(new Rigidbody3D{BodyType=RigidbodyBodyType3D.Kinematic,UseGravity=false});movingObjects.Add(obj);}
  movingScene.Physics.QueryBounds(movingScene,new(-1),new(1));int movingBuilds=movingScene.Physics.MovingIndexRebuilds;
  for(int frame=0;frame<40;frame++)
  {
   foreach(var obj in movingObjects)obj.Transform.LocalPosition+=new Vector3(.25f,0,0);
   int test=frame*19;var position=movingObjects[test].Transform.WorldPosition;
   var found=movingScene.Physics.QueryBounds(movingScene,position-new Vector3(.6f),position+new Vector3(.6f));
   Check(found.Count==1&&ReferenceEquals(found[0],movingObjects[test]),"Refit gives stale or incorrect moving bounds");
  }
  Check(movingScene.Physics.MovingIndexRebuilds==movingBuilds&&movingScene.Physics.MovingIndexRefits>=40,"Moving bounds rebuilt instead of refitting");
  movingScene.DestroyGameObject(movingObjects[100]);var target=movingObjects[101];
  Check(movingScene.Physics.QueryBounds(movingScene,target.Transform.WorldPosition-new Vector3(.6f),target.Transform.WorldPosition+new Vector3(.6f)).Single()==target,"Membership rebuild loses source index");
  Console.WriteLine("PASS 1000 moving collider refits, live query accuracy and membership rebuild");
  var contacts=new Scene("Compound triggers");var a=contacts.CreateGameObject("A");a.AddComponent(new BoxCollider3D{IsTrigger=true});a.AddComponent(new BoxCollider3D{IsTrigger=true,Center=new(.1f,0,0)});var probe=a.AddComponent(new TriggerProbe());a.AddComponent(new Rigidbody3D{UseGravity=false});var b=contacts.CreateGameObject("B");b.AddComponent(new BoxCollider3D());contacts.LoadInternal();contacts.Physics.Step(contacts,1f/60);Check(probe.Enter==2,"Compound trigger contacts collapse");contacts.Physics.Step(contacts,1f/60);Check(probe.Enter==2&&probe.Stay==2,"Compound stay repeats enter");b.Transform.WorldPosition=new(10,0,0);contacts.Physics.Step(contacts,1f/60);Check(probe.Exit==2,"Compound exit lost");contacts.UnloadInternal();
  var motorPositions=new List<float>();foreach(int fps in new[]{30,60,144})
  {
   var scene=new Scene("Motor cadence"){FixedSimulation=true};var obj=scene.CreateGameObject("motor");var motor=obj.AddComponent(new CharacterController3D{Gravity=0});scene.LoadInternal();for(int i=0;i<fps;i++){motor.Move(Vector3.UnitX);ByteEngine.Core.Time.Update(1d/fps);scene.UpdateInternal();}motorPositions.Add(obj.Transform.WorldPosition.X);scene.UnloadInternal();
  }
  Check(motorPositions.All(x=>Math.Abs(x-motorPositions[0])<.001f),"Motor cadence diverges");
  const int side=64;var vertices=new float[side*side*8];var indices=new List<uint>();for(int z=0;z<side;z++)for(int x=0;x<side;x++){int o=(z*side+x)*8;vertices[o]=x/(float)(side-1);vertices[o+2]=z/(float)(side-1);vertices[o+4]=1;vertices[o+6]=vertices[o];vertices[o+7]=vertices[o+2];if(x<side-1&&z<side-1){uint i=(uint)(z*side+x);indices.AddRange(new[]{i,i+1,i+(uint)side,i+1,i+(uint)side+1,i+(uint)side});}}
  using var mesh=new Mesh(vertices,indices.ToArray());using var low=MeshSimplifier.Simplify(mesh,.15f);Check(low.VertexCount<mesh.VertexCount*.8f&&low.IndexCount<mesh.IndexCount&&low.VertexData.Span.ToArray().All(float.IsFinite),"Generated LOD does not reduce finite geometry");
  var generatedScene=new Scene("Generated LOD lifecycle");var generatedRoot=generatedScene.CreateGameObject("Source");var generatedSource=generatedRoot.AddComponent(new MeshRenderer{Mesh=mesh,Visible=false});generatedRoot.AddComponent(new MeshLodGroup{GenerateOnStart=true});
  for(int cycle=0;cycle<3;cycle++){generatedScene.LoadInternal();Check(generatedRoot.Children.Count==3&&generatedRoot.Children.All(c=>!c.GetComponent<MeshRenderer>()!.Visible),"Generated LOD ignores hidden source or leaks children");generatedScene.UnloadInternal();Check(generatedRoot.Children.Count==0&&!generatedSource.Visible&&ReferenceEquals(generatedSource.Mesh,mesh),"LOD stop loses source visibility, identity or generated cleanup");}
  var lod=new MeshLodGroup();var view=new RenderView3D(Matrix4x4.Identity,Matrix4x4.Identity,Vector3.Zero,1280,720);Check(lod.SelectForViewport("Game",view,29,0)==0&&lod.SelectForViewport("Game",view,31,0)==0,"LOD hysteresis flickers");Check(lod.SelectForViewport("Scene",view,31,0)==1,"Same-size viewport shares LOD history");
  var uiScene=new Scene("Responsive");var canvas=uiScene.CreateGameObject("Canvas");canvas.AddComponent(new UiCanvas());var row=uiScene.CreateGameObject("Grid");row.SetParent(canvas,false);row.AddComponent(new UiWidget{Size=new(400,400)});row.AddComponent(new UiContainer{Kind=UiContainerKind.Grid,CellSize=new(120,80),Spacing=new(8),Padding=Vector4.Zero});var buttons=new List<GameObject>();for(int i=0;i<4;i++){var child=uiScene.CreateGameObject("Button");child.SetParent(row,false);child.AddComponent(new UiWidget{Size=new(120,80)});buttons.Add(child);}var rect=UiLayout.Resolve(buttons[3],UiAnchor.TopLeft,Vector2.Zero,new(120,80),new(1280,720));Check(rect.Position.Y>=80,"Responsive grid does not wrap");
  var heightGrid=new ByteEngine.Core.Navigation.NavigationGrid(3,1){MaximumStepHeight=.4f};heightGrid.SetHeight(0,0,0);heightGrid.SetHeight(1,0,2);heightGrid.SetHeight(2,0,2);Check(heightGrid.FindPath(new(.5f,0,.5f),new(2.5f,2,.5f)).Count==0,"Agent crosses impassable step");heightGrid.AddLink(new(.5f,0,.5f),new(2.5f,2,.5f));var heightPath=heightGrid.FindPath(new(.5f,0,.5f),new(2.5f,2,.5f));Check(heightPath.Count==2&&heightPath[^1].Y==2,"Traversal link loses baked elevation");
  var issues=PlatformCapabilities.InspectJson("{\"components\":[{\"type\":\"SpriteRenderer\"},{\"type\":\"PointLight\"}]}",RuntimePlatform.Browser);Check(issues.Count==2&&issues.Count(i=>i.BlocksExport)==1,"Capability policy misses unsupported features");
  Console.WriteLine($"PASS sparse indexing, compound enter/stay/exit, motor FPS parity, generated LOD {mesh.VertexCount}->{low.VertexCount}, per-viewport hysteresis, grid wrap and capability warnings");
 }
}
