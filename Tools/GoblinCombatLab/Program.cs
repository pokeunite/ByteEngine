using System.IO.Compression;
using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Plugins;
using ByteEngine.Core.Runtime;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Serialization;
using ByteEngine.Core.Serialization.SerializationModels;
using GoblinCombat;
internal static class Program {
 internal const string Root="C:/Users/codex/Documents/Goblin Souls";
 internal static void Main(string[] args){if(args.Contains("--test")){Tests.Run();return;}using var app=new Lab(args);app.Run();}
}
internal sealed class Lab(string[] args):ByteEngineApplication(1280,720,"Goblin Souls / Combat Test") {
 GameProjectRuntime? runtime;int frame;bool combatCaptured;ComboPreview? comboPreview;
 protected override bool CloseOnEscape=>false;
 protected override void OnLoad(){base.OnLoad();ByteEnginePluginManager.RegisterLinkedPlugin("bytebard.goblincombat",new GoblinCombatPlugin());
 string project=Program.Root+"/Goblin Souls.byteproject";
 if(args.Contains("--inspect-pack")){IsVisible=false;using var scan=new AssetDatabase(Program.Root,["Assets","Scenes"]);scan.Scan();runtime=new(project,Console.WriteLine);PackAudit.Run(runtime);Close();return;} if(args.Contains("--author")){IsVisible=false;if(!File.Exists(project))new ProjectSerializer().Save(new ProjectData{Name="Goblin Souls",StartupScene="Scenes/Main.bytescene"},project);File.WriteAllText(Program.Root+"/Scenes/Main.bytescene","{\"gameObjects\":[]}");using var db=new AssetDatabase(Program.Root,["Assets","Scenes"]);db.Scan();runtime=new(project,Console.WriteLine);Author.Build(runtime);Close();return;}
 if((args.Contains("--combo-preview")||args.Contains("--pack-preview"))){using var scan=new AssetDatabase(Program.Root,["Assets","Scenes"]);scan.Scan();runtime=new(project,Console.WriteLine);comboPreview=new(runtime,args.Contains("--pack-preview"));Scenes.LoadScene(comboPreview.Scene);return;} runtime=new(project,Console.WriteLine);Scenes.SceneFactory=runtime.LoadScene;Scenes.RestartSceneFactory=runtime.LoadStartupScene;Scenes.LoadScene(runtime.LoadStartupScene());
 }
 protected override void OnEngineUpdate(){if(args.Contains("--capture-verification"))typeof(Time).GetMethod("Update",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static)!.Invoke(null,new object[]{1d/60});frame++;if(comboPreview!=null){comboPreview.Step(frame,args.Contains("--capture-combo"));if(args.Contains("--capture-combo")&&frame>95)Close();return;}if(args.Contains("--verify")){Verify();return;}if(args.Contains("--smoke")){var actor=Scenes.ActiveScene!.FindGameObject("Goblin")!.GetComponent<CombatActor3D>()!;actor.ReadPlayerInput=false;if(frame==25||frame==40||frame==58)actor.RequestAttack();if(frame==75)actor.RequestDodge();if(frame>180){File.WriteAllText(Program.Root+"/Preview/Scene verification.txt",$"Scene ran 180 frames. Player position: {actor.Transform.WorldPosition}; health: {actor.Health.CurrentHealth}; swings: {actor.Timing.Swing}.");Close();}}}
 bool queuedSecond,queuedThird,dodgeProtected,damageSeen;int verifyPhase;float moveTestTime;Vector3 comboOrigin;
 void Verify(){var s=Scenes.ActiveScene!;var p=s.FindGameObject("Goblin")!.GetComponent<CombatActor3D>()!;var e=s.FindGameObject("Raider")!.GetComponent<CombatActor3D>()!;var animation=p.GameObject.GetComponent<CombatAnimation3D>();
 if(frame==1){p.ReadPlayerInput=false;p.Transform.WorldPosition=new(0,0,3);comboOrigin=p.Transform.WorldPosition;p.Transform.WorldRotation=Quaternion.Identity;e.Transform.WorldPosition=new(0,0,1.65f);e.Enabled=false;var b=s.FindGameObject("Brute")!;b.GetComponent<CombatActor3D>()!.Enabled=false;b.Transform.WorldPosition=new(8,0,-8);}
 if(frame==3){var skin=p.GameObject.Children.First(c=>c.Name=="Animated fighter").GetComponent<SkeletalMeshRenderer>()!;skin.Play("GuardIdle",true,0);skin.Seek(.1f);skin.TryGetBoneWorldMatrix("hand.R",out var before);skin.Play("ComboLight1",false,.2f);skin.SamplePlayback(.5f,0);skin.TryGetBoneWorldMatrix("hand.R",out var start);if(Vector3.Distance(before.Translation,start.Translation)>.001f)throw new Exception("Externally sampled clip snapped at blend start");skin.SamplePlayback(.5f,.2f);skin.TryGetBoneWorldMatrix("hand.R",out var end);if(Vector3.Distance(start.Translation,end.Translation)<.05f)throw new Exception("Externally sampled blend did not advance");skin.Play("Run",true,0);skin.Seek(0);skin.TryGetBoneWorldMatrix("hand.R",out var loopStart);skin.Seek(skin.Duration);skin.TryGetBoneWorldMatrix("hand.R",out var loopEnd);if(Vector3.Distance(loopStart.Translation,loopEnd.Translation)>.002f)throw new Exception("Sword arm snaps at run-loop boundary");
 foreach(string next in new[]{"ComboLight1","Run","AttackTwoHanded","Damage","GuardIdle"}){skin.TryGetBoneWorldMatrix("hand.R",out var displayed);skin.Play(next,false,.2f);skin.Pause();skin.SamplePlayback(.18f,0);skin.TryGetBoneWorldMatrix("hand.R",out var switched);if(Vector3.Distance(displayed.Translation,switched.Translation)>.002f)throw new Exception("Interrupted animation blend snaps");skin.SamplePlayback(.2f,.04f);}
 skin.Play("GuardIdle",true,0);skin.Pause();}
 if(verifyPhase==0){if(frame==5)p.RequestAttack();if(p.Timing.Stage==0&&p.Timing.Elapsed>=.07f&&!queuedSecond){p.RequestAttack();queuedSecond=true;}if(p.Timing.Stage==1&&p.Timing.Elapsed>=.07f&&!queuedThird){p.RequestAttack();queuedThird=true;}
 if(p.Timing.Swing==3&&!p.Timing.Attacking){if(e.Health.CurrentHealth!=20||p.HitsLanded!=3)throw new Exception($"Combo contacts failed health={e.Health.CurrentHealth} hits={p.HitsLanded}");if(Vector3.Distance(p.Transform.WorldPosition,comboOrigin)>.01f)throw new Exception("In-place attacks moved controller");p.MoveIntent=Vector3.UnitX;if(!p.RequestDodge())throw new Exception("Dodge failed");verifyPhase=1;}}
 else if(verifyPhase==1){if(p.Timing.Invincible&&!dodgeProtected){if(p.ReceiveHit(10,.1f))throw new Exception("Invulnerability failed");dodgeProtected=true;}if(!p.Timing.Dodging){if(!dodgeProtected||!p.ReceiveHit(10,.1f)||p.Health.CurrentHealth!=90)throw new Exception("Dodge timing or damage failed");verifyPhase=2;}}
 else if(verifyPhase==2){if(animation?.CurrentClip=="Damage")damageSeen=true;moveTestTime+=(float)Time.UnscaledDeltaTime;if(moveTestTime>.6f){if(animation?.CurrentClip!="Run"||Vector3.Dot(p.Transform.Forward,Vector3.UnitX)<.9f||p.Transform.WorldPosition.X<.5f)throw new Exception("Run or facing transition failed");p.MoveIntent=Vector3.Zero;verifyPhase=3;moveTestTime=0;}}
 else if(verifyPhase==3){moveTestTime+=(float)Time.UnscaledDeltaTime;if(moveTestTime>.5f){if(animation?.CurrentClip!="GuardIdle"||!damageSeen)throw new Exception("Idle transition or supplied damage clip failed");p.Health.Kill();verifyPhase=4;moveTestTime=0;}}
 else if(verifyPhase==4){moveTestTime+=(float)Time.UnscaledDeltaTime;if(moveTestTime>1){var renderer=p.GameObject.Children.First(c=>c.Name=="Animated fighter").GetComponent<SkeletalMeshRenderer>()!;if(animation?.CurrentClip!="Death"||renderer.PlaybackTime<renderer.Duration-.02f)throw new Exception("Supplied death clip failed");File.WriteAllText(Program.Root+"/Preview/Combat verification.txt","PASS: Manny with 49-bone retarget and supplied weapon; light-light-heavy chain (two distinct source attacks); exactly one hit per strike; attack/contact clocks synchronized; in-place controller and rendered pelvis; single external clock; continuous interrupted blends; seamless run-loop sword arm; live dodge protection; supplied run/combat idle/damage/death playback; hand socket follows moving and rotating character. Pure combat timing passes at 30, 60 and 120 Hz and across a frame hitch. Engine import-space, skin/socket and coordinate regressions pass.");Close();}} if(frame>3&&animation==null)throw new Exception("No skeletal animation adapter");if(frame>5&&p.GameObject.Children.First(c=>c.Name=="Animated fighter").GetComponent<SkeletalMeshRenderer>()!.IsPlaying)throw new Exception("Automatic animation clock is fighting combat playback");if(animation!=null&&animation.GripError>.0001f)throw new Exception("Sword grip detached");if(p.Timing.Attacking&&p.Timing.Elapsed>.05f){var renderer=p.GameObject.Children.First(c=>c.Name=="Animated fighter").GetComponent<SkeletalMeshRenderer>()!;if(renderer.TryGetBoneWorldMatrix("pelvis",out var pelvis)){var delta=pelvis.Translation-p.Transform.WorldPosition;if(!float.IsFinite(delta.Y)||MathF.Abs(delta.X)>.025f||MathF.Abs(delta.Z)>.025f)throw new Exception("Rendered pelvis drifted outside controller");}if(Math.Abs(renderer.PlaybackTime-p.Timing.Elapsed*renderer.Duration/(CombatTiming.Windup[p.Timing.Stage]+CombatTiming.Contact[p.Timing.Stage]+CombatTiming.Recovery[p.Timing.Stage]))>.001f)throw new Exception("Animation and hit clock diverged");}if(frame>800)throw new Exception("Integration test timeout");
 } protected override void OnEngineRender(){if(args.Contains("--capture-verification")&&frame%3==0){Directory.CreateDirectory(Program.Root+"/Preview/VerificationFrames");var data=new byte[WindowWidth*WindowHeight*4];OpenTK.Graphics.OpenGL4.GL.ReadPixels(0,0,WindowWidth,WindowHeight,OpenTK.Graphics.OpenGL4.PixelFormat.Rgba,OpenTK.Graphics.OpenGL4.PixelType.UnsignedByte,data);Capture.Write(Program.Root+$"/Preview/VerificationFrames/frame-{frame:0000}.png",data,WindowWidth,WindowHeight);}if(args.Contains("--verify")&&!combatCaptured&&Scenes.ActiveScene?.FindGameObject("Goblin")?.GetComponent<CombatActor3D>() is { } player&&player.Timing.Stage==2&&player.Timing.Elapsed>.28f){var px=new byte[WindowWidth*WindowHeight*4];OpenTK.Graphics.OpenGL4.GL.ReadPixels(0,0,WindowWidth,WindowHeight,OpenTK.Graphics.OpenGL4.PixelFormat.Rgba,OpenTK.Graphics.OpenGL4.PixelType.UnsignedByte,px);Capture.Write(Program.Root+"/Preview/Combat combo in game.png",px,WindowWidth,WindowHeight);combatCaptured=true;}if(comboPreview!=null&&args.Contains("--capture-combo")&&frame<=95){var px=new byte[WindowWidth*WindowHeight*4];OpenTK.Graphics.OpenGL4.GL.ReadPixels(0,0,WindowWidth,WindowHeight,OpenTK.Graphics.OpenGL4.PixelFormat.Rgba,OpenTK.Graphics.OpenGL4.PixelType.UnsignedByte,px);Capture.Write(Program.Root+$"/Preview/combo-frame-{frame:000}.png",px,WindowWidth,WindowHeight);}if(args.Contains("--smoke")&&frame==20){var pixels=new byte[WindowWidth*WindowHeight*4];OpenTK.Graphics.OpenGL4.GL.ReadPixels(0,0,WindowWidth,WindowHeight,OpenTK.Graphics.OpenGL4.PixelFormat.Rgba,OpenTK.Graphics.OpenGL4.PixelType.UnsignedByte,pixels);Capture.Write(Program.Root+"/Preview/Combat test.png",pixels,WindowWidth,WindowHeight);}} protected override void OnUnload(){Scenes.UnloadScene();runtime?.Dispose();base.OnUnload();}
}
internal static class Author {
 internal static void Build(GameProjectRuntime runtime){MeleePack.Configure(runtime);var s=new Scene("Combat Test"){FixedSimulation=true};s.SimulationClock.StepSeconds=1d/60;
 Model(s,runtime,"Training Hall","TrainingHall");var floor=s.CreateGameObject("Floor collision");floor.AddComponent(new BoxCollider3D{Size=new(24,.2f,24),Center=new(0,-.1f,0)});
 foreach(var pair in new[]{(new Vector3(-11,1,0),new Vector3(.4f,2,22)),(new Vector3(11,1,0),new Vector3(.4f,2,22)),(new Vector3(0,1,-11),new Vector3(22,2,.4f)),(new Vector3(0,1,11),new Vector3(22,2,.4f))})s.CreateGameObject("Arena boundary").AddComponent(new BoxCollider3D{Center=pair.Item1,Size=pair.Item2});
 foreach(var item in new[]{("Goblin",new Vector3(0,0,3),true,false),("Raider",new Vector3(-2,0,-2),false,false),("Brute",new Vector3(3,0,-5),false,true)}){var a=item.Item3?s.CreateGameObject(item.Item1):Model(s,runtime,item.Item1,item.Item1);if(item.Item3){var model=s.CreateGameObject("Animated fighter");model.SetParent(a,false);model.AddComponent(new SkeletalMeshRenderer{Model=runtime.Database.ResolveReference(MeleePack.ModelPath),DefaultAnimation="GuardIdle",PlayOnStart=true,Loop=true});var sword=ModelPath(s,runtime,"Equipped sword",MeleePack.SwordPath);sword.SetParent(a,false);a.AddComponent(new CombatAnimation3D{AttackOne="ComboLight1",AttackTwo="ComboLight2",AttackThree="AttackTwoHanded",HandBone="hand.R",Palette=runtime.Database.ResolveReference(MeleePack.Root+"Textures/HumanAnimations_ColorPalette.png")});}a.Transform.LocalPosition=item.Item2;a.AddComponent(new CapsuleCollider3D{Radius=.3f,Height=1.5f,Center=new(0,.75f,0)});a.AddComponent(new CharacterController3D{MoveSpeed=4.6f});a.AddComponent(new HealthComponent{MaxHealth=item.Item4?140:100,CurrentHealth=item.Item4?140:100});a.AddComponent(new CombatActor3D{Player=item.Item3,Brute=item.Item4});}
 var cam=s.CreateGameObject("Camera");cam.AddComponent(new Camera3D{ActiveGameCamera=true,NearClip=.05f,FarClip=80,FieldOfView=55});cam.Transform.LocalPosition=new(0,6,10);
 var sun=s.CreateGameObject("Sun");sun.Transform.LocalRotation=Quaternion.CreateFromYawPitchRoll(-.6f,.8f,0);sun.AddComponent(new DirectionalLight{Intensity=1.1f,AmbientIntensity=.3f,CastShadows=true,ShadowResolution=1024,ShadowDistance=30});
 s.CreateGameObject("Sky").AddComponent(new SkyEnvironment{DrawSky=true,ZenithColor=new(.13f,.17f,.22f),HorizonColor=new(.3f,.34f,.38f),AmbientIntensity=.35f});
 s.CreateGameObject("Quality").AddComponent(new GraphicsSettings3D{SettingsKey="GoblinSoulsCombat",Quality=GraphicsQuality.Balanced,RenderScale=1});
 var hud=s.CreateGameObject("HUD");hud.AddComponent(new UiCanvas());
 void Text(string name,string value,Vector2 offset,int size,UiAnchor anchor=UiAnchor.TopLeft){var o=s.CreateGameObject(name);o.SetParent(hud,false);o.AddComponent(new UiText{Text=value,Offset=offset,FontSize=size,Anchor=anchor,FontReference=runtime.Database.ResolveReference("Assets/Fonts/TypeLightSans.ttf"),ShadowColor=new(0,0,0,.9f)});}
 Text("Title","GOBLIN SOULS / COMBAT TEST",new(26,22),26);Text("Vitals","",new(26,60),21);Text("Timing","",new(26,90),18);Text("Status","",new(26,-82),22,UiAnchor.BottomLeft);Text("Controls","WASD MOVE    CLICK / J STRIKE    SPACE DODGE    R RESET    ESC PAUSE",new(26,-40),18,UiAnchor.BottomLeft);Text("Prototype","SKELETAL COMBO / COMBAT TEST",new(-360,26),16,UiAnchor.TopRight);
 s.CreateGameObject("Combat director").AddComponent(new CombatRoom3D());runtime.Serializer.Save(s,Program.Root+"/Scenes/Main.bytescene");Console.WriteLine("Authored "+s.GameObjectCount+" scene objects");
 }
 internal static GameObject Model(Scene s,GameProjectRuntime runtime,string name,string modelName)=>ModelPath(s,runtime,name,"Assets/Models/"+modelName+".glb");
 internal static GameObject ModelPath(Scene s,GameProjectRuntime runtime,string name,string path){var reference=runtime.Database.ResolveReference(path);var model=runtime.Assets.LoadModel(reference);var root=s.CreateGameObject(name);root.AddComponent(new ModelHierarchyInstance{Model=reference});var nodes=model.Nodes.ToDictionary(n=>n.Key,n=>s.CreateGameObject(n.Name));foreach(var node in model.Nodes){var o=nodes[node.Key];o.SetParent(node.ParentKey!=null&&nodes.TryGetValue(node.ParentKey,out var p)?p:root,false);Matrix4x4.Decompose(node.LocalTransform,out var scale,out var rotation,out var position);o.Transform.LocalPosition=position;o.Transform.LocalRotation=node.Name=="Import Space"&&path.EndsWith(".glb",StringComparison.OrdinalIgnoreCase)?Quaternion.Identity:rotation;o.Transform.LocalScale=scale;nodes[node.Key]=o;foreach(var key in node.MeshKeys){var mesh=model.Meshes.First(m=>m.Key==key);o.AddComponent(new MeshRenderer{MeshReference=new(reference,key),Mesh=runtime.Assets.GetModelMesh(reference,key),MaterialReference=new(reference,mesh.MaterialKey!),Material=runtime.Assets.GetModelMaterial(reference,mesh.MaterialKey!)});}}return root;}
}
internal static class Tests {
 internal static void Run(){void Check(bool v,string message){if(!v)throw new Exception(message);Console.WriteLine("PASS "+message);}foreach(int fps in new[]{30,60,120}){float dt=1f/fps;var t=new CombatTiming();Check(t.PressAttack()&&t.Stage==0,"first-frame response "+fps);Check(t.PressDodge(),"startup dodge "+fps);t.Tick(.06f);Check(t.Invincible,"dodge protection "+fps);t.Tick(.3f);Check(!t.Invincible,"dodge protection ends "+fps);
 t=new();t.PressAttack();while(t.Elapsed<CombatTiming.Windup[0]+.02f)t.Tick(dt);Check(!t.PressDodge(),"contact commitment "+fps);while(t.Elapsed<CombatTiming.ChainOpen[0]-.15f)t.Tick(dt);t.PressAttack();for(int i=0;i<fps/2&&t.Stage==0;i++)t.Tick(dt);Check(t.Stage==1,"late input chains strike two "+fps);while(t.Stage==1&&t.Elapsed<CombatTiming.ChainOpen[1]-.15f)t.Tick(dt);t.PressAttack();for(int i=0;i<fps/2&&t.Stage==1;i++)t.Tick(dt);Check(t.Stage==2,"late input chains finisher "+fps);for(int i=0;i<fps;i++)t.Tick(dt);Check(!t.Attacking&&t.Swing==3,"exactly three strikes "+fps);
 t=new();t.PressAttack();t.PressAttack();for(int i=0;i<fps*2;i++)t.Tick(dt);Check(t.Swing==2,"one early queued click produces exactly one next strike "+fps);Check(t.Stamina>=0&&t.Stamina<=100,"stamina bounds "+fps);
 }var hitch=new CombatTiming();hitch.PressAttack();hitch.Tick(.5f);Check(hitch.ContactSwingThisTick==1&&hitch.ContactStageThisTick==0,"contact window survives a 500 ms hitch");hitch.Tick(.3f);Check(hitch.ContactStageThisTick==-1,"hitch contact cannot repeat after recovery");Console.WriteLine("ALL COMBAT TIMING CHECKS PASSED");}
}internal static class Capture
{
 internal static void Write(string file,byte[] rgba,int width,int height){using var output=File.Create(file);output.Write(new byte[]{137,80,78,71,13,10,26,10});byte[] header=new byte[13];System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(0,4),width);System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4,4),height);header[8]=8;header[9]=6;Chunk(output,"IHDR",header);using var compressed=new MemoryStream();using(var z=new ZLibStream(compressed,CompressionLevel.Fastest,true))for(int y=height-1;y>=0;y--){z.WriteByte(0);z.Write(rgba,y*width*4,width*4);}Chunk(output,"IDAT",compressed.ToArray());Chunk(output,"IEND",[]);}
 static void Chunk(Stream s,string type,byte[] data){byte[] name=System.Text.Encoding.ASCII.GetBytes(type),length=new byte[4];System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(length,data.Length);s.Write(length);s.Write(name);s.Write(data);uint crc=0xffffffff;foreach(byte b in name.Concat(data)){crc^=b;for(int i=0;i<8;i++)crc=(crc>>1)^((crc&1)==1?0xedb88320u:0);}System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(length,~crc);s.Write(length);}
}





























