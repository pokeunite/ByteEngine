using System.Reflection;
using ByteEngine.Core;
using ByteEngine.Core.Runtime;
using ByteEngine.Core.Serialization;
using KarlsonDemo.Plugin;
internal static class MiningPlayerStartupCheck
{
 public static void Run()
 {
  string root=Program.Root,path=Path.Combine(root,"startup-check-"+Guid.NewGuid().ToString("N")+".byteproject");
  var serializer=new ProjectSerializer();var data=serializer.Load(Path.Combine(root,"palclone.byteproject"));data.RuntimeSaveId=Guid.NewGuid();serializer.Save(data,path);
  try {
   using var project=new GameProjectRuntime(path,Console.WriteLine);var assembly=Assembly.LoadFrom(Path.GetFullPath("Player/ByteEngine.Player/bin/Release/net9.0-windows/win-x64/ByteEngine.Player.dll"));var type=assembly.GetType("ByteEngine.Player.StandaloneGame",true)!;ByteEngineApplication? app=null;bool checkedFrame=false;
   int frames=0,displayStep=0;Action frame=()=>{if(++frames>1000)throw new Exception("Startup timed out");var scene=app!.Scenes.ActiveScene;if(scene==null)return;var menus=scene.GameObjects.SelectMany(o=>o.Components).Where(c=>c.GetType().Name=="MiningPauseMenu3D").ToArray();if(menus.Length==0)return;foreach(var c in scene.GameObjects.SelectMany(o=>o.Components)){c.GetType().GetProperty("PersistProgress")?.SetValue(c,false);if(c is MiningPauseMenu3D m)m.PersistSettings=false;}if(displayStep==0&&app.WindowState!=OpenTK.Windowing.Common.WindowState.Fullscreen)throw new Exception("Player did not start fullscreen");var menu=menus.Single();bool Read(string n)=>(bool)menu.GetType().GetProperty(n)!.GetValue(menu)!;if(!Read("IsOpen")||!Read("MainMenuMode"))return;if(Read("HasSavedGame"))throw new Exception("Fresh runtime should show New Game without Continue");if(displayStep==0){Console.WriteLine("PASS PLAYER fresh build opens fullscreen without Continue");GameWindowDisplay.Request(GameWindowMode.Windowed);displayStep=1;return;}
    if(displayStep==1){if(app.WindowState!=OpenTK.Windowing.Common.WindowState.Normal||app.WindowBorder!=OpenTK.Windowing.Common.WindowBorder.Resizable)throw new Exception("Windowed mode failed");Console.WriteLine("PASS PLAYER windowed mode is resizable with window chrome");GameWindowDisplay.Request(GameWindowMode.Borderless);displayStep=2;return;}
    if(displayStep==2){var monitor=OpenTK.Windowing.Desktop.Monitors.GetMonitorFromWindow(app);if(app.WindowState!=OpenTK.Windowing.Common.WindowState.Normal||app.WindowBorder!=OpenTK.Windowing.Common.WindowBorder.Hidden||app.ClientSize!=monitor.ClientArea.Size)throw new Exception("Borderless mode failed");Console.WriteLine("PASS PLAYER borderless mode fills the monitor without window chrome");GameWindowDisplay.Request(GameWindowMode.Fullscreen);displayStep=3;return;}
    if(app.WindowState!=OpenTK.Windowing.Common.WindowState.Fullscreen)throw new Exception("Fullscreen restore failed");Console.WriteLine("PASS PLAYER fullscreen mode restores correctly");checkedFrame=true;app.Close();};
   using(app=(ByteEngineApplication)Activator.CreateInstance(type,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,[project,false,false,null],null)!){app.RenderFrame+=_=>frame();app.Run();}if(!checkedFrame)throw new Exception("No rendered player frame");
  } finally {File.Delete(path);}
 }
}
