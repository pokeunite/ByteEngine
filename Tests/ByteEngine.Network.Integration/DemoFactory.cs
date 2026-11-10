using ByteEngine.Core.Graphics;
using System.Numerics;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Net;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Serialization;
using ByteEngine.Core.Serialization.SerializationModels;
internal static class DemoFactory
{
 internal static void Validate(string root){using var db=new AssetDatabase(root,["Assets","Scenes"]);db.Scan();using var runtime=new ByteEngine.Core.Runtime.GameProjectRuntime(Path.Combine(root,"MultiplayerDemo.byteproject"));var scene=runtime.LoadStartupScene();if(scene.GameObjects.Count(o=>o.GetComponent<NetworkIdentity>()!=null)!=4)throw new Exception("Missing network identities");Console.WriteLine("PASS demo component serialization and runtime load");}
 internal static void Create(string root)
 {
  Directory.CreateDirectory(root);Directory.CreateDirectory(Path.Combine(root,"Assets"));Directory.CreateDirectory(Path.Combine(root,"Scenes"));
  string project=Path.Combine(root,"MultiplayerDemo.byteproject");if(File.Exists(project))throw new InvalidOperationException("Demo already exists; refusing to replace it");
  new ProjectSerializer().Save(new ProjectData{Name="ByteEngine Multiplayer Demo",Window=new(){StartFullscreen=false,Width=960,Height=640}},project);
  using var database=new AssetDatabase(root,["Assets","Scenes"]);using var assets=new AssetManager(database);
  var serializer=new SceneSerializer(new ComponentSerializer(root,database,assets));var scene=new Scene("Multiplayer Demo");
  var settings=new GameObject("Network Settings");settings.AddComponent(new NetworkSettings());scene.AddGameObject(settings);
  var camera=new GameObject("Camera");camera.Transform.WorldPosition=new(0,12,16);camera.Transform.EulerAngles=new(-35,0,0);camera.AddComponent(new Camera3D{ActiveGameCamera=true});scene.AddGameObject(camera);
  var floor=new GameObject("Floor");floor.Transform.WorldPosition=new(0,-.7f,0);floor.Transform.LocalScale=new(12,.1f,12);floor.AddComponent(new MeshRenderer{UsePrimitive=true,Material=new(){Shading=MaterialShadingMode.Unlit,BaseColor=new(.12f,.16f,.21f,1)}});scene.AddGameObject(floor);
  Vector4[] colors=[new(.2f,.7f,1,1),new(1,.4f,.2f,1),new(.4f,1,.3f,1),new(1,.8f,.2f,1)];
  for(int id=0;id<4;id++){var avatar=new GameObject($"Player {id}");avatar.Transform.WorldPosition=new(id*2-3,0,0);avatar.Transform.LocalScale=new(.5f,.7f,.5f);avatar.AddComponent(new MeshRenderer{UsePrimitive=true,Material=new(){Shading=MaterialShadingMode.Unlit,BaseColor=colors[id]}});avatar.AddComponent(new NetworkIdentity{PeerId=id});avatar.AddComponent(new NetworkTransform());scene.AddGameObject(avatar);}
  serializer.Save(scene,Path.Combine(root,"Scenes/Main.bytescene"));
  database.Scan();
  using var runtime=new ByteEngine.Core.Runtime.GameProjectRuntime(project);
  var loaded=runtime.LoadStartupScene();if(loaded.GameObjects.Count(o=>o.GetComponent<NetworkIdentity>()!=null)!=4 || loaded.GameObjects.Count(o=>o.GetComponent<NetworkSettings>()!=null)!=1)throw new Exception("Network component serialization failed");
  Console.WriteLine("PASS created and reloaded multiplayer demo: "+project);
 }
}
