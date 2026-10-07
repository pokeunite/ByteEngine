using ByteEngine.Core.Runtime;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Serialization;
using ByteEngine.Core.Serialization.SerializationModels;
namespace ByteEngine.Tests;
internal static class RuntimeCompatibilityTests
{
 public static void Run(string root,string oldRuntime){var projectFile=Path.Combine(root,"Test.byteproject");new ProjectSerializer().Save(new ProjectData{Name="Runtime compatibility",StartupScene="Scenes/Main.bytescene"},projectFile);var scene=new Scene("Required component test");scene.CreateGameObject("Required terrain").AddComponent(new MissingComponent(new ComponentData{Type="test.RequiredHeightfield"}));using var database=new AssetDatabase(root,new[]{"Assets","Scenes"});using var assets=new AssetManager(database);new SceneSerializer(new ComponentSerializer(root,database,assets)).Save(scene,Path.Combine(root,"Scenes/Main.bytescene"));database.Scan();using(var runtime=new GameProjectRuntime(projectFile)){bool failed=false;try{runtime.LoadStartupScene();}catch(InvalidDataException e){failed=e.Message.Contains("test.RequiredHeightfield");}if(!failed)throw new InvalidOperationException("Player still permits missing required components");}Console.WriteLine("PASS Player refuses missing scene components and identifies the missing type");bool stale=false;try{GamePackageExporter.Export(projectFile,oldRuntime,Path.Combine(root,"Exports"),"Scenes/Main.bytescene");}catch(InvalidOperationException e){stale=e.Message.Contains("Windows player runtime differs");}if(!stale)throw new InvalidOperationException("Export still permits an old player core");if(Directory.Exists(Path.Combine(root,"Exports")))throw new InvalidOperationException("Stale runtime produced a partial export");Console.WriteLine("PASS Export refuses stale player before creating a build");}
}
