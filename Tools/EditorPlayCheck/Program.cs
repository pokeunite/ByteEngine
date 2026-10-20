using System.Reflection;
using System.Runtime.Loader;

internal static class Program
{
 [STAThread]
 static int Main(string[] args)
 {
  string directory=Path.GetFullPath(args[0]),project=Path.GetFullPath(args[1]);
  AssemblyLoadContext.Default.Resolving+=(_,name)=>File.Exists(Path.Combine(directory,name.Name+".dll"))?AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(directory,name.Name+".dll")):null;
  var assembly=Assembly.LoadFrom(Path.Combine(directory,"ByteEngine.Editor.dll"));
  var type=assembly.GetType("ByteEngine.Editor.EditorApplication",true)!;
  object editor=Activator.CreateInstance(type,[project])!;
  object? Invoke(Type t,string name,object? target,params object?[] values)=>t.GetMethod(name,BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic)!.Invoke(target,values);
  try{
   var core=Assembly.LoadFrom(Path.Combine(directory,"ByteEngine.Core.dll"));var app=core.GetType("ByteEngine.Core.ByteEngineApplication",true)!;
   object context=type.GetProperty("Context")!.GetValue(editor)!;Invoke(context.GetType(),"MakeCurrent",context);
   Invoke(app,"OnLoad",editor);
   Console.WriteLine("ENGINE="+core.Location);
   var renderer=core.GetType("ByteEngine.Core.Graphics.ThreeD.SkeletalMeshRenderer",true)!;
   if(renderer.GetProperty("EvaluatePoseAfterPhysics")?.SetMethod==null||renderer.GetMethod("SetIkPoseTarget")==null)throw new Exception("Installed engine lacks riding APIs");
   Invoke(type,"EnterPlayMode",editor);
   object scenes=app.GetProperty("Scenes")!.GetValue(editor)!;object scene=scenes.GetType().GetProperty("ActiveScene")!.GetValue(scenes)!;
   var time=core.GetType("ByteEngine.Core.Time",true)!;
   var frameType=Assembly.Load("OpenTK.Windowing.Common").GetType("OpenTK.Windowing.Common.FrameEventArgs",true)!;
   for(int frame=0;frame<120;frame++){Invoke(time,"Update",null,1d/60);Invoke(scenes.GetType(),"UpdateInternal",scenes);if(frame%30==0)Invoke(app,"OnRenderFrame",editor,Activator.CreateInstance(frameType,[1d/60]));}
   Console.WriteLine("PASS installed editor EnterPlayMode, serialization-isolated scene clone, plugin startup, 120 runtime updates and four editor render frames");
   Invoke(type,"StopPlayMode",editor);
   Console.WriteLine("PASS StopPlayMode");
   return 0;
  }catch(Exception e){Console.Error.WriteLine(e);return 1;}
  finally{((IDisposable)editor).Dispose();}
 }
}
