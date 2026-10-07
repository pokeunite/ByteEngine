using ByteEngine.Core.Gameplay;
using ByteEngine.Core.VisualLogic;

using System.Numerics;

using System.Diagnostics;

using ByteEngine.Core;

using ByteEngine.Core.Graphics;

using ByteEngine.Core.Graphics.ThreeD;

using ByteEngine.Core.Scene;

using DesertTerrain;

using OpenTK.Graphics.OpenGL4;

namespace ByteEngine.Tests;

internal sealed partial class DunePolishDiagnostic

{

 void RunSandVehicle(string root)

 {

  Renderer3D.EnableStateBatching=Environment.GetEnvironmentVariable("SAND_LAB_DISABLE_BATCH")!="1";

  var scene=_project!.Scenes.Load(Path.Combine(root,"Scenes/SandVehicleLab.bytescene"));Scenes.LoadScene(scene);Tick(scene,new(.5f),false);

  var workshop=scene.GameObjects.SelectMany(o=>o.Components).OfType<DuneCompany.DuneWorkshop3D>().Single();workshop.Command("Load vehicle");Capture(scene,Path.Combine(root,"Preview/sand-vehicle-build.png"),false);

  workshop.Command("Toggle drive");if(workshop.Building)throw new Exception("Saved vehicle did not enter the sand lab");

  for(int i=0;i<60;i++)Tick(scene,new(.5f),false);var start=workshop.Transform.WorldPosition;

  using var fb=new ByteEngine.Editor.SceneFramebuffer();var frames=new List<double>();var timer=new Stopwatch();

  for(int i=0;i<100;i++){timer.Restart();Tick(scene,new(.5f),false,Key.W);fb.RenderGame(Renderer,Renderer3D,scene,ByteEngine.Editor.EditorMode.Play,1280,720,1280,720);GL.Finish();timer.Stop();frames.Add(timer.Elapsed.TotalMilliseconds);}

  float distance=Vector3.Distance(start,workshop.Transform.WorldPosition);if(distance<2)throw new Exception("Saved vehicle failed to drive on displaced sand");

  var surface=scene.GameObjects.SelectMany(o=>o.Components).OfType<InteractiveSand3D>().Single();float minimum=0;for(int z=0;z<surface.Rows;z++)for(int x=0;x<surface.Columns;x++)minimum=Math.Min(minimum,surface.HeightAt(x,z));

  if(minimum>-.025f)throw new Exception("Real wheel contacts did not depress the sand surface");

  Capture(scene,Path.Combine(root,"Preview/sand-vehicle-driving.png"),false);frames.Sort();File.WriteAllText(Path.Combine(root,"Preview/sand-vehicle-benchmark.txt"),$"Saved vehicle{workshop.Blocks.Count}blocks, drove{distance:0.00}m, deepest surface{minimum:0.000}m.\nFull update/render/explicitGPUcompletion: median{frames[50]:0.00}ms p95{frames[95]:0.00}ms\n");

  Console.WriteLine($"PASS Saved vehicle driving and contact displacement: {distance:0.00}m /{minimum:0.000}m");Scenes.UnloadScene();_project.Dispose();Close();

 }

 void RunSandLab(string root)

