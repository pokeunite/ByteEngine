using System.Numerics;
using System.IO.Compression;
using System.Diagnostics;
using System.Text.Json;
using DesertTerrain;
using ByteEngine.Core;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Serialization;
using ByteEngine.Core.Serialization.SerializationModels;
using ByteEngine.Core.Plugins;
using ByteEngine.Editor;
using OpenTK.Graphics.OpenGL4;
namespace ByteEngine.Tests;
internal sealed class DesertTerrainDiagnostic : ByteEngineApplication
{
    private readonly string _file;private readonly bool _prepare;private EditorProjectContext? _project;
    public DesertTerrainDiagnostic(string file,bool prepare):base(1280,720,"Dune Company terrain validation"){_file=Path.GetFullPath(file);_prepare=prepare;IsVisible=false;}
    protected override bool ShouldUpdateScene=>false;
    protected override bool ShouldRenderSceneToWindow=>false;
    protected override void OnEngineStart()
    {
        string root=Path.GetDirectoryName(_file)!;
        if(_prepare)
        {
            if(File.Exists(Path.Combine(root,"Scenes","SandTest.bytescene")))throw new InvalidOperationException("Project already exists; refusing to overwrite authored work.");
            Directory.CreateDirectory(Path.Combine(root,"Assets"));Directory.CreateDirectory(Path.Combine(root,"Scenes"));
            if(!File.Exists(_file))new ProjectSerializer().Save(new(){Name="Dune Company",StartupScene="Scenes/SandTest.bytescene"},_file);
            var package=Path.GetFullPath("artifacts/plugins/DesertTerrain.byteplugin");
            var installed=ByteEnginePluginPackageManager.ListInstalled(root).FirstOrDefault(p=>p.Id=="bytebard.desertterrain");
            if(installed?.PackagePath is string existing)File.Copy(package,existing,true);else Console.WriteLine(ByteEnginePluginPackageManager.Import(root,package).Message);
        }
        _project=EditorProjectContext.Open(_file,m=>Console.WriteLine("WARNING: "+m));
        DesertTerrainTests.Check(_project.IsPluginLoaded("bytebard.desertterrain"),"Terrain package imported and loaded into the new project");
        string path=Path.Combine(root,_project.Project.StartupScene);
        if(_prepare){var authored=CreateScene();_project.Scenes.Save(authored,path);foreach(var go in authored.GameObjects.ToArray())authored.DestroyGameObject(go);}
        var scene=_project.Scenes.Load(path);var terrain=scene.GameObjects.SelectMany(g=>g.Components).OfType<DesertTerrain3D>().Single();
        DesertTerrainTests.Check(terrain.TerrainSourceInfo.Contains("16-bit PNG"),"Scene loads a 16-bit heightmap landscape");
        DesertTerrainTests.Check(terrain.ChunkCount==64&&scene.GameObjects.Any(g=>g.Name=="Test Rover"),"Terrain and editable rover survive native scene save/reload");
        string output=Path.Combine(root,"Preview");Directory.CreateDirectory(output);
        Capture(scene,Path.Combine(output,"terrain-editor.png"),true);
        Scenes.LoadScene(scene);var rover=scene.FindGameObject("Test Rover")!.GetComponent<DesertSandbox3D>()!;rover.UseControls=false;rover.FollowCamera=false;
        // Exercise real Game View input, including the explicit save slot.
        rover.UseControls=true;
        int priorRevision=terrain.DeformationRevision;Tick(scene,[Key.Tab],true);
        DesertTerrainTests.Check(terrain.DeformationRevision==priorRevision,"Mouse input and Tab no longer enable sculpting");
        Tick(scene,[]);var inputStart=rover.Transform.WorldPosition;for(int i=0;i<60;i++)Tick(scene,[Key.W]);
        DesertTerrainTests.Check(Vector3.Distance(inputStart,rover.Transform.WorldPosition)>1,"Native W input drives the rover over the heightmap");
        Tick(scene,[Key.R]);rover.UseControls=false;
        var start=rover.Transform.WorldPosition;for(int i=0;i<300;i++){Time.Update(1f/60);rover.StepDrive(1,i>120?.12f:0,false,1f/60);}
        DesertTerrainTests.Check(Vector3.Distance(start,rover.Transform.WorldPosition)>20&&terrain.DeformationRevision>10,"Drive test moves over dunes and leaves actual sand ruts");
        terrain.TryGetSand(new(4,0,-4),out var original);
        terrain.StampTrack(original.Position-Vector3.UnitX*3,original.Position+Vector3.UnitX*3,1,.15f);
        terrain.TryGetSand(new(4,0,-4),out var tracked);DesertTerrainTests.Check(tracked.Position.Y<original.Position.Y&&original.Position.Y-tracked.Position.Y<=terrain.MaximumRutDepth,"Wheel marks remain shallow on the heightmap");
        rover.UseControls=true;Tick(scene,[Key.F5]);DesertTerrainTests.Check(terrain.LastSaveMessage.StartsWith("Sand saved"),"F5 saves Play-mode sand to the project save slot");
        terrain.ResetSand();Tick(scene,[Key.F9]);terrain.TryGetSand(new(4,0,-4),out var disk);
        DesertTerrainTests.Check(Math.Abs(disk.Position.Y-tracked.Position.Y)<.0001f,"F9 restores the saved sand after a reset");
        // Keep a fresh project for the user; this diagnostic save must not auto-load into their first play session.
        string slot=Path.Combine(root,"Saves","sand-"+terrain.GameObject.Id.ToString("N")+".json");File.Delete(slot);
        var camera=scene.ActiveCamera!;Look(camera,new(13,16,14),new(0,0,-10));Capture(scene,Path.Combine(output,"heightmap-and-tracks.png"),false);
        string statePath=Path.Combine(output,"roundtrip.bytescene");_project.Scenes.Save(scene,statePath);
        var restored=_project.Scenes.Load(statePath);var saved=restored.GameObjects.SelectMany(g=>g.Components).OfType<DesertTerrain3D>().Single();saved.TryGetSand(new(4,0,-4),out var reload);
        DesertTerrainTests.Check(Math.Abs(reload.Position.Y-tracked.Position.Y)<.0001f,"Native scene save/reload preserves heightmap wheel ruts");
        // Benchmark warmed terrain renders, excluding screenshot readback and first-frame resource creation.
        using var framebuffer=new SceneFramebuffer();var ec=EditCamera(camera);for(int i=0;i<8;i++)framebuffer.Render(Renderer,Renderer3D,scene,EditorMode.Edit,new EditorCamera(),ec,true,1280,720,1280,720,drawGrid3D:false);
        var times=new List<double>();for(int i=0;i<45;i++){var timer=Stopwatch.StartNew();framebuffer.Render(Renderer,Renderer3D,scene,EditorMode.Edit,new EditorCamera(),ec,true,1280,720,1280,720,drawGrid3D:false);GL.Finish();times.Add(timer.Elapsed.TotalMilliseconds);}times.Sort();
        File.WriteAllText(Path.Combine(output,"validation.json"),JsonSerializer.Serialize(new{engine=ByteEngineInfo.Version,source=terrain.TerrainSourceInfo,heightmapHash=terrain.HeightmapContentHash,metres=terrain.Cells*terrain.Spacing,chunks=terrain.ChunkCount,deformationRevision=terrain.DeformationRevision,renderMedianMs=times[times.Count/2],renderP95Ms=times[(int)(times.Count*.95)],tests="import, component placement, edit rendering, heightmap loading, drive/ruts, save/reload passed"},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"Native warmed render median {times[times.Count/2]:0.00}ms; p95 {times[(int)(times.Count*.95)]:0.00}ms. Screenshots: {output}");
        foreach(var go in restored.GameObjects.ToArray())restored.DestroyGameObject(go);Scenes.UnloadScene();foreach(var go in scene.GameObjects.ToArray())scene.DestroyGameObject(go);
        _project.Dispose();Close();
    }
    private static void Tick(Scene scene,ByteEngine.Core.Key[] keys,bool mouse=false)
    {
        var input=new ByteEngine.Core.InputSystem.RawInputSnapshot();input.KeysDown.UnionWith(keys);if(mouse)input.MouseButtonsDown.Add(ByteEngine.Core.MouseButton.Left);
        Input.UpdatePortable(input,false);Input.SetGameViewPointer(new(.5f,.5f),new(1280,720),true);Time.Update(1f/60);scene.UpdateInternal();
    }
    private static Scene CreateScene()
    {
        var scene=new Scene("Dune Company - Sand Proving Ground");
        scene.CreateGameObject("Desert - Editable Dunes").AddComponent(new DesertTerrain3D{Cells=256,Spacing=.5f,HeightmapPath="Assets/Terrain/dune-company-heightmap.png",HeightmapHeight=24});
        var camera=scene.CreateGameObject("Camera - Rover").AddComponent(new Camera3D{ActiveGameCamera=true,FarClip=400,FieldOfView=62});Look(camera,new(22,18,26),new(0,0,-8));
        var sun=scene.CreateGameObject("Sun - Late Afternoon");sun.Transform.EulerAngles=new(42,-35,0);sun.AddComponent(new DirectionalLight{Color=new(1,.86f,.66f),Intensity=1.7f,AmbientIntensity=.35f,CastShadows=false,ShadowResolution=1024,ShadowDistance=45,ShadowStrength=.65f});
        scene.CreateGameObject("Desert Sky and Dust Haze").AddComponent(new SkyEnvironment{ZenithColor=new(.27f,.42f,.56f),HorizonColor=new(.86f,.71f,.51f),GroundColor=new(.35f,.23f,.13f),EnvironmentLightingEnabled=false,OverrideAmbient=true,AmbientIntensity=.42f,FogEnabled=true,FogColor=new(.73f,.58f,.39f),FogStartDistance=55,FogEndDistance=145,FogMaxOpacity=.8f});
        var rover=scene.CreateGameObject("Test Rover");rover.Transform.WorldPosition=new(0,1,0);rover.AddComponent(new DesertSandbox3D());
        Part(scene,rover,"Body - GLM Model Placeholder",new(0,.12f,0),new(2.1f,.65f,3.5f),new(.22f,.25f,.24f,1));
        Part(scene,rover,"Cab - GLM Model Placeholder",new(0,.72f,-.25f),new(1.65f,.65f,1.4f),new(.72f,.37f,.10f,1));
        Part(scene,rover,"Windshield",new(0,.78f,-.97f),new(1.35f,.36f,.08f),new(.10f,.18f,.22f,1));
        Part(scene,rover,"Front bumper",new(0,-.03f,-1.84f),new(2.3f,.22f,.22f),new(.08f,.09f,.09f,1));
        for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)Part(scene,rover,$"Wheel {(x<0?"Left":"Right")} {(z<0?"Front":"Rear")}",new(x*1.22f,-.38f,z*1.15f),new(.55f,1.25f,1.25f),new(.08f,.075f,.07f,1),PrimitiveMeshType.Sphere);
        var hud=scene.CreateGameObject("HUD - Editable Canvas");hud.AddComponent(new UiCanvas());
        var panel=scene.CreateGameObject("HUD - Header Background");panel.SetParent(hud,false);panel.AddComponent(new UiWidget{Kind=UiWidgetKind.Panel,Offset=new(24,24),Size=new(670,108),Color=new(.075f,.095f,.10f,.88f),Interactable=false});
        Text(scene,hud,"HUD - Title","DUNE COMPANY",new(40,38),27,new(.96f,.79f,.47f,1));
        Text(scene,hud,"HUD - Subtitle","HEIGHTMAP DESERT / SAND PROVING GROUND",new(40,72),13,new(.72f,.75f,.73f,1));
        Text(scene,hud,"HUD - Status","ROVER / LOOSE SAND TEST",new(40,102),14,Vector4.One);
        Text(scene,hud,"HUD - Help","WASD Drive   Space Brake   R Recover\nF5 Save tracks   F9 Load   F6 Reset tracks\nJ/L Orbit   I/K Tilt   U/O Zoom",new(28,-80),14,new(.96f,.94f,.85f,1),UiAnchor.BottomLeft);
        return scene;
    }
    private static GameObject Part(Scene s,GameObject parent,string name,Vector3 p,Vector3 scale,Vector4 color,PrimitiveMeshType primitive=PrimitiveMeshType.Cube)
    {var o=s.CreateGameObject(name);o.SetParent(parent,false);o.Transform.LocalPosition=p;o.Transform.LocalScale=scale;o.AddComponent(new MeshRenderer{UsePrimitive=true,Primitive=primitive,Material=new(){BaseColor=color,Roughness=.85f}});return o;}
    private static void Text(Scene s,GameObject parent,string name,string text,Vector2 offset,int size,Vector4 color,UiAnchor anchor=UiAnchor.TopLeft)
    {var o=s.CreateGameObject(name);o.SetParent(parent,false);o.AddComponent(new UiText{Text=text,Offset=offset,FontSize=size,Color=color,Anchor=anchor,ShadowColor=new(0,0,0,.7f),ShadowOffset=new(1,1)});}
    private static void Look(Camera3D c,Vector3 p,Vector3 target){c.Transform.WorldPosition=p;Matrix4x4.Invert(Matrix4x4.CreateLookAt(p,target,Vector3.UnitY),out var world);c.Transform.WorldRotation=Quaternion.CreateFromRotationMatrix(world);}
    private static EditorCamera3D EditCamera(Camera3D c){var f=c.Transform.Forward;return new(){Position=c.Transform.WorldPosition,Yaw=MathF.Atan2(f.Z,f.X)*180/MathF.PI,Pitch=MathF.Asin(f.Y)*180/MathF.PI,FieldOfView=c.FieldOfView};}
    private void Capture(Scene scene,string file,bool edit)
    {
        using var fb=new SceneFramebuffer();fb.Render(Renderer,Renderer3D,scene,edit?EditorMode.Edit:EditorMode.Play,new EditorCamera(),EditCamera(scene.ActiveCamera!),true,1280,720,1280,720,drawGrid3D:false);GL.Finish();var pixels=new byte[1280*720*4];GL.BindTexture(TextureTarget.Texture2D,(int)fb.TextureId);GL.GetTexImage(TextureTarget.Texture2D,0,PixelFormat.Rgba,PixelType.UnsignedByte,pixels);GL.BindTexture(TextureTarget.Texture2D,0);
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
}
