using System.Numerics;
using System.Reflection;
using System.Text.Json;
using ByteEngine.Core;
using ByteEngine.Core.AI;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Blueprints;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Navigation;
using ByteEngine.Core.Runtime;
using ByteEngine.Core.InputSystem;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Variables;
using ByteEngine.Core.VisualLogic;
using KarlsonDemo.Plugin;
using OpenTK.Graphics.OpenGL4;
internal static class Program
{
 public const string Root="C:/Users/codex/Documents/palclone";
 [STAThread] static int Main(string[] args){try{if(args.Contains("--verify-player-startup")){MiningPlayerStartupCheck.Run();return 0;}using var app=new MiningApp(args);app.Run();return 0;}catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
internal sealed class MiningApp(string[] args):ByteEngineApplication(args.Contains("--hd")?1920:args.Contains("--wide")?1600:1280,args.Contains("--hd")?1080:args.Contains("--tall")?800:720,"Alien mining — prototype check")
{
 GameProjectRuntime? runtime;Scene? scene;int frame;MiningOperation3D? op;float combatTime;bool captured;
 protected override void OnEngineStart()
 {
  Console.WriteLine("PLUGIN IMAGE "+typeof(MiningOperation3D).Assembly.Location);runtime=new(Program.Root+"/palclone.byteproject",Console.WriteLine);scene=runtime.LoadStartupScene();
  if(args.Contains("--canyon-author")){MiningCanyonAuthor.Build(scene,runtime);Close();return;}
  if(args.Contains("--ambush-author")){MiningAmbushCheck.Author(scene,runtime);Close();return;}
  if(args.Contains("--refinery-audio-author")){MiningRefineryAudioCheck.Author(scene,runtime);Close();return;}
  if(args.Contains("--press-audio-author")){MiningPressAudioCheck.Author(scene,runtime);Close();return;}
  if(args.Contains("--effects-author")){MiningEffectsAuthor.Build(scene,runtime);Close();return;}
  if(args.Contains("--defence-visual-author")){MiningDefenceVisualCheck.Author(scene,runtime);Close();return;}
  if(args.Contains("--verify-defence-models")){MiningDefenceModelCheck.Run(runtime);Close();return;}
  if(args.Contains("--full-floor-author")){MiningFloorAuthor.Build(scene,runtime);Close();return;}
  if(args.Contains("--ground-sky-author")){MiningGroundSkyAuthor.Build(scene,runtime);Close();return;}
  if(args.Contains("--camp-author")){MiningCampAuthor.Build(scene,runtime);Close();return;}
  if(args.Contains("--author")){MiningAuthor.Build(scene,runtime);Close();return;}
  if(args.Contains("--upgrade-author")){MiningModelAuthor.Build(scene,runtime);MiningUiAuthor.Build(scene,runtime);Close();return;}
  if(args.Contains("--third-person-author")){MiningThirdPersonAuthor.Build(scene,runtime);Close();return;}
  if(args.Contains("--streamline-author")){MiningStreamlineAuthor.Build(scene,runtime);Close();return;}
  if(args.Contains("--flow-author")){MiningFlowAuthor.Build(scene,runtime);Close();return;}
  if(args.Contains("--progression-author")){MiningProgressionAuthor.Build(scene,runtime);Close();return;}
  if(args.Contains("--polish-author")){MiningPolishAuthor.Build(scene,runtime);Close();return;}
  if(args.Contains("--audio-author")){MiningAudioAuthor.Build(scene,runtime);Close();return;}
  if(args.Contains("--press-author")){MiningPressAuthor.Build(scene,runtime);Close();return;}
  if(args.Contains("--refinery-author")){MiningRefineryAuthor.Build(scene,runtime);Close();return;}
  if(args.Contains("--terminal-author")){MiningTerminalAuthor.Build(scene,runtime);Close();return;}
  if(args.Contains("--ui-author")){MiningUiAuthor.Build(scene,runtime);Close();return;}
  foreach(var menu in scene.GameObjects.SelectMany(o=>o.Components.OfType<MiningPauseMenu3D>()))menu.ShowMainMenuOnStart=args.Contains("--main-menu-preview");
  op=scene.GameObjects.SelectMany(o=>o.Components).OfType<MiningOperation3D>().Single();op.PersistProgress=false;foreach(var camp in scene.GameObjects.SelectMany(o=>o.Components).OfType<MiningCampLayout3D>())camp.PersistProgress=false;foreach(var planner in scene.GameObjects.SelectMany(o=>o.Components).OfType<MiningDefensePlanner3D>())planner.PersistProgress=false;
  foreach(var trade in scene.GameObjects.SelectMany(o=>o.Components).OfType<MiningTrade3D>())trade.PersistProgress=false;
  if(args.Contains("--verify-camp")){MiningCampVerify.Run(scene,op);Close();return;}
  if(args.Contains("--verify-canyon")){MiningCanyonVerify.Run(scene,op);Close();return;}
  if(args.Contains("--verify-ambush")){MiningAmbushCheck.Verify(scene,op);Close();return;}
  if(args.Contains("--verify-spiders")){MiningSpiderCheck.Verify(scene,op);Close();return;}
  if(args.Contains("--verify-refinery-audio")){MiningRefineryAudioCheck.Verify(scene,op);Close();return;}
  if(args.Contains("--verify-press-audio")){MiningPressAudioCheck.Verify(scene,op);Close();return;}
  foreach(var menu in scene.GameObjects.SelectMany(o=>o.Components.OfType<MiningPauseMenu3D>()))menu.PersistSettings=false;
  if(args.Contains("--verify-machine-menus")){MiningMachineMenuCheck.Run(scene,op);Close();return;}
  if(args.Contains("--verify-main-menu")){MiningMainMenuCheck.Run(scene,op);Close();return;}
  if(args.Contains("--verify-pause-audio")){MiningPauseAudioCheck.Run(scene,op);Close();return;}
  if(args.Contains("--verify-projectile-music")){MiningProjectileMusicCheck.Run(scene,op);Close();return;}
  if(args.Contains("--verify-effects")){MiningEffectsCheck.Run(scene,op);Close();return;}
  if(args.Contains("--verify-defence-visuals")){MiningDefenceVisualCheck.Verify(scene,op);Close();return;}
  if(args.Contains("--verify-audio")){MiningAudioVerify.Run(scene,op);Close();return;}
  if(args.Contains("--verify-press")){MiningPressVerify.Run(scene,op);Close();return;}
  if(args.Contains("--verify-refinery")){MiningRefineryVerify.Run(scene,op);Close();return;}
  if(args.Contains("--verify-terminal")){MiningTerminalVerify.Run(scene,op);Close();return;}
  if(args.Contains("--verify")){MiningVerify.Run(scene,runtime,op);Close();return;}
  if(args.Contains("--verify-third-person")){MiningThirdPersonVerify.Run(scene);Close();return;}
  if(args.Contains("--verify-ui")){MiningUiVerify.Run(scene);Close();return;}
  if(args.Contains("--verify-flow")){MiningFlowVerify.Run(scene);Close();return;}
  if(args.Contains("--defence-model-preview"))MiningDefenceVisualCheck.Preview(scene,op);
  if(args.Contains("--spider-model-preview"))MiningSpiderCheck.Preview(scene,op);
  if(args.Contains("--effects-preview"))MiningEffectsCheck.Preview(scene,op);
  if(args.Contains("--canyon-preview"))
  {
   var actor=scene.FindGameObject(op.PlayerObjectId)!;actor.GetComponent<PlayerController3D>()!.Enabled=false;actor.GetComponent<MiningThirdPerson3D>()!.Enabled=false;actor.GetComponent<CameraBoom3D>()!.Enabled=false;var camera=scene.ActiveCamera!;camera.GameObject.SetParent(null,true);var eye=new Vector3(53,37,48);camera.Transform.WorldPosition=eye;Matrix4x4.Invert(Matrix4x4.CreateLookAt(eye,new Vector3(8,1,-35),Vector3.UnitY),out var pose);camera.Transform.WorldRotation=Quaternion.CreateFromRotationMatrix(pose);camera.Orthographic=false;camera.FieldOfView=48;camera.FarClip=500;foreach(var name in new[]{"HUD canvas","Mining HUD","Mining business interface"})if(scene.FindGameObject(name) is {} hud)hud.Active=false;
  }
  if(args.Contains("--overview"))
  {
   foreach(var o in scene.GameObjects.Where(o=>o.GetComponent<Camera3D>()!=null)){foreach(var c in o.Components.Where(c=>c is TopDownCamera3D or ThirdPersonCamera3D))c.Enabled=false;o.GetComponent<Camera3D>()!.ActiveGameCamera=false;}
   var oview=scene.CreateGameObject("Temporary overview");oview.AddComponent(new Camera3D{Orthographic=true,OrthographicWidth=54,ActiveGameCamera=true});var eye=new Vector3(18,53,41);oview.Transform.WorldPosition=eye;Matrix4x4.Invert(Matrix4x4.CreateLookAt(eye,new(0,0,-1),Vector3.UnitY),out var m);oview.Transform.WorldRotation=Quaternion.CreateFromRotationMatrix(m);
   foreach(var name in new[]{"HUD canvas","Mining HUD","Mining business interface"})if(scene.FindGameObject(name) is {} hud)hud.Active=false;
  }
  Scenes.LoadScene(scene);
  if(args.Contains("--ground-sky-preview")||args.Contains("--ground-sky-gameplay"))
  {
   var actor=scene.FindGameObject(op.PlayerObjectId)!;actor.GetComponent<PlayerController3D>()!.Enabled=false;actor.GetComponent<MiningThirdPerson3D>()!.Enabled=false;actor.GetComponent<CameraBoom3D>()!.Enabled=false;var camera=scene.ActiveCamera!;camera.GameObject.SetParent(null,true);
   var eye=args.Contains("--ground-sky-gameplay")?new Vector3(11,2.6f,5):new Vector3(10,4,9);var target=args.Contains("--ground-sky-gameplay")?new Vector3(9,1,-18):new Vector3(3,4.8f,-8);camera.Transform.WorldPosition=eye;Matrix4x4.Invert(Matrix4x4.CreateLookAt(eye,target,Vector3.UnitY),out var pose);camera.Transform.WorldRotation=Quaternion.CreateFromRotationMatrix(pose);camera.FieldOfView=args.Contains("--ground-sky-gameplay")?70:80;actor.Transform.WorldPosition=new(10,0,2);
   if(!args.Contains("--ground-sky-gameplay"))foreach(var name in new[]{"HUD canvas","Mining HUD","Mining business interface"})if(scene.FindGameObject(name) is {} hud)hud.Active=false;
  }
  if(args.Contains("--camp-preview")){var camp=scene.FindGameObject(op.PlayerObjectId)!.GetComponent<MiningCampLayout3D>()!;for(int i=0;i<4;i++){typeof(Time).GetMethod("Update",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[.05d]);typeof(Scene).GetMethod("UpdateInternal",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(scene,null);}camp.EnterEdit();}
  if(args.Contains("--defence-demo"))
  {
   var trader=scene.GameObjects.SelectMany(o=>o.Components).OfType<MiningTrade3D>().Single();typeof(MiningOperation3D).GetProperty("Credits")!.SetValue(op,1000000);for(int pass=0;pass<100;pass++)foreach(var node in trader.Research)if(trader.CanBuyResearch(node.Id))trader.BuyResearch(node.Id);
   var builder=scene.FindGameObject(op.PlayerObjectId)!.GetComponent<MiningDefensePlanner3D>()!;foreach(var pair in new[]{(MiningDefenseKind.Sentry,new Vector3(3,0,0)),(MiningDefenseKind.Repeater,new Vector3(-3,0,0)),(MiningDefenseKind.Mortar,new Vector3(3,0,4)),(MiningDefenseKind.Mine,new Vector3(-3,0,4))}){typeof(MiningDefensePlanner3D).GetProperty("SelectedKind")!.SetValue(builder,pair.Item1);if(!builder.TryPlace(pair.Item2))throw new Exception("Defence preview placement failed: "+pair.Item1);}
   typeof(MiningOperation3D).GetProperty("Credits")!.SetValue(op,1240);var actor=scene.FindGameObject(op.PlayerObjectId)!;actor.GetComponent<CameraBoom3D>()!.Enabled=false;actor.GetComponent<MiningThirdPerson3D>()!.Enabled=false;var cam=scene.ActiveCamera!;cam.GameObject.SetParent(null,true);var eye=new Vector3(10,9,15);cam.Transform.WorldPosition=eye;Matrix4x4.Invert(Matrix4x4.CreateLookAt(eye,new(0,.5f,2),Vector3.UnitY),out var matrix);cam.Transform.WorldRotation=Quaternion.CreateFromRotationMatrix(matrix);
  }
  if(args.Contains("--bucket-preview")||args.Contains("--press-preview"))
  {
   var previewUi=scene.GameObjects.SelectMany(o=>o.Components).OfType<MiningBusinessUi3D>().Single();var press=scene.FindGameObject(previewUi.AmmoMachineObjectId)!;var player=scene.FindGameObject(op.PlayerObjectId)!;player.Transform.WorldPosition=new(18,0,15);player.GetComponent<PlayerController3D>()!.Enabled=false;player.GetComponent<CameraBoom3D>()!.Enabled=false;player.GetComponent<MiningThirdPerson3D>()!.Enabled=false;
   var camera=scene.ActiveCamera!;camera.GameObject.SetParent(null,true);var eye=press.Transform.WorldPosition+new Vector3(4.5f,3.5f,-6.2f);camera.Transform.WorldPosition=eye;Matrix4x4.Invert(Matrix4x4.CreateLookAt(eye,press.Transform.WorldPosition+new Vector3(0,1.65f,-1.0f),Vector3.UnitY),out var pose);camera.Transform.WorldRotation=Quaternion.CreateFromRotationMatrix(pose);camera.FieldOfView=43;foreach(var name in new[]{"HUD canvas","Mining HUD","Mining business interface"})if(scene.FindGameObject(name) is {} hud)hud.Active=false;press.GetComponent<MiningAmmoBucket3D>()!.AutoPickup=false;scene.GameObjects.SelectMany(o=>o.Components).OfType<MiningTrade3D>().Single().Tick(25);
  }
  if(args.Contains("--refinery-preview"))
  {
   var station=scene.FindGameObject("Refinery station")!;var actor=scene.FindGameObject(op.PlayerObjectId)!;actor.Transform.WorldPosition=new(18,0,15);actor.GetComponent<PlayerController3D>()!.Enabled=false;actor.GetComponent<CameraBoom3D>()!.Enabled=false;actor.GetComponent<MiningThirdPerson3D>()!.Enabled=false;
   foreach(var name in new[]{"HUD canvas","Mining HUD","Mining business interface"})if(scene.FindGameObject(name) is {} hud)hud.Active=false;
   var camera=scene.ActiveCamera!;camera.GameObject.SetParent(null,true);var eye=station.Transform.WorldPosition+new Vector3(-7,6.3f,-9);camera.Transform.WorldPosition=eye;Matrix4x4.Invert(Matrix4x4.CreateLookAt(eye,station.Transform.WorldPosition+new Vector3(-.8f,1.65f,-.3f),Vector3.UnitY),out var pose);camera.Transform.WorldRotation=Quaternion.CreateFromRotationMatrix(pose);camera.Orthographic=true;camera.OrthographicWidth=10;
   var trade=op.GameObject.GetComponent<MiningTrade3D>()!;trade.Enabled=false;typeof(MiningOperation3D).GetProperty("RefinedOre")!.SetValue(op,op.CargoCapacity/2);
   typeof(Time).GetMethod("Update",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[.05d]);typeof(Scene).GetMethod("UpdateInternal",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(scene,null);
   typeof(MiningOperation3D).GetProperty("RefinedOre")!.SetValue(op,op.CargoCapacity/2+24);
  }
  if(args.Contains("--terminal-preview")||args.Contains("--massive-drill-preview"))
  {
   bool terminal=args.Contains("--terminal-preview");var station=terminal?scene.FindGameObject("Company tent")!:scene.FindGameObject(op.DrillObjectId)!;var actor=scene.FindGameObject(op.PlayerObjectId)!;actor.Transform.WorldPosition=terminal?new Vector3(18,0,15):station.Transform.WorldPosition+new Vector3(6,0,-7);actor.GetComponent<PlayerController3D>()!.Enabled=false;actor.GetComponent<CameraBoom3D>()!.Enabled=false;actor.GetComponent<MiningThirdPerson3D>()!.Enabled=false;
   foreach(var name in new[]{"HUD canvas","Mining HUD","Mining business interface"})if(scene.FindGameObject(name) is {} hud)hud.Active=false;
   var camera=scene.ActiveCamera!;camera.GameObject.SetParent(null,true);var eye=station.Transform.WorldPosition+(terminal?new Vector3(3.6f,3.2f,-12):new Vector3(-18,13,-24));camera.Transform.WorldPosition=eye;Matrix4x4.Invert(Matrix4x4.CreateLookAt(eye,station.Transform.WorldPosition+(terminal?new Vector3(0,1.64f,0):new Vector3(-1,3.8f,0)),Vector3.UnitY),out var pose);camera.Transform.WorldRotation=Quaternion.CreateFromRotationMatrix(pose);camera.Orthographic=true;camera.OrthographicWidth=terminal?9.6f:25;
   if(!terminal){op.Enabled=false;op.GameObject.GetComponent<MiningTrade3D>()!.Enabled=false;typeof(MiningOperation3D).GetProperty("Phase")!.SetValue(op,MiningPhase.Drilling);}
  }
  if(scene.GameObjects.SelectMany(o=>o.Components).OfType<MiningBusinessUi3D>().FirstOrDefault() is {} ui){if(args.Contains("--ui-demo")){typeof(MiningOperation3D).GetProperty("Credits")!.SetValue(op,1240);typeof(MiningOperation3D).GetProperty("RefinedOre")!.SetValue(op,80);var trade=scene.GameObjects.SelectMany(o=>o.Components).OfType<MiningTrade3D>().Single();typeof(MiningOperation3D).GetProperty("Phase")!.SetValue(op,MiningPhase.Drilling);for(int i=0;i<60;i++)trade.Tick(6);typeof(MiningOperation3D).GetProperty("Phase")!.SetValue(op,MiningPhase.Preparation);}if(args.Contains("--credits-ui")||args.Contains("--market-ui")||args.Contains("--research-ui")||args.Contains("--company-ui")){var p=scene.FindGameObject(op.PlayerObjectId)!;p.Transform.WorldPosition=scene.FindGameObject(ui.TerminalObjectId)!.Transform.WorldPosition+new Vector3(0,0,-1);}if(args.Contains("--ammo-ui"))ui.Open(MiningScreen.AmmoPress);if(args.Contains("--market-ui"))ui.Open(MiningScreen.Market);if(args.Contains("--drill-ui"))ui.Open(MiningScreen.Drill);if(args.Contains("--refinery-ui"))ui.Open(MiningScreen.Refinery);if(args.Contains("--research-demo")){var workshop=scene.GameObjects.SelectMany(o=>o.Components).OfType<MiningTrade3D>().Single();workshop.BuyResearch("company");workshop.BuyResearch("arms1");ui.SelectResearch(1);}if(args.Contains("--credits-ui"))ui.Open(MiningScreen.AudioCredits);if(args.Contains("--research-ui"))ui.Open(MiningScreen.Research);if(args.Contains("--company-ui"))ui.Open(MiningScreen.Company);}
 }
 protected override void OnEngineUpdate(){frame++;if(frame==20&&args.Contains("--pause-preview")){var menu=op!.GameObject.GetComponent<MiningPauseMenu3D>()!;menu.Open();if(args.Contains("--pause-sound"))menu.ShowPage(MiningPausePage.Sound);if(args.Contains("--pause-controls"))menu.ShowPage(MiningPausePage.Controls);if(args.Contains("--pause-graphics"))menu.ShowPage(MiningPausePage.Graphics);}if(args.Contains("--effects-preview"))MiningEffectsCheck.PreviewFrame(scene!,op!,frame);if(frame==1&&args.Contains("--cargo-preview")){typeof(MiningOperation3D).GetProperty("RefinedOre")!.SetValue(op,(int)(op!.CargoCapacity*.9f));scene!.FindGameObject(op.PlayerObjectId)!.Transform.WorldPosition=scene.FindGameObject(op.DrillObjectId)!.Transform.WorldPosition+new Vector3(0,0,3);}if(frame==5&&args.Contains("--shipment-demo")){var t=scene!.GameObjects.SelectMany(o=>o.Components).OfType<MiningTrade3D>().Single();t.BeginSale(true,20);}if(frame==5&&args.Contains("--refining-demo")){typeof(MiningOperation3D).GetProperty("RawOre")!.SetValue(op,20);op!.BeginRefining();}CheckMenuCursor();if(frame==15&&args.Contains("--combat"))op!.StartOperation();if(frame==10&&(args.Contains("--look-up")||args.Contains("--look-down"))){var player=scene!.FindGameObject(op!.PlayerObjectId)!;player.GetComponent<PlayerController3D>()!.ControlPitch=args.Contains("--look-up")?-35:55;}if(args.Contains("--combat")&&frame>=15)combatTime+=(float)Time.DeltaTime;if(args.Contains("--combat")?combatTime>12.5f:frame>100)Close();}
 protected override bool CloseOnEscape=>!args.Contains("--verify-cursor");
 [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool PostMessage(IntPtr hwnd,uint message,IntPtr key,IntPtr data);
 void CheckMenuCursor()
 {
  if(frame==10&&args.Contains("--defence-model-preview"))foreach(var d in scene!.GameObjects.SelectMany(o=>o.Components.OfType<MiningDefense3D>()).ToArray())d.Tick(2);
  if(args.Contains("--verify-pause-cursor")){
   var menu=op!.GameObject.GetComponent<MiningPauseMenu3D>()!;
   if(frame>=14&&frame<=31)Console.WriteLine($"PAUSEKEY {frame}: down={Input.IsKeyDown(Key.Escape)} pressed={Input.IsKeyPressed(Key.Escape)} enabled={menu.Enabled} open={menu.IsOpen} time={Time.TotalTime}");
   if(frame==10)CaptureGameInput(true);
   if(frame is 15 or 24)PostMessage(System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle,0x100,new IntPtr(27),new IntPtr(0x00010001));
   if(frame is 17 or 26)PostMessage(System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle,0x101,new IntPtr(27),new IntPtr(unchecked((int)0xC0010001)));
   if(frame==20){if(!menu.IsOpen||CursorState!=OpenTK.Windowing.Common.CursorState.Normal||!Input.IsGameInputCaptured)throw new Exception("Escape did not open pause and release pointer");Console.WriteLine("PASS native Escape opens pause without losing capture");}
   if(frame==30){if(menu.IsOpen||CursorState!=OpenTK.Windowing.Common.CursorState.Grabbed||!Input.IsGameInputCaptured)throw new Exception($"Escape did not restore captured gameplay: open={menu.IsOpen} cursor={CursorState} captured={Input.IsGameInputCaptured} pointer={Input.IsUiPointerRequested} page={menu.Page}");Console.WriteLine("PASS native Escape resumes gameplay without clicking");}
  }
  if(!args.Contains("--verify-cursor"))return;
  var ui=scene!.GameObjects.SelectMany(o=>o.Components).OfType<MiningBusinessUi3D>().Single();
  if(frame==10)CaptureGameInput(true);
  if(frame==15)ui.Open(MiningScreen.AmmoPress);
  if(frame==18){if(CursorState!=OpenTK.Windowing.Common.CursorState.Normal||!Input.IsUiPointerRequested)throw new Exception("Menu failed to release native cursor");Console.WriteLine("PASS native menu releases cursor for buttons");}
  if(frame==24)PostMessage(System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle,0x100,new IntPtr(27),new IntPtr(0x00010001));
  if(frame==26)PostMessage(System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle,0x101,new IntPtr(27),new IntPtr(unchecked((int)0xC0010001)));
  if(frame==28){if(ui.Screen!=MiningScreen.None||CursorState!=OpenTK.Windowing.Common.CursorState.Grabbed||Input.IsUiPointerRequested)throw new Exception("Menu failed to restore mouse look");Console.WriteLine("PASS native Escape closes menu and restores mouse look without clicking");}
 }
 protected override void OnEngineRender()
 {
  if(WindowWidth<1||WindowHeight<1)return;if(captured||(args.Contains("--combat")?combatTime<11:frame<35))return;captured=true;var pixels=new byte[WindowWidth*WindowHeight*4];GL.ReadPixels(0,0,WindowWidth,WindowHeight,PixelFormat.Bgra,PixelType.UnsignedByte,pixels);
  using var image=new System.Drawing.Bitmap(WindowWidth,WindowHeight,System.Drawing.Imaging.PixelFormat.Format32bppArgb);var locked=image.LockBits(new(0,0,WindowWidth,WindowHeight),System.Drawing.Imaging.ImageLockMode.WriteOnly,image.PixelFormat);System.Runtime.InteropServices.Marshal.Copy(pixels,0,locked.Scan0,pixels.Length);image.UnlockBits(locked);image.RotateFlip(System.Drawing.RotateFlipType.RotateNoneFlipY);string p="artifacts/mining-"+(args.Contains("--pause-preview")?"pause-preview":args.Contains("--canyon-preview")?"canyon-preview":args.Contains("--ground-sky-preview")?"ground-sky-preview":args.Contains("--ground-sky-gameplay")?"ground-sky-gameplay":args.Contains("--terminal-preview")?"terminal-preview":args.Contains("--massive-drill-preview")?"massive-drill-preview":args.Contains("--refinery-preview")?"refinery-preview":args.Contains("--press-preview")?"press-preview":args.Contains("--credits-ui")?"credits-ui":args.Contains("--research-ui")?"research-ui":args.Contains("--refinery-ui")?"refinery-ui":args.Contains("--company-ui")?"company-ui":args.Contains("--ammo-ui")?"ammo-ui":args.Contains("--market-ui")?"market-ui":args.Contains("--drill-ui")?"drill-ui":args.Contains("--overview")?"overview":args.Contains("--combat")?"combat":"hud")+(args.Contains("--hd")?"-1080":args.Contains("--wide")?"-wide":args.Contains("--tall")?"-1610":"")+".png";image.Save(p);Console.WriteLine("CAPTURE "+p);
 }
 protected override void OnUnload(){Scenes.UnloadScene();runtime?.Dispose();base.OnUnload();}
}
internal static class MiningAuthor
{
 static Scene s=null!;static GameObject root=null!;
 static GameObject O(string name,Vector3 p,GameObject? parent=null){var o=s.CreateGameObject(name);o.SetParent(parent??root,false);o.Transform.LocalPosition=p;return o;}
 static GameObject B(string name,Vector3 p,Vector3 size,Vector4 color,bool collision=true,GameObject? parent=null,PrimitiveMeshType shape=PrimitiveMeshType.Cube)
 {var o=O(name,p,parent);o.Transform.LocalScale=size;o.AddComponent(new MeshRenderer{UsePrimitive=true,Primitive=shape,Material=new(){BaseColor=color,Roughness=.9f}});if(collision)o.AddComponent(new BoxCollider3D());return o;}
 static void Meta(string path,Guid id,string type)=>File.WriteAllText(Program.Root+"/"+path+".meta",JsonSerializer.Serialize(new{guid=id,type}));
 static void SaveTree(BehaviorTreeDefinition tree,string p,Guid id){BehaviorTreeSerializer.Save(Program.Root+"/"+p,tree);Meta(p,id,"AIBehavior");}
 static VisualInstruction I(string id,params (string Name,EventValue Value)[] values)=>new(){Id=id,Arguments=values.ToDictionary(x=>x.Name,x=>x.Value)};
 static EventValue T(GameObject o)=>EventValue.String("id:"+o.Id);
 public static void Build(Scene scene,GameProjectRuntime runtime)
 {
  s=scene;string backup=Program.Root+"/.byteengine/Backups/mining-first-test-"+DateTime.Now.ToString("yyyyMMdd-HHmmss");Directory.CreateDirectory(backup);File.Copy(Program.Root+"/Scenes/Main.bytescene",backup+"/Main.bytescene");
  foreach(string name in new[]{"Mining first test","Mining HUD"})if(s.FindGameObject(name) is {} old)s.DestroyGameObject(old);
  root=s.CreateGameObject("Mining first test");
  var floor=B("Alien worksite floor",new(0,-.15f,0),new(44,.3f,36),new(.115f,.16f,.145f,1));floor.Layer=20;floor.AddComponent(new GroundSurface());
  var ground=s.FindGameObject("Ground")!;ground.Transform.WorldPosition=new(0,-.55f,0);ground.Transform.LocalScale=new(70,1,60);
  var player=s.FindGameObject("Mannequin")!;player.Transform.WorldPosition=new(0,.05f,4);player.GetComponent<HealthComponent>()!.DestroyOnDeath=false;
  var pistol=s.FindGameObject("Pistol pickup")!;pistol.Active=true;pistol.Transform.WorldPosition=new(-2,.3f,8);var def=pistol.GetComponent<WeaponDefinition3D>()!;def.HitMaskBits=~(1<<19);def.ReserveCapacity=300;def.DamagePerPellet=24;def.AutoReload=true;def.ReloadSeconds=.85f;pistol.GetComponent<WeaponInstance3D>()!.ReserveAmmo=60;
  foreach(string name in new[]{"SCAR pickup","Shotgun pickup"})if(s.FindGameObject(name) is {} gun){gun.Active=false;gun.GetComponent<WeaponInstance3D>()!.StartInInventory=false;}
  var equipment=player.GetComponent<FpsGunLab3D>()!;equipment.DefaultWeaponObjectId=pistol.Id;equipment.StartWithNoWeapons=false;equipment.CarryLimit=1;
  if(player.GetComponent<CharacterMotionDebugHud3D>() is {} debug)debug.Enabled=false;
  var follow=s.FindGameObject("Mannequin Top Down Camera")!.GetComponent<TopDownCamera3D>()!;follow.Orthographic=true;follow.OrthographicWidth=30;follow.MinimumZoom=22;follow.MaximumZoom=38;follow.Pitch=60;follow.CursorLookAhead=true;follow.LookAheadDistance=3;
  // Rounded worksite boundaries: explicit collision, with no off-floor escape.
  B("West perimeter",new(-22,1,0),new(.7f,2,36),new(.16f,.20f,.21f,1));B("East perimeter",new(22,1,0),new(.7f,2,36),new(.16f,.20f,.21f,1));B("North perimeter",new(0,1,-18),new(44,2,.7f),new(.16f,.20f,.21f,1));B("South perimeter",new(0,1,18),new(44,2,.7f),new(.16f,.20f,.21f,1));
  foreach(var p in new[]{new Vector3(-19,1,-14),new(-12,.8f,-16),new(17,1,-15),new(20,.8f,-4),new(-20,.8f,4),new(18,1,15),new(-18,1,15)}){var rock=B("Planet rock",p,new(3,2,2.5f),new(.21f,.23f,.25f,1),true,null,PrimitiveMeshType.Sphere);rock.Transform.EulerAngles=new(0,p.X*6,15);}
  foreach(float x in new[]{-8f,8})B("Cargo cover",new(x,.7f,-3),new(3,1.4f,2),new(.31f,.22f,.12f,1));
  var drill=O("Resource drill",new(0,0,-3));drill.Layer=19;drill.AddComponent(new HealthComponent{MaxHealth=180,CurrentHealth=180});drill.AddComponent(new BoxCollider3D{Size=new(3,2.5f,3),Center=new(0,1.25f,0)});
  B("Drill platform",new(0,.3f,0),new(4,.6f,4),new(.18f,.22f,.24f,1),false,drill);
  B("Drill mast",new(0,1.6f,0),new(1,2.6f,1),new(.34f,.31f,.15f,1),false,drill);
  var spin=B("Rotating drill head",new(0,1.4f,-.8f),new(1.6f,2.3f,1.6f),new(.16f,.55f,.61f,1),false,drill,PrimitiveMeshType.Cylinder);
  for(int i=0;i<3;i++){var blade=B("Drill cutting vane",new(0,-.35f+i*.3f,0),new(1.2f,.08f,.35f),new(.25f,.35f,.38f,1),false,spin);blade.Transform.EulerAngles=new(0,i*60,0);}
  var company=O("Mining company operation",Vector3.Zero);var op=company.AddComponent(new MiningOperation3D{PlayerObjectId=player.Id,DrillObjectId=drill.Id});
  drill.AddComponent(new MiningRigVisual3D{OperationObjectId=company.Id,SpinningPartObjectId=spin.Id});
  B("Ammo press body",new(-9,1,12),new(3,2,2),new(.2f,.31f,.39f,1));B("Ammo press hopper",new(-9,2.3f,12),new(1.6f,1,1.6f),new(.32f,.42f,.48f,1),true,null,PrimitiveMeshType.Cylinder);
  B("Refinery body",new(9,1.2f,12),new(3,2.4f,2.2f),new(.34f,.2f,.12f,1));B("Refinery stack",new(9,3,12),new(.8f,1.3f,.8f),new(.3f,.32f,.34f,1),true,null,PrimitiveMeshType.Cylinder);
  B("Galactic market console",new(0,1,13),new(2,2,1),new(.12f,.21f,.27f,1));var screen=B("Market screen",new(0,1.4f,12.48f),new(1.5f,.8f,.05f),new(.08f,.65f,.7f,1),false);screen.GetComponent<MeshRenderer>()!.Material.Shading=MaterialShadingMode.Unlit;
  var nav=O("Worksite navigation",new(-22,0,-18));nav.AddComponent(new NavigationRegion3D{Width=88,Height=72,CellSize=.5f,AgentRadius=.38f,ExcludeDynamicActors=true,SampleColliderFloors=true,FloorLayerMask=1<<20,FloorProbeHeight=2,FloorProbeDepth=4});
  foreach(var p in new[]{new Vector3(-16,0,-11),new(0,0,-15),new(16,0,-11),new(-17,0,1),new(17,0,1)})
  {var point=O("Alien emergence point",p);point.AddComponent(new MiningSpawnPoint3D{OperationObjectId=company.Id});var ring=B("Emergence pad",p+new Vector3(0,.015f,0),new(2.2f,.035f,2.2f),new(.22f,.48f,.3f,1),false,null,PrimitiveMeshType.Cylinder);}
  Directory.CreateDirectory(Program.Root+"/Assets/AI/Mining");Directory.CreateDirectory(Program.Root+"/Assets/Blueprints/Mining");Directory.CreateDirectory(Program.Root+"/Assets/Events/Mining");
  op.RaiderBlueprint=Alien(runtime,false);op.HunterBlueprint=Alien(runtime,true);
  var canvas=O("Mining HUD",Vector3.Zero);canvas.SetParent(null,false);canvas.AddComponent(new UiCanvas());
  GameObject Text(string name,GameObject parent,string value,Vector2 offset,int size=20,UiAnchor anchor=UiAnchor.TopLeft,float wrap=0)
  {var o=O(name,Vector3.Zero,parent);o.AddComponent(new UiText{Text=value,Offset=offset,FontSize=size,Anchor=anchor,WrapWidth=wrap,Color=new(.92f,.96f,.97f,1),ShadowColor=new(0,0,0,.9f),OrderInLayer=201});return o;}
  GameObject Panel(string name,GameObject parent,Vector2 offset,Vector2 size,UiAnchor anchor=UiAnchor.TopLeft)
  {var o=O(name,Vector3.Zero,parent);o.AddComponent(new UiWidget{Kind=UiWidgetKind.Panel,Offset=offset,Size=size,Anchor=anchor,Color=new(.025f,.045f,.06f,.94f),BlocksWorldPointer=false,OrderInLayer=200});return o;}
  var header=Panel("Operation header",canvas,new(16,16),new(460,110));var status=Text("Mining status",header,"PREPARATION",new(14,10),28,wrap:430);
  var bar=O("Extraction progress",Vector3.Zero,header);bar.AddComponent(new UiWidget{Kind=UiWidgetKind.ProgressBar,Offset=new(14,82),Size=new(430,10),Maximum=1,Value=0,Color=new(.12f,.17f,.18f,1),FillColor=new(.12f,.75f,.73f,1),OrderInLayer=201});
  var moneyPanel=Panel("Cargo balance panel",canvas,new(-16,16),new(360,92),UiAnchor.TopRight);var wallet=Text("Company balance",moneyPanel,"20 CREDITS",new(14,12),28,wrap:330);
  var messagePanel=Panel("Business feedback panel",canvas,new(16,134),new(540,82));var message=Text("Business feedback",messagePanel,"Start the drill when ready.",new(14,10),25,wrap:510);
  var shop=Panel("Between-operation shop",canvas,new(-16,120),new(360,430),UiAnchor.TopRight);shop.GetComponent<UiWidget>()!.BlocksWorldPointer=true;Text("Shop title",shop,"SITE UPGRADES",new(16,12),32);
  GameObject Button(string name,int y,int height)
  {var o=O(name,Vector3.Zero,shop);o.AddComponent(new UiWidget{Kind=UiWidgetKind.Button,Label=name,Offset=new(14,y),Size=new(332,height),FontSize=24,Color=new(.09f,.19f,.22f,1),HoverColor=new(.13f,.32f,.36f,1),DisabledColor=new(.09f,.12f,.14f,.98f),BlocksWorldPointer=true,ActivateWithSpace=false,OrderInLayer=202});return o;}
  var start=Button("Start operation",48,36);var refine=Button("Refine ore",90,36);var sell=Button("Sell cargo",132,36);var upgradeDrill=Button("Upgrade drill",176,54);var upgradeAmmo=Button("Upgrade ammo press",238,54);var upgradeRefinery=Button("Upgrade refinery",300,54);var research=Button("Company research",362,54);
  canvas.AddComponent(new MiningHud3D{OperationObjectId=company.Id,StatusObjectId=status.Id,WalletObjectId=wallet.Id,MessageObjectId=message.Id,MessagePanelObjectId=messagePanel.Id,ExtractionBarObjectId=bar.Id,ShopPanelObjectId=shop.Id,StartButtonObjectId=start.Id,RefineButtonObjectId=refine.Id,SellButtonObjectId=sell.Id,DrillButtonObjectId=upgradeDrill.Id,AmmoButtonObjectId=upgradeAmmo.Id,RefineryButtonObjectId=upgradeRefinery.Id,ResearchButtonObjectId=research.Id});
  var module=new EventModuleDefinition{Name="Mining business - controls and shop"};
  void Bind(string name,string action,GameObject button,string key)
  {
   var execute=I("bytebard.karlsondemo.mining."+action,("target",T(company)));
   module.Rules.Add(new(){DisplayName=name+" - click",Conditions=[I("ui.buttonClicked",("target",T(button)))],Actions=[execute]});
   var keyConditions=new List<VisualInstruction>{I("input.keyPressed",("key",EventValue.String(key)))};if(action!="start")keyConditions.Add(I("bytebard.karlsondemo.mining.phase",("target",T(company)),("phase",EventValue.String("Preparation"))));module.Rules.Add(new(){DisplayName=name+" - key "+key,Conditions=keyConditions,Actions=[I(execute.Id,("target",T(company)))]});
  }
  Bind("Start drill or recover site","start",start,"E");Bind("Refine a batch","refine",refine,"F");Bind("Sell cargo","sell",sell,"X");Bind("Buy drill upgrade","upgradeDrill",upgradeDrill,"D1");Bind("Buy ammo press upgrade","upgradeAmmoPress",upgradeAmmo,"D2");Bind("Buy refinery upgrade","upgradeRefinery",upgradeRefinery,"D3");Bind("Buy permanent research","upgradeCompanyResearch",research,"D4");
  string events="Assets/Events/Mining/Mining business controls.byteevents";new EventModuleSerializer().Save(module,Program.Root+"/"+events);Meta(events,module.Id,"EventModule");company.AddComponent(new EventModuleComponent()).AddModuleReference(new(module.Id,events));
  runtime.Serializer.Save(s,Program.Root+"/Scenes/Main.bytescene");Console.WriteLine("MINING AUTHORED "+s.GameObjects.Count+" objects. Backup "+backup);
 }
 static AssetReference Alien(GameProjectRuntime runtime,bool hunter)
 {
  string name=hunter?"Alien Hunter":"Alien Raider";string treePath="Assets/AI/Mining/"+name+".byteai";string attackPath="Assets/AI/Mining/Alien melee attack.byteai";
  Guid attackId=Guid.Parse("7e8d7736-9579-4ef6-9d75-2e192ae526f3");var attack=AIBehaviorPresets.AttackStages();SaveTree(attack,attackPath,attackId);
  // Both actors have one visible-target approach and a properly latched attack branch.
  var close=new BehaviorNode{Kind=BehaviorNodeKind.Gate,Name="Within attack reach",Test=BehaviorTest.WithinDistance,Distance=hunter?1.7f:2.9f,Interruption=BehaviorInterruption.FinishCommittedAttack,Children=[new(){Kind=BehaviorNodeKind.SubBehavior,Name="Warn, strike and recover",SubBehavior=new(attackId,attackPath)}]};
  var tree=new BehaviorTreeDefinition{Name=name+" - approach assigned target and attack",Root=new(){Kind=BehaviorNodeKind.Priority,Name="Pressure assigned target",Children=[close,new(){Kind=BehaviorNodeKind.MoveToTarget,Name=hunter?"Chase the operator":"Approach the drill",Speed=hunter?3.1f:1.9f,Distance=hunter?1.3f:2.55f,Timeout=12},new(){Kind=BehaviorNodeKind.Wait,Name="Reassess",Duration=.2f}]}};
  var treeId=Guid.NewGuid();SaveTree(tree,treePath,treeId);
  var previousScene=s;var previousRoot=root;s=new Scene("Alien blueprint");root=s.CreateGameObject(name);
  root.AddComponent(new CapsuleCollider3D{Radius=hunter?.3f:.4f,Height=hunter?1.5f:1.8f,Center=new(0,hunter?.75f:.9f,0)});root.AddComponent(new HealthComponent{MaxHealth=hunter?35:55,CurrentHealth=hunter?35:55,DestroyOnDeath=true});
  root.AddComponent(new NavigationAgent3D{CollisionAvoidance=true,CollisionRadius=hunter?.28f:.37f,Speed=hunter?3.1f:1.9f});
  root.AddComponent(new SimpleEnemyAI3D{BehaviorControlled=true,AttackStyle=EnemyAttackStyle.Rusher,UseNavigation=true,MoveSpeed=hunter?3.1f:1.9f,AttackRange=hunter?1.8f:3,StopDistance=hunter?1.3f:2.55f,Damage=hunter?6:8,WindupSeconds=hunter?.5f:.8f,RecoverySeconds=.7f,AttackCooldown=1.3f});
  root.AddComponent(new AIBehavior3D{Behavior=new(treeId,treePath),FindNearestCharacter=false,DetectionRange=80,FieldOfView=360,DecisionInterval=.15f,Squad=hunter?"Mining hunters":"Mining raiders",MaximumAttackers=hunter?2:4});
  var color=hunter?new Vector4(.52f,.18f,.39f,1):new Vector4(.16f,.45f,.35f,1);
  B("Alien carapace",new(0,hunter?.75f:.9f,0),new(hunter?.8f:1.1f,hunter?1:1.2f,hunter?1.1f:1.3f),color,false,root,PrimitiveMeshType.Sphere);
  B("Alien head",new(0,hunter?.45f:.7f,-.6f),new(.45f,.4f,.45f),new(.3f,.6f,.55f,1),false,root,PrimitiveMeshType.Sphere);
  foreach(float x in new[]{-.5f,.5f})foreach(float z in new[]{-.35f,.35f}){var leg=B("Alien leg",new(x,.24f,z),new(.15f,.48f,.18f),color,false,root);leg.Transform.EulerAngles=new(0,0,x>0?-25:25);}
  var bp=new BlueprintDefinition{Name=name,Type=BlueprintType.Character,Root=runtime.Serializer.SerializeObject(root),Children=s.GameObjects.Where(o=>o!=root).Select(runtime.Serializer.SerializeObject).ToList()};string path="Assets/Blueprints/Mining/"+name+".byteblueprint";new BlueprintSerializer().Save(bp,Program.Root+"/"+path);Meta(path,bp.Id,"Blueprint");s=previousScene;root=previousRoot;return new(bp.Id,path);
 }
}

internal static class MiningVerify
{
 static void Check(bool value,string message){if(!value)throw new Exception(message);Console.WriteLine("PASS "+message);}
 public static void Run(Scene scene,GameProjectRuntime runtime,MiningOperation3D op)
 {
  var warnings=new List<string>();var registry=VisualLogicRegistry.CreateDefault();var module=new EventModuleSerializer().Load(Program.Root+"/Assets/Events/Mining/Mining business controls.byteevents");
  foreach(var rule in module.Rules){foreach(var condition in rule.Conditions)Check(registry.TryGetCondition(condition.Id,out _),"Event condition registered: "+condition.Id);foreach(var action in rule.Actions)Check(registry.TryGetAction(action.Id,out _),"Event action registered: "+action.Id);}
  op.PersistProgress=false;foreach(var camp in scene.GameObjects.SelectMany(o=>o.Components).OfType<MiningCampLayout3D>())camp.PersistProgress=false;foreach(var planner in scene.GameObjects.SelectMany(o=>o.Components).OfType<MiningDefensePlanner3D>())planner.PersistProgress=false;op.ExtractionSeconds=2;op.SpawnInterval=.1f;
  var player=scene.FindGameObject(op.PlayerObjectId)!;var drill=scene.FindGameObject(op.DrillObjectId)!;
  player.GetComponent<HealthComponent>()!.MaxHealth=10000;op.BaseDrillHealth=10000;
  var host=new SceneManager();host.LoadScene(scene);
  var region=scene.GameObjects.SelectMany(o=>o.Components).OfType<NavigationRegion3D>().Single();Check(region.Ready&&region.WalkableCells>1000,"Alien worksite navigation baked");
  foreach(var point in scene.GameObjects.SelectMany(o=>o.Components).OfType<MiningSpawnPoint3D>())Check(region.FindPath(point.Transform.WorldPosition,drill.Transform.WorldPosition+new Vector3(0,0,3),20000).Count>0,"Spawn point has a reachable route to drill");
  var sourceHash=System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(Program.Root+"/Scenes/Main.bytescene"));
  void KeyFrame(Key? key)
  {
   var raw=new RawInputSnapshot();if(key.HasValue)raw.KeysDown.Add(key.Value);
   typeof(Input).GetMethod("UpdatePortable",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,[raw,true]);typeof(Input).GetMethod("SetGameInputCaptured",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,[true]);Input.SetGameViewPointer(new(.5f),new(1280,720),true);
   typeof(Time).GetMethod("Update",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,[.05d]);typeof(Scene).GetMethod("UpdateInternal",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(scene,null);
  }
  op.StartOperation();KeyFrame(null);Check(op.Phase==MiningPhase.Drilling,"Native mining operation starts correctly");
  var update=typeof(Scene).GetMethod("UpdateInternal",BindingFlags.NonPublic|BindingFlags.Instance)!;var clock=typeof(Time).GetMethod("Update",BindingFlags.NonPublic|BindingFlags.Static)!;
  bool raiderAttack=false,hunterAttack=false,scaledNavigation=false;int maxAlive=0;var seenRoles=new HashSet<Guid>();
  for(int i=0;i<500;i++)
  {
   clock.Invoke(null,[.05d]);update.Invoke(scene,null);maxAlive=Math.Max(maxAlive,op.AliveEnemies);
   foreach(var enemy in op.SpawnedEnemies.Where(e=>e.Scene==scene))
   {
    var ai=enemy.GetComponent<AIBehavior3D>()!;CheckQuiet(ai.LastError.Length==0,"AI binding error: "+ai.LastError);seenRoles.Add(ai.TargetObjectId);if(enemy.GetComponent<NavigationAgent3D>() is {Moving:true} moving&&enemy.GetComponent<SimpleEnemyAI3D>() is {} executor&&Math.Abs(moving.Speed-executor.MoveSpeed)<.01f)scaledNavigation=true;
    if(enemy.GetComponent<SimpleEnemyAI3D>()?.AttackPhase=="Windup"){if(ai.TargetObjectId==drill.Id)raiderAttack=true;else if(ai.TargetObjectId==player.Id)hunterAttack=true;}
   }
  }
  Check(scaledNavigation,"Behaviour assets use the configured role movement speed in actual navigation");
  Check(seenRoles.Contains(player.Id)&&seenRoles.Contains(drill.Id),"Hunters target operator and raiders target drill");
  Console.WriteLine($"ROLE ATTACKS raider={raiderAttack} hunter={hunterAttack}");foreach(var e in op.SpawnedEnemies){var a=e.GetComponent<AIBehavior3D>()!;var co=e.GetComponent<SimpleEnemyAI3D>()!;var nav=e.GetComponent<NavigationAgent3D>()!;Console.WriteLine($"ROLE {e.Name} pos={e.Transform.WorldPosition} target={a.TargetObjectId} task={a.CurrentTask} phase={co.AttackPhase} nav={nav.Status} destination={nav.Destination} health={e.GetComponent<HealthComponent>()!.CurrentHealth} error={a.LastError}");}
  Check(raiderAttack&&hunterAttack,"Both alien roles navigate into range and telegraph attacks");Check(player.GetComponent<HealthComponent>()!.CurrentHealth<10000&&drill.GetComponent<HealthComponent>()!.CurrentHealth<10000,"Both alien roles actually damage their assigned targets");Check(maxAlive<=op.MaximumAlive&&op.EnemiesSpawned==op.EnemiesScheduled,"Finite native wave respects active-enemy budget");
  Check(op.Phase==MiningPhase.Drilling&&op.ExtractionProgress==1&&op.RawOre==0,"Completed extraction cannot pay while attackers remain");
  foreach(var enemy in op.SpawnedEnemies.ToArray())enemy.GetComponent<HealthComponent>()?.Kill();clock.Invoke(null,[.05d]);update.Invoke(scene,null);
  Check(op.Phase==MiningPhase.Preparation&&op.RawOre==18&&op.CompletedRounds==1,"Killing final attacker completes operation once");op.BeginRefining();KeyFrame(null);Check(op.RefiningAmount==18,"Native refinery accepts one batch");op.Tick(4);Check(op.RefinedOre==18&&op.RawOre==0,"Native refinery finishes and conserves ore");op.SellCargo();KeyFrame(null);Check(op.Credits==110,"Legacy full-cargo sale remains compatible");op.Buy(MiningUpgrade.Drill);KeyFrame(null);Check(op.DrillLevel==1&&op.Credits==70,"Native upgrade deducts the correct cost");Check(op.StartOperation()&&op.RoundQuota==25,"Second operation uses upgraded extraction quota");
  op.PlayerObjectId=Guid.Empty;op.Tick(.1f);Check(op.Phase==MiningPhase.Failed&&op.AliveEnemies==0,"Failure stops extraction and clears spawned attackers");
  Check(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(Program.Root+"/Scenes/Main.bytescene")).SequenceEqual(sourceHash),"Verification never changes authored scene or real company save");
  host.UnloadScene();Console.WriteLine("MINING NATIVE VERIFICATION COMPLETE");
 }
 static void CheckQuiet(bool value,string message){if(!value)throw new Exception(message);}
}
