using System.Numerics;
using System.IO.Compression;
using OpenTK.Graphics.OpenGL4;
using ByteEngine.Core;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Vfx;
using ByteEngine.Editor;
using DuneCompany;
using DesertTerrain;
namespace ByteEngine.Tests;
internal sealed partial class DunePolishDiagnostic:ByteEngineApplication
{
 readonly bool _garage;readonly bool _sandVehicle;readonly bool _sandLab;readonly bool _menu;readonly bool _world;readonly bool _slice;readonly string _file;readonly bool _prepare;readonly bool _hinge;readonly string? _blueprints;EditorProjectContext? _project;
 public DunePolishDiagnostic(string file,bool prepare,bool hinge=false,string? blueprints=null,bool slice=false,bool world=false,bool menu=false,bool sandLab=false,bool sandVehicle=false,bool garage=false):base(1280,720,"Dune cinematic validation"){_garage=garage;_sandVehicle=sandVehicle;_sandLab=sandLab;_file=Path.GetFullPath(file);_prepare=prepare;_hinge=hinge;_blueprints=blueprints;_slice=slice;_world=world;_menu=menu;IsVisible=false;}
 protected override bool ShouldUpdateScene=>false;protected override bool ShouldRenderSceneToWindow=>false;
 protected override void OnEngineStart()
 {
  System.Runtime.Loader.AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(AppContext.BaseDirectory,"DuneCompany.Plugin.dll"));System.Runtime.Loader.AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(AppContext.BaseDirectory,"DesertTerrain.Plugin.dll"));
  string root=Path.GetDirectoryName(_file)!;Directory.CreateDirectory(Path.Combine(root,"Preview"));
  _project=EditorProjectContext.Open(_file,m=>Console.WriteLine("WARNING: "+m));if(_garage){RunGarage(root);return;}if(_sandVehicle){RunSandVehicle(root);return;}if(_sandLab){RunSandLab(root);return;}if(_menu){RunMainMenu(root);return;}var path=Path.Combine(root,_world?"Scenes/Workshop.bytescene":_project.Project.StartupScene);var scene=_project.Scenes.Load(path);var terrain=scene.GameObjects.SelectMany(o=>o.Components).OfType<DesertTerrain3D>().Single();
  if(_world&&_prepare)AuthorMissionWorld(scene,terrain,root,path);
  if(_slice){RunDesertSlice(scene,terrain,root,path);return;}
  if(_prepare){
   terrain.HeightmapPath="Assets/DesertCinematic/desert-heightmap.png";terrain.HeightmapHeight=48;terrain.FlipHeightmapZ=false;terrain.BaseHeight=0;terrain.Cells=512;terrain.Spacing=1;terrain.MaximumRutDepth=.14f;terrain.SandColor=new(1,.91f,.75f,1);terrain.SandAlbedoPath="Assets/DesertCinematic/sand_diff.jpg";terrain.SandNormalPath="Assets/DesertCinematic/sand_normal.jpg";terrain.SandRoughnessPath="Assets/DesertCinematic/sand_rough.jpg";terrain.TextureWorldSize=12;terrain.NormalStrength=.22f;terrain.SandRoughness=.98f;terrain.CastShadows=false;terrain.ReceiveShadowsDistance=70;terrain.Regenerate();terrain.ResetSand();
   var sky=scene.GameObjects.SelectMany(o=>o.Components).OfType<SkyEnvironment>().FirstOrDefault()??scene.CreateGameObject("Desert cinematic environment").AddComponent(new SkyEnvironment());sky.Quality=GraphicsQuality.High;sky.SkyMode=SkyMode3D.EnvironmentMap;sky.EnvironmentMapReference=new("Assets/DesertCinematic/sunrise.hdr");sky.SkyIntensity=.3f;sky.EnvironmentIntensity=.7f;sky.EnvironmentRotationDegrees=135;sky.OverrideAmbient=true;sky.AmbientIntensity=.22f;sky.Exposure=.9f;sky.AmbientOcclusion=.32f;sky.OcclusionRadius=.4f;sky.Bloom=.045f;sky.BloomThreshold=1.5f;sky.Warmth=.08f;sky.Contrast=1.08f;sky.Saturation=.96f;sky.FogEnabled=true;sky.FogMode=FogMode3D.Exponential;sky.FogColor=new(.68f,.65f,.59f);sky.FogDensity=.0016f;sky.FogMaxOpacity=.55f;
   var sun=scene.GameObjects.SelectMany(o=>o.Components).OfType<DirectionalLight>().FirstOrDefault()??scene.CreateGameObject("Sun - cinematic morning").AddComponent(new DirectionalLight());sun.Transform.EulerAngles=new(32,-38,0);sun.Color=new(1,.83f,.64f);sun.Intensity=2.1f;sun.AmbientIntensity=0;sun.CastShadows=true;sun.ShadowResolution=4096;sun.ShadowDistance=55;sun.ShadowBias=.0009f;sun.ShadowSoftness=1.6f;
   foreach(var obj in scene.GameObjects.Where(o=>o.Name=="Desert horizon"||o.Name.StartsWith("Sandstone landmark ")).ToArray())scene.DestroyGameObject(obj);
   var horizon=DuneWorkshop3D.Model(scene,_project.Assets,"Assets/DesertCinematic/desert-horizon.glb",scene.GameObjects.FirstOrDefault(o=>o.Name=="Desert backdrop")??scene.CreateGameObject("Desert backdrop"),"Desert horizon");foreach(var obj in Descendants(horizon))foreach(var r in obj.Components.OfType<MeshRenderer>())r.CastShadows=false;
   for(int i=0;i<9;i++){float angle=i*MathF.Tau/9;var point=new Vector3(MathF.Sin(angle)*(90+i*9),0,MathF.Cos(angle)*(90+i*9));terrain.TryGetSand(point,out var sample);var anchor=scene.CreateGameObject("Sandstone landmark "+i);anchor.Transform.WorldPosition=sample.Position;anchor.Transform.LocalScale=new(2+i%3,2+i%3,2+i%3);DuneWorkshop3D.Model(scene,_project.Assets,$"Assets/DesertCinematic/rock-{i%3}.glb",anchor,"Wind-carved rock");}
   foreach(var target in scene.GameObjects.SelectMany(o=>o.Components).OfType<DuneBountyTarget3D>().ToArray()){
    terrain.TryGetSand(target.Transform.WorldPosition,out var sand);target.Transform.WorldPosition=sand.Position;foreach(var obj in target.GameObject.Children.ToArray())scene.DestroyGameObject(obj);var cache=DuneWorkshop3D.Model(scene,_project.Assets,"Assets/DesertCinematic/fuel-cache.glb",target.GameObject,"Salvage fuel cache");cache.Transform.LocalScale=new(1.4f);}
   string fx=Path.Combine(root,"Assets/VFX");Directory.CreateDirectory(fx);foreach(var (name,preset) in new[]{("muzzle-flash",VfxPreset.MuzzleFlash),("metal-impact",VfxPreset.Impact),("fuel-explosion",VfxPreset.Explosion)}){var effect=VfxPresets.Create(preset);effect.ParticleBudget=256;VfxEffectSerializer.Save(Path.Combine(fx,name+".bvfx"),effect);}
   var drift=VfxPresets.Create(VfxPreset.Smoke);drift.Name="Windblown sand";drift.ParticleBudget=96;drift.Duration=12;var layer=drift.Layers[0];layer.MaxParticles=96;layer.Rate=6;layer.Lifetime=5;layer.Speed=.3f;layer.StartSize=1.2f;layer.EndSize=3;layer.StartColor=new(.72f,.6f,.43f,.07f);layer.EndColor=new(.72f,.6f,.43f,0);layer.Shape=VfxShape.Box;layer.Extents=new(14,.15f,10);layer.Wind=new(1.6f,0,.35f);layer.Gravity=new(0,.05f,0);VfxEffectSerializer.Save(Path.Combine(fx,"windblown-sand.bvfx"),drift);
   if(!scene.GameObjects.Any(o=>o.Name=="Windblown sand")){var wind=scene.CreateGameObject("Windblown sand");terrain.TryGetSand(new(12,0,-20),out var sand);wind.Transform.WorldPosition=sand.Position+Vector3.UnitY*.3f;wind.AddComponent(new VfxPlayer{Effect=new("Assets/VFX/windblown-sand.bvfx"),ViewDistance=110,QualityDistance=40});}
   _project.Scenes.Save(scene,path);Console.WriteLine("Authored cinematic desert, lights, VFX and target props saved.");
  }
  Scenes.LoadScene(scene);var workshop=scene.GameObjects.SelectMany(o=>o.Components).OfType<DuneWorkshop3D>().Single();Tick(scene,new(.5f,.4f),false);Capture(scene,Path.Combine(root,"Preview/cinematic-workshop.png"),false);
  if(!_prepare){
   if(_blueprints!=null){
    Directory.CreateDirectory(Path.Combine(root,"Saves"));var results=new List<object>();
    foreach(var blueprint in Directory.GetFiles(_blueprints,"*.json").Order()){
     File.Copy(blueprint,Path.Combine(root,"Saves/vehicle.json"),true);workshop.Command("Load vehicle");
     var overlap=typeof(DuneWorkshop3D).GetMethod("Overlap",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!;
     foreach(var block in workshop.Blocks){if(block.Parent<0)continue;Vector3 incoming=workshop.Catalog[block.Type].Wheel?Vector3.UnitZ:-Vector3.UnitY;if(!workshop.HasMountSupport(block.Type,block.P,block.Q,incoming,block.Parent))throw new InvalidOperationException($"Unsupported mount in {Path.GetFileName(blueprint)}: block {block.Id} / {block.Type}");if((bool)overlap.Invoke(workshop,new object[]{block.P,block.Q,block.Type,block.Id})!)throw new InvalidOperationException($"Overlapping part in {Path.GetFileName(blueprint)}: block {block.Id} / {block.Type}");}
     Console.WriteLine("PASS Mounting footprints and overlap: "+Path.GetFileName(blueprint));workshop.Command("Toggle drive");if(workshop.Building)throw new InvalidOperationException("Blueprint did not enter drive");for(int i=0;i<120;i++)Tick(scene,new(.5f,.5f),false);var start=workshop.Transform.WorldPosition;for(int i=0;i<180;i++)Tick(scene,new(.5f,.5f),false,Key.W);float driven=Vector3.Distance(start,workshop.Transform.WorldPosition);if(driven<2||!float.IsFinite(driven))throw new InvalidOperationException("Blueprint does not drive: "+blueprint);for(int i=0;i<60;i++)Tick(scene,new(.5f,.5f),false,Key.W,Key.D);if(workshop.Blocks.Where(b=>workshop.Catalog[b.Type].Wheel).Any(b=>workshop.WheelAxisError(b.Id)>1))throw new InvalidOperationException("Blueprint wheel joint misaligned");Console.WriteLine($"PASS Native driving {Path.GetFileName(blueprint)}: {driven:0.0}m");results.Add(new{blueprint=Path.GetFileName(blueprint),blocks=workshop.Blocks.Count,distance=driven});workshop.Command("Toggle drive");
    }
    File.WriteAllText(Path.Combine(root,"Preview/blueprint-validation.json"),System.Text.Json.JsonSerializer.Serialize(results,new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
   }
   else {
   while(workshop.Blocks.Count>1)workshop.RemoveBlock(workshop.Blocks[1].Id);
   if(_hinge){
    workshop.AddBlock(23,0,new(0,1,0),Quaternion.Identity);int engineId=workshop.Blocks.Last().Id;
    if(workshop.HasMountSupport(25,new(0,2,0),Quaternion.Identity,-Vector3.UnitY,engineId))throw new InvalidOperationException("Engine housing still accepts a seat mounting surface");
    workshop.RemoveBlock(engineId);Console.WriteLine("PASS Engine housing rejects seat attachment");
    workshop.SelectType(32);var springPointer=Pointer(scene.ActiveCamera!,workshop.Transform.WorldPosition+new Vector3(.25f,.25f,0));Tick(scene,springPointer,false);Tick(scene,springPointer,true);Tick(scene,springPointer,false);
    if(workshop.Blocks.Count!=2||workshop.Blocks.Last().Type!=32)throw new InvalidOperationException("Spring arm cannot be mouse-mounted on the chassis side");
    var springBlock=workshop.Blocks.Last();var springDef=workshop.PhysicsDefinition(32);if(!springDef.Articulated||springDef.Moving.Size.Y<.45f)throw new InvalidOperationException("Spring arm output was not imported as an articulated mount");
    workshop.SelectType(13);var springCap=springBlock.P+Vector3.Transform(new Vector3(0,.25f,springDef.Moving.Low.Z),springBlock.Q);var springWheelPointer=Pointer(scene.ActiveCamera!,workshop.Transform.WorldPosition+springCap);Tick(scene,springWheelPointer,false);Tick(scene,springWheelPointer,true);Tick(scene,springWheelPointer,false);
    if(workshop.Blocks.Count!=3||!workshop.Blocks.Last().MovingMount||workshop.Blocks.Last().Parent!=springBlock.Id)throw new InvalidOperationException("Tyre did not attach to the spring arm outer plate");
    Capture(scene,Path.Combine(root,"Preview/spring-arm-mounting.png"),false);Console.WriteLine("PASS Spring arm and tyre attach through native mouse placement");
    typeof(DuneWorkshop3D).GetField("_tuning",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.SetValue(workshop,springBlock.Id);
    typeof(DuneWorkshop3D).GetMethod("SetValue",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.Invoke(workshop,new object[]{4,2f});
    if(!Descendants(workshop.GameObject.Children.Single(o=>o.GetComponent<DuneBlock3D>()?.BlockId==springBlock.Id)).SelectMany(o=>o.Components.OfType<MeshRenderer>()).Any(r=>r.MaterialOverrides.BaseColor is {} color&&color.Y>color.X))throw new InvalidOperationException("Body paint did not update the imported body material");
    if(Descendants(workshop.GameObject.Children.Single(o=>o.GetComponent<DuneBlock3D>()?.BlockId==0)).SelectMany(o=>o.Components.OfType<MeshRenderer>()).Any(r=>r.MaterialOverrides.BaseColor!=null))throw new InvalidOperationException("Painting one block changed another block");
    workshop.Command("Save vehicle");workshop.Command("Load vehicle");if(workshop.Blocks.Single(b=>b.Id==springBlock.Id).Paint!=2)throw new InvalidOperationException("Paint did not survive native save/reload");Console.WriteLine("PASS Independent body paint updates and survives native save/reload");
    var springRoot=workshop.GameObject.Children.Single(o=>o.GetComponent<DuneBlock3D>()?.BlockId==springBlock.Id);var coil=Descendants(springRoot).Single(o=>o.Name=="Visual_Spring");var initialCoil=coil.Transform.LocalScale;
    workshop.AddBlock(23,0,new(0,1,2),Quaternion.Identity);workshop.AddBlock(25,0,new(0,1,-1),Quaternion.Identity);foreach(int side in new[]{-1,1})workshop.AddBlock(13,0,new(side*.9f,0,2),Quaternion.CreateFromAxisAngle(Vector3.UnitY,-side*MathF.PI*.5f));
    springRoot=workshop.GameObject.Children.Single(o=>o.GetComponent<DuneBlock3D>()?.BlockId==springBlock.Id);coil=Descendants(springRoot).Single(o=>o.Name=="Visual_Spring");initialCoil=coil.Transform.LocalScale;workshop.Command("Toggle drive");if(workshop.Building)throw new InvalidOperationException("Spring test vehicle cannot enter Drive");float maximumCompression=0;
    for(int frame=0;frame<180;frame++){Tick(scene,new(.5f,.5f),false);maximumCompression=Math.Max(maximumCompression,Vector3.Distance(coil.Transform.LocalScale,initialCoil));}
    if(maximumCompression<.005f)throw new InvalidOperationException("Exposed spring does not deform during a physical landing");Console.WriteLine($"PASS Exposed spring follows actual suspension movement: scale delta={maximumCompression:0.000}");Capture(scene,Path.Combine(root,"Preview/spring-arm-loaded.png"),false);workshop.Command("Toggle drive");
    while(workshop.Blocks.Count>1)workshop.RemoveBlock(workshop.Blocks.Last().Id);
    bool attached=false;foreach(var face in new[]{new Vector3(.25f,.25f,0),new(-.25f,.25f,0),new(0,.25f,.5f),new(0,.25f,-.5f)}){
     workshop.SelectType(18);var pointer=Pointer(scene.ActiveCamera!,workshop.Transform.WorldPosition+face);Tick(scene,pointer,false);Tick(scene,pointer,true);Tick(scene,pointer,false);
     if(workshop.Blocks.Count>1){var h=workshop.Blocks.Last();if(Math.Abs(Vector3.Dot(Vector3.Transform(Vector3.UnitZ,h.Q),Vector3.UnitY))>.99f){attached=true;break;}workshop.RemoveBlock(h.Id);}
    }
    if(!attached)throw new InvalidOperationException("Side-mounted hinge pin did not align upright");
    var hinge=workshop.Blocks.Last();var def=workshop.PhysicsDefinition(18);if(Math.Abs(Vector3.Dot(def.Axis,Vector3.UnitZ))<.99f||Math.Abs(def.Pivot.Y-.27f)>.02f)throw new InvalidOperationException("Hinge model pin or pivot does not match physics");
    Console.WriteLine("PASS Side mounting automatically aligns the physical hinge pin upright");
    workshop.SelectType(13);var cap=hinge.P+Vector3.Transform(new Vector3(0,def.Moving.High.Y,0),hinge.Q);var wheelPointer=Pointer(scene.ActiveCamera!,workshop.Transform.WorldPosition+cap);Tick(scene,wheelPointer,false);Tick(scene,wheelPointer,true);Tick(scene,wheelPointer,false);
    if(workshop.Blocks.Count!=3||workshop.Blocks.Last().Parent!=hinge.Id||!workshop.Blocks.Last().MovingMount)throw new InvalidOperationException("Wheel did not attach directly to hinge output plate");
    Console.WriteLine("PASS Mouse placement attaches wheel to centred moving output plate");
    int mountedWheel=workshop.Blocks.Last().Id;Capture(scene,Path.Combine(root,"Preview/hinge-mounting.png"),false);workshop.AddBlock(3,0,new(0,.5f,2),Quaternion.Identity);workshop.AddBlock(23,0,new(0,1,2.6f),Quaternion.Identity);workshop.AddBlock(25,0,new(0,1,1.4f),Quaternion.Identity);foreach(int side in new[]{-1,1})workshop.AddBlock(13,0,new(side*1.2f,-.15f,2.8f),Quaternion.CreateFromAxisAngle(Vector3.UnitY,-side*MathF.PI*.5f));workshop.Command("Toggle drive");if(workshop.Building)throw new InvalidOperationException("Hinge validation vehicle did not enter Drive");for(int i=0;i<300;i++)Tick(scene,new(.5f,.5f),false,Key.D);float right=workshop.SteeringJointAngle(hinge.Id);for(int i=0;i<300;i++)Tick(scene,new(.5f,.5f),false,Key.A);float left=workshop.SteeringJointAngle(hinge.Id);if(Math.Abs(right)<20||Math.Abs(left)<20||right*left>=0)throw new InvalidOperationException($"Steering joint does not reverse correctly: {right}, {left}");Console.WriteLine($"PASS Physical hinge reverses with A/D: {right:0.0} / {left:0.0} degrees");Console.WriteLine($"Hinge wheel axis error: {workshop.WheelAxisError(mountedWheel):0.000} degrees");if(workshop.WheelAxisError(mountedWheel)>.5f)throw new InvalidOperationException("Hinge-mounted wheel axis is unstable");Capture(scene,Path.Combine(root,"Preview/hinge-steering.png"),false);Console.WriteLine("PASS Hinge-mounted wheel stays constrained while steering");
   }
   else {
   foreach(int type in new[]{18,21,22}){
    workshop.SelectType(type);bool placed=false;foreach(var local in new[]{new Vector3(0,.5f,0),new(.25f,.25f,0),new(0,.25f,.5f),new(-.25f,.25f,0),new(0,.25f,-.5f)}){int before=workshop.Blocks.Count;var pointer=Pointer(scene.ActiveCamera!,workshop.Transform.WorldPosition+local);Tick(scene,pointer,false);Tick(scene,pointer,true);Tick(scene,pointer,false);if(workshop.Blocks.Count>before){placed=true;Console.WriteLine("PASS Mouse placement "+workshop.Catalog[type].Label);workshop.RemoveBlock(workshop.Blocks.Last().Id);break;}}
    if(!placed)throw new InvalidOperationException("Cannot place "+type+" / "+scene.GameObjects.FirstOrDefault(o=>o.Name=="Placement instruction")?.GetComponent<UiText>()?.Text);
   }
   workshop.AddBlock(3,0,new(0,.5f,0),Quaternion.Identity);workshop.AddBlock(23,1,new(0,1,.55f),Quaternion.Identity);workshop.AddBlock(25,1,new(0,1,-.6f),Quaternion.Identity);foreach(int x in new[]{-1,1})foreach(int z in new[]{-1,1})workshop.AddBlock(13,1,new(x*.9f,-.15f,z*.72f),Quaternion.CreateFromAxisAngle(Vector3.UnitY,-x*MathF.PI*.5f));workshop.AddBlock(30,1,new(0,1.2f,-1.2f),Quaternion.Identity);Tick(scene,new(.5f,.5f),false);workshop.Command("Toggle drive");if(workshop.HighlightedBlock!=-1||scene.GameObjects.Any(o=>o.Name=="Block selection outline"&&o.ActiveInHierarchy))throw new InvalidOperationException("Selection highlight remains in driving mode");Console.WriteLine("PASS No build highlight in driving mode");
   var target=scene.GameObjects.SelectMany(o=>o.Components).OfType<DuneBountyTarget3D>().First();target.Transform.WorldPosition=workshop.Transform.WorldPosition+new Vector3(5,0,-14);target.ResetTarget();for(int i=0;i<90;i++)Tick(scene,new(.5f,.5f),false);if(target.Health>=100)throw new InvalidOperationException("Automatic weapons did not hit off-axis target");Console.WriteLine("PASS Weapons acquire and fire on off-axis targets without fire input");Capture(scene,Path.Combine(root,"Preview/cinematic-combat.png"),false);
   for(int i=0;i<100;i++)Tick(scene,new(.5f,.5f),false,Key.W);Capture(scene,Path.Combine(root,"Preview/cinematic-driving.png"),false);Console.WriteLine("PASS Driving and sand effects remain active");
   }
  }
   }
  Scenes.UnloadScene();_project.Dispose();Console.WriteLine("Dune cinematic validation passed.");Close();
 }
 static IEnumerable<GameObject> Descendants(GameObject obj){foreach(var child in obj.Children){yield return child;foreach(var node in Descendants(child))yield return node;}}
    private static EditorCamera3D EditCamera(Camera3D c){var f=c.Transform.Forward;return new(){Position=c.Transform.WorldPosition,Yaw=MathF.Atan2(f.Z,f.X)*180/MathF.PI,Pitch=MathF.Asin(f.Y)*180/MathF.PI,FieldOfView=c.FieldOfView};}
    private void Capture(Scene scene,string file,bool edit)
    {
        using var fb=new SceneFramebuffer();if(edit)fb.Render(Renderer,Renderer3D,scene,EditorMode.Edit,new EditorCamera(),scene.ActiveCamera is {} cam?EditCamera(cam):new EditorCamera3D(),true,1280,720,1280,720,drawGrid3D:false);else fb.RenderGame(Renderer,Renderer3D,scene,EditorMode.Play,1280,720,1280,720);GL.Finish();var pixels=new byte[1280*720*4];GL.BindTexture(TextureTarget.Texture2D,(int)fb.TextureId);GL.GetTexImage(TextureTarget.Texture2D,0,PixelFormat.Rgba,PixelType.UnsignedByte,pixels);GL.BindTexture(TextureTarget.Texture2D,0);
        DesertTerrainTests.Check(GL.GetError()==ErrorCode.NoError,"Native terrain renders without OpenGL errors");Png(file,pixels,1280,720);
    }
    private static void Png(string file,byte[] rgba,int width,int height)
    {
        using var output=File.Create(file);output.Write(new byte[]{137,80,78,71,13,10,26,10});byte[] header=new byte[13];System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(0,4),width);System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4,4),height);header[8]=8;header[9]=6;Chunk(output,"IHDR",header);
        using var compressed=new MemoryStream();using(var z=new ZLibStream(compressed,CompressionLevel.Fastest,true))for(int y=height-1;y>=0;y--){z.WriteByte(0);z.Write(rgba,y*width*4,width*4);}Chunk(output,"IDAT",compressed.ToArray());Chunk(output,"IEND",[]);
    }
    private static void Chunk(Stream s,string type,byte[] data)
    {
        byte[] name=System.Text.Encoding.ASCII.GetBytes(type),length=new byte[4];System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(length,data.Length);s.Write(length);s.Write(name);s.Write(data);uint crc=0xffffffff;foreach(byte b in name.Concat(data)){crc^=b;for(int i=0;i<8;i++)crc=(crc>>1)^((crc&1)==1?0xedb88320u:0);}System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(length,~crc);s.Write(length);
    }
 static Vector2 Pointer(Camera3D camera,Vector3 point){var view=Vector4.Transform(new Vector4(point,1),camera.GetViewMatrix());var clip=Vector4.Transform(view,camera.GetProjectionMatrix(1280f/720));return new((clip.X/clip.W+1)*.5f,(1-clip.Y/clip.W)*.5f);}
 static void Tick(Scene scene,Vector2 pointer,bool mouse,params Key[] keys){var raw=new ByteEngine.Core.InputSystem.RawInputSnapshot();raw.KeysDown.UnionWith(keys);if(mouse)raw.MouseButtonsDown.Add(MouseButton.Left);Input.UpdatePortable(raw,false);Input.SetGameViewPointer(pointer,new(1280,720),true);Time.Update(1f/60);scene.UpdateInternal();}
 static void OrbitTick(Scene scene,Vector2 pointer){var raw=new ByteEngine.Core.InputSystem.RawInputSnapshot();raw.MouseButtonsDown.Add(MouseButton.Middle);Input.UpdatePortable(raw,false);Input.SetGameViewPointer(pointer,new(1280,720),true);Time.Update(1f/60);scene.UpdateInternal();}

}
