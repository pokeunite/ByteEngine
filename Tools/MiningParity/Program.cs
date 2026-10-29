using System.Numerics;
using System.Text.Json;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.InputSystem;
using ByteEngine.Core.Plugins;
using ByteEngine.Core.Runtime;
using KarlsonDemo.Plugin;
using ByteEngine.Core;

if(args.FirstOrDefault()=="--verify-runtime"){
 Console.WriteLine("Verified browser boot resources: "+BrowserRuntimeIntegrity.Validate(args[1]));return;
}
string projectFile=args.ElementAtOrDefault(1)??"C:/Users/codex/Documents/palclone/palclone.byteproject";
if(args.FirstOrDefault()=="--export"){
 var result=WebGamePackageExporter.Export(projectFile,Path.GetFullPath("Dist/ByteEngine/BrowserRuntime"),Path.GetFullPath("artifacts/HazardPayBuilds"),"Scenes/Main.bytescene",new Progress<string>(s=>Console.WriteLine(s)),new(){Quality="Balanced",WorldResolution="Auto",TextureLimit=2048});
 Console.WriteLine("WEB_TEST_SITE="+Path.Combine(result.Directory,"site"));return;
}
using var app=new NativeApp(()=>CheckNative(projectFile));app.Run();
static void CheckNative(string projectFile){
ByteEnginePluginManager.RegisterLinkedPlugin("bytebard.karlsondemo",new KarlsonDemoPlugin());
using var runtime=new GameProjectRuntime(projectFile,Console.WriteLine);
var scene=runtime.LoadStartupScene();
foreach(var c in scene.GameObjects.SelectMany(o=>o.Components)){
 if(c is MiningPauseMenu3D menu){menu.ShowMainMenuOnStart=false;menu.PersistSettings=false;}
 var p=c.GetType().GetProperty("PersistProgress");if(p?.CanWrite==true)p.SetValue(c,false);
}
using var loop=new PortableGameLoop();loop.Scenes.LoadScene(scene);var sink=new Sink();var input=new RawInputSnapshot();
scene.ComponentProfiler.Enabled=true;
for(int i=0;i<5;i++)loop.Tick(.016,input,1280,720,new(.5f),true,true,sink);
var skins=scene.GameObjects.SelectMany(o=>o.Components.OfType<SkeletalMeshRenderer>()).Select(s=>{bool ok=s.TryGetCurrentModelBounds(out var b);return new{name=s.GameObject.Name,s.Enabled,s.Visible,s.CurrentAnimation,s.PlaybackTime,s.SkinnedMeshCount,model=s.ResolvedModel?.Name,bounds=ok?new[]{b.Minimum.X,b.Minimum.Y,b.Minimum.Z,b.Maximum.X,b.Maximum.Y,b.Maximum.Z}:null,firstNode=s.ResolvedModel?.Nodes.FirstOrDefault()?.LocalTransform.ToString(),firstRotation=s.ResolvedModel?.Animations.FirstOrDefault()?.Channels.FirstOrDefault(c=>c.Rotation?.Keys.Count>0)?.Rotation?.Keys.FirstOrDefault().Value.ToString()};}).ToArray();
Directory.CreateDirectory("artifacts/mining-parity");File.WriteAllText("artifacts/mining-parity/native.json",JsonSerializer.Serialize(new{skins,metrics=scene.LastFrameMetrics,draws=sink.Draws,components=scene.ComponentProfiler.Snapshot()},new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine("Native scene snapshot saved; draws="+sink.Draws);
}

sealed class NativeApp(Action check):ByteEngineApplication(320,180,"Hazard Pay parity check"){
 protected override void OnEngineStart(){check();Close();}
}

sealed class Sink:IRenderFrameSink{
 public int Draws;
 public void Draw3D(RenderView3D? view,RenderLighting3D light,RenderEnvironment3D environment,IReadOnlyList<RenderSubmission> submissions){Draws=submissions.Count;}
 public void DrawText(string value,string? font,int size,Vector2 position,Vector4 color,float wrap,UiAnchor anchor){}
 public void DrawQuad(Vector2 position,Vector2 size,Vector4 color){}
 public void DrawImage(Texture2D texture,Vector2 position,Vector2 size,Vector4 color){}
}
