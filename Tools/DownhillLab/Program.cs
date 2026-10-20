using System.Numerics;
using System.Text.Json;
using System.IO.Compression;
using System.Reflection;
using ByteEngine.Core.InputSystem;
using OpenTK.Graphics.OpenGL4;
using ByteEngine.Core;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Audio;
using ByteEngine.Core.Animation;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Physics;
using ByteEngine.Core.Plugins;
using ByteEngine.Core.Runtime;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Serialization;
using Downhill;

internal static class Program
{
 internal const string Root="C:/Users/codex/Documents/Legends Road";
 internal static string Project=>Root+"/Legends Road.byteproject";
 static void Main(string[] args){using var app=new Lab(args);app.Run();}
}
internal sealed class Lab(string[] args):ByteEngineApplication(1280,720,"Legends Road / Ridge Run")
{
 GameProjectRuntime? runtime; int frame; Vector3[] route=[];readonly System.Diagnostics.Stopwatch watch=System.Diagnostics.Stopwatch.StartNew();Scene? beforeRetry;
 ControllerCheck? controllerCheck;
 PoseCheck? poseCheck;
 PerformanceCheck? performanceCheck;
 static readonly MethodInfo SetTime=typeof(Time).GetMethod("Update",BindingFlags.Static|BindingFlags.NonPublic)!;
 static readonly MethodInfo SetInput=typeof(Input).GetMethod("UpdatePortable",BindingFlags.Static|BindingFlags.NonPublic)!;
 protected override bool CloseOnEscape=>false;
 protected override void OnLoad(){base.OnLoad();
  ByteEnginePluginManager.RegisterLinkedPlugin("bytebard.downhill",new DownhillPlugin());
  if(args.Contains("--audit-push")){IsVisible=false;PushAudit.Run();Close();return;}
  if(args.Contains("--align-push")){IsVisible=false;PushAudit.Align();Close();return;}
  if(args.Contains("--calibrate-rider")){IsVisible=false;RiderCalibration.Run();Close();return;}
  if(args.Contains("--audit-poses")){IsVisible=false;RiderCalibration.Audit();Close();return;}
  if(args.Contains("--import-hands")){IsVisible=false;RiderCalibration.ImportHands();Close();return;}
  if(args.Contains("--controller-check")){IsVisible=false;controllerCheck=new();Scenes.LoadScene(controllerCheck.Scene);return;}
  if(args.Contains("--author")||args.Contains("--inspect"))IsVisible=false;
  Directory.CreateDirectory(Program.Root+"/Preview");
  if(args.Contains("--author")||args.Contains("--inspect")){
   using var db=new AssetDatabase(Program.Root,["Assets","Scenes"],Console.WriteLine,Console.WriteLine);
   db.Scan();using var assets=new AssetManager(db,Console.WriteLine);
   var model=assets.LoadModel(db.ResolveReference("Assets/Animations/Longboard/neutral ride.fbx"));
   Console.WriteLine("MODEL nodes="+model.Nodes.Count+" meshes="+model.Meshes.Count+" animations="+model.Animations.Count);
   foreach(var node in model.Nodes.Where(n=>n.Name.Contains("Foot")||n.ParentKey==null))Console.WriteLine(node.Name+" "+node.LocalTransform.Translation);
   var vertices=model.Meshes.SelectMany(m=>Enumerable.Range(0,m.Vertices.Length/8).Select(i=>new Vector3(m.Vertices[i*8],m.Vertices[i*8+1],m.Vertices[i*8+2]))).ToArray();
   Console.WriteLine("BOUNDS "+vertices.Aggregate(Vector3.Min)+" "+vertices.Aggregate(Vector3.Max));
   if(args.Contains("--author"))Author.Build(db,assets);
   Close();return;
  }
  runtime=new(Program.Project,Console.WriteLine);Scenes.SceneFactory=runtime.LoadScene;Scenes.RestartSceneFactory=runtime.LoadStartupScene;Scenes.LoadScene(runtime.LoadStartupScene());
  if(args.Contains("--controls-test"))Scenes.ActiveScene!.FindComponent<GraphicsSettings3D>()!.Quality=GraphicsQuality.Balanced;
  route=JsonDocument.Parse(File.ReadAllText(Program.Root+"/ArtSource/ThreeJS/course-layout.json")).RootElement.GetProperty("points").EnumerateArray().Select(p=>{var a=p.GetProperty("p");return new Vector3(a[0].GetSingle(),a[1].GetSingle(),a[2].GetSingle());}).ToArray();watch.Restart();
  if(args.Contains("--push-check")||args.Contains("--pose-check"))PreparePushCheck();
  if(args.Contains("--pose-check")){poseCheck=new(Scenes.ActiveScene!);if(args.Contains("--hands-test"))frame=1800;if(args.Contains("--tuck-test"))frame=1500;}
  if(args.Contains("--performance-check"))performanceCheck=new(Scenes.ActiveScene!,args.Contains("--early-pose"));
 }
 void PreparePushCheck(){var s=Scenes.ActiveScene!;var b=s.FindGameObject("Board")!;b.Transform.WorldPosition=new(0,.13f,0);b.Transform.WorldRotation=Quaternion.Identity;b.GetComponent<ByteEngine.Core.Physics.Rigidbody3D>()!.Enabled=false;b.GetComponent<BoardController3D>()!.Enabled=false;b.GetComponent<BoardFeedback3D>()!.Enabled=false;s.FindGameObject("Run director")!.GetComponent<DownhillRun3D>()!.Enabled=false;foreach(string name in new[]{"Road","Mountain landscape","Guardrail collision","HUD"})s.FindGameObject(name)!.Active=false;var floor=s.CreateGameObject("Animation inspection floor");floor.Transform.LocalPosition=new(0,-.05f,0);floor.Transform.LocalScale=new(8,.1f,8);floor.AddComponent(new MeshRenderer{UsePrimitive=true,Material=new(){BaseColor=new(.15f,.18f,.2f,1),Roughness=1}});var camera=s.FindGameObject("Follow camera")!;camera.GetComponent<BoardCamera3D>()!.Enabled=false;PlacePushCamera(new(2.8f,1.25f,-2.4f));}
 void PlacePushCamera(Vector3 position){var camera=Scenes.ActiveScene!.FindGameObject("Follow camera")!;camera.Transform.WorldPosition=position;Matrix4x4.Invert(Matrix4x4.CreateLookAt(position,new(0,.85f,0),Vector3.UnitY),out var basis);camera.Transform.WorldRotation=Quaternion.CreateFromRotationMatrix(basis);}
 readonly List<Vector3> pushFootSamples=[];float plantedError;
 protected override void OnEngineUpdate(){frame++;
  if(poseCheck!=null||performanceCheck!=null||args.Contains("--drive-test")){if(WindowState==OpenTK.Windowing.Common.WindowState.Minimized)WindowState=OpenTK.Windowing.Common.WindowState.Normal;if(WindowWidth!=1280||WindowHeight!=720)ClientSize=new(1280,720);}
  if(poseCheck!=null){SetTime.Invoke(null,[1d/60]);if(poseCheck.Step(frame))Close();return;}
  if(performanceCheck!=null){SetTime.Invoke(null,[1d/60]);if(performanceCheck.Step(frame))Close();return;}
  if(controllerCheck!=null){SetTime.Invoke(null,[1d/60]);if(controllerCheck.Step(frame)){File.WriteAllText(Program.Root+"/Preview/Controller verification.txt","PASS straight braking without yaw; force-based slide breakaway; release to recover grip; camera follows travel during drift.\n");Close();}return;}
  if(args.Contains("--push-check")){SetTime.Invoke(null,[1d/60]);var scene=Scenes.ActiveScene!;var pushBoard=scene.FindGameObject("Board")!.GetComponent<BoardController3D>()!;pushBoard.PushInput=frame>=60&&frame<270;typeof(BoardController3D).GetProperty("PushPhase")!.SetValue(pushBoard,((frame-60+69)%69)/69f);if(frame==150)PlacePushCamera(new(3.1f,1.05f,0));var renderer=scene.FindGameObject("Rider")!.GetComponent<SkeletalMeshRenderer>()!;if(frame>100&&frame<260){if(renderer.TryGetBoneWorldMatrix("mixamorig:LeftFoot",out var left))plantedError=Math.Max(plantedError,Vector3.Distance(left.Translation,Vector3.Transform(scene.FindGameObject("Rider")!.GetComponent<BoardRider3D>()!.LeftFootAnchor,pushBoard.Transform.WorldMatrix)));if(renderer.TryGetBoneWorldMatrix("mixamorig:RightFoot",out var right))pushFootSamples.Add(right.Translation);}
   if(frame==330){var range=pushFootSamples.Aggregate(Vector3.Max)-pushFootSamples.Aggregate(Vector3.Min);Console.WriteLine($"PUSH CHECK plantedError={plantedError:0.000000} range={range}");if(plantedError>.015f||range.Z<range.X*2||range.Z<.35f)throw new Exception("Push alignment/stance test failed");File.WriteAllText(Program.Root+"/Preview/Push verification.txt",$"PASS planted-foot error {plantedError:0.000000} m; free-foot range {range}.\nVerified three repeated push cycles and ride transitions. Handling unchanged.\n");Close();}return;}
  if(args.Contains("--controls-test")){var scene=Scenes.ActiveScene!;var run=scene.FindGameObject("Run director")!.GetComponent<DownhillRun3D>()!;
   void KeyPress(Key key){var snapshot=new RawInputSnapshot();snapshot.KeysDown.Add(key);SetInput.Invoke(null,[snapshot,false]);}
   if(frame==10){var snapshot=new RawInputSnapshot();snapshot.KeysDown.Add(Key.C);snapshot.KeysDown.Add(Key.A);SetInput.Invoke(null,[snapshot,false]);}
   if(frame==11){var b=scene.FindGameObject("Board")!.GetComponent<BoardController3D>()!;if(!b.HandDragInput||!b.SlideInput||b.SteerInput>=0)throw new Exception("C + A does not request a hands-down slide");Console.WriteLine("PASS C + A hands-down slide binding");}
   if(frame==30||frame==60)KeyPress(Key.Escape);
   if(frame==40&&(!run.Paused||scene.SimulationTimeScale!=0))throw new Exception("Pause failed");
   if(frame==70&&(run.Paused||scene.SimulationTimeScale!=1))throw new Exception("Resume failed");
   if(frame==90){beforeRetry=scene;KeyPress(Key.R);}
   if(frame==110&&ReferenceEquals(beforeRetry,scene))throw new Exception("Retry failed");
   if(frame==120)scene.FindGameObject("Board")!.GetComponent<BoardController3D>()!.ResetAt(run.FinishCenter+Vector3.UnitY*.12f,Quaternion.Identity);
   if(frame==130&&(!run.Finished||!scene.FindGameObject("Result panel")!.Active))throw new Exception("Finish panel failed");
   if(frame==140)KeyPress(Key.Q);
   if(frame==150){var graphics=scene.FindComponent<GraphicsSettings3D>()!;if(graphics.Quality!=GraphicsQuality.High)throw new Exception("Quality toggle failed");graphics.Quality=GraphicsQuality.Balanced;}
   if(frame==155){Console.WriteLine("PASS pause/resume, retry, delivery panel and quality toggle");Close();}return;
  }
  if(!args.Contains("--smoke")&&!args.Contains("--drive-test"))return;
  if(args.Contains("--drive-test"))SetTime.Invoke(null,[1d/30]);
  var board=Scenes.ActiveScene?.FindGameObject("Board")?.GetComponent<BoardController3D>();
  if(board!=null){board.ReadPlayerInput=false;board.PushInput=frame<180;
   if(args.Contains("--drive-test")){int nearest=Enumerable.Range(0,route.Length).MinBy(i=>Vector3.DistanceSquared(route[i],board.Transform.WorldPosition));var aim=route[Math.Min(route.Length-1,nearest+4)]-board.Transform.WorldPosition;aim.Y=0;aim=Vector3.Normalize(aim);var f=board.Transform.Forward;f.Y=0;f=Vector3.Normalize(f);float angle=MathF.Atan2(Vector3.Dot(aim,board.Transform.Right),Vector3.Dot(aim,f));float maxRate=Math.Max(.1f,board.TurnRateAt(board.Speed))*MathF.PI/180;board.SteerInput=Math.Clamp(2*board.Speed*MathF.Sin(angle)/8/maxRate,-1,1);board.BrakeInput=board.Speed>6;board.PushInput=board.Speed<3;}
   if(args.Contains("--slide-lap")){board.SlideInput=frame is >=360 and <390;if(board.SlideInput){board.SteerInput=.5f;board.BrakeInput=false;}if(frame==389&&!board.IsSliding)throw new Exception("Corner slide was not entered");}
   if(frame%60==0)Console.WriteLine($"FRAME {frame} pos={board.Transform.WorldPosition} speed={board.Speed} grounded={board.Grounded} footError={Scenes.ActiveScene?.FindGameObject("Rider")?.GetComponent<BoardRider3D>()?.FootError} sliding={board.IsSliding} fps={frame/watch.Elapsed.TotalSeconds:0.0} physicsMs={Scenes.ActiveScene?.Physics.LastStepMilliseconds:0.0}");
   if(args.Contains("--drive-test")&&Scenes.ActiveScene?.FindGameObject("Run director")?.GetComponent<DownhillRun3D>()?.Finished==true){Console.WriteLine("PASS FULL COURSE");CaptureNext=true;}
  }
  if(frame>=4200&&args.Contains("--drive-test")&&!CaptureNext)throw new Exception("Course test did not finish");
  if(frame>=(args.Contains("--drive-test")?4200:420)||CaptureNext)Close();
 }
 bool CaptureNext;
 protected override void OnEngineRender(){bool push=args.Contains("--push-check")&&new[]{45,90,110,135,170,190,215,260,300}.Contains(frame);if(poseCheck?.Capture==true||push||(args.Contains("--smoke")||args.Contains("--drive-test"))&&(frame==30||frame==180||frame==400||frame%600==0||CaptureNext)){var pixels=new byte[WindowWidth*WindowHeight*4];GL.ReadPixels(0,0,WindowWidth,WindowHeight,PixelFormat.Rgba,PixelType.UnsignedByte,pixels);Capture.Write(Program.Root+$"/Preview/{(poseCheck!=null?"pose-"+poseCheck.Label:push?"push":"ride")}-{frame}.png",pixels,WindowWidth,WindowHeight);}}
 protected override void OnUnload(){Scenes.UnloadScene();runtime?.Dispose();base.OnUnload();}
}
internal static class Author
{
 internal static void Build(AssetDatabase db,AssetManager assets){
  string root=Program.Root;
  var targetRef=db.ResolveReference("Assets/Animations/Longboard/neutral ride.fbx");var target=assets.LoadModel(targetRef);
  var clips=new Dictionary<string,string>{{"Neutral","neutral ride.fbx"},{"Push","push.fbx"},{"Soft Heel","Soft Heelside Carve.fbx"},{"Soft Toe","Soft Toeside Carve.fbx"},{"Hard Heel","11_Hard_Heelside_Grip_Carve.fbx"},{"Hard Toe","12_Hard_Toeside_Grip_Carve.fbx"},{"Tuck","Tuck loop.fbx"},{"Tuck Heel","Tuck Carve Heelside.fbx"},{"Tuck Toe","Tuck Carve Toeside.fbx"},{"Heelside Slide","21_Heelside_Stand_Up_Speed_Check.fbx"},{"Toeside Slide","22_Toeside_Stand_Up_Speed_Check.fbx"},{"Foot Brake","23_Foot_Brake.fbx"}};
  foreach(var c in clips){if(target.Animations.Any(a=>a.Name==c.Key))continue;var reference=db.ResolveReference("Assets/Animations/Longboard/"+c.Value);var source=assets.LoadModel(reference);var clip=source.Animations.First();ModelOwnedAnimationStore.Bake(root,target,clip,c.Key,source.Guid,reference.CachedProjectPath,clip.Key,clip.Name,true);}
  var profile=new AnimationProfile{Name="Longboard Riding",Rig=new(){ReferenceModel=targetRef,Type=AnimationRigType.Humanoid},Locomotion=new(){Enabled=false,RootMotionMode=RootMotionMode.InPlace}};
  profile.BlendSpaces.Add(new(){Name="Ride",ParameterX="Steer",MinX=-1,MaxX=1,Samples=[new(){Clip="Hard Heel",X=-1},new(){Clip="Soft Heel",X=-.4f},new(){Clip="Neutral",X=0},new(){Clip="Soft Toe",X=.4f},new(){Clip="Hard Toe",X=1}]});
  profile.BlendSpaces.Add(new(){Name="Tuck",ParameterX="Steer",MinX=-1,MaxX=1,Samples=[new(){Clip="Tuck Heel",X=-1},new(){Clip="Tuck",X=0},new(){Clip="Tuck Toe",X=1}]});
  profile.Procedural.FootIkEnabled=true;profile.Procedural.HandIkEnabled=true;
  foreach(string side in new[]{"Left","Right"})profile.Procedural.IkChains.Add(new(){Name=side+"Foot",RootBone="mixamorig:"+side+"UpLeg",MidBone="mixamorig:"+side+"Leg",EndBone="mixamorig:"+side+"Foot",Weight=1,FootGrounding=false,PoleOffset=new(1,.4f,0)});
  AnimationProfileSerializer.Save(root+"/Assets/Animations/Longboard/Riding.byteanim",profile);db.Scan();
  using var runtime=new GameProjectRuntime(Program.Project,Console.WriteLine);
  var scene=new Scene("Ridge Run"){FixedSimulation=true};scene.SimulationClock.StepSeconds=1.0/120;scene.SimulationClock.MaximumCatchUpSteps=6;
  var layout=JsonDocument.Parse(File.ReadAllText(root+"/ArtSource/ThreeJS/course-layout.json")).RootElement;
  Vector3 Vec(JsonElement a)=>new(a[0].GetSingle(),a[1].GetSingle(),a[2].GetSingle());
  var road=Model(scene,runtime,"Road","Assets/Models/Downhill/RidgeRoad.glb",true);
  Model(scene,runtime,"Mountain landscape","Assets/Models/Downhill/RidgeLandscape.glb",false);
  var samples=layout.GetProperty("points").EnumerateArray().Select(p=>Vec(p.GetProperty("p"))).ToArray();
  float halfWidth=JsonDocument.Parse(File.ReadAllText(root+"/ArtSource/ThreeJS/course.json")).RootElement.GetProperty("roadWidth").GetSingle()*.5f;
  // Batch immutable rail volumes into one native BVH-backed static mesh.
  // This keeps exact local contacts without scanning dozens of box actors per wheel cast.
  var railVertices=new List<Vector3>();var railTriangles=new List<uint>();uint[] boxFaces=[0,1,3,0,3,2,4,6,7,4,7,5,0,4,5,0,5,1,2,3,7,2,7,6,0,2,6,0,6,4,1,5,7,1,7,3];
  for(int i=40;i<samples.Length-6;i+=2){var a=samples[i];var b=samples[i+2];var direction=Vector3.Normalize(b-a);var right=Vector3.Normalize(Vector3.Cross(direction,Vector3.UnitY));var position=(a+b)*.5f+right*(halfWidth+.8f)+Vector3.UnitY*.4f;var rotation=Quaternion.CreateFromYawPitchRoll(MathF.Atan2(-direction.X,-direction.Z),0,0);var half=new Vector3(.08f,.425f,Vector3.Distance(a,b)*.5f);uint first=(uint)railVertices.Count;foreach(int x in new[]{-1,1})foreach(int y in new[]{-1,1})foreach(int z in new[]{-1,1})railVertices.Add(position+Vector3.Transform(half*new Vector3(x,y,z),rotation));railTriangles.AddRange(boxFaces.Select(index=>first+index));}
  scene.CreateGameObject("Guardrail collision").AddComponent(new MeshCollider3D{Vertices=railVertices.ToArray(),Triangles=railTriangles.ToArray()});
  var board=scene.CreateGameObject("Board");board.Transform.LocalPosition=Vec(layout.GetProperty("spawn"))-Vector3.UnitY*.17f;
  var forward=Vec(layout.GetProperty("forward"));Matrix4x4.Invert(Matrix4x4.CreateLookAt(Vector3.Zero,forward,Vector3.UnitY),out var boardBasis);board.Transform.LocalRotation=Quaternion.CreateFromRotationMatrix(boardBasis);
  foreach(float x in new[]{-.155f,.155f})foreach(float z in new[]{-.38f,.38f})board.AddComponent(new CapsuleCollider3D{Radius=.037f,Height=.074f,Center=new(x,-.077f,z)});
  board.AddComponent(new Rigidbody3D{Mass=80,UseGravity=true,AllowSleep=false,SimulateRotation=false,ContinuousCollision=true,Friction=.005f,LinearDamping=.002f});
  board.AddComponent(new BoardController3D{LowSpeedSteeringDegrees=15,HighSpeedSteeringDegrees=2.4f});
  board.AddComponent(new BoardFeedback3D());
  foreach(var sound in new[]{("Wheel audio","Rolling.wav"),("Wind audio","Wind.wav")}){var audio=scene.CreateGameObject(sound.Item1);audio.SetParent(board,false);audio.AddComponent(new AudioSource3D{ClipReference=db.ResolveReference("Assets/Audio/"+sound.Item2),PlayOnStart=true,Loop=true,Spatial=false,Volume=0});}
  var deck=Model(scene,runtime,"Longboard","Assets/Models/Downhill/Longboard.glb",false);deck.SetParent(board,false);
  var rider=scene.CreateGameObject("Rider");rider.SetParent(board,false);
  rider.Transform.LocalRotation=Quaternion.CreateFromAxisAngle(Vector3.UnitY,.913f+MathF.PI);rider.Transform.LocalPosition=new(.122f,.024f,.058f);
  rider.AddComponent(new SkeletalMeshRenderer{Model=targetRef,PlayOnStart=true,DefaultAnimation="Neutral",Loop=true});
  rider.AddComponent(new BoardRider3D{PoseProfile=db.ResolveReference("Assets/Animations/Longboard/Riding.byteanim"),LeftFootAnchor=new(0,.142f,-.153f),RightFootAnchor=new(0,.147f,.153f),FootLockWeight=1});
  var camera=scene.CreateGameObject("Follow camera");camera.AddComponent(new Camera3D{ActiveGameCamera=true,NearClip=.05f,FarClip=1200});camera.AddComponent(new AudioListener3D());camera.AddComponent(new BoardCamera3D{Distance=5,Height=2.1f,LookAhead=4});
  var sun=scene.CreateGameObject("Late afternoon sun");sun.Transform.LocalRotation=Quaternion.CreateFromYawPitchRoll(-.6f,.8f,0);sun.AddComponent(new DirectionalLight{Intensity=1.15f,AmbientIntensity=.25f,CastShadows=true,ShadowResolution=1024,ShadowDistance=55});
  scene.CreateGameObject("Mountain atmosphere").AddComponent(new SkyEnvironment{DrawSky=true,ZenithColor=new(.12f,.31f,.51f),HorizonColor=new(.7f,.78f,.78f),AmbientIntensity=.28f,FogEnabled=true,FogColor=new(.58f,.69f,.72f),FogStartDistance=100,FogEndDistance=650,FogDensity=.001f});
  scene.CreateGameObject("Graphics quality").AddComponent(new GraphicsSettings3D{SettingsKey="LegendsRoad",Quality=GraphicsQuality.Balanced,RenderScale=.85f});
  var hud=scene.CreateGameObject("HUD");hud.AddComponent(new UiCanvas());
  void Text(string name,string text,Vector2 pos,int size=24,UiAnchor anchor=UiAnchor.TopLeft,GameObject? parent=null){var o=scene.CreateGameObject(name);o.SetParent(parent??hud,false);o.AddComponent(new UiText{Text=text,Offset=pos,FontSize=size,Anchor=anchor,FontReference=db.ResolveReference("Assets/Fonts/ByteEngine/TypeLightSans.ttf"),ShadowColor=new(0,0,0,.85f),OutlineWidth=0});}
  Text("Title","LEGENDS ROAD",new(28,24),26);Text("Course name","01 / RIDGE RUN",new(28,59),18);
  Text("Speed","0 km/h",new(-180,26),34,UiAnchor.TopRight);Text("Run time","0.0s",new(-180,70),22,UiAnchor.TopRight);
  Text("Ride state","W TO PUSH / GRAVITY TAKES OVER",new(28,-76),21,UiAnchor.BottomLeft);
  Text("Controls","A / D CARVE    W PUSH    S BRAKE    SPACE SLIDE    SHIFT TUCK    R RETRY    ESC PAUSE    Q QUALITY",new(28,-38),16,UiAnchor.BottomLeft);
  foreach(string name in new[]{"Result panel","Pause panel"}){var panel=scene.CreateGameObject(name);panel.SetParent(hud,false);panel.AddComponent(new UiWidget{Anchor=UiAnchor.Center,Offset=new(-250,-90),Size=new(500,180),Color=new(.025f,.06f,.08f,.94f),CornerRadius=16});Text(name+" title",name=="Result panel"?"RIDGE CONQUERED":"PAUSED",new(-202,-50),32,UiAnchor.Center,panel);Text(name+" hint",name=="Result panel"?"R to ride again":"Escape to resume / R to restart",new(-202,20),20,UiAnchor.Center,panel);panel.Active=false;}
  scene.CreateGameObject("Run director").AddComponent(new DownhillRun3D{FinishCenter=Vec(layout.GetProperty("finish")),FinishRadius=5});
  string path=root+"/Scenes/Main.bytescene";if(!File.Exists(root+"/ArtSource/OriginalMain.bytescene"))File.Copy(path,root+"/ArtSource/OriginalMain.bytescene");runtime.Serializer.Save(scene,path);
  var project=new ProjectSerializer().Load(Program.Project);project.Window.Width=1280;project.Window.Height=720;new ProjectSerializer().Save(project,Program.Project);
  var token=AnimationRuntimeAssets.Configure(runtime.Assets);var renderer=rider.GetComponent<SkeletalMeshRenderer>()!;renderer.ResolveRuntimeResources();renderer.Play("Neutral",true,0);renderer.Seek(0);
  foreach(string bone in new[]{"mixamorig:LeftFoot","mixamorig:RightFoot","mixamorig:Hips"})if(renderer.TryGetBoneModelMatrix(bone,out var matrix))Console.WriteLine("POSE "+bone+" "+matrix.Translation);
  if(renderer.TryGetCurrentModelBounds(out var bounds))Console.WriteLine("POSE BOUNDS "+bounds);
  AnimationRuntimeAssets.Clear(token);Console.WriteLine("AUTHORED "+path+" objects="+scene.GameObjectCount);
 }
 static GameObject Model(Scene scene,GameProjectRuntime runtime,string name,string path,bool collision){
  var reference=runtime.Database.ResolveReference(path);var model=runtime.Assets.LoadModel(reference);var root=scene.CreateGameObject(name);root.AddComponent(new ModelHierarchyInstance{Model=reference});var nodes=new Dictionary<string,GameObject>();
  foreach(var node in model.Nodes){var o=scene.CreateGameObject(node.Name);o.SetParent(node.ParentKey!=null&&nodes.TryGetValue(node.ParentKey,out var p)?p:root,false);Matrix4x4.Decompose(node.LocalTransform,out var scale,out var rotation,out var position);o.Transform.LocalPosition=position;o.Transform.LocalRotation=rotation;o.Transform.LocalScale=scale;nodes[node.Key]=o;
   foreach(var key in node.MeshKeys){var mesh=model.Meshes.First(m=>m.Key==key);o.AddComponent(new MeshRenderer{MeshReference=new(reference,key),Mesh=runtime.Assets.GetModelMesh(reference,key),MaterialReference=new(reference,mesh.MaterialKey!),Material=runtime.Assets.GetModelMaterial(reference,mesh.MaterialKey)});if(collision&&node.Name.Contains("driving",StringComparison.OrdinalIgnoreCase))o.AddComponent(new MeshCollider3D{Model=reference,MeshKey=key});}
  }root.Transform.LocalRotation=Quaternion.CreateFromAxisAngle(Vector3.UnitY,MathF.PI);return root;
 }
}
internal static class Capture
{
 internal static void Write(string file,byte[] rgba,int width,int height){using var output=File.Create(file);output.Write(new byte[]{137,80,78,71,13,10,26,10});byte[] header=new byte[13];System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(0,4),width);System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4,4),height);header[8]=8;header[9]=6;Chunk(output,"IHDR",header);using var compressed=new MemoryStream();using(var z=new ZLibStream(compressed,CompressionLevel.Fastest,true))for(int y=height-1;y>=0;y--){z.WriteByte(0);z.Write(rgba,y*width*4,width*4);}Chunk(output,"IDAT",compressed.ToArray());Chunk(output,"IEND",[]);}
 static void Chunk(Stream s,string type,byte[] data){byte[] name=System.Text.Encoding.ASCII.GetBytes(type),length=new byte[4];System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(length,data.Length);s.Write(length);s.Write(name);s.Write(data);uint crc=0xffffffff;foreach(byte b in name.Concat(data)){crc^=b;for(int i=0;i<8;i++)crc=(crc>>1)^((crc&1)==1?0xedb88320u:0);}System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(length,~crc);s.Write(length);}
}










