using System.Numerics;
using System.Diagnostics;
using System.IO.Compression;
using System.Buffers.Binary;
using DesertTerrain;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Physics;
using ByteEngine.Core.Scene;
namespace ByteEngine.Tests;
internal static class DesertTerrainTests
{
    internal static void Check(bool condition,string message){if(!condition)throw new InvalidOperationException(message);Console.WriteLine("PASS: "+message);}
    public static void Run()
    {
        string root=Path.Combine(Path.GetTempPath(),"ByteEngine-heightmap-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        var scene=new Scene("heightmap tests");var physicsScene=new Scene("terrain physics");
        try
        {
            string file=Path.Combine(root,"precision.png");ushort[] pixels=[0,1000,65535,65535,30000,0];WriteMap(file,3,2,pixels,16,false);
            var image=HeightmapImage.Load(file);
            Check(image.Width==3&&image.Height==2&&image.BitDepth==16,"16-bit rectangular heightmap dimensions are retained");
            Check(Math.Abs(image.Sample(.5f,0)-1000/65535f)<1e-7,"16-bit heights retain sub-8-bit precision");
            Check(image.Sample(0,0)==0&&image.Sample(1,0)==1,"Black and white map to exact normalized endpoints");
            string eight=Path.Combine(root,"eight.png");WriteMap(eight,3,2,[0,128,255,255,64,0],8,false);Check(Math.Abs(HeightmapImage.Load(eight).Sample(.5f,0)-128/255f)<1e-6,"8-bit PNG heightmaps are supported");
            ushort[] filters=Enumerable.Range(0,25).Select(i=>(ushort)(i*2571%65536)).ToArray();string filtered=Path.Combine(root,"filters.png");WriteMap(filtered,5,5,filters,16,true);var decoded=HeightmapImage.Load(filtered);
            Check(Enumerable.Range(0,25).All(i=>Math.Abs(decoded.Sample((i%5)/4f,(i/5)/4f)-filters[i]/65535f)<1e-6),"PNG filters None, Sub, Up, Average and Paeth decode correctly");
            byte[] broken=File.ReadAllBytes(file);broken[45]^=1;string corrupt=Path.Combine(root,"corrupt.png");File.WriteAllBytes(corrupt,broken);bool rejected=false;try{HeightmapImage.Load(corrupt);}catch(InvalidDataException){rejected=true;}Check(rejected,"Corrupt heightmap checksums are rejected");
            var t=scene.CreateGameObject("Heightmap").AddComponent(new DesertTerrain3D{HeightmapPath=file,HeightmapHeight=20,BaseHeight=2,Cells=32,Spacing=.5f});t.EnsureGenerated();
            Check(Math.Abs(t.HeightAt(0,0)-2)<.00001f&&Math.Abs(t.HeightAt(32,0)-22)<.00001f,"Height scale and base height convert PNG data to metres");
            Check(Math.Abs(t.HeightAt(16,0)-(2+20*1000/65535f))<.00001f,"Terrain grid resamples the source image without quantizing heights");
            t.DuneHeight=70;t.Seed=99;Check(Math.Abs(t.HeightAt(16,0)-(2+20*1000/65535f))<.00001f,"Legacy dune controls do not alter an image-based landscape");
            int hits=0;var rng=new Random(71);for(int i=0;i<150;i++){var start=new Vector3((float)(rng.NextDouble()*15-7.5),50,(float)(rng.NextDouble()*15-7.5));if(t.TrySampleWorld(start,out var surface,out _)&&t.Cast(start,-Vector3.UnitY,0,100,out var distance,out _)&&Math.Abs(50-distance-surface.Y)<.0002f)hits++;}
            Check(hits==150,"Terrain rays match heightmap-rendered triangles at 150 positions");
            Check(!t.Cast(new(100,50,100),-Vector3.UnitY,0,100,out _,out _),"Finite heightmap bounds reject outside rays");
            t.FlipHeightmapX=true;Check(Math.Abs(t.HeightAt(0,0)-22)<.00001f,"Horizontal heightmap orientation is configurable");t.FlipHeightmapX=false;t.FlipHeightmapZ=true;Check(Math.Abs(t.HeightAt(0,0)-22)<.00001f,"Vertical heightmap orientation is configurable");t.FlipHeightmapZ=false;
            string hash=t.HeightmapContentHash;WriteMap(file,3,2,[65535,65535,65535,65535,65535,65535],16,false);t.Regenerate();Check(t.HeightmapContentHash!=hash&&Math.Abs(t.HeightAt(0,0)-22)<.00001f,"Reloading an edited heightmap rebuilds its source and fingerprint");
            t.HeightmapPath=Path.Combine(root,"missing.png");t.EnsureGenerated();Check(t.TerrainSourceInfo.StartsWith("Heightmap error")&&Math.Abs(t.HeightAt(0,0)-22)<.00001f,"Missing replacement maps report an error and retain the last good terrain");
            t.HeightmapPath=file;File.WriteAllBytes(file,[1,2,3]);t.Regenerate();Check(Math.Abs(t.HeightAt(0,0)-22)<.00001f&&t.TerrainSourceInfo.StartsWith("Heightmap error"),"A corrupt live reload preserves the last good landscape");
            var flat=scene.CreateGameObject("Sand").AddComponent(new DesertTerrain3D{Cells=64,Spacing=.5f,DuneHeight=0,FlatCenterRadius=100});flat.EnsureGenerated();flat.TryGetSand(Vector3.Zero,out var before);
            var windowTerrain=scene.CreateGameObject("Window test").AddComponent(new DesertTerrain3D{Cells=64,Spacing=.5f,DuneHeight=0,FlatCenterRadius=100});windowTerrain.EnsureGenerated();
            var patch=windowTerrain.EnableSimulationPatch(Vector3.Zero);var initialWindow=windowTerrain.SimulationPatch!.WindowRevision;
            for(int i=0;i<100;i++)windowTerrain.FocusSimulationPatch(new(i%2==0?-.1f:.1f,0,i%2==0?-.1f:.1f));
            Check(windowTerrain.SimulationPatch.WindowRevision==initialWindow,"Resting motion across tile boundaries does not churn the sand window");
            windowTerrain.FocusSimulationPatch(new(11,0,0));Check(windowTerrain.SimulationPatch.WindowRevision>initialWindow&&windowTerrain.SimulationPatch.ContainsLocal(11,0),"Travelling beyond hysteresis advances the sand window with coverage");
            int rev=flat.DeformationRevision;flat.StampTrack(new(0,20,0),new(1,20,0),1,.1f);flat.StampTrack(Vector3.Zero,Vector3.Zero,1,.1f);Check(flat.DeformationRevision==rev,"Airborne and stationary contacts leave no ruts");
            flat.StampTrack(new(-3,0,0),new(3,0,0),1,.1f);flat.TryGetSand(Vector3.Zero,out var tracked);
            Check(tracked.Position.Y<before.Position.Y&&tracked.Grip>before.Grip&&tracked.RollingResistance<before.RollingResistance,"Wheel ruts compact sand and change grip and resistance");
            Check(flat.FlushMeshChanges()<=4&&flat.LastUpdatedChunks>0,"Ruts update only affected chunks and seam neighbours");Check(flat.FlushMeshChanges()==0,"Static terrain does not upload meshes every frame");
            var fineTerrain=scene.CreateGameObject("Fine sand").AddComponent(new DesertTerrain3D{Cells=64,Spacing=.5f,DuneHeight=0,FlatCenterRadius=100});fineTerrain.ResetSand();var fine=(SandSimulationPatch3D)fineTerrain.EnableSimulationPatch(Vector3.Zero);Check(fine.Columns==129&&fine.CellSpacing==.25f,"Local soil patch uses 25cm collision samples");fine.Stamp(new(-1,0,0),new(1,0,0),.6f,2500,5,.1f);fine.Sample(Vector3.Zero,out var fineRut);Check(fineRut.Position.Y<-.01f&&fineRut.Position.Y>=-fineTerrain.MaximumRutDepth,"Fine soil patch develops bounded physical ruts");string fineSave=fineTerrain.ExportSand();fine.Focus(new(80,0,0));fine.Focus(Vector3.Zero);fine.Sample(Vector3.Zero,out var revisited);Check(Math.Abs(revisited.Position.Y-fineRut.Position.Y)<.00001f,"Ruts survive moving the active sand window away and back");fineTerrain.ResetSand();fineTerrain.ImportSand(fineSave);fine.Sample(Vector3.Zero,out var restoredFine);Check(Math.Abs(restoredFine.Position.Y-fineRut.Position.Y)<.00001f,"Fine soil deformation survives save and reload");fineTerrain.ResetSand();

            for(int i=0;i<100;i++){flat.TryGetSand(Vector3.Zero,out var surface);flat.StampTrack(new(-3,surface.Position.Y,0),new(3,surface.Position.Y,0),1,1);}
            flat.TryGetSand(Vector3.Zero,out var saturated);Check(saturated.Position.Y>=-flat.MaximumRutDepth-.0001f,"Repeated wheel passes stay within the shallow rut limit");
            string data=flat.ExportSand();var copy=scene.CreateGameObject("Saved sand").AddComponent(new DesertTerrain3D{Cells=64,Spacing=.5f,DuneHeight=0,FlatCenterRadius=100});copy.ImportSand(data);copy.TryGetSand(Vector3.Zero,out var restored);Check(Vector3.Distance(restored.Position,saturated.Position)<.00001f&&Math.Abs(restored.Compaction-saturated.Compaction)<.00001f,"Sand state restores ruts and compaction");
            flat.ResetSand();flat.TryGetSand(Vector3.Zero,out var reset);Check(reset.Compaction==0&&Math.Abs(reset.Position.Y)<.00001f,"Reset restores original terrain heights");
            var soil=scene.CreateGameObject("Contact soil").AddComponent(new DesertTerrain3D{Cells=64,Spacing=.5f,DuneHeight=0,FlatCenterRadius=100});soil.EnsureGenerated();
            Check(soil.StampWheelContact(new(0,10,0),new(0,10,0),.4f,1000,8,.05f)==0,"Airborne wheel pressure cannot deform soil");
            for(int i=0;i<60;i++){soil.TryGetSand(Vector3.Zero,out var surface);soil.StampWheelContact(surface.Position,surface.Position,.4f,1500,8,.05f);}
            soil.TryGetSand(Vector3.Zero,out var compressed);soil.TryGetSand(new(.5f,0,0),out var shoulder);
            Check(compressed.Position.Y<-.001f&&shoulder.Position.Y>0&&shoulder.Position.Y<=.06001f,"Loaded spinning contact compacts and shears bounded sand shoulders");
            var restoredSoil=scene.CreateGameObject("Soil copy").AddComponent(new DesertTerrain3D{Cells=64,Spacing=.5f,DuneHeight=0,FlatCenterRadius=100});restoredSoil.ImportSand(soil.ExportSand());restoredSoil.TryGetSand(new(.5f,0,0),out var savedShoulder);Check(Math.Abs(savedShoulder.Position.Y-shoulder.Position.Y)<.00001f,"Displaced sand shoulders survive save/reload");
            var ground=physicsScene.CreateGameObject("Ground").AddComponent(new DesertTerrain3D{Cells=32,DuneHeight=0,FlatCenterRadius=100});var body=physicsScene.CreateGameObject("Crate");body.Transform.WorldPosition=new(0,5,0);body.AddComponent(new BoxCollider3D());body.AddComponent(new Rigidbody3D());var capsule=physicsScene.CreateGameObject("Capsule");capsule.Transform.WorldPosition=new(3,5,0);capsule.AddComponent(new CapsuleCollider3D());capsule.AddComponent(new Rigidbody3D());
            for(int i=0;i<300;i++)physicsScene.Physics.Step(physicsScene,1f/60);Check(Math.Abs(body.Transform.WorldPosition.Y-.5f)<.03f&&Math.Abs(capsule.Transform.WorldPosition.Y)<.03f,"Native box and capsule bodies settle on terrain collision");Check(GameplayQuery3D.Raycast(physicsScene,new(8,8,8),-Vector3.UnitY,out var hit,20)&&hit.Collider==ground,"Native gameplay rays include the terrain");
            var big=scene.CreateGameObject("Benchmark").AddComponent(new DesertTerrain3D{Cells=256,Spacing=.5f});var sw=Stopwatch.StartNew();big.EnsureGenerated();double gen=sw.Elapsed.TotalMilliseconds;
            sw.Restart();for(int i=0;i<200;i++){big.TryGetSand(new(i%8,0,i%5),out var sand);big.StampTrack(sand.Position-Vector3.UnitX*.5f,sand.Position+Vector3.UnitX*.5f,1,.01f);big.FlushMeshChanges();}double update=sw.Elapsed.TotalMilliseconds/200;
            Console.WriteLine($"TERRAIN BENCHMARK: generation {gen:0.00}ms; track stamping and chunk updates {update:0.000}ms; chunks {big.ChunkCount}.");Console.WriteLine("Heightmap terrain and wheel-rut regressions passed.");
        }
        finally
        {
            foreach(var go in scene.GameObjects.ToArray())scene.DestroyGameObject(go);foreach(var go in physicsScene.GameObjects.ToArray())physicsScene.DestroyGameObject(go);
            string full=Path.GetFullPath(root);if(full.StartsWith(Path.GetFullPath(Path.GetTempPath())+"ByteEngine-heightmap-",StringComparison.OrdinalIgnoreCase))Directory.Delete(full,true);
        }
    }
    private static void WriteMap(string file,int width,int height,ushort[] samples,int depth,bool filters)
    {
        using var output=File.Create(file);output.Write(new byte[]{137,80,78,71,13,10,26,10});byte[] header=new byte[13];BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(0,4),width);BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4,4),height);header[8]=(byte)depth;Chunk(output,"IHDR",header);
        using var compressed=new MemoryStream();using(var z=new ZLibStream(compressed,CompressionLevel.Fastest,true))
        {
            int bpp=depth/8;var previous=new byte[width*bpp];for(int y=0;y<height;y++)
            {
                var row=new byte[previous.Length];for(int x=0;x<width;x++)if(depth==16)BinaryPrimitives.WriteUInt16BigEndian(row.AsSpan(x*2,2),samples[y*width+x]);else row[x]=(byte)samples[y*width+x];
                int filter=filters?y%5:0;z.WriteByte((byte)filter);for(int i=0;i<row.Length;i++){int a=i>=bpp?row[i-bpp]:0,b=previous[i],c=i>=bpp?previous[i-bpp]:0;int p=a+b-c,pa=Math.Abs(p-a),pb=Math.Abs(p-b),pc=Math.Abs(p-c);int predictor=filter switch{1=>a,2=>b,3=>(a+b)/2,4=>pa<=pb&&pa<=pc?a:pb<=pc?b:c,_=>0};z.WriteByte(unchecked((byte)(row[i]-predictor)));}previous=row;
            }
        }
        Chunk(output,"IDAT",compressed.ToArray());Chunk(output,"IEND",[]);
    }
    private static void Chunk(Stream s,string type,byte[] data)
    {
        byte[] name=System.Text.Encoding.ASCII.GetBytes(type),length=new byte[4];BinaryPrimitives.WriteInt32BigEndian(length,data.Length);s.Write(length);s.Write(name);s.Write(data);uint crc=0xffffffff;foreach(byte b in name.Concat(data)){crc^=b;for(int i=0;i<8;i++)crc=(crc>>1)^((crc&1)==1?0xedb88320u:0);}BinaryPrimitives.WriteUInt32BigEndian(length,~crc);s.Write(length);
    }
}
