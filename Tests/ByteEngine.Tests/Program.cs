using System.Numerics;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Serialization;
using ByteEngine.Core.Serialization.SerializationModels;
using ByteEngine.Core.Variables;
using ByteEngine.Editor;
using ByteEngine.Editor.Panels;

string root=Path.Combine(Path.GetTempPath(),"ByteEngine-v05-tests-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(Path.Combine(root,"Assets"));Directory.CreateDirectory(Path.Combine(root,"Scenes"));
try
{
    using var database=new AssetDatabase(root,new[]{"Assets","Scenes"});using var assets=new AssetManager(database);
    var serializer=new SceneSerializer(new ComponentSerializer(root,database,assets));

    var scene=new Scene("3D Test");scene.Variables.Set("Wave",VariableValue.FromNumber(2));scene.Variables.Set("Wind",VariableValue.FromVector3(new(1,2,3)));
    GameObject cube=scene.CreateGameObject("Cube");cube.Transform.LocalPosition=new(1,2,3);cube.Transform.EulerAngles=new(10,20,30);cube.Variables.Set("Health",VariableValue.FromNumber(100));cube.AddComponent(new MeshRenderer{Primitive=PrimitiveMeshType.Sphere,Material=new Material{BaseColor=new(.2f,.4f,.8f,1)}});
    GameObject camera=scene.CreateGameObject("Main Camera");camera.Transform.LocalPosition=new(0,2,6);camera.AddComponent(new Camera3D{FieldOfView=70});
    GameObject ground=scene.CreateGameObject("Ground");ground.Transform.LocalPosition=new(0,-1,0);ground.AddComponent(new MeshRenderer{Primitive=PrimitiveMeshType.Plane});ground.AddComponent(new GroundSurface{SurfaceType="Concrete"});
    Scene clone=serializer.CloneForRuntime(scene);Assert(clone.FindGameObject("Cube")?.GetComponent<MeshRenderer>()?.Primitive==PrimitiveMeshType.Sphere,"MeshRenderer round-trip");Assert(clone.FindComponent<Camera3D>()?.FieldOfView==70,"Camera3D round-trip");Assert(clone.FindGameObject("Ground")?.GetComponent<GroundSurface>()?.SurfaceType=="Concrete","GroundSurface round-trip");Assert(clone.Variables.TryGet("Wave",out var wave)&&wave!.Number==2,"Scene variable round-trip");Assert(clone.Variables["Wind"].Vector3==new Vector3(1,2,3),"Vector3 variable round-trip");
    string scenePath=Path.Combine(root,"Scenes","RoundTrip.bytescene");serializer.Save(scene,scenePath);Scene diskScene=serializer.Load(scenePath);Assert(diskScene.Variables["Wind"].Vector3==new Vector3(1,2,3),"Vector3 disk persistence");
    string projectPath=Path.Combine(root,"Test.byteproject");var projectData=new ProjectData{Name="Test",GlobalVariables={new VariableData{Name="Spawn",Value=VariableValue.FromVector3(new(4,5,6))}}};var projectSerializer=new ProjectSerializer();projectSerializer.Save(projectData,projectPath);Assert(projectSerializer.Load(projectPath).GlobalVariables[0].Value.Vector3==new Vector3(4,5,6),"Global default disk persistence");
    clone.Variables["Wave"].Number=9;clone.FindGameObject("Cube")!.Variables["Health"].Number=1;Assert(scene.Variables["Wave"].Number==2&&cube.Variables["Health"].Number==100,"Play clone isolation");

    var legacy=new SceneData{Name="Legacy",SceneId=Guid.NewGuid(),GameObjects={new GameObjectData{Id=Guid.NewGuid(),Name="Legacy Sprite",Transform=new TransformData{Position=new Vector2Data{X=5,Y=7},Rotation=45,Size=new Vector2Data{X=32,Y=48}},Components={new ComponentData{Type="SpriteRenderer"}}}}};
    Scene migrated=serializer.Deserialize(legacy);GameObject legacyObject=migrated.GameObjects[0];Assert(legacyObject.Transform.LocalPosition==new Vector3(5,7,0),"v0.4 position migration");Assert(legacyObject.GetComponent<SpriteRenderer>()?.Size==new Vector2(32,48),"v0.4 size migration");

    var child=scene.CreateGameObject("Child");child.Transform.WorldPosition=new(2,0,0);child.SetParent(cube,true);Assert(Vector3.Distance(child.Transform.WorldPosition,new(2,0,0))<.001f,"3D parenting preserves world position");
    var globals=new VariableStore();globals.Set("Score",VariableValue.FromNumber(10));var context=new VariableResolutionContext{Globals=globals,Scene=scene,Self=cube};
    Assert(VariableResolver.TryGet(new(){Scope=VariableScope.Global,MemberName="Score"},context,out object? score)&&Convert.ToDouble(score)==10,"Global resolver");Assert(VariableResolver.TryGet(new(){Scope=VariableScope.Self,MemberName="Health"},context,out object? health)&&Convert.ToDouble(health)==100,"Self resolver");Assert(VariableResolver.TryGet(new(){Scope=VariableScope.Component,ObjectId=camera.Id,ComponentType="Camera3D",MemberName="FieldOfView"},context,out object? fov)&&Convert.ToSingle(fov)==70,"Component resolver");

    string incoming=Path.Combine(root,"Incoming");Directory.CreateDirectory(incoming);string fbx=Path.Combine(incoming,"character.fbx");File.WriteAllText(fbx,"FBX test payload");
    string requestedProject=Path.Combine(root,"ImportProject.byteproject");using EditorProjectContext editorProject=EditorProjectContext.Create(requestedProject,_=>{});var editorLog=new EditorLog();var externalImporter=new ExternalAssetImporter(editorProject,editorLog);
    IReadOnlyList<AssetRecord> firstImport=externalImporter.Import(new[]{fbx});IReadOnlyList<AssetRecord> secondImport=externalImporter.Import(new[]{fbx});
    Assert(firstImport.Count==1&&firstImport[0].Type==AssetType.Model3D&&File.Exists(firstImport[0].FullPath)&&File.Exists(firstImport[0].MetaPath),"External FBX import and registration");
    Assert(secondImport.Count==1&&!string.Equals(firstImport[0].FullPath,secondImport[0].FullPath,StringComparison.OrdinalIgnoreCase),"External import collision naming");

    Scene cleanTemplate=ProjectTemplateFactory.Create(ProjectTemplate.Clean);Scene starterTemplate=ProjectTemplateFactory.Create(ProjectTemplate.Starter3D);
    Assert(cleanTemplate.Name=="Main"&&cleanTemplate.GameObjectCount==0,"Clean project template");
    Assert(starterTemplate.GameObjectCount==4&&starterTemplate.FindComponent<Camera3D>()!=null&&starterTemplate.FindComponent<DirectionalLight>()!=null&&starterTemplate.FindGameObject("Ground")?.GetComponent<GroundSurface>()!=null,"3D starter project template");
    Console.WriteLine("ByteEngine v0.5 tests passed: transform migration, 3D persistence, variables, resolver, play isolation, and external FBX import.");
}
finally { try{Directory.Delete(root,true);}catch{} }

static void Assert(bool condition,string name){if(!condition)throw new InvalidOperationException("FAILED: "+name);}