 {
    if(Path.GetFileName(root)=="InteractiveSand")
    {
        var builtInScene=_project!.Scenes.Load(Path.Combine(root,"Scenes/Main.bytescene"));Scenes.LoadScene(builtInScene);Tick(builtInScene,new(.5f),false);
        var builtInSand=builtInScene.GameObjects.SelectMany(o=>o.Components).OfType<InteractiveSand3D>().Single();
        var builtInProbe=builtInScene.GameObjects.SelectMany(o=>o.Components).OfType<SandLabProbe3D>().Single();var before=builtInProbe.Transform.WorldPosition;
        for(int i=0;i<90;i++)Tick(builtInScene,new(.5f),false,Key.W);
        if(Vector3.Distance(before,builtInProbe.Transform.WorldPosition)<2)throw new Exception("Standalone ball did not move");
        builtInSand.TrySampleWorld(builtInProbe.Transform.WorldPosition,out var depression,out _);if(depression.Y>-.03f)throw new Exception("Standalone sand did not deform");
        Capture(builtInScene,Path.Combine(root,"Preview/interactive-sand.png"),false);
        Console.WriteLine("PASS Built-in standalone scene: native WASD, sand deformation and GL rendering without plugins");Scenes.UnloadScene();_project.Dispose();Close();return;
    }


  var scene=new Scene("Dune Company - Interactive Sand Lab");

  var sand=scene.CreateGameObject("Sand - persistent 12.5cm contact field").AddComponent(new InteractiveSand3D());

  var camera=scene.CreateGameObject("Sand inspection camera").AddComponent(new Camera3D{ActiveGameCamera=true,FieldOfView=52,FarClip=100});camera.Transform.WorldPosition=new(10,9,13);Matrix4x4.Invert(Matrix4x4.CreateLookAt(camera.Transform.WorldPosition,Vector3.Zero,Vector3.UnitY),out var cameraWorld);camera.Transform.WorldRotation=Quaternion.CreateFromRotationMatrix(cameraWorld);

  var sun=scene.CreateGameObject("Low sun - reveal displaced edges").AddComponent(new DirectionalLight{Intensity=2.3f,Color=new(1,.91f,.76f),CastShadows=true,ShadowResolution=1024,ShadowDistance=35});sun.Transform.EulerAngles=new(20,-65,0);

  scene.CreateGameObject("Sand lighting").AddComponent(new SkyEnvironment{SkyMode=SkyMode3D.Procedural,Quality=GraphicsQuality.Fast,OverrideAmbient=true,AmbientIntensity=.28f,Exposure=1,Contrast=1.04f});

  var probe=scene.CreateGameObject("Blue contact probe - video comparison");probe.Transform.LocalScale=new(1.7f);probe.Transform.WorldPosition=new(0,.7f,0);probe.AddComponent(new MeshRenderer{UsePrimitive=true,Primitive=PrimitiveMeshType.Sphere,Material=new(){BaseColor=new(.07f,.34f,.7f,1),Roughness=.32f}});probe.AddComponent(new SandLabProbe3D());

  var canvas=scene.CreateGameObject("Surface lab controls");canvas.AddComponent(new UiCanvas());canvas.AddComponent(new UiText{Text="INTERACTIVE SAND / SURFACE LAB\nWASD move  |  TAB twin tyre tracks  |  F6 clear\nI J K L camera  |  U O zoom\nPersistent displacement / compressed trough / raised shoulders",FontSize=18,Offset=new(24,22),Color=new(.95f,.92f,.83f,1),ShadowColor=new(0,0,0,.8f)});

  var nav=scene.CreateGameObject("Vehicle lab button");nav.SetParent(canvas,false);nav.AddComponent(new UiWidget{Kind=UiWidgetKind.Button,Anchor=UiAnchor.TopRight,Offset=new(-24,20),Size=new(170,40),Label="VEHICLE TEST",FontSize=18,OrderInLayer=30});

  var module=new EventModuleDefinition{Name="Sand Lab Navigation"};module.Rules.Add(new EventRuleDefinition{DisplayName="Open real vehicle test",Conditions=[new VisualInstruction{Id="ui.buttonClicked",Arguments=new(){{"target",EventValue.String("Vehicle lab button")}}}],Actions=[new VisualInstruction{Id="scene.load",Arguments=new(){{"path",EventValue.String("Scenes/SandVehicleLab.bytescene")}}}]});

  const string navFile="Assets/Events/SandLab.byteevents";Directory.CreateDirectory(Path.Combine(root,"Assets/Events"));new EventModuleSerializer().Save(module,Path.Combine(root,navFile));nav.AddComponent(new EventModuleComponent()).AddResolvedModule(new(navFile),module);

  DesertTerrainTests.Check(UiLayout.IsVisible(nav)&&nav.GetComponent<UiWidget>()!.Contains(new(1130,40),new(1280,720)),"Vehicle test button is visible and clickable inside the UI canvas");

  var scenePath=Path.Combine(root,"Scenes/SandLab.bytescene");if(_prepare)_project!.Scenes.Save(scene,scenePath);

  Scenes.LoadScene(scene);Tick(scene,new(.5f),false);Capture(scene,Path.Combine(root,"Preview/sand-lab-before.png"),false);

  float untouched=sand.HeightAt(128,128);

  var timer=Stopwatch.StartNew();for(int i=0;i<480;i++){float a=i*MathF.Tau/480,b=(i+1)*MathF.Tau/480;sand.Stamp(new(MathF.Sin(a)*3,0,MathF.Cos(a)*3),new(MathF.Sin(b)*3,0,MathF.Cos(b)*3),.85f);}timer.Stop();

  sand.TrySampleLocal(0,3,out float trough,out _);sand.TrySampleLocal(0,4.1f,out float rim,out _);

  DesertTerrainTests.Check(trough<-.15f&&rim>.025f,"Contact field produces real depressed geometry and raised shoulders");

  DesertTerrainTests.Check(Math.Abs(sand.HeightAt(128,128)-untouched)<1e-6,"Sand beyond the contact footprint remains unchanged");

  probe.Transform.WorldPosition=new(0,.5f,3);Capture(scene,Path.Combine(root,"Preview/sand-lab-groove.png"),false);

  // Compare collision samples with the exact RG16 height consumed by the vertex shader.

  var pixels=typeof(InteractiveSand3D).GetField("_pixels",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.GetValue(sand) as byte[];

  int cell=152*257+128,j=cell*4;float gpu=((pixels![j]<<8)|pixels[j+1])/65535f*.6f-.4f;

  DesertTerrainTests.Check(Math.Abs(gpu-sand.HeightAt(128,152))<.00002f,"GPU displacement and CPU collision share the same encoded height");

  // Stationary surface frames must perform no texture updates or geometry rebuilds.

  int count=sand.UploadCount;using var fb=new ByteEngine.Editor.SceneFramebuffer();var frameTimes=new List<double>();

  for(int i=0;i<90;i++){timer.Restart();fb.RenderGame(Renderer,Renderer3D,scene,ByteEngine.Editor.EditorMode.Play,1280,720,1280,720);GL.Finish();timer.Stop();if(i>=10)frameTimes.Add(timer.Elapsed.TotalMilliseconds);}

  DesertTerrainTests.Check(sand.UploadCount==count,"Unchanged sand performs zero texture uploads across90 frames");

  sand.Stamp(new(-6,0,0),new(-5.9f,0,0),.3f);fb.RenderGame(Renderer,Renderer3D,scene,ByteEngine.Editor.EditorMode.Play,1280,720,1280,720);GL.Finish();

  DesertTerrainTests.Check(sand.LastUploadBytes<10000,"Small contact uploads a bounded rectangle rather than the whole texture");

  frameTimes.Sort();File.WriteAllText(Path.Combine(root,"Preview/sand-lab-benchmark.txt"),$"GPU: {GL.GetString(StringName.Renderer)}\n1280x720 static displaced scene including explicit GPU completion: median{frameTimes[frameTimes.Count/2]:0.00}ms p95{frameTimes[(int)(frameTimes.Count*.95)]:0.00}ms\nlast contact upload{sand.LastUploadBytes}bytes\nNo claim of editor FPS or granular particle simulation.\n");

  sand.ResetTracks();DesertTerrainTests.Check(Math.Abs(sand.HeightAt(128,128)-untouched)<1e-6,"Reset restores the uncompressed surface");

  probe.Transform.WorldPosition=new(0,.7f,0);for(int i=0;i<30;i++)Tick(scene,new(.5f),false,Key.W);

  DesertTerrainTests.Check(probe.Transform.WorldPosition.Z<-1,"Native WASD input moves the contact probe");

  sand.ResetTracks();probe.Transform.WorldPosition=new(0,.7f,0);

  // Persist the pristine scene; test grooves are intentionally not pre-painted into it.

  if(_prepare)_project!.Scenes.Save(scene,scenePath);

  Tick(scene,new(1130f/1280,40f/720),true);DesertTerrainTests.Check(scene.LoadRequested=="Scenes/SandVehicleLab.bytescene","Clicking Vehicle Test requests the real vehicle workshop scene");scene.ClearHostRequests();

  Scenes.UnloadScene();_project!.Dispose();Console.WriteLine("Sand lab geometry, collision, GPU displacement and upload checks passed.");Close();

 }

}
