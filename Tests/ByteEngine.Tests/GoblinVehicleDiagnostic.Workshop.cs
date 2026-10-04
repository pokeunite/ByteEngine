using GoblinScrapper.Construction;
using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.Scene;
using ByteEngine.Editor;
using OpenTK.Graphics.OpenGL4;
namespace ByteEngine.Tests;
internal sealed partial class GoblinVehicleDiagnostic
{
 private void RunHudBraceWorkshop(Scene scene,VehicleBuilder3D builder)
 {
  Scenes.LoadScene(scene);var c=VehiclePartCatalog.Load(Path.Combine(_project!.ProjectRoot,"Assets/GarageUI/parts-catalog.json"));
  string output=Path.Combine(Environment.CurrentDirectory,"Designs/WorkshopHud/screenshots");Directory.CreateDirectory(output);
  Vector2 Project(Vector3 local){var camera=scene.ActiveCamera!;var clip=Vector4.Transform(new Vector4(builder.Transform.WorldPosition+local,1),camera.GetViewMatrix()*camera.GetProjectionMatrix(1280f/720));return new((clip.X/clip.W+1)/2,(1-clip.Y/clip.W)/2);}
  void Idle(){for(int i=0;i<30;i++)TickInput(scene,new(.98f,.5f),[]);}
  Idle();Screenshot(scene,Path.Combine(output,"workshop-empty.png"));
  int front=builder.PlacePartAtConnector("goblin_double_wooden_block",0,"Front"),rear=builder.PlacePartAtConnector("goblin_double_wooden_block",0,"Rear");Assert(front>0&&rear>0,"Brace frame beams failed");Idle();
  var a=builder.Assembly!;
  string Top(int id)=>c[a.Parts[id].File].Sockets.Where(s=>Vector3.Transform(s.Normal,a.Parts[id].Rotation).Y>.99f).OrderBy(s=>Math.Abs(s.Position.Z)).First().Name;
  var first=new BraceEndpoint(front,Top(front));var second=new BraceEndpoint(rear,Top(rear));
  Assert(builder.SelectBuildPart("goblin_brace"),"Brace missing from palette");
  var p1=Project(a.BracePoint(first));TickInput(scene,p1,[]);TickInput(scene,p1,[],true);TickInput(scene,p1,[]);
  var p2=Project(a.BracePoint(second));TickInput(scene,p2,[]);Screenshot(scene,Path.Combine(output,"brace-preview.png"));TickInput(scene,p2,[],true);TickInput(scene,p2,[]);
  Assert(a.Braces.Count==1,"Two-click brace placement failed: "+builder.FreePlacementIssue);Idle();Screenshot(scene,Path.Combine(output,"brace-built.png"));
  Assert(builder.UndoBuild()&&builder.Assembly!.Braces.Count==0,"Brace undo failed");Assert(builder.RedoBuild()&&builder.Assembly!.Braces.Count==1,"Brace redo failed");
  string save=Path.Combine(_project.ProjectRoot,"Saves/vehicle-build.json");byte[]? previous=File.Exists(save)?File.ReadAllBytes(save):null;
  try{builder.SaveBuild();Assert(builder.RemoveAssemblyPart(-builder.Assembly!.Braces.Keys.Single()-1),"Saved brace removal failed");builder.LoadBuild();Assert(builder.Assembly!.Braces.Count==1,"Brace disk reload failed");}
  finally{if(previous!=null)File.WriteAllBytes(save,previous);else if(File.Exists(save))File.Delete(save);}
  a=builder.Assembly!;var brace=a.Braces.Values.Single();var mid=Vector3.Lerp(a.BracePoint(brace.A),a.BracePoint(brace.B),.2f);
  builder.SetEraseTool(true);var pointer=Project(mid);TickInput(scene,pointer,[]);Assert(builder.HoveredBlockId < -1,"Pointer did not pick brace span: "+builder.HoveredBlockId);TickInput(scene,pointer,[],true);TickInput(scene,pointer,[]);Assert(a.Braces.Count==0&&a.Parts.Count==3,"Brace erase removed a supporting beam");Assert(builder.UndoBuild(),"Brace erase undo failed");builder.SetEraseTool(false);
  Console.WriteLine("PASS: native brace model import; two-click endpoints; preview; undo/redo; disk save/load; pointer span deletion.");
  // Toolbar copy, move and rotate must act on a subsequent world click.
  builder.RestoreAssembly(new VehicleAssembly(c).ToJson());int testBeam=builder.PlacePartAtConnector("goblin_double_wooden_block",0,"Front");Idle();
  var targetPart=builder.Assembly!.Parts[testBeam];var blockPointer=Project(targetPart.Position);
  TickInput(scene,new(682f/1280,24f/720),[],true);TickInput(scene,blockPointer,[]);TickInput(scene,blockPointer,[]);Console.WriteLine("Toolbar world hover="+builder.HoveredBlockId+" point="+blockPointer+" feedback="+builder.FreePlacementIssue);TickInput(scene,blockPointer,[],true);TickInput(scene,blockPointer,[]);
  Assert(scene.GameObjects.Single(o=>o.Name=="Selected block").GetComponent<ByteEngine.Core.Graphics.UiText>()!.Text==c["goblin_double_wooden_block"].Label,"Toolbar copy selection failed: "+scene.GameObjects.Single(o=>o.Name=="Selected block").GetComponent<ByteEngine.Core.Graphics.UiText>()!.Text);
  TickInput(scene,new(558f/1280,24f/720),[],true);TickInput(scene,blockPointer,[]);TickInput(scene,blockPointer,[]);Console.WriteLine("Toolbar world hover="+builder.HoveredBlockId+" point="+blockPointer+" feedback="+builder.FreePlacementIssue);TickInput(scene,blockPointer,[],true);TickInput(scene,blockPointer,[]);
  var topPointer=Project(new(0,.252f,0));TickInput(scene,topPointer,[]);TickInput(scene,topPointer,[],true);TickInput(scene,topPointer,[]);
  Assert(builder.Assembly.Parts[testBeam].ParentConnector=="Top","Toolbar move branch failed");Assert(builder.UndoBuild(),"Move toolbar undo failed");
  builder.RestoreAssembly(new VehicleAssembly(c).ToJson());builder.SelectBuildPart("goblin_double_wooden_block");Idle();topPointer=Project(new(0,.252f,0));
  TickInput(scene,new(601f/1280,24f/720),[],true);TickInput(scene,topPointer,[]);TickInput(scene,topPointer,[]);TickInput(scene,topPointer,[],true);TickInput(scene,topPointer,[]);
  Assert(builder.Assembly!.Parts.Count==2&&builder.Assembly.Parts.Values.Single(p=>p.Id!=0).OwnConnector.Contains("Branch_0_"),"Toolbar crosswise beam rotation failed");
  Console.WriteLine("PASS: toolbar copy, branch move and queued crosswise beam rotation.");
  builder.RestoreAssembly(ContraptionTests.Build(c).ToJson());builder.SelectBuildPart("goblin_double_wooden_block");Idle();
  Screenshot(scene,Path.Combine(output,"workshop-720p.png"));Screenshot(scene,Path.Combine(output,"workshop-1080p.png"),1920,1080);Screenshot(scene,Path.Combine(output,"workshop-16x10.png"),1280,800);Screenshot(scene,Path.Combine(output,"workshop-ultrawide.png"),2560,1080);
  TickInput(scene,new(602f/1280,24f/720),[]);Screenshot(scene,Path.Combine(output,"tool-tooltip.png"));
  TickInput(scene,new(195f/1280,665f/720),[],true);TickInput(scene,new(.98f,.5f),[]);Screenshot(scene,Path.Combine(output,"flight-tray.png"));
  Assert(builder.SelectBuildPart("goblin_double_wooden_block"),"Beam selection");Assert(builder.BeginDriving(),"New HUD simulation start failed");
  for(int i=0;i<120;i++)TickInput(scene,new(.98f,.5f),[Key.W]);Screenshot(scene,Path.Combine(output,"running.png"));builder.ReturnToBuild();Idle();
  Assert(builder.Building,"Return to build failed");
  var suspended=new VehicleAssembly(c);var spring=c.Parts.Values.First(d=>d.ReferenceId==16);var longBeam=c.Parts.Values.First(d=>d.ReferenceId==1);var wheel=c.Parts.Values.First(d=>d.ReferenceId==46);
  int springId=suspended.AddAtSocket(spring.File,0,"Bottom",spring.Sockets.First(s=>s.Bone=="Root").Name);
  int hangingBeam=suspended.AddAtSocket(longBeam.File,springId,spring.Sockets.First(s=>s.Bone=="Moving"&&!s.Name.StartsWith("SOCKET_Surface_")).Name,longBeam.Sockets[0].Name);
  Assert(springId>0&&hangingBeam>0,"Suspended wheel fixture supports");
  foreach(int side in new[]{-1,1}){
   var part=suspended.Parts[hangingBeam];var socket=longBeam.Sockets.Where(s=>Vector3.Transform(s.Normal,part.Rotation).X*side>.99f).OrderBy(s=>(part.Position+Vector3.Transform(s.Position,part.Rotation)).Y).First();
   Assert(suspended.AddAtSocket(wheel.File,hangingBeam,socket.Name,wheel.Sockets.First(s=>s.Bone=="Root").Name)>0,"Suspended wheel placement");
  }
  builder.RestoreAssembly(suspended.ToJson());Idle();
  float Lowest()=>builder.Assembly!.Parts.Values.SelectMany(p=>c[p.File].Clearance.Select(b=>b.Transform(p.Position,p.Rotation).Min.Y)).Min()+builder.Transform.WorldPosition.Y;
  Assert(Lowest()>.05f&&builder.Transform.WorldPosition.Y>1.25f,"Build mode clips suspended wheels through floor");
  var movingSocket=spring.Sockets.First(s=>s.Bone=="Moving"&&!s.Name.StartsWith("SOCKET_Surface_")).Name;
  int movingBrace=builder.AddAssemblyBrace(springId,movingSocket,hangingBeam,longBeam.Sockets[1].Name);Assert(movingBrace>0,"Suspension brace native placement");
  Screenshot(scene,Path.Combine(output,"suspension-brace-036-build.png"));
  var before=builder.Assembly.ToJson();Assert(builder.BeginDriving(),"Suspension brace run start");for(int i=0;i<180;i++)TickInput(scene,new(.98f,.5f),[]);builder.ReturnToBuild();Idle();
  Assert(Lowest()>.05f&&builder.Assembly.ToJson()==before,"Return to build changed suspension layout or clipped wheels");
  Screenshot(scene,Path.Combine(output,"suspension-brace-036-returned.png"));Console.WriteLine("PASS: native suspension/wooden beam brace with powered wheels; build floor clearance and authored layout survive run/build.");
  builder.RestoreAssembly(File.ReadAllText("Designs/GearWorkshop/reducer.json"));Idle();
  builder.OrbitBuildCamera(MathF.PI/2-.65f,-.58f);Idle();Screenshot(scene,Path.Combine(output,"cogs-035.png"));
  var timer=System.Diagnostics.Stopwatch.StartNew();for(int i=0;i<120;i++)TickInput(scene,new(.98f,.5f),[]);timer.Stop();
  Console.WriteLine($"BUILD PROFILE: 9-block gear assembly, 120 idle updates, mean CPU update {timer.Elapsed.TotalMilliseconds/120:0.000} ms (rendering excluded).");
  using(var fb=new SceneFramebuffer()){
   var camera=scene.ActiveCamera!;var forward=camera.Transform.Forward;var view=new EditorCamera3D{Position=camera.Transform.WorldPosition,Yaw=MathF.Atan2(forward.Z,forward.X)*180/MathF.PI,Pitch=MathF.Asin(forward.Y)*180/MathF.PI,FieldOfView=camera.FieldOfView};
   for(int frame=0;frame<3;frame++){fb.Render(Renderer,Renderer3D,scene,EditorMode.Play,new EditorCamera(),view,true,1280,720,1280,720,drawGrid3D:false);GL.Finish();}
   timer.Restart();for(int frame=0;frame<30;frame++){TickInput(scene,new(.98f,.5f),[]);fb.Render(Renderer,Renderer3D,scene,EditorMode.Play,new EditorCamera(),view,true,1280,720,1280,720,drawGrid3D:false);GL.Finish();}timer.Stop();
   Console.WriteLine($"BUILD PROFILE: native 1280x720 scene update plus synchronized render, mean {timer.Elapsed.TotalMilliseconds/30:0.00} ms, {30000/timer.Elapsed.TotalMilliseconds:0.0} offscreen frames/s (editor panels excluded).");
   var lights=scene.GameObjects.SelectMany(o=>o.Components).OfType<ByteEngine.Core.Graphics.DirectionalLight>().ToArray();
   foreach(int quality in new[]{2048,0}){
    foreach(var light in lights){light.ShadowResolution=quality==0?1024:quality;light.CastShadows=quality!=0;}
    for(int frame=0;frame<3;frame++){fb.Render(Renderer,Renderer3D,scene,EditorMode.Play,new EditorCamera(),view,true,1280,720,1280,720,drawGrid3D:false);GL.Finish();}
    timer.Restart();for(int frame=0;frame<30;frame++){TickInput(scene,new(.98f,.5f),[]);fb.Render(Renderer,Renderer3D,scene,EditorMode.Play,new EditorCamera(),view,true,1280,720,1280,720,drawGrid3D:false);GL.Finish();}timer.Stop();
    Console.WriteLine($"BUILD RENDER BREAKDOWN: shadows={quality}, mean {timer.Elapsed.TotalMilliseconds/30:0.00} ms.");
   }
   foreach(var light in lights){light.ShadowResolution=1024;light.CastShadows=true;}

  }
  var occupied=builder.Assembly!.OccupiedSockets();foreach(var part in builder.Assembly.Parts.Values)foreach(var socket in c[part.File].Sockets)Assert(occupied.Contains((part.Id,socket.Name))==builder.Assembly.IsSocketOccupied(part.Id,socket.Name),"Indexed connected occupancy differs");
  Assert(builder.BeginDriving(),"Cog simulation start");for(int i=0;i<90;i++)TickInput(scene,new(.98f,.5f),[Key.F]);Screenshot(scene,Path.Combine(output,"cogs-035-running.png"));builder.ReturnToBuild();Idle();
  Screenshot(scene,Path.Combine(output,"cogs-035-returned.png"));
Console.WriteLine("PASS: compact HUD native renders at 720p,1080p,16:10,ultrawide; tool tooltip; flight category; run/build transition.");
 }
}
