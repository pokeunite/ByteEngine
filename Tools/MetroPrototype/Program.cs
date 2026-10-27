using System.Numerics;
using System.Reflection;
using System.Text.Json;
using ByteEngine.Core;
using ByteEngine.Core.AI;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Blueprints;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Gameplay.Hydraulics;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Navigation;
using ByteEngine.Core.Runtime;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Serialization;
using ByteEngine.Core.Variables;
using ByteEngine.Core.VisualLogic;
using OpenTK.Graphics.OpenGL4;

internal static class Program
{
 public const string Root="C:/Users/codex/Documents/palclone";
 [STAThread] static int Main(string[] args){try{using var app=new MetroApp(args);app.Run();return 0;}catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
internal sealed class MetroApp(string[] args):ByteEngineApplication(1500,1000,"Metro prototype verification")
{
 GameProjectRuntime? runtime;int frames;Camera3D? camera;Scene? scene;
 protected override void OnEngineStart()
 {
  runtime=new(Program.Root+"/palclone.byteproject",Console.WriteLine);scene=runtime.LoadStartupScene();
  if(args.Contains("--author")){MetroAuthor.Build(scene,runtime);Close();return;}
  if(args.Contains("--verify")){MetroVerify.Run(scene,runtime);Close();return;}
  if(args.Contains("--game")){Scenes.LoadScene(scene);camera=scene.ActiveCamera;return;}
  foreach(var o in scene.GameObjects.Where(o=>o.GetComponent<Camera3D>()!=null)){foreach(var c in o.Components.Where(c=>c is TopDownCamera3D or ThirdPersonCamera3D))c.Enabled=false;o.GetComponent<Camera3D>()!.ActiveGameCamera=false;}
  scene.FindGameObject("HUD canvas")!.Active=false;
  var view=scene.CreateGameObject("Verification overview");camera=view.AddComponent(new Camera3D{Orthographic=true,OrthographicWidth=117,FarClip=500,ActiveGameCamera=true});
  Vector3 aim=new(50,0,36),eye=new(50,112,116);if(args.Contains("--entry")){aim=new(12,0,43);eye=aim+new Vector3(13,32,26);camera.OrthographicWidth=43;}
  view.Transform.WorldPosition=eye;Matrix4x4.Invert(Matrix4x4.CreateLookAt(eye,aim,Vector3.UnitY),out var matrix);view.Transform.WorldRotation=Quaternion.CreateFromRotationMatrix(matrix);
  Scenes.LoadScene(scene);
 }
 protected override void OnEngineUpdate(){frames++;if(frames>45)Close();}
 protected override void OnEngineRender()
 {
  if(frames!=25||camera==null)return;
  var bytes=new byte[WindowWidth*WindowHeight*4];GL.ReadPixels(0,0,WindowWidth,WindowHeight,PixelFormat.Bgra,PixelType.UnsignedByte,bytes);
  using var bmp=new System.Drawing.Bitmap(WindowWidth,WindowHeight,System.Drawing.Imaging.PixelFormat.Format32bppArgb);var locked=bmp.LockBits(new(0,0,WindowWidth,WindowHeight),System.Drawing.Imaging.ImageLockMode.WriteOnly,bmp.PixelFormat);System.Runtime.InteropServices.Marshal.Copy(bytes,0,locked.Scan0,bytes.Length);bmp.UnlockBits(locked);bmp.RotateFlip(System.Drawing.RotateFlipType.RotateNoneFlipY);
  string output="artifacts/metro-"+(args.Contains("--game")?"game":args.Contains("--entry")?"entry":"overview")+".png";bmp.Save(output);Console.WriteLine("CAPTURE "+Path.GetFullPath(output));
 }
 protected override void OnUnload(){Scenes.UnloadScene();runtime?.Dispose();base.OnUnload();}
}
internal static class MetroAuthor
{
 static readonly Vector4 Concrete=new(.18f,.20f,.21f,1),Floor=new(.10f,.115f,.12f,1),Rust=new(.42f,.20f,.08f,1),Dark=new(.11f,.13f,.14f,1);
 static Scene scene=null!;static GameObject parent=null!;
 static GameObject Obj(string name,Vector3 position,GameObject? under=null){var o=scene.CreateGameObject(name);o.SetParent(under??parent,false);o.Transform.LocalPosition=position;return o;}
 static GameObject Box(string name,Vector3 pos,Vector3 size,Vector4 color,bool collider=true,GameObject? under=null)
 {
  var o=Obj(name,pos,under);o.Transform.LocalScale=size;o.AddComponent(new MeshRenderer{UsePrimitive=true,Primitive=PrimitiveMeshType.Cube,Material=new(){BaseColor=color,Roughness=.92f}});if(collider)o.AddComponent(new BoxCollider3D());return o;
 }
 static void Wall(string name,float x1,float z1,float x2,float z2,float height=2.8f)
 {Box(name,new((x1+x2)/2,height/2,(z1+z2)/2),new(Math.Max(.45f,Math.Abs(x2-x1)),height,Math.Max(.45f,Math.Abs(z2-z1))),Concrete);}
 static void WallGap(string name,float x1,float z1,float x2,float z2,float a,float b)
 {if(z1==z2){if(a>x1)Wall(name,x1,z1,a,z1);if(b<x2)Wall(name,b,z1,x2,z1);}else{if(a>z1)Wall(name,x1,z1,x1,a);if(b<z2)Wall(name,x1,b,x1,z2);}}
 static GameObject Cabinet(string name,float x,float z,Vector4 color)
 {var o=Box(name,new(x,1,z),new(1.2f,2,.65f),color);var display=Box(name+" display",new(x,1.3f,z-.34f),new(.65f,.65f,.04f),new(.08f,.65f,.72f,1),false);display.SetParent(o,true);return o;}
 static void Meta(string relative,Guid id,string type)=>File.WriteAllText(Program.Root+"/"+relative+".meta",JsonSerializer.Serialize(new{guid=id,type}));
 static void SaveEvents(EventModuleDefinition module,string relative){new EventModuleSerializer().Save(module,Program.Root+"/"+relative);Meta(relative,module.Id,"EventModule");}
 static VisualInstruction I(string id,params (string Key,EventValue Value)[] values)=>new(){Id=id,Arguments=values.ToDictionary(p=>p.Key,p=>p.Value)};
 static EventValue Target(GameObject o)=>EventValue.String("id:"+o.Id);
 static EventRuleDefinition Rule(string name,List<VisualInstruction> conditions,List<VisualInstruction> actions)=>new(){DisplayName=name,Conditions=conditions,Actions=actions};
 public static void Build(Scene source,GameProjectRuntime runtime)
 {
  scene=source;
  string backup=Program.Root+"/.byteengine/Backups/metro-build-"+DateTime.Now.ToString("yyyyMMdd-HHmmss");Directory.CreateDirectory(backup);File.Copy(Program.Root+"/Scenes/Main.bytescene",backup+"/Main.bytescene");
  foreach(string file in new[]{"Assets/AI/Metro/Shambler.byteai","Assets/Blueprints/Enemies/Zombie Shambler.byteblueprint","Assets/Events/Metro/Station interactions.byteevents"})if(File.Exists(Program.Root+"/"+file))File.Copy(Program.Root+"/"+file,backup+"/"+Path.GetFileName(file));
  if(scene.FindGameObject("Metro Station") is {} previous)scene.DestroyGameObject(previous);
  if(scene.FindGameObject("Metro objective") is {} oldObjective)scene.DestroyGameObject(oldObjective);
  parent=scene.CreateGameObject("Metro Station");
  var surfaces=new Dictionary<(int X,int Z),float>();
  void Rect(int x1,int z1,int x2,int z2,float y=0){for(int z=z1;z<z2;z++)for(int x=x1;x<x2;x++)surfaces[(x,z)]=y;}
  Rect(0,48,18,66,-.8f);Rect(0,28,22,48);Rect(0,10,22,28);Rect(22,10,78,30);Rect(22,3,78,10);Rect(78,10,100,34);Rect(91,34,100,50);Rect(68,50,91,66);Rect(91,50,100,66);Rect(18,66,91,72,-.8f);Rect(12,62,20,70,-.8f);
  Rect(48,30,53,39);Rect(50,38,61,48);
  foreach(var hole in new[]{(82,16,87,21),(92,16,97,21),(82,25,87,30),(92,25,97,30)})for(int z=hole.Item2;z<hole.Item4;z++)for(int x=hole.Item1;x<hole.Item3;x++)surfaces.Remove((x,z));
  // Entry steps and the power-room return steps have real lower and upper landings.
  for(int z=44;z<48;z++)Rect(6,z,12,z+1,-.2f*(z-44));
  for(int z=66;z<70;z++)Rect(78,z,84,z+1,-.2f*(z-65));
  var seen=new HashSet<(int,int)>();
  foreach(var cell in surfaces.Keys.OrderBy(p=>p.Z).ThenBy(p=>p.X))
  {
   if(seen.Contains(cell))continue;float y=surfaces[cell];int width=1,depth=1;
   while(surfaces.TryGetValue((cell.X+width,cell.Z),out var h)&&h==y&&!seen.Contains((cell.X+width,cell.Z)))width++;
   bool row=true;while(row){for(int x=0;x<width;x++)if(!surfaces.TryGetValue((cell.X+x,cell.Z+depth),out var h2)||h2!=y||seen.Contains((cell.X+x,cell.Z+depth))){row=false;break;}if(row)depth++;}
   for(int z=0;z<depth;z++)for(int x=0;x<width;x++)seen.Add((cell.X+x,cell.Z+z));
   var slab=Box("Walkable floor",new(cell.X+width/2f,y-.15f,cell.Z+depth/2f),new(width,.3f,depth),Floor);slab.Layer=20;slab.AddComponent(new GroundSurface());
  }
  // Merge exposed floor edges into wall runs; internal doors are explicit partitions below.
  var edges=new Dictionary<(bool Horizontal,int Fixed),List<int>>();
  void Edge(bool horizontal,int fixedPos,int from){var key=(horizontal,fixedPos);if(!edges.TryGetValue(key,out var list))edges[key]=list=[];list.Add(from);}
  foreach(var c in surfaces.Keys)
  {
   if(!surfaces.ContainsKey((c.X,c.Z-1)))Edge(true,c.Z,c.X);
   if(!surfaces.ContainsKey((c.X,c.Z+1)))Edge(true,c.Z+1,c.X);
   if(!surfaces.ContainsKey((c.X-1,c.Z)))Edge(false,c.X,c.Z);
   if(!surfaces.ContainsKey((c.X+1,c.Z)))Edge(false,c.X+1,c.Z);
  }
  foreach(var edge in edges){var list=edge.Value.Distinct().Order().ToArray();for(int i=0;i<list.Length;){int from=list[i],end=from+1;while(++i<list.Length&&list[i]==end)end++;if(edge.Key.Horizontal)Wall("Boundary wall",from,edge.Key.Fixed,end,edge.Key.Fixed);else Wall("Boundary wall",edge.Key.Fixed,from,edge.Key.Fixed,end);}}
  WallGap("Waiting room south wall and open door",0,28,22,28,8,13);
  WallGap("Ticket barriers and entry landing",0,48,18,48,6,12);
  WallGap("Ticket hall to platform doorway",22,10,22,48,23,28);
  WallGap("North platform bypass west entrance",22,10,78,10,24,29); // second opening at east end
  // Replace the last segment with two openings, keeping a connected level bypass.
  foreach(var o in scene.GameObjects.Where(o=>o.Name=="North platform bypass west entrance").ToArray())scene.DestroyGameObject(o);
  Wall("Platform bypass partition",29,10,73,10);Wall("Platform bypass end post",22,10,24,10);Wall("Platform bypass end post",77,10,78,10);
  WallGap("Crossing west doorway",78,10,78,34,23,28);
  WallGap("Power hall pump-room doors",91,50,91,66,57,62);
  WallGap("Pump north door",91,50,100,50,94,98);
  WallGap("Return tunnel partition",68,66,91,66,78,84);
  // Visible guards keep every water recess inaccessible; cross is a continuous slab between recesses.
  foreach(float x in new[]{82f,87,92,97})foreach(var z in new[]{(16f,21f),(25f,30f)})Rail(x,z.Item1,x,z.Item2);
  foreach(float z in new[]{16f,21,25,30}){Rail(82,z,87,z);Rail(92,z,97,z);}
  // Simple kiosk, benches, boxes, pillars and plain carriage blocks match the reference.
  Box("Ticket kiosk",new(10,1.15f,38),new(4.2f,2.3f,4),Rust);
  for(int i=0;i<4;i++)Box("Ticket barrier",new(3+i*3.4f,.55f,46),new(.6f,1.1f,2),Dark);
  foreach(float z in new[]{16f,21f,25f})foreach(float x in new[]{5f,13f})Box("Waiting bench",new(x,.4f,z),new(4,.8f,1.1f),Dark);
  foreach(float x in new[]{27f,38,49,60,71})foreach(float z in new[]{11.8f,27f})Box("Platform pillar",new(x,1.5f,z),new(.85f,3,.85f),Concrete);
  Box("Carriage left block",new(38,1.6f,17),new(17,3.2f,4),Rust);Box("Carriage right block",new(61,1.6f,17),new(17,3.2f,4),Rust);
  foreach(float z in new[]{14.5f,19.5f})Box("Recessed track strip",new(50,.012f,z),new(52,.025f,.16f),Dark,false);
  foreach(var pt in new[]{new Vector2(3,31),new(18,43),new(31,7),new(67,7),new(97,31),new(59,46)}){Box("Supply stack",new(pt.X,.5f,pt.Y),new(1,1,1),Rust);Box("Supply stack upper",new(pt.X,.5f+1,pt.Y),new(.9f,.9f,.9f),Rust);}
  foreach(float x in new[]{74f,79,84}){var bank=Box("Electrical cabinet bank",new(x,1.25f,57.5f),new(2.2f,2.5f,6),Concrete);Box("Cabinet bank panel",new(x,1.3f,54.47f),new(1.3f,.7f,.04f),Dark,false);}
  foreach(float z in new[]{54f,59f,63f})
  {var tank=Box("Pump tank",new(98,1,z),new(1.5f,2,2),Dark);tank.GetComponent<MeshRenderer>()!.Primitive=PrimitiveMeshType.Cylinder;}
  var breaker=Cabinet("Power breaker - E to restore sector",88,51.5f,Dark);
  var unusedSwitch=Obj("Power switch - unused state",Vector3.Zero);
  var gate=Box("Return shutter - opens after power",new(81,1,65.8f),new(5.6f,3,.35f),new(.45f,.12f,.055f,1));
  var medicine=Box("Waiting room medical cache - E",new(18,.5f,13),new(1.1f,1,1),new(.15f,.4f,.22f,1));
  var crossA=Box("Medical cross horizontal",new(18,.65f,12.48f),new(.65f,.13f,.025f),Vector4.One,false);var crossB=Box("Medical cross vertical",new(18,.65f,12.46f),new(.13f,.65f,.025f),Vector4.One,false);
  crossA.SetParent(medicine,true);crossB.SetParent(medicine,true);
  var supplies=Box("Storage ammunition cache - E",new(58,.5f,45),new(1.4f,1,1.1f),Rust);
  var navObject=Obj("Metro navigation - auto-found by zombie blueprints",Vector3.Zero);var nav=navObject.AddComponent(new NavigationRegion3D{Width=134,Height=96,CellSize=.75f,AgentRadius=.38f,MaximumStepHeight=.3f,ExcludeDynamicActors=true,SampleColliderFloors=true,FloorLayerMask=1<<20,FloorProbeHeight=2,FloorProbeDepth=5});
  foreach(var hole in new[]{(82,16,87,21),(92,16,97,21),(82,25,87,30),(92,25,97,30)})
  {
   var water=Obj("Utility water surface",new((hole.Item1+hole.Item3)/2f,-.35f,(hole.Item2+hole.Item4)/2f));
   water.AddComponent(new MeshRenderer{UsePrimitive=true,Primitive=PrimitiveMeshType.Plane,CastShadows=false});
   water.AddComponent(new WaterSurface3D{RendererName=water.Name+" "+water.Id.ToString("N")[..6],SurfaceSize=new(hole.Item3-hole.Item1,hole.Item4-hole.Item2),Segments=12,WaveHeight=.018f,ScreenSpaceReflections=false,Color=new(.035f,.18f,.23f,.9f)});water.Name=water.GetComponent<WaterSurface3D>()!.RendererName;
   Box("Water recess bottom",new(water.Transform.WorldPosition.X,-.85f,water.Transform.WorldPosition.Z),new(hole.Item3-hole.Item1,.1f,hole.Item4-hole.Item2),Dark);
  }
  foreach(var p in new[]{new Vector3(2,2,12),new(20,2,24),new(4,2,36),new(21,2,43),new(28,2,4),new(49,2,4),new(72,2,4),new(79,2,13),new(99,2,33),new(93,2,45),new(69,2,52),new(89,2,64),new(55,2,40),new(7,1.5f,60),new(30,1.5f,71),new(60,1.5f,71)})
  {var lamp=Box("Amber wall lamp",p,new(.45f,.2f,.2f),new(1,.55f,.15f,1),false);lamp.AddComponent(new PointLight{Color=new(1,.6f,.25f),Intensity=2,Range=7});}
  var poweredLights=Obj("Sector lights - powered",Vector3.Zero);poweredLights.Active=false;foreach(var p in new[]{new Vector3(14,2,35),new(42,2,23),new(68,2,23),new(89,2,25),new(81,2,61)}){var l=Obj("Powered sector light",p,poweredLights);l.AddComponent(new PointLight{Intensity=2.3f,Range=12,Color=new(.65f,.78f,.8f)});}
  var player=scene.FindGameObject("Mannequin")!;player.Transform.WorldPosition=new(9,-.76f,60);player.Transform.EulerAngles=Vector3.Zero;
  if(player.GetComponent<CharacterMotionDebugHud3D>() is {} debug)debug.Enabled=false;
  foreach(var name in new[]{"Pistol pickup","SCAR pickup","Shotgun pickup"})if(scene.FindGameObject(name) is {} gun)gun.Transform.WorldPosition=new(name=="Pistol pickup"?7:name=="SCAR pickup"?10:13,-.44f,59);
  if(scene.FindGameObject("Ground") is {} ground){ground.Transform.WorldPosition=new(50,-1.2f,36);ground.Transform.LocalScale=new(120,1,90);}
  foreach(var light in scene.GameObjects.SelectMany(o=>o.Components).OfType<DirectionalLight>()){light.Intensity=.6f;light.AmbientIntensity=.32f;light.Color=new(.84f,.87f,.88f);}
  var hud=scene.FindGameObject("HUD canvas")!;var objective=Obj("Metro objective",Vector3.Zero,hud);objective.AddComponent(new UiText{Text="RESTORE POWER  |  Reach the electrical hall",Anchor=UiAnchor.TopCenter,Offset=new(0,22),FontSize=20,Color=new(.8f,.9f,.9f,1),ShadowColor=new(0,0,0,.85f)});
  // Editable native event sheets own all interactions.
  var module=new EventModuleDefinition{Name="Metro station - power and supply interactions"};
  List<VisualInstruction> Near(GameObject o)=>[I("object.distanceBelow",("target",Target(player)),("other",Target(o)),("distance",EventValue.Number(2.4))),I("input.keyPressed",("key",EventValue.String("E"))),I("object.isActive",("target",Target(o)))];
  var powerConditions=Near(breaker);powerConditions.Add(I("object.isActive",("target",Target(unusedSwitch))));
  module.Rules.Add(Rule("E at breaker - restore power and open return passage",powerConditions,[I("object.setActive",("target",Target(gate)),("active",EventValue.Boolean(false))),I("object.setActive",("target",Target(poweredLights)),("active",EventValue.Boolean(true))),I("ai.bakeNavigation",("target",Target(navObject))),I("ui.setText",("target",Target(objective)),("text",EventValue.String("POWER RESTORED  |  Return to the entrance safe room"))),I("object.setActive",("target",Target(unusedSwitch)),("active",EventValue.Boolean(false)))]));
  module.Rules.Add(Rule("E at medical cache - heal 35 HP once",Near(medicine),[I("health.heal",("target",Target(player)),("amount",EventValue.Number(35))),I("object.setActive",("target",Target(medicine)),("active",EventValue.Boolean(false)))]));
  var pistol=scene.FindGameObject("Pistol pickup")!;
  module.Rules.Add(Rule("E at storage cache - add 24 pistol rounds once",Near(supplies),[I("bytebard.karlsondemo.addReserve",("target",Target(pistol)),("amount",EventValue.Number(24))),I("object.setActive",("target",Target(supplies)),("active",EventValue.Boolean(false)))]));
  Directory.CreateDirectory(Program.Root+"/Assets/Events/Metro");SaveEvents(module,"Assets/Events/Metro/Station interactions.byteevents");var driver=Obj("Station interaction events",Vector3.Zero);driver.AddComponent(new EventModuleComponent()).AddModuleReference(new(module.Id,"Assets/Events/Metro/Station interactions.byteevents"));
  SaveZombie(runtime);
  runtime.Serializer.Save(scene,Program.Root+"/Scenes/Main.bytescene");
  File.WriteAllText("artifacts/metro-waypoints.json",JsonSerializer.Serialize(new{entry=new[]{9f,60},ticket=new[]{15f,35},waiting=new[]{10f,20},platform=new[]{25f,25},carriage=new[]{50f,17},bypass=new[]{50f,7},storage=new[]{57f,44},crossing=new[]{90f,23},approach=new[]{95f,42},pump=new[]{95f,58},power=new[]{88f,54},returned=new[]{30f,70}}));
  Console.WriteLine("AUTHORED "+scene.GameObjects.Count+" objects; zero placed zombies. Backup "+backup);
 }
 static void Rail(float x1,float z1,float x2,float z2)
 {Box("Utility railing",new((x1+x2)/2,.7f,(z1+z2)/2),new(Math.Max(.12f,Math.Abs(x2-x1)),.12f,Math.Max(.12f,Math.Abs(z2-z1))),Dark);foreach(float t in new[]{0f,1f})Box("Railing post",new(x1+(x2-x1)*t,.4f,z1+(z2-z1)*t),new(.12f,.8f,.12f),Dark);}
 static void SaveZombie(GameProjectRuntime runtime)
 {
  Directory.CreateDirectory(Program.Root+"/Assets/AI/Metro");Directory.CreateDirectory(Program.Root+"/Assets/Blueprints/Enemies");
  var shared=AIBehaviorPresets.AttackStages();shared.Name="Zombie - warn, strike once, recover";string attackPath="Assets/AI/Metro/Zombie attack.byteai";BehaviorTreeSerializer.Save(Program.Root+"/"+attackPath,shared);var attackId=Guid.NewGuid();Meta(attackPath,attackId,"AIBehavior");
  BehaviorNode Gate(string name,BehaviorTest test,BehaviorNode child)=>new(){Kind=BehaviorNodeKind.Gate,Name=name,Test=test,Children=[child]};
  var close=Gate("Within arm reach",BehaviorTest.WithinDistance,new(){Kind=BehaviorNodeKind.SubBehavior,Name="Telegraph, strike once and recover",SubBehavior=new(attackId,attackPath)});close.Distance=1.6f;close.Interruption=BehaviorInterruption.FinishCommittedAttack;
  var fight=Gate("Visible player: approach or strike",BehaviorTest.LineOfSight,new(){Kind=BehaviorNodeKind.Priority,Name="Attack or follow",Children=[close,new(){Kind=BehaviorNodeKind.MoveToTarget,Name="Shamble toward visible player",Speed=1.65f,Distance=1.3f,Timeout=8},new(){Kind=BehaviorNodeKind.Wait,Name="Pause between strikes",Duration=.3f}]});fight.Interruption=BehaviorInterruption.FinishCommittedAttack;
  var behavior=new BehaviorTreeDefinition{Name="Zombie Shambler - sight, hearing, search and wander",Root=new(){Kind=BehaviorNodeKind.Priority,Name="See player, investigate sound, otherwise wander",Children=[fight,Gate("Last seen or heard something",BehaviorTest.HasMemory,new(){Kind=BehaviorNodeKind.Search,Name="Investigate last sound or sight position",Speed=1.8f,SearchRadius=2,Duration=7}),new(){Kind=BehaviorNodeKind.Wander,Name="Wander close to placed starting position",SearchRadius=3,Speed=.65f,Duration=5,Key="WanderPoint"}]}};
  string behaviorPath="Assets/AI/Metro/Zombie Shambler.byteai";BehaviorTreeSerializer.Save(Program.Root+"/"+behaviorPath,behavior);var behaviorId=Guid.NewGuid();Meta(behaviorPath,behaviorId,"AIBehavior");
  var oldScene=scene;var oldParent=parent;scene=new Scene("Zombie blueprint authoring");parent=scene.CreateGameObject("Zombie Shambler");
  parent.AddComponent(new CapsuleCollider3D{Radius=.35f,Height=1.8f,Center=new(0,.9f,0)});parent.AddComponent(new HealthComponent{MaxHealth=65,CurrentHealth=65,DestroyOnDeath=true});
  parent.AddComponent(new NavigationAgent3D{CollisionAvoidance=true,CollisionRadius=.32f,Speed=1.65f,ArrivalDistance=.12f,RepathInterval=.55f});
  parent.AddComponent(new SimpleEnemyAI3D{BehaviorControlled=true,AttackStyle=EnemyAttackStyle.Rusher,UseNavigation=true,MoveSpeed=1.65f,Damage=12,AttackRange=1.7f,StopDistance=1.3f,WindupSeconds=.65f,RecoverySeconds=.85f,AttackCooldown=1.1f,DodgeEnabled=false,AutomaticDamage=true});
  parent.AddComponent(new AIBehavior3D{Behavior=new(behaviorId,behaviorPath),FindNearestCharacter=true,PerceptionMemory=true,DetectionRange=10,LoseTargetRange=14,FieldOfView=220,HearingRange=24,OccludedHearingMultiplier=.3f,MemorySeconds=7,HearingMemorySeconds=7,DecisionInterval=.15f,Squad="Metro infected",MaximumAttackers=3,RecordHistory=true});
  Box("Torso - replace with animated zombie model later",new(0,1.05f,0),new(.6f,.66f,.32f),new(.21f,.27f,.18f,1),false);Box("Head",new(0,1.55f,-.04f),new(.36f,.38f,.34f),new(.44f,.5f,.32f,1),false);
  foreach(float x in new[]{-.18f,.18f})Box("Leg",new(x,.36f,0),new(.23f,.72f,.27f),Dark,false);
  foreach(float x in new[]{-.4f,.4f}){var arm=Box("Reaching arm",new(x,1.12f,-.3f),new(.18f,.2f,.7f),new(.33f,.39f,.25f,1),false);}
  var definition=new BlueprintDefinition{Name="Zombie Shambler",Type=BlueprintType.Character,Root=runtime.Serializer.SerializeObject(parent),Children=scene.GameObjects.Where(o=>o!=parent).Select(runtime.Serializer.SerializeObject).ToList()};
  string path="Assets/Blueprints/Enemies/Zombie Shambler.byteblueprint";new BlueprintSerializer().Save(definition,Program.Root+"/"+path);Meta(path,definition.Id,"Blueprint");
  scene=oldScene;parent=oldParent;
 }
}

internal static class MetroVerify
{
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);Console.WriteLine("PASS "+message);}
 public static void Run(Scene scene,GameProjectRuntime runtime)
 {
  var region=scene.GameObjects.SelectMany(o=>o.Components).OfType<NavigationRegion3D>().Single();var timer=System.Diagnostics.Stopwatch.StartNew();region.Bake();Console.WriteLine("BAKE "+timer.ElapsedMilliseconds+"ms "+region.WalkableCells+" cells");
  Check(region.Ready&&region.WalkableCells>3000,"Navigation floor sampling rejects empty space and handles real step heights");
  var points=new Dictionary<string,Vector3>{{"Entry",new(9,-.8f,60)},{"Ticket",new(15,0,35)},{"Waiting",new(10,0,20)},{"Platform",new(25,0,25)},{"Carriage",new(50,0,17)},{"Bypass",new(50,0,7)},{"Storage",new(57,0,44)},{"Crossing",new(90,0,23)},{"Approach",new(95,0,42)},{"Pump",new(95,0,58)},{"Power",new(88,0,54)}};
  foreach(var (name,point) in points){var route=region.FindPath(points["Entry"],point,60000);Check(route.Count>0&&Vector3.Distance(route[^1],point)<1,"Connected walkable path: entrance -> "+name);}
  var gate=scene.FindGameObject("Return shutter - opens after power")!;
  Check(region.FindPath(new(81,0,63),new(81,-.8f,71),60000).Count>0,"Return is reachable by long outward path while shutter is closed");
  gate.Active=false;region.Bake();var shortcut=region.FindPath(new(81,0,63),new(81,-.8f,71),60000);Check(shortcut.Count>0&&shortcut.Sum(p=>0)==0&&shortcut.Count<30,"Opening power shutter creates the short direct return connection");gate.Active=true;
  Check(!scene.GameObjects.Any(o=>o.GetComponent<AIBehavior3D>()!=null),"No enemies placed in the actual level");
  var reference=new AssetReference(Guid.Empty,"Assets/Blueprints/Enemies/Zombie Shambler.byteblueprint");
  var instance=BlueprintRuntimeFactory.Spawn(scene,reference,new(25,0,25),runtime.Database,runtime.Serializer,Console.WriteLine);
  Check(instance!=null,"Zombie blueprint loads and instantiates through native factory");var ai=instance!.GetComponent<AIBehavior3D>()!;
  Check(instance.GetComponent<SimpleEnemyAI3D>() is {BehaviorControlled:true}&&instance.GetComponent<NavigationAgent3D>() is {RegionObject:var regionId}&&regionId==Guid.Empty,"Zombie uses one behaviour driver and automatically finds scene navigation");
  var actor=scene.FindGameObject("Mannequin")!;actor.Transform.WorldPosition=new(45,0,25);var combat=instance.GetComponent<SimpleEnemyAI3D>()!;var hp=actor.GetComponent<HealthComponent>()!;
  var behavior=BehaviorTreeSerializer.Load(Program.Root+"/Assets/AI/Metro/Zombie Shambler.byteai");ai.SetSubBehaviorResolver(r=>BehaviorTreeSerializer.Load(Program.Root+"/"+r.ProjectPath));ai.SetDefinition(behavior);
  region.Bake();for(int i=0;i<120;i++)ai.Tick(.02f);Check(ai.CurrentTask.Contains("Wander",StringComparison.OrdinalIgnoreCase)&&ai.LastError=="","Idle zombie wanders locally using auto-bound navigation");
  actor.Transform.WorldPosition=new(40,0,25);AIWorld.ReportNoise(scene,new(30,0,25),20,actor.Id,"Test gunshot");ai.Tick(.2f);Check(ai.HasInterest&&Vector3.Distance(ai.LastKnownPosition,new(30,0,25))<.2f,"Gunshot creates a remembered sound position without telepathic tracking");
  instance.Transform.WorldPosition=new(24,0,25);actor.Transform.WorldPosition=new(24,0,23.8f);ai.ForgetTarget();instance.Transform.EulerAngles=Vector3.Zero;float before=hp.CurrentHealth;bool windup=false;for(int i=0;i<150;i++){ai.Tick(.02f);if(combat.AttackPhase=="Windup")windup=true;}
  Check(windup&&hp.CurrentHealth<before,"Zombie acquires player, telegraphs and applies melee damage");
  Check(ai.LastError=="","Zombie staged subtree runs without execution errors");
  instance.GetComponent<HealthComponent>()!.Kill();ai.Tick(.1f);Check(!scene.GameObjects.Contains(instance),"Zombie death removes its AI actor and collision");
  var module=new EventModuleSerializer().Load(Program.Root+"/Assets/Events/Metro/Station interactions.byteevents");
  var registry=VisualLogicRegistry.CreateDefault();
  var context=new EventExecutionContext{Scene=scene,Self=scene.FindGameObject("Station interaction events")!,Globals=new VariableStore(),WarningSink=m=>throw new Exception(m)};
  foreach(var rule in module.Rules)foreach(var condition in rule.Conditions)Check(registry.TryGetCondition(condition.Id,out _),"Recognised condition: "+condition.Id);
  void RunRule(int index){foreach(var action in module.Rules[index].Actions){Check(registry.TryGetAction(action.Id,out var entry),"Recognised action: "+action.Id);entry!.Execute(action,context);}}
  actor.GetComponent<HealthComponent>()!.CurrentHealth=30;RunRule(1);Check(actor.GetComponent<HealthComponent>()!.CurrentHealth==65&&!scene.FindGameObject("Waiting room medical cache - E")!.Active,"Medical cache grants exactly 35 HP and disappears");
  RunRule(0);Check(!gate.Active&&scene.FindGameObject("Sector lights - powered")!.Active&&scene.FindGameObject("Metro objective")!.GetComponent<UiText>()!.Text.StartsWith("POWER RESTORED"),"Breaker opens shortcut, lights station and updates objective");
  RunRule(2);Check(!scene.FindGameObject("Storage ammunition cache - E")!.Active,"Storage cache grants ammunition through the weapon plugin action");
  var motor=actor.GetComponent<CharacterController3D>()!;motor.MoveSpeed=4.5f;
  var simulate=typeof(CharacterController3D).GetMethod("Simulate",BindingFlags.NonPublic|BindingFlags.Instance)!;
  void Walk(Vector3 start,Vector3 end,string name)
  {
   actor.Transform.WorldPosition=start;motor.SetVelocity(Vector3.Zero);
   for(int i=0;i<500;i++){var delta=end-actor.Transform.WorldPosition;delta.Y=0;if(delta.Length()<.25f)break;motor.Move(Vector3.Normalize(delta));simulate.Invoke(motor,[1f/60]);}
   Console.WriteLine("MOTOR "+name+" "+actor.Transform.WorldPosition);
   Check(Vector2.Distance(new(actor.Transform.WorldPosition.X,actor.Transform.WorldPosition.Z),new(end.X,end.Z))<.5f&&Math.Abs(actor.Transform.WorldPosition.Y-end.Y)<.3f,name);
  }
  Walk(new(9,-.76f,60),new(9,0,42),"Player motor can climb entry steps into ticket hall");
  Walk(new(81,.04f,63),new(81,-.8f,71),"Player motor can descend open return steps");
  Check(module.Rules.Count==3&&module.Rules.All(r=>r.Actions.Count>0),"Power and both supply caches are editable native Event Sheets");
  Console.WriteLine("PASS METRO VERIFICATION COMPLETE (test clone only; not saved)");
 }
}
