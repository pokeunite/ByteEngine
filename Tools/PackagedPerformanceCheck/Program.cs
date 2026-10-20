using System.Reflection;
using System.Runtime.Loader;
using System.Diagnostics;
using System.Numerics;
using System.Text.Json;

internal static class Program
{
 [STAThread] static int Main(string[] args){
  string package=Path.GetFullPath(args[0]),dir=Path.Combine(package,"Runtime");
  AssemblyLoadContext.Default.Resolving+=(_,n)=>File.Exists(Path.Combine(dir,n.Name+".dll"))?AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(dir,n.Name+".dll")):null;
  object? Call(Type t,string n,object? o,params object?[] a)=>t.GetMethods(BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic).Single(m=>m.Name==n&&!m.ContainsGenericParameters&&m.GetParameters().Length==a.Length&&m.GetParameters().Select((p,i)=>a[i]==null||p.ParameterType.IsInstanceOfType(a[i])).All(x=>x)).Invoke(o,a);
  object? Get(object o,string n)=>o.GetType().GetProperty(n)!.GetValue(o);
  void Set(object o,string n,object v)=>o.GetType().GetProperty(n)!.SetValue(o,v);
  var core=Assembly.LoadFrom(Path.Combine(dir,"ByteEngine.Core.dll"));var player=Assembly.LoadFrom(Path.Combine(dir,"ByteEngine.Player.dll"));
  var sessionType=core.GetType("ByteEngine.Core.Runtime.GameContentSession",true)!;using var content=(IDisposable)Activator.CreateInstance(sessionType,[package])!;
  if(args.Contains("--patch-plugin")){
   string root=Path.GetDirectoryName((string)Get(content,"ProjectFile")!)!;
   File.Copy(args[1],Path.Combine(root,"Plugins","bytebard.downhill.byteplugin"),true);
   int sync=Array.IndexOf(args,"--sync-riding");
   if(sync>=0){string source=Path.GetFullPath(args[sync+1]);foreach(string relative in new[]{"Assets/Animations/Longboard",".byteengine/ModelAnimations"})foreach(string file in Directory.EnumerateFiles(Path.Combine(source,relative),"*",SearchOption.AllDirectories)){string target=Path.Combine(root,Path.GetRelativePath(source,file));Directory.CreateDirectory(Path.GetDirectoryName(target)!);File.Copy(file,target,true);}File.Copy(Path.Combine(source,"Scenes/Main.bytescene"),Path.Combine(root,"Scenes/Main.bytescene"),true);}
   string repaired=Path.Combine(package,"Game.bytepak.repaired");Call(core.GetType("ByteEngine.Core.Runtime.ByteAssetPackage",true)!,"Create",null,root,repaired);
   File.Move(repaired,Path.Combine(package,"Game.bytepak"),true);
   string manifest=Path.Combine(package,"game-package.json");var data=System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(manifest))!;var entry=data["files"]!.AsArray().Single(e=>e!["Path"]!.GetValue<string>()=="Game.bytepak")!;entry["Length"]=new FileInfo(Path.Combine(package,"Game.bytepak")).Length;using var file=File.OpenRead(Path.Combine(package,"Game.bytepak"));entry["Sha256"]=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(file));File.WriteAllText(manifest,data.ToJsonString(new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine("PASS plugin patched and package integrity manifest refreshed");return 0;
  }
  var runtimeType=core.GetType("ByteEngine.Core.Runtime.GameProjectRuntime",true)!;using var runtime=(IDisposable)Activator.CreateInstance(runtimeType,[Get(content,"ProjectFile"),new Action<string>(Console.WriteLine)])!;
  var gameType=player.GetType("ByteEngine.Player.StandaloneGame",true)!;using var game=(IDisposable)Activator.CreateInstance(gameType,[runtime,false,false,null])!;
  var app=core.GetType("ByteEngine.Core.ByteEngineApplication",true)!;
  try{
   object context=Get(game,"Context")!;Call(context.GetType(),"MakeCurrent",context);Set(game,"ClientSize",Activator.CreateInstance(Assembly.Load("OpenTK.Mathematics").GetType("OpenTK.Mathematics.Vector2i",true)!,[1280,720])!);Call(app,"OnLoad",game);
   object scenes=Get(game,"Scenes")!,scene=Get(scenes,"ActiveScene")!;
   object Find(string n)=>Call(scene.GetType(),"FindGameObject",scene,n)!;
   object Component(object obj,string name)=>((System.Collections.IEnumerable)Get(obj,"Components")!).Cast<object>().First(c=>c.GetType().Name==name);
   var boardObject=Find("Board");var board=Component(boardObject,"BoardController3D");Set(board,"Enabled",false);Set(Component(boardObject,"Rigidbody3D"),"Enabled",false);Set(Component(Find("Run director"),"DownhillRun3D"),"Enabled",false);Set(Component(Find("Follow camera"),"BoardCamera3D"),"Enabled",false);
   var renderer=Component(Find("Rider"),"SkeletalMeshRenderer");var rt=renderer.GetType();var updates=rt.GetField("DiagnosticPoseUpdates",BindingFlags.NonPublic|BindingFlags.Static)!;
   foreach(string field in new[]{"_resolved","_model","_skeleton","_runtimeMeshes","_nodes"}){var v=rt.GetField(field,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(renderer);Console.WriteLine($"RENDERER {field}={(v is System.Collections.ICollection list?list.Count:v)}");}
   Console.WriteLine("RENDERER ASSEMBLY="+rt.Assembly.Location+" late="+Get(renderer,"EvaluatePoseAfterPhysics"));
   try{var assets=Get(runtime,"Assets")!;var model=Call(assets.GetType(),"LoadModel",assets,Get(renderer,"Model"));Console.WriteLine("MODEL LOADED="+model);}catch(Exception e){Console.WriteLine("MODEL LOAD FAILURE "+e);}
   var time=core.GetType("ByteEngine.Core.Time",true)!;var frameType=Assembly.Load("OpenTK.Windowing.Common").GetType("OpenTK.Windowing.Common.FrameEventArgs",true)!;var ev=Activator.CreateInstance(frameType,[1d/60]);
   var profiler=core.GetType("ByteEngine.Core.Diagnostics.RuntimeComponentProfiler");profiler?.GetProperty("Enabled")?.SetValue(null,true);
   Console.WriteLine("RUNTIME="+core.Location+" WINDOW="+Get(game,"WindowWidth")+"x"+Get(game,"WindowHeight"));
   if(args.Contains("--drive")){
    Set(board,"Enabled",true);Set(board,"ReadPlayerInput",false);Set(board,"ControlsEnabled",true);var body=Component(boardObject,"Rigidbody3D");Set(body,"Enabled",true);Set(body,"UseGravity",true);Set(Component(Find("Follow camera"),"BoardCamera3D"),"Enabled",true);
    var points=JsonDocument.Parse(File.ReadAllText("C:/Users/codex/Documents/Legends Road/ArtSource/ThreeJS/course-layout.json")).RootElement.GetProperty("points").EnumerateArray().Select(p=>{var a=p.GetProperty("p");return new Vector3(a[0].GetSingle(),a[1].GetSingle(),a[2].GetSingle());}).ToArray();var frames=new List<double>();double physics=0,animation=0;double maxPhysics=0;var totals=Stopwatch.StartNew();
    for(int f=1;f<=(args.Contains("--quick")?120:720);f++){
     var transform=Get(boardObject,"Transform")!;var position=(Vector3)Get(transform,"WorldPosition")!;var forward=(Vector3)Get(transform,"Forward")!;var right=(Vector3)Get(transform,"Right")!;int nearest=Enumerable.Range(0,points.Length).MinBy(i=>Vector3.DistanceSquared(points[i],position));var aim=points[Math.Min(points.Length-1,nearest+4)]-position;aim.Y=0;aim=Vector3.Normalize(aim);forward.Y=0;forward=Vector3.Normalize(forward);float speed=(float)Get(board,"Speed")!;float angle=MathF.Atan2(Vector3.Dot(aim,right),Vector3.Dot(aim,forward));float rate=(float)Call(board.GetType(),"TurnRateAt",board,speed)!*MathF.PI/180;Set(board,"SteerInput",Math.Clamp(2*speed*MathF.Sin(angle)/8/Math.Max(.1f,rate),-1,1));Set(board,"BrakeInput",speed>6);Set(board,"PushInput",speed<3);
     long before=(long)updates.GetValue(null)!;var watch=Stopwatch.StartNew();Call(time,"Update",null,1d/30);Call(scenes.GetType(),"UpdateInternal",scenes);Call(app,"OnRenderFrame",game,ev);var metrics=Get(scene,"LastFrameMetrics")!;double phys=Convert.ToDouble(Get(metrics,"PhysicsMs"));if(f>60){frames.Add(watch.Elapsed.TotalMilliseconds);physics+=phys;animation+=Convert.ToDouble(Get(metrics,"AnimationMs"));maxPhysics=Math.Max(maxPhysics,phys);}
     if(f%120==0)Console.WriteLine($"DRIVE {f} fps={f/totals.Elapsed.TotalSeconds:0.0} physics={phys:0.00}ms animation={Get(metrics,"AnimationMs")}ms pos={position} speed={speed:0.0}");
    }
    frames.Sort();Console.WriteLine($"MOVING FPS={1000/frames.Average():0.0} p95={frames[(int)(frames.Count*.95)]:0.00}ms physics={physics/frames.Count:0.00}ms maxPhysics={maxPhysics:0.00}ms animation={animation/frames.Count:0.00}ms");Call(gameType,"OnEngineShutdown",game);return 0;
   }
   for(int phase=0;phase<4;phase++){
    var samples=new List<double>();double animation=0,physics=0;long count=0;
    for(int f=0;f<240;f++){
     Set(board,"Speed",phase==1?2f:18f);Set(board,"PushInput",phase==1);Set(board,"PushPhase",f%69/69f);Set(board,"TuckInput",phase==3);Set(board,"SlideInput",phase==2);Set(board,"IsSliding",phase==2);Set(board,"Grounded",true);Set(board,"SmoothedSteer",phase==2?.7f:0f);Set(board,"SlipAngle",phase==2?-45f:0f);
     long before=(long)updates.GetValue(null)!;var watch=Stopwatch.StartNew();Call(time,"Update",null,1d/60);Call(scenes.GetType(),"UpdateInternal",scenes);Call(app,"OnRenderFrame",game,ev);double ms=watch.Elapsed.TotalMilliseconds;
     if(f>60){samples.Add(ms);count+=(long)updates.GetValue(null)!-before;var metrics=Get(scene,"LastFrameMetrics")!;animation+=Convert.ToDouble(Get(metrics,"AnimationMs"));physics+=Convert.ToDouble(Get(metrics,"PhysicsMs"));}
    }
    samples.Sort();Console.WriteLine($"PERF {new[]{"Ride","Push","Slide","Tuck"}[phase]} FPS={1000/samples.Average():0.0} p95={samples[(int)(samples.Count*.95)]:0.00}ms animation={animation/samples.Count:0.00}ms physics={physics/samples.Count:0.00}ms poses/frame={count/(double)samples.Count:0.00}");
    if(profiler!=null)foreach(var s in (System.Collections.IEnumerable)Call(profiler,"Snapshot",null)!)Console.WriteLine("COMPONENT "+s);
   }
   Console.WriteLine("PASS packaged rendering benchmark");Call(gameType,"OnEngineShutdown",game);return 0;
  }catch(Exception e){Console.Error.WriteLine(e);return 1;}
 }
}



