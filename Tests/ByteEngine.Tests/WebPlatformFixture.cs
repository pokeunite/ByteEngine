using System.Numerics;
using System.Text.Json;
using ByteEngine.Core;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Audio;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Gameplay.Hydraulics;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.InputSystem;
using ByteEngine.Core.Runtime;
using ByteEngine.Core.Physics;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Serialization;
using ByteEngine.Core.Variables;
using ByteEngine.Core.VisualLogic;
using ByteEngine.Editor;
using OpenTK.Graphics.OpenGL4;
namespace ByteEngine.Tests;
internal static class WebPlatformFixture
{
 public static readonly string Root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),"ByteEngineWebValidation","WebValidation");
 public static string ProjectFile=>Path.Combine(Root,"WebValidation.byteproject");
 public static void Create(){
  if(Directory.Exists(Root)&&!File.Exists(ProjectFile)&&!File.Exists(Path.Combine(Root,"validation-fixture.txt")))throw new InvalidOperationException("Validation project folder already exists without its fixture marker.");
  Directory.CreateDirectory(Root);File.WriteAllText(Path.Combine(Root,"validation-fixture.txt"),"Reusable ByteEngine web-platform regression project.");
  using var p=File.Exists(ProjectFile)?EditorProjectContext.Open(ProjectFile,Console.WriteLine):EditorProjectContext.Create(ProjectFile,Console.WriteLine);
  Directory.CreateDirectory(Path.Combine(Root,"Assets","Fonts"));File.Copy(Path.GetFullPath("Editor/ByteEngine.Editor/Resources/Fonts/TypeLightSans.ttf"),Path.Combine(Root,"Assets/Fonts/UI.ttf"),true);
  // A real short PCM cue lets both runtimes exercise their audio backends.
  using(var w=new BinaryWriter(File.Create(Path.Combine(Root,"Assets","Cue.wav")))){int count=11025;w.Write("RIFF"u8);w.Write(36+count*2);w.Write("WAVEfmt "u8);w.Write(16);w.Write((short)1);w.Write((short)1);w.Write(22050);w.Write(44100);w.Write((short)2);w.Write((short)16);w.Write("data"u8);w.Write(count*2);for(int i=0;i<count;i++)w.Write((short)(Math.Sin(i*Math.PI*2*440/22050)*1800*Math.Sin(Math.PI*i/count)));}
  p.AssetDatabase.Scan();AssetReference Ref(string path){if(!p.AssetDatabase.TryGetAsset(path,out var a)||a is null)throw new InvalidOperationException("Missing fixture asset: "+path);return new(a.Guid,a.ProjectPath);}
  var s=new Scene("Web platform comparison"){FixedSimulation=true};s.SimulationClock.StepSeconds=1.0/120;s.Variables.Set("Fill",VariableValue.FromBoolean(false));
  GameObject Box(string n,Vector3 pos,Vector3 size,Vector4 c,PrimitiveMeshType type=PrimitiveMeshType.Cube){var o=s.CreateGameObject(n);o.Transform.LocalPosition=pos;o.Transform.LocalScale=size;o.AddComponent(new MeshRenderer{UsePrimitive=true,Primitive=type,Material=new Material{BaseColor=c,Roughness=.65f}});return o;}
  var camera=s.CreateGameObject("Camera").AddComponent(new Camera3D{ActiveGameCamera=true,Orthographic=true,OrthographicWidth=27});camera.Transform.LocalPosition=new(15,21,23);Matrix4x4.Invert(Matrix4x4.CreateLookAt(camera.Transform.WorldPosition,new(0,1,0),Vector3.UnitY),out var world);camera.Transform.WorldRotation=Quaternion.CreateFromRotationMatrix(world);
  var light=s.CreateGameObject("Light");light.Transform.EulerAngles=new(-55,-35,0);light.AddComponent(new DirectionalLight{Color=new(1,.9f,.75f),Intensity=2.6f,AmbientIntensity=.3f,CastShadows=true,ShadowResolution=2048,ShadowDistance=40});
  s.CreateGameObject("Environment").AddComponent(new SkyEnvironment{DrawSky=false,EnvironmentLightingEnabled=false,OverrideAmbient=true,AmbientIntensity=.42f,Quality=GraphicsQuality.High,AmbientOcclusion=0,Bloom=0,SmoothEdges=true});
  s.CreateGameObject("Graphics").AddComponent(new GraphicsSettings3D{Quality=GraphicsQuality.High,PreservePostEffects=true});
  s.CreateGameObject("Ocean").AddComponent(new WaterOcean3D{SeaLevel=-.6f,Size=new(90),Segments=96,WaveAmplitude=.08f,SeabedHeight=-3.5f});Box("Sea bed",new(0,-3.5f,0),new(90,.2f,90),new(.20f,.25f,.22f,1));
  for(int i=0;i<22;i++){float angle=i*MathF.Tau/22;Box("Coastal rock "+i,new(MathF.Cos(angle)*9,-.65f+(i%3)*.16f,MathF.Sin(angle)*5.5f),new(1.2f+(i%3)*.2f,1.3f,1),new(.28f+i%3*.025f,.31f,.27f,1),PrimitiveMeshType.Sphere);}
  var stone=new Vector4(.68f,.65f,.51f,1);Box("Foundation",new(0,-.25f,0),new(16.6f,.7f,6.6f),stone);
  for(int x=-8;x<=8;x++)foreach(float z in new[]{-3.1f,3.1f})Box("Stone bank "+x+" "+z,new(x,1.2f,z),new(.98f,2.5f,.42f),stone);
  foreach(float x in new[]{-8.1f,8.1f})Box("End bank "+x,new(x,1.2f,0),new(.42f,2.5f,6.4f),stone);
  for(int i=0;i<16;i++){var leaf=Box("Vegetation "+i,new(-7+i,.2f+(i%4)*.18f,3.45f),new(.16f,.5f,.12f),new(.18f,.35f,.06f,1));leaf.Transform.EulerAngles=new(0,0,(i%3-1)*25);}
  s.CreateGameObject("Water network").AddComponent(new WaterNetwork3D());
  void Basin(string n,float min,float max,float level){var o=s.CreateGameObject(n);o.AddComponent(new WaterBasin3D{MinX=min,MaxX=max,CenterZ=0,Depth=5.6f,Floor=.1f,Rim=3,InitialLevel=level,SurfaceName=n+" Surface",VolumeVisualName=n+" Volume",VisualDepth=5.6f});var volume=Box(n+" Volume",new((min+max)/2,level/2,0),Vector3.One,new(.02f,.25f,.3f,.6f));var vs=s.CreateGameObject(n+" Volume Mesh");vs.SetParent(volume,false);var mr=volume.GetComponent<MeshRenderer>()!;volume.RemoveComponent(mr);vs.AddComponent(mr);var surf=Box(n+" Surface",new((min+max)/2,level,0),new(max-min,.02f,5.6f),new(.02f,.35f,.4f,.9f));var sr=s.CreateGameObject(n+" Surface Mesh");sr.SetParent(surf,false);var sm=surf.GetComponent<MeshRenderer>()!;surf.RemoveComponent(sm);sr.AddComponent(sm);sr.Transform.LocalScale=surf.Transform.LocalScale;surf.Transform.LocalScale=Vector3.One;surf.AddComponent(new WaterSurface3D{RendererName=sr.Name,BasinName=n,SurfaceSize=new(max-min,5.6f),FlowGate="Supply",Absorption=new(.78f,.16f,.075f),Scattering=new(.018f,.07f,.085f),CausticStrength=.18f});o.AddComponent(new WaterDynamics3D{AmbientAmplitude=.015f});}
  Basin("Storage",-7.8f,-2,2.6f);Basin("Channel",-2,7.8f,.8f);
  var blade=Box("Sluice",new(-2,1.6f,0),new(.2f,3,5.6f),new(.8f,.22f,.03f,1));blade.AddComponent(new SluiceActuator3D{CommandVariable="Fill",ClosedHeight=1.6f,Travel=2.7f,Speed=1.3f});
  s.CreateGameObject("Supply").AddComponent(new WaterGate3D{Source="Storage",Destination="Channel",Conductance=1.2f,Opening=1,OneWay=true,ApertureObject="Sluice"});
  var raft=Box("Raft",new(1,1,0),new(1.7f,.3f,1.2f),new(.45f,.23f,.07f,1));raft.Transform.LocalScale=Vector3.One;var raftMesh=s.CreateGameObject("Raft Mesh");raftMesh.SetParent(raft,false);raftMesh.Transform.LocalScale=new(1.7f,.3f,1.2f);var rm=raft.GetComponent<MeshRenderer>()!;raft.RemoveComponent(rm);raftMesh.AddComponent(rm);raft.AddComponent(new BoxCollider3D{Size=new(1.7f,.3f,1.2f)});raft.AddComponent(new Rigidbody3D{Mass=180,FreezePositionZ=true,SimulateRotation=false});raft.AddComponent(new BuoyantBody3D{Size=new(1.7f,.3f,1.2f)});
  s.CreateGameObject("Current").AddComponent(new WaterCurrentPath3D{BasinName="Channel",FlowGate="Supply",Start=new(0,0,0),End=new(7,0,0),VelocityScale=8});
  Box("Dock",new(6.5f,1.8f,1.8f),new(2,.2f,2),new(.45f,.26f,.09f,1));
  s.CreateGameObject("Cue").AddComponent(new AudioSource3D{ClipReference=Ref("Assets/Cue.wav"),Volume=.2f,Spatial=false,PlayOnStart=true});
  var canvas=s.CreateGameObject("Canvas");canvas.AddComponent(new UiCanvas{ReferenceResolution=new(1280,720)});canvas.AddComponent(new UiTheme());
  var title=s.CreateGameObject("Title");title.SetParent(canvas,false);title.AddComponent(new UiText{Text="WEB PLATFORM / COMPARISON",FontReference=Ref("Assets/Fonts/UI.ttf"),FontSize=28,Offset=new(24,24),Color=Vector4.One});
  void Button(string n,string text,float x){var o=s.CreateGameObject(n);o.SetParent(canvas,false);o.AddComponent(new UiWidget{Kind=UiWidgetKind.Button,Label=text,Offset=new(x,640),Size=new(180,52),FontReference=Ref("Assets/Fonts/UI.ttf"),FontSize=22,AutoFitLabel=false,ThemeKey="primary",CornerRadius=7});}
  Button("Release","Release water",24);Button("Fast","Fast",220);Button("Balanced","Balanced",416);Button("High","High",612);Button("Reset","Reset",808);
  var module=new EventModuleDefinition{Name="Web comparison controls"};VisualInstruction I(string id,params(string,EventValue)[] fields)=>new(){Id=id,Arguments=fields.ToDictionary(v=>v.Item1,v=>v.Item2)};EventValue S(string v)=>EventValue.String(v);
  void Rule(string name,VisualInstruction condition,VisualInstruction action)=>module.Rules.Add(new(){DisplayName=name,Conditions=[condition],Actions=[action]});
  Rule("Release",I("ui.buttonClicked",("target",S("Release"))),I("variable.toggle",("target",EventValue.FromReference(new VariableReference{Scope=VariableScope.Scene,MemberName="Fill"}))));
  foreach(string q in new[]{"Fast","Balanced","High"})Rule(q,I("ui.buttonClicked",("target",S(q))),I("variable.set",("target",EventValue.FromReference(new VariableReference{Scope=VariableScope.Component,ObjectName="Graphics",ComponentType="GraphicsSettings3D",MemberName="Quality"})),("value",S(q))));
  Rule("Reset",I("ui.buttonClicked",("target",S("Reset"))),I("scene.load",("path",S(p.Project.StartupScene))));
  Directory.CreateDirectory(Path.Combine(Root,"Assets/Events"));new EventModuleSerializer().Save(module,Path.Combine(Root,"Assets/Events/Comparison.byteevents"));p.AssetDatabase.Scan();s.CreateGameObject("Events").AddComponent(new EventModuleComponent()).AddModuleReference(Ref("Assets/Events/Comparison.byteevents"));s.CreateGameObject("Loading").AddComponent(new LoadingScreenStyle{FontReference=Ref("Assets/Fonts/UI.ttf")});
  p.Scenes.Save(s,p.ResolveProjectPath(p.Project.StartupScene));p.AssetDatabase.Scan();Console.WriteLine("Comparison project: "+ProjectFile);
 }
}


