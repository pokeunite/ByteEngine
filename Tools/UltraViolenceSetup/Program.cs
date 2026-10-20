using ByteEngine.Core.Assets;
using ByteEngine.Core.Runtime;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;

// Offline authoring/check entry point. No gameplay code is installed in the project.
internal static class Program
{
 const string Root="C:/Users/codex/Documents/ultra violence";
 [STAThread] static void Main(string[] args)
 {
  if(args.Contains("--playcheck")){using var check=new PlayCheck();check.Run();return;}
  if(args.Contains("--fix-hit-test")){WeaponEventAuthor.UpgradeTargetTest();return;}
  if(!args.Contains("--sketchfab")&&!args.Contains("--weapon-events"))throw new ArgumentException("Use --sketchfab, --weapon-events or --playcheck.");
  string project=Root+"/ultra violence.byteproject";
  if(!File.Exists(project))throw new FileNotFoundException("Open/create the native Ultra Violence project first.",project);
  using var window=new GameWindow(GameWindowSettings.Default,new NativeWindowSettings{StartVisible=false,ClientSize=new(1280,720),APIVersion=new(3,3),Profile=ContextProfile.Core});
  window.Context.MakeCurrent();
  using(var database=new AssetDatabase(Root,["Assets","Scenes"]))database.Scan();
  using var runtime=new GameProjectRuntime(project,Console.WriteLine);
  if(args.Contains("--weapon-events"))WeaponEventAuthor.Run(runtime);else SketchfabAuthor.Run(runtime);
 }
}
