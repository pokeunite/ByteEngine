using System.Reflection;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using ByteEngine.Core;
using ByteEngine.Core.Runtime;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Animation;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.InputSystem;
using ByteEngine.Core.VisualLogic;
using ByteEngine.Core.Variables;
using OpenTK.Graphics.OpenGL4;
const string root="C:/Users/codex/Documents/ultra violence";
if(args.Contains("--motor-check")){
 Vector3 Step(Vector3 v,Vector3 input,bool ground,float dt)=>CharacterController3D.ArenaVelocity(v,input,ground,7,95,8,4,22,13,dt);
 void Assert(bool yes,string message){if(!yes)throw new Exception(message);Console.WriteLine("PASS "+message);}
 Vector3 Sim(Vector3 input,int fps){var v=Vector3.Zero;for(int n=0;n<fps;n++)v=Step(v,input,true,1f/fps);return v;}
 var forward=Sim(-Vector3.UnitZ,60);var diagonal=Sim(new(1,0,-1),60);
 Assert(Math.Abs(forward.Length()-7)<.01&&Math.Abs(diagonal.Length()-7)<.01,"Full ground speed reached without diagonal boost");
 var stop=forward;for(int n=0;n<15;n++)stop=Step(stop,Vector3.Zero,true,1f/60);Assert(stop.Length()<.1,"Ground input release stops within 250ms");
 var reverse=forward;for(int n=0;n<10;n++)reverse=Step(reverse,Vector3.UnitZ,true,1f/60);Assert(reverse.Z>1,"Direction reversal responds within 167ms");
 var coast=Step(new(7,3,0),Vector3.Zero,false,1f/60);Assert(coast==new Vector3(7,3,0),"Releasing air input retains horizontal momentum and vertical velocity");
 var strafe=coast;for(int n=0;n<60;n++)strafe=Step(strafe,-Vector3.UnitZ,false,1f/60);Assert(strafe.Z< -6&&strafe.Length()<14,"Air steering changes trajectory with bounded speed");
 Assert(Vector3.Distance(Sim(-Vector3.UnitZ,30),Sim(-Vector3.UnitZ,144))<.01,"30/60/144fps converge to the same authored ground speed");return;
}
if(args.Length>0&&args[0]=="--convert-audio"){
 foreach(var path in Directory.GetFiles(args[1],"*.ogg")){
  using var reader=new ByteEngine.Core.Audio.StreamingPcmReader(path);var samples=new List<short>();var buffer=new short[16384];int n;
  while((n=reader.Read(buffer))>0)for(int i=0;i<n;i+=reader.Channels){int sum=0;for(int ch=0;ch<reader.Channels;ch++)sum+=buffer[i+ch];samples.Add((short)(sum/reader.Channels));}
  int peak=samples.Max(s=>Math.Abs((int)s));float gain=peak>0?28000f/peak:1;
  string output=Path.Combine(args[2],Path.GetFileNameWithoutExtension(path)+".wav");Directory.CreateDirectory(args[2]);using var file=new BinaryWriter(File.Create(output));
  file.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));file.Write(36+samples.Count*2);file.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));file.Write(16);file.Write((short)1);file.Write((short)1);file.Write(reader.SampleRate);file.Write(reader.SampleRate*2);file.Write((short)2);file.Write((short)16);file.Write(System.Text.Encoding.ASCII.GetBytes("data"));file.Write(samples.Count*2);foreach(short sample in samples)file.Write((short)Math.Clamp((int)(sample*gain),-30000,30000));Console.WriteLine(Path.GetFileName(output)+" "+reader.DurationSeconds.ToString("0.00")+"s");
 }return;
}
using(var database=new AssetDatabase(root,new[]{"Assets","Scenes"},Console.WriteLine,Console.WriteLine)){}
if(args.Contains("--muzzle-inspect")||args.Contains("--muzzle-author")){
 using var window=new OpenTK.Windowing.Desktop.GameWindow(OpenTK.Windowing.Desktop.GameWindowSettings.Default,new OpenTK.Windowing.Desktop.NativeWindowSettings{StartVisible=false,ClientSize=new(800,600)});window.Context.MakeCurrent();
 using var runtime=new GameProjectRuntime(root+"/ultra violence.byteproject",Console.WriteLine);var scene=runtime.LoadStartupScene();
 var document=JsonNode.Parse(File.ReadAllText(root+"/Scenes/Combat Prototype.bytescene"))!;
 foreach(var o in scene.GameObjects.Where(o=>o.Name.Contains("view model"))){var renderer=o.GetComponent<SkeletalMeshRenderer>()!;renderer.ResolveRuntimeResources();renderer.Play(renderer.DefaultAnimation);renderer.SamplePlayback(.2f,1);
 Console.WriteLine("WEAPON "+o.Name);foreach(var mesh in renderer.ResolvedModel!.Meshes)Console.WriteLine("MESH "+mesh.Name+" "+mesh.Vertices.Length);
 foreach(var bone in renderer.ResolvedModel.Skeleton!.Bones.Where(b=>b.Name.Contains("Weapon")||b.Name.Contains("Cuerpo")||b.Name.Contains("root")||b.Name.Contains("Root")))if(renderer.TryGetBoneWorldMatrix(bone.Name,out var matrix))Console.WriteLine("BONE "+bone.Name+" "+matrix.Translation);
 var parts=renderer.CapturePoseMeshes();Vector3 muzzle=default;float minimum=float.MaxValue;int partIndex=0;
 foreach(var part in parts){var v=part.Mesh.VertexData.Span;var points=new List<Vector3>();for(int n=0;n<v.Length;n+=8)points.Add(Vector3.Transform(new(v[n],v[n+1],v[n+2]),part.World));float z=points.Min(p=>p.Z);var tip=points.Where(p=>p.Z<z+.012f).ToArray();var center=tip.Aggregate(Vector3.Zero,(a,b)=>a+b)/tip.Length;Console.WriteLine("TIP "+center);
 string meshName=renderer.ResolvedModel.Meshes[partIndex++].Name;if(!meshName.Contains("Gloves")&&!meshName.Contains("Shirt")&&!meshName.Contains("Case")&&!meshName.Contains("CARGADOR")&&z<minimum){minimum=z;muzzle=center;}
 part.Mesh.Dispose();}
 string boneName=o.Name.StartsWith("Pistol")?"DEF_Cuerpo_02":o.Name.StartsWith("SPAS")?"Root_0106":"DEF-Weapon_013";
 if(!renderer.TryGetBoneWorldMatrix(boneName,out var boneWorld)||!Matrix4x4.Invert(boneWorld,out var inverse))throw new Exception("Missing muzzle attachment bone "+boneName);
 var offset=Vector3.Transform(muzzle,inverse);Console.WriteLine("CALIBRATED "+boneName+" "+offset);
 var node=document["gameObjects"]!.AsArray().First(n=>n!["name"]!.GetValue<string>()==o.Name)!;var definition=node["components"]!.AsArray().First(n=>n!["type"]!.GetValue<string>()=="bytebard.karlsondemo.WeaponDefinition3D")!["properties"]!;
 definition["muzzleSource"]="ModelBone";definition["muzzleBone"]=boneName;definition["muzzleBoneOffset"]=new JsonArray(offset.X,offset.Y,offset.Z);
 }
 if(args.Contains("--muzzle-author")){string path=root+"/Scenes/Combat Prototype.bytescene";File.Copy(path,path+".before-muzzle-attachment",true);File.WriteAllText(path,document.ToJsonString(new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine("Saved animated barrel attachments for all three weapons.");}return;
}
if(args.Contains("--movement-check")){using var application=new MovementPreview(root);application.Run();return;}
if(args.Contains("--check")||args.Contains("--small")||args.Contains("--audio-check")){using var application=new Preview(root,args.Contains("--small"),args.Contains("--audio-check"));application.Run();return;}
using(var runtime=new GameProjectRuntime(root+"/ultra violence.byteproject",Console.WriteLine)){
 foreach(string name in new[]{"George","Leela","Mike"}){
  var model=runtime.Assets.LoadModel(runtime.Database.ResolveReference("Assets/Enemies/Quaternius/"+name+".gltf"));
  Console.WriteLine($"MODEL {name} joints={model.Skeleton?.Bones.Count} meshes={model.Meshes.Count} animations={model.Animations.Count}");
  foreach(var mesh in model.Meshes){float min=float.MaxValue,max=float.MinValue;for(int n=1;n<mesh.Vertices.Length;n+=8){min=Math.Min(min,mesh.Vertices[n]);max=Math.Max(max,mesh.Vertices[n]);}Console.WriteLine($" MESH {mesh.Name} Y {min}..{max}");}
 }
 using var window=new OpenTK.Windowing.Desktop.GameWindow(OpenTK.Windowing.Desktop.GameWindowSettings.Default,new OpenTK.Windowing.Desktop.NativeWindowSettings{StartVisible=false,ClientSize=new(1280,720)});window.Context.MakeCurrent();
 var scene=runtime.LoadStartupScene();var registry=VisualLogicRegistry.CreateDefault();
 foreach(var o in scene.GameObjects)foreach(var c in o.Components.OfType<EventModuleComponent>())foreach(var asset in c.Modules){var m=new EventModuleSerializer().Load(root+"/"+asset.ProjectPath);foreach(var r in m.Rules){foreach(var condition in r.Conditions)if(!registry.TryGetCondition(condition.Id,out _))throw new Exception("Missing condition "+condition.Id);foreach(var action in r.Actions)if(!registry.TryGetAction(action.Id,out _))throw new Exception("Missing action "+action.Id);}}
 Console.WriteLine("PASS all scene components and event conditions/actions resolve");
}
sealed class Preview(string root,bool small=false,bool audioCheck=false):ByteEngineApplication(small?900:1280,small?650:720,"Ultra Violence prototype validation")
{
 GameProjectRuntime? runtime;int frame;string? capture;readonly List<string> checks=[];Vector3 start;float energyBefore;bool activeSeen;
 readonly HashSet<Key> previousKeys=[];readonly HashSet<MouseButton> previousMouse=[];bool boltSeen;
 readonly HashSet<string> heard=[];bool audibleVoice;bool afterimageSeen,playerProjectileSeen;
 protected override bool CloseOnEscape=>false;
 protected override void OnLoad(){base.OnLoad();IsVisible=false;runtime=new(root+"/ultra violence.byteproject",Console.WriteLine);InputActions.Configure(runtime.Project.InputMap);Scenes.RestartSceneFactory=()=>runtime.LoadStartupScene();Scenes.LoadScene(runtime.LoadStartupScene());}
 void Check(bool condition,string message){if(!condition)throw new Exception(message);checks.Add(message);Console.WriteLine("PASS "+message);}
 protected override void OnEngineUpdate(){
  frame++;var scene=Scenes.ActiveScene!;var player=scene.FindGameObject("FPS Character")!;var meter=player.GetComponent<ResourceMeter3D>()!;
  var raw=new RawInputSnapshot();if((frame>=55&&frame<90)||(frame>=180&&frame<220))raw.MouseButtonsDown.Add(MouseButton.Right);
  if(frame==165)raw.MouseButtonsDown.Add(MouseButton.Left);if(frame==365)raw.KeysDown.Add(Key.Enter);
  typeof(ByteEngine.Core.Input).GetMethod("UpdatePortable",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,[raw,true]);
  var prior=(HashSet<Key>)typeof(ByteEngine.Core.Input).GetField("PreviousKeysDown",BindingFlags.NonPublic|BindingFlags.Static)!.GetValue(null)!;prior.Clear();prior.UnionWith(previousKeys);previousKeys.Clear();previousKeys.UnionWith(raw.KeysDown);
  var priorMouse=(HashSet<MouseButton>)typeof(ByteEngine.Core.Input).GetField("PreviousMouseButtonsDown",BindingFlags.NonPublic|BindingFlags.Static)!.GetValue(null)!;priorMouse.Clear();priorMouse.UnionWith(previousMouse);previousMouse.Clear();previousMouse.UnionWith(raw.MouseButtonsDown);
  InputActions.Update(raw,true);ByteEngine.Core.Input.SetGameViewPointer(new(.5f),new(WindowWidth,WindowHeight),true);
  typeof(Time).GetMethod("Update",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,[1d/60]);
  if(audioCheck){
   foreach(var voice in scene.GameObjects.Where(o=>o.Name.StartsWith("SFX voice: "))){var sound=voice.GetComponent<ByteEngine.Core.Audio.AudioSource3D>()!;heard.Add(sound.ClipReference.CachedProjectPath??"");audibleVoice|=sound.IsPlaying;}
   if(frame==30){
    var library=scene.FindGameObject("SFX Pistol")!;var registry=VisualLogicRegistry.CreateDefault();registry.TryGetAction("audio.playOneShot",out var action);
    var context=new EventExecutionContext{Scene=scene,Self=player,Globals=new VariableStore(),WarningSink=message=>throw new Exception(message)};
    var instruction=new VisualInstruction{Id="audio.playOneShot",Arguments=new(){["target"]=EventValue.String("SFX Pistol"),["maxVoices"]=EventValue.Number(2),["variation"]=EventValue.Number(0)}};
    for(int n=0;n<12;n++)action!.Execute(instruction,context);
    Check(scene.GameObjects.Count(o=>o.Name=="SFX voice: "+library.Id)==2,"One-shot audio overlaps without restarting and obeys its voice limit");
   }
   if(frame==175){Check(audibleVoice,"Native OpenAL voices actually play");Check(heard.Any(p=>p.Contains("Pistol shot")),"Weapon audio cue plays");Check(heard.Any(p=>p.Contains("forceField_000"))&&heard.Any(p=>p.Contains("forceField_002")),"Overdrive activation and release audio play");}
   if(frame==350){Check(heard.Any(p=>p.Contains("laserSmall")),"Gunner projectile audio plays");Check(heard.Any(p=>p.Contains("explosionCrunch")),"Enemy death audio plays");Check(heard.Any(p=>p.Contains("impactPunch_heavy")),"Player damage audio plays");Check(heard.Any(p=>p.Contains("impactBell")),"Victory audio plays");Check(scene.GameObjects.Count(o=>o.Name.StartsWith("SFX voice: "))<64,"Transient sound objects remain bounded and expire");}
  }
  if(frame==45){start=player.Transform.WorldPosition;Check(player.GetComponent<CharacterController3D>()!.IsGrounded,"Player stands on arena floor");Check(scene.GameObjects.Count(o=>o.GetComponent<SimpleEnemyAI3D>()!=null)==6,"Three enemy roles / six animated opponents loaded");Check(scene.GameObjects.Where(o=>o.Name.EndsWith(" visual")).All(o=>o.GetComponent<SkeletalMeshRenderer>()!.ModelLoaded),"All enemy meshes import on original rigs");capture="arena-normal.png";energyBefore=meter.Current;}
  if(frame==70){Check(scene.FindGameObject("Enemy time domain")!.GetComponent<SimulationRate3D>()!.Rate==.25f,"Overdrive slows enemy domain only");Check(meter.Current<energyBefore,"Overdrive drains energy in real time");Check(Math.Abs(scene.FindGameObject("Energy bar")!.GetComponent<UiWidget>()!.Value-meter.Current)<.5f,"Rendered energy bar tracks the live resource");Check(scene.FindGameObject("Energy label")!.GetComponent<UiText>()!.Text!="OVERDRIVE 100","Rendered energy number updates while draining");Check(scene.FindGameObject("Overdrive screen effect")!.GetComponent<ScreenOverlay>()!.Strength==1,"Screen overlay responds to ability sheet");capture="arena-overdrive.png";activeSeen=true;}
  if(frame==95){energyBefore=meter.Current;Check(scene.FindGameObject("Enemy time domain")!.GetComponent<SimulationRate3D>()!.Rate==1,"Releasing right mouse restores enemies");}
  if(frame==130){Check(meter.Current>energyBefore,"Energy regenerates after release");Check(activeSeen,"Ability activation observed");capture="arena-recharged.png";}
  boltSeen|=scene.GameObjects.Any(o=>o.GetComponent<Projectile3D>()!=null);
  foreach(var ghost in scene.GameObjects.SelectMany(o=>o.Components).Where(c=>c.GetType().Name=="Afterimage3D")){
   int count=(int)ghost.GetType().GetProperty("ActiveGhosts")!.GetValue(ghost)!;afterimageSeen|=count>0;if(count>4)throw new Exception("Afterimage budget exceeded");
   if(frame==130)Check(count==0,"Afterimages fade and dispose after Overdrive ends");
  }
  playerProjectileSeen|=scene.GameObjects.Any(o=>o.GetComponent<Projectile3D>()?.OwnerId==player.Id);
  if(frame==166)capture="arena-muzzle-flash.png";
  if(frame==175){Check(afterimageSeen,"Moving enemies emit frozen afterimages in Overdrive");Check(playerProjectileSeen,"Player weapon fires real projectiles rather than a debug ray");}
  if(frame==150&&small){capture="arena-small.png";}
  if(frame==155&&small){Close();return;}
  if(frame==160){var enemy=scene.FindGameObject("Rusher 1")!;enemy.Transform.WorldPosition=player.Transform.WorldPosition-new Vector3(0,0,4);enemy.GetComponent<HealthComponent>()!.CurrentHealth=1;}
  if(frame==175)Check(scene.FindGameObject("Rusher 1")!.GetComponent<HealthComponent>()!.IsDead,"Player's real weapon shot kills an enemy");
  if(frame==195)meter.SetAmount(0);
  if(frame==200){Check(scene.FindGameObject("Enemy time domain")!.GetComponent<SimulationRate3D>()!.Rate==1,"Exhaustion automatically restores normal time");Check(!player.Variables["Overdrive"].Boolean,"Exhausted ability stays off while right mouse remains held");}
  if(frame==340){Check(player.GetComponent<HealthComponent>()!.CurrentHealth<100,"Enemies damage player through authored combat rules");Check(boltSeen,"Gunners fire real visible projectiles");Check(scene.FindGameObject("Rusher 1 visual")!.GetComponent<SkeletalMeshRenderer>()!.CurrentAnimation=="Death","Dead enemy stays in death animation rather than returning to Idle");foreach(var enemy in scene.GameObjects.Where(o=>o.GetComponent<SimpleEnemyAI3D>()!=null))enemy.GetComponent<HealthComponent>()!.Kill();}
  if(frame==350)Check(scene.FindGameObject("Victory message")!.GetComponent<UiText>()!.Visible,"Clearing all threats displays the result");
  if(frame==380){Check(Scenes.ActiveScene!.FindGameObject("FPS Character")!.GetComponent<HealthComponent>()!.CurrentHealth==100,"Enter restarts the combat prototype with fresh health");Directory.CreateDirectory(root+"/Preview");File.WriteAllText(root+"/Preview/combat-validation.json",JsonSerializer.Serialize(checks,new JsonSerializerOptions{WriteIndented=true}));Close();}
 }
 protected override void OnEngineRender(){if(capture==null)return;var pixels=new byte[WindowWidth*WindowHeight*4];GL.ReadPixels(0,0,WindowWidth,WindowHeight,PixelFormat.Bgra,PixelType.UnsignedByte,pixels);Directory.CreateDirectory(root+"/Preview");using var bmp=new System.Drawing.Bitmap(WindowWidth,WindowHeight,System.Drawing.Imaging.PixelFormat.Format32bppArgb);var d=bmp.LockBits(new(0,0,WindowWidth,WindowHeight),System.Drawing.Imaging.ImageLockMode.WriteOnly,bmp.PixelFormat);System.Runtime.InteropServices.Marshal.Copy(pixels,0,d.Scan0,pixels.Length);bmp.UnlockBits(d);bmp.RotateFlip(System.Drawing.RotateFlipType.RotateNoneFlipY);bmp.Save(root+"/Preview/"+capture);Console.WriteLine("CAPTURE "+capture);capture=null;}
 protected override void OnUnload(){Scenes.UnloadScene();runtime?.Dispose();base.OnUnload();}
}
sealed class MovementPreview(string root):ByteEngineApplication(900,650,"Arena movement validation")
{
 GameProjectRuntime? runtime;int frame;Vector3 start;bool airborne;
 protected override bool CloseOnEscape=>false;
 protected override void OnLoad(){base.OnLoad();IsVisible=false;runtime=new(root+"/ultra violence.byteproject",Console.WriteLine);Scenes.LoadScene(runtime.LoadStartupScene());}
 static void Check(bool yes,string message){if(!yes)throw new Exception(message);Console.WriteLine("PASS "+message);}
 protected override void OnEngineUpdate(){
  frame++;var scene=Scenes.ActiveScene!;var player=scene.FindGameObject("FPS Character")!;var motor=player.GetComponent<CharacterController3D>()!;
  player.GetComponent<PlayerController3D>()!.Enabled=false;player.GetComponent<HealthComponent>()!.Invulnerable=true;
  foreach(var o in scene.GameObjects){if(o.GetComponent<SimpleEnemyAI3D>()is {} ai)ai.Enabled=false;}
  typeof(Time).GetMethod("Update",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,[1d/60]);
  if(frame==20){start=player.Transform.WorldPosition;Check(motor.IsGrounded,"Native capsule grounds in arena");}
  if(frame>=20&&frame<60)motor.Move(-Vector3.UnitZ);
  if(frame==60){Check(start.Z-player.Transform.WorldPosition.Z>3.8f,"Native motor reaches fast arena speed over the floor");Check(Math.Abs(motor.Speed-7)<.15,"Live native speed matches event-authored 7m/s");}
  if(frame==85){Check(motor.Speed<.1,"Native motor releases ground movement without ice sliding");motor.Jump();}
  if(frame>=85&&frame<105)motor.Move(-Vector3.UnitZ);
  if(frame==95){airborne=!motor.IsGrounded;Check(airborne&&motor.VerticalVelocity>0,"Buffered jump lifts the native capsule");}
  if(frame==110)Check(!motor.IsGrounded&&motor.Speed>5,"Native airborne capsule retains momentum after input release");
  if(frame>=110&&frame<125)motor.Move(Vector3.UnitX);
  if(frame==125){Check(motor.Velocity.X>1&&motor.Speed<=13.1f,"Native air steering responds without unbounded acceleration");Close();}
 }
 protected override void OnUnload(){Scenes.UnloadScene();runtime?.Dispose();base.OnUnload();}
}
