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
   if(args.Contains("--mining-effects")){if(renderer.GetMethod("BeginLightRagdoll")==null||renderer.GetMethod("EndLightRagdoll")==null)throw new Exception("Installed engine lacks skeletal explosion APIs");}
   else if(renderer.GetProperty("EvaluatePoseAfterPhysics")?.SetMethod==null||renderer.GetMethod("SetIkPoseTarget")==null)throw new Exception("Installed engine lacks riding APIs");
   Invoke(type,"EnterPlayMode",editor);
   object scenes=app.GetProperty("Scenes")!.GetValue(editor)!;object scene=scenes.GetType().GetProperty("ActiveScene")!.GetValue(scenes)!;
   foreach(object obj in (System.Collections.IEnumerable)scene.GetType().GetProperty("GameObjects")!.GetValue(scene)!)
    foreach(object component in (System.Collections.IEnumerable)obj.GetType().GetProperty("Components")!.GetValue(obj)!)
     if(component.GetType().Name is "MiningOperation3D" or "MiningTrade3D" or "MiningDefensePlanner3D" or "MiningCampLayout3D")component.GetType().GetProperty("PersistProgress")?.SetValue(component,false);
   var time=core.GetType("ByteEngine.Core.Time",true)!;
   var frameType=Assembly.Load("OpenTK.Windowing.Common").GetType("OpenTK.Windowing.Common.FrameEventArgs",true)!;
   for(int frame=0;frame<120;frame++){Invoke(time,"Update",null,1d/60);Invoke(scenes.GetType(),"UpdateInternal",scenes);if(frame%30==0)Invoke(app,"OnRenderFrame",editor,Activator.CreateInstance(frameType,[1d/60]));}
   Console.WriteLine("PASS installed editor EnterPlayMode, serialization-isolated scene clone, plugin startup, 120 runtime updates and four editor render frames");
   if(args.Contains("--mining-effects"))
   {
    object[] Objects()=>((System.Collections.IEnumerable)scene.GetType().GetProperty("GameObjects")!.GetValue(scene)!).Cast<object>().ToArray();
    object[] Components(object obj)=>((System.Collections.IEnumerable)obj.GetType().GetProperty("Components")!.GetValue(obj)!).Cast<object>().ToArray();
    var operation=Objects().SelectMany(Components).Single(c=>c.GetType().Name=="MiningOperation3D");var effects=Objects().SelectMany(Components).Single(c=>c.GetType().Name=="MiningEffects3D");
    if((int)effects.GetType().GetProperty("PooledEffects")!.GetValue(effects)!<20)throw new Exception("Installed effects failed to prewarm");
    var spawn=core.GetType("ByteEngine.Core.VisualLogic.RuntimeSpawnService",true)!.GetMethod("SpawnBlueprint")!;var blueprint=operation.GetType().GetProperty("HunterBlueprint")!.GetValue(operation)!;
    var enemy=spawn.Invoke(null,[scene,blueprint,new System.Numerics.Vector3(14,0,-30)])!;
    void Frames(int count){for(int i=0;i<count;i++){Invoke(time,"Update",null,1d/60);Invoke(scenes.GetType(),"UpdateInternal",scenes);}}
    Frames(3);var animation=Components(enemy).Single(c=>c.GetType().Name=="MiningSpiderAnimation3D");
    if(!(bool)Invoke(animation.GetType(),"ApplyExplosionImpact",animation,System.Numerics.Vector3.UnitX,6f,3.5f)!)throw new Exception("Installed editor rejected spider blast");Frames(5);
    var skin=animation.GetType().GetProperty("Renderer")!.GetValue(animation)!;if(!(bool)skin.GetType().GetProperty("LightRagdollActive")!.GetValue(skin)!)throw new Exception("Installed editor did not enter ragdoll");
    var health=Components(enemy).Single(c=>c.GetType().Name=="HealthComponent");Invoke(health.GetType(),"Kill",health);Frames(2);
    if(!Objects().Any(o=>(string)o.GetType().GetProperty("Name")!.GetValue(o)! == "Spider death remains"&&Components(o).Any(c=>c.GetType().Name=="Rigidbody3D")))throw new Exception("Installed editor did not retain ballistic corpse");
    Invoke(app,"OnRenderFrame",editor,Activator.CreateInstance(frameType,[1d/60]));Console.WriteLine("PASS installed editor warmed VFX assets, animated spider skin, blast knockdown, ballistic death corpse and render");
   }
   if(args.Contains("--defence-cursor"))
   {
    var input=core.GetType("ByteEngine.Core.Input",true)!;object owner=new();
    var view=type.GetField("_gameView",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(editor)!;Invoke(view.GetType(),"RequestFocus",view);
    void Render()=>Invoke(app,"OnRenderFrame",editor,Activator.CreateInstance(frameType,[1d/60]));
    Invoke(app,"CaptureGameInput",editor,true);Render();
    Invoke(input,"RequestUiPointer",null,owner,true);Render();Render();
    bool captured=(bool)input.GetProperty("IsGameInputCaptured")!.GetValue(null)!;string cursor=type.GetProperty("CursorState")!.GetValue(editor)!.ToString()!;
    if(!captured||cursor!="Normal")throw new Exception("Temporary defence cursor lost gameplay capture: "+captured+" "+cursor);
    Console.WriteLine("PASS editor defence cursor shows without releasing gameplay capture");
    Invoke(input,"RequestUiPointer",null,owner,false);Render();Render();
    captured=(bool)input.GetProperty("IsGameInputCaptured")!.GetValue(null)!;cursor=type.GetProperty("CursorState")!.GetValue(editor)!.ToString()!;
    if(!captured||cursor!="Grabbed")throw new Exception("Defence selection failed to restore camera capture: "+captured+" "+cursor);
    Console.WriteLine("PASS editor defence selection restores grabbed camera without a second click");
   }
   Invoke(type,"StopPlayMode",editor);
   Console.WriteLine("PASS StopPlayMode");
   return 0;
  }catch(Exception e){Console.Error.WriteLine(e);return 1;}
  // This harness invokes OnLoad directly, so it must also execute the matching
  // unload lifecycle before disposing the native window/context.
  finally{Invoke(type.BaseType!,"OnUnload",editor);((IDisposable)editor).Dispose();}
 }
}
