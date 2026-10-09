using System.Numerics;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Physics;
using ByteEngine.Core.Scene;
using ByteEngine.Editor;
namespace ByteEngine.Tests;
internal static class RoadmapBrowserFixture
{
 public static void Create(string output)
 {
  string root=Path.GetFullPath(output);if(!root.Contains(".artifacts"))throw new ArgumentException("Private fixture directory required");Directory.CreateDirectory(root);
  using var project=EditorProjectContext.Create(Path.Combine(root,"Content","Content.byteproject"),Console.WriteLine);var scene=new Scene("Roadmap browser fixture"){FixedSimulation=true};
  var camera=scene.CreateGameObject("Camera").AddComponent(new Camera3D{ActiveGameCamera=true});camera.Transform.LocalPosition=new(0,2,8);Matrix4x4.Invert(Matrix4x4.CreateLookAt(camera.Transform.WorldPosition,new(0,1,0),Vector3.UnitY),out var world);camera.Transform.WorldRotation=Quaternion.CreateFromRotationMatrix(world);
  var material=new Material{BaseColor=new(.8f,.3f,.1f,1),Shading=MaterialShadingMode.Unlit};
  for(int i=0;i<20;i++){var cube=scene.CreateGameObject("Instanced "+i);cube.Transform.LocalPosition=new((i%5-2)*1.2f,1+(i/5)*.6f,-i/5);cube.Transform.LocalScale=new(.4f);cube.AddComponent(new MeshRenderer{UsePrimitive=true,Primitive=PrimitiveMeshType.Cube,Material=material,CastShadows=false});}
  var body=scene.CreateGameObject("Fixed body");body.Transform.LocalPosition=new(-2,0,0);body.AddComponent(new MeshRenderer{UsePrimitive=true,Material=material,CastShadows=false});body.AddComponent(new Rigidbody3D{UseGravity=false,Velocity=new(.2f,0,0),LinearDamping=0});
  var collision=scene.CreateGameObject("Triangle collision fixture");collision.AddComponent(new ByteEngine.Core.Characters.MeshCollider3D{Vertices=[new(-10,-2,-10),new(10,-2,-10),new(10,-2,10),new(-10,-2,10)],Triangles=[0,1,2,0,2,3]});
  var navigation=scene.CreateGameObject("Mesh navigation fixture");navigation.AddComponent(new ByteEngine.Core.Navigation.NavigationRegion3D{BakeMode=ByteEngine.Core.Navigation.NavigationBakeMode.MeshSurface,SurfaceObject=collision.Id,AgentRadius=.1f});
  var walker=scene.CreateGameObject("Navigation agent fixture");walker.Transform.LocalPosition=new(-2,-2,-2);walker.AddComponent(new MeshRenderer{UsePrimitive=true,FrustumCulling=false,Material=new Material{BaseColor=new(.3f,.8f,.2f,1),Shading=MaterialShadingMode.Unlit},CastShadows=false});walker.AddComponent(new ByteEngine.Core.Navigation.NavigationAgent3D{RegionObject=navigation.Id,Destination=new(2,-2,2),Moving=true,Speed=10});
  var canvas=scene.CreateGameObject("UI");canvas.AddComponent(new UiCanvas{ReferenceResolution=new(1280,720)});canvas.AddComponent(new UiTheme());var panel=scene.CreateGameObject("Clipped list");panel.SetParent(canvas,false);panel.AddComponent(new UiWidget{Size=new(240,150)});panel.AddComponent(new UiScrollContainer{ContentHeight=300});panel.AddComponent(new UiContainer{Kind=UiContainerKind.Column});for(int i=0;i<5;i++){var row=scene.CreateGameObject("Row "+i);row.SetParent(panel,false);row.AddComponent(new UiWidget{Kind=UiWidgetKind.Button,Size=new(200,50),Label="Roadmap test "+i,ThemeKey="primary",AutoFitLabel=true});}
  string rigPath=Path.Combine(project.ProjectRoot,"Assets","BrowserRig.gltf");File.WriteAllText(rigPath,"{\"asset\":{\"version\":\"2.0\"}}");project.AssetDatabase.Scan();project.AssetDatabase.TryGetAsset("Assets/BrowserRig.gltf",out var rigAsset);
  var rig=new ByteEngine.Core.Assets.Importers.ImportedModel{Guid=rigAsset!.Guid,SourceAssetGuid=rigAsset.Guid,Name="Browser animation",Nodes=[new(){Key="Root",Name="Root",MeshKeys=["AnimatedQuad"]},new(){Key="Tip",Name="Tip",ParentKey="Root"}],Skeleton=new(){Key="Skeleton",Bones=[new(){Name="Root",ParentIndex=-1},new(){Name="Tip",ParentIndex=0}]},
   Materials=[new(){Key="Cyan",BaseColor=new(.1f,.8f,.8f,1),Unlit=true}],
   Meshes=[new(){Key="AnimatedQuad",MaterialKey="Cyan",Vertices=[-.5f,-.5f,0,0,0,1,0,0,.5f,-.5f,0,0,0,1,1,0,.5f,.5f,0,0,0,1,1,1,-.5f,.5f,0,0,0,1,0,1],Indices=[0,1,2,0,2,3],JointIndices=[new(0),new(0),new(1,0,0,0),new(1,0,0,0)],JointWeights=[new(1,0,0,0),new(1,0,0,0),new(1,0,0,0),new(1,0,0,0)]}],
   Animations=[new(){Key="Pulse",Name="Pulse",Duration=1,Channels=[new(){NodeName="Tip",Translation=new(){Keys=[new(0,Vector3.Zero,Vector3.Zero,Vector3.Zero),new(.5f,new(.5f,0,0),Vector3.Zero,Vector3.Zero),new(1,Vector3.Zero,Vector3.Zero,Vector3.Zero)]}}]}]};
  CookedModelStore.Save(project.ProjectRoot,rig);
  var animated=scene.CreateGameObject("Animated fixture");animated.Transform.LocalPosition=new(-3.5f,1,-1);animated.AddComponent(new SkeletalMeshRenderer{Model=new AssetReference(rigAsset.Guid,rigAsset.ProjectPath),DefaultAnimation="Pulse",PlayOnStart=true,Loop=true});
  project.Scenes.Save(scene,project.ResolveProjectPath(project.Project.StartupScene));new ByteEngine.Core.Serialization.ProjectSerializer().Save(project.Project,Path.Combine(root,"Content","Game.byteproject"));File.WriteAllText(Path.Combine(root,"Content","Assets","fixture.txt"),"Private regression fixture");project.AssetDatabase.Scan();Console.WriteLine("Prepared browser regression fixture");
 }
}
