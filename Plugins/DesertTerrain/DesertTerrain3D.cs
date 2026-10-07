using ByteEngine.Core.Gameplay;

using System.Numerics;

using System.IO.Compression;

using System.Text.Json.Nodes;

using ByteEngine.Core.Characters;

using ByteEngine.Core.Graphics;

using ByteEngine.Core.Graphics.ThreeD;



namespace DesertTerrain;



public readonly record struct SandSurface(Vector3 Position,Vector3 Normal,float Compaction,float Grip,float RollingResistance);



/// <summary>Deterministic, chunked desert heightfield. Rendering, collision and deformation share one grid.</summary>

public sealed partial class DesertTerrain3D : HeightfieldCollider3D

{

    public bool UseReferenceSandSurface{get;set;}

    InteractiveSand3D? _referenceSand;

    internal InteractiveSand3D ReferenceSand {

        get {

            if(_referenceSand==null){foreach(var child in GameObject.Children)if(child.GetComponent<InteractiveSand3D>() is {} existing){_referenceSand=existing;break;}}

            if(_referenceSand==null){var obj=GameObject.Scene!.CreateGameObject("Interactive sand - fixed GPU field");obj.SetParent(GameObject,false);_referenceSand=obj.AddComponent(new InteractiveSand3D{Center=Center});}

            _referenceSand.ContactDepth=Math.Clamp(ContactRutDepth,.01f,.32f);return _referenceSand;

        }

    }

    public SandSimulationPatch3D? SimulationPatch{get;private set;}

    readonly Dictionary<Chunk,Mesh> _patchCuts=[];int _patchCutRevision=-1;

    public HeightfieldCollider3D EnableSimulationPatch(Vector3 focus){if(UseReferenceSandSurface)return ReferenceSand;if(SimulationPatch==null){var obj=GameObject.Scene!.CreateGameObject("Local sand simulation - 25cm cells");obj.SetParent(GameObject,false);SimulationPatch=obj.AddComponent(new SandSimulationPatch3D{Terrain=this,Center=Center});}SimulationPatch.Focus(focus);return SimulationPatch;}

    public void FocusSimulationPatch(Vector3 focus){if(!UseReferenceSandSurface)SimulationPatch?.Focus(focus);}

    void ClearPatchCuts(){foreach(var mesh in _patchCuts.Values)mesh.Dispose();_patchCuts.Clear();}

    Mesh CutPatch(Chunk c){if(_patchCuts.TryGetValue(c,out var cached))return cached;var indices=new List<uint>();float half=_cells*_spacing*.5f;for(int z=0;z<c.Depth;z++)for(int x=0;x<c.Width;x++){float px=(c.X+x+.5f)*_spacing-half,pz=(c.Z+z+.5f)*_spacing-half;if(SimulationPatch!.ContainsLocal(px,pz))continue;uint a=(uint)(z*(c.Width+1)+x),b=a+1,d=a+(uint)c.Width+1,e=d+1;indices.Add(a);indices.Add(d);indices.Add(b);indices.Add(b);indices.Add(d);indices.Add(e);}var mesh=new Mesh(c.Vertices,indices.ToArray());_patchCuts[c]=mesh;return mesh;}

    public string PackedSurfaceMaskPath { get; set; } = "";

    public string HeightmapPath { get; set; } = "";

    public float HeightmapHeight { get; set; } = 24;

    public bool FlipHeightmapX { get; set; }

    public bool FlipHeightmapZ { get; set; }

    public bool ReloadHeightmapRequested { get; set; }

    public string TerrainSourceInfo { get; private set; } = "Procedural fallback";

    private HeightmapImage? _heightmap;

    private Settings? _failedMapSettings;

    private string _loadedMapPath="";

    private DateTime _mapWriteTime;

    private long _nextMapPoll;

    public int Seed { get; set; } = 1731;

    public int Cells { get; set; } = 256;

    public float Spacing { get; set; } = 1;

    public float DuneHeight { get; set; } = 9;

    public float DuneWavelength { get; set; } = 48;

    public float WindAngle { get; set; } = 25;

    public float BaseHeight { get; set; }

    public float FlatCenterRadius { get; set; } = 16;

    public float ContactRutDepth {get;set;}=.065f;

    public float MaximumRutDepth { get; set; } = .22f;

    public float LooseGrip { get; set; } = .65f;

    public float PackedGrip { get; set; } = 1;

    public float LooseResistance { get; set; } = .18f;

    public bool CastShadows { get; set; } = false;

    public bool UseDistanceLod {get;set;}=true;

    public float LodNearDistance {get;set;}=100;

    public float LodFarDistance {get;set;}=240;

    public float ReceiveShadowsDistance { get; set; } = 36;

    public bool AutoLoadSavedSand { get; set; } = true;

    public string LastSaveMessage { get; private set; } = "F5 saves sand; F9 reloads it";

    internal string? ProjectRoot { get; set; }

    public bool ResetDeformationRequested { get; set; }

    public Vector4 SandColor { get; set; } = new(.73f,.53f,.30f,1);

    public string HeightmapContentHash { get {EnsureGenerated();return _heightmap?.ContentHash??"";} }

    public int ChunkCount => _chunks.Count;

    public int LastUpdatedChunks { get; private set; }

    public int DeformationRevision { get; private set; }

    public override int Columns { get {EnsureGenerated();return _cells+1;} }

    public override int Rows => Columns;

    public override float CellSpacing { get {EnsureGenerated();return _spacing;} }

    public override Vector2 GridMinimum { get {EnsureGenerated();return new(-_cells*_spacing/2);} }

    public override Vector3 Size { get => LocalTerrainBounds.Size; set {Cells=(int)Math.Clamp(Math.Max(value.X,value.Z)/Math.Max(Spacing,.25f),32,512);} }

    public override BoundingBox3D LocalTerrainBounds { get {EnsureGenerated();float half=_cells*_spacing/2;return new(new(-half,_minimum-32,-half),new(half,_maximum,half));} }

    private const int ChunkCells=32;

    private int _cells;private float _spacing,_minimum,_maximum;

    private Settings? _settings;

    private float[] _base=[],_heights=[],_packed=[],_initialPacked=[];

    private string _packingKey="";

    private readonly List<Chunk> _chunks=[];

    private readonly HashSet<int> _dirty=[];

    public string SandAlbedoPath {get;set;}="";

    public string SandNormalPath {get;set;}="";

    public string SandRoughnessPath {get;set;}="";

    public float TextureWorldSize {get;set;}=8;

    public bool SandShadingEnabled {get;set;}=true;

    public float NormalStrength {get;set;}=.65f;

    public float SandRoughness {get;set;}=1;

    private string _textureKey="";

    private Texture2D? _sandTexture,_normalTexture,_roughTexture;

    private readonly Material _material=new(){Roughness=1,Metallic=0};

    private readonly record struct Settings(int Seed,int Cells,float Spacing,float Height,float Wavelength,float Wind,float Base,float Flat,string Map,float MapHeight,bool FlipX,bool FlipZ,string MapHash);

    private sealed record Chunk(int X,int Z,int Width,int Depth,float[] Vertices,Mesh Mesh)

    {

        public Mesh? MediumMesh;public Mesh? FarMesh;

        public void InvalidateLod(){MediumMesh?.Dispose();FarMesh?.Dispose();MediumMesh=FarMesh=null;}

    }

    private static float Finite(float v,float fallback)=>float.IsFinite(v)?v:fallback;

    public void EnsureGenerated()

    {

        MaximumRutDepth=Math.Clamp(Finite(MaximumRutDepth,.22f),0,.5f);

        LooseGrip=Math.Clamp(Finite(LooseGrip,.65f),0,3);PackedGrip=Math.Clamp(Finite(PackedGrip,1),0,3);LooseResistance=Math.Clamp(Finite(LooseResistance,.18f),0,2);

        Cells=Math.Clamp(Cells,32,512);Spacing=Math.Clamp(Finite(Spacing,1),.25f,8);

        DuneHeight=Math.Clamp(Finite(DuneHeight,9),0,80);DuneWavelength=Math.Clamp(Finite(DuneWavelength,48),8,300);

        WindAngle=Finite(WindAngle,25);BaseHeight=Math.Clamp(Finite(BaseHeight,0),-1000,1000);FlatCenterRadius=Math.Clamp(Finite(FlatCenterRadius,16),0,200);

        HeightmapHeight=Math.Clamp(Finite(HeightmapHeight,24),0,500);HeightmapPath=HeightmapPath?.Trim()??"";

        if(_packingKey!=PackedSurfaceMaskPath){_settings=null;_packingKey=PackedSurfaceMaskPath;}

        bool fromImage=HeightmapPath.Length>0;

        var settings=new Settings(fromImage?0:Seed,Cells,Spacing,fromImage?0:DuneHeight,fromImage?0:DuneWavelength,fromImage?0:WindAngle,BaseHeight,fromImage?0:FlatCenterRadius,HeightmapPath,HeightmapHeight,FlipHeightmapX,FlipHeightmapZ,_heightmap?.ContentHash??"");

        if(_settings==settings)

        {

            if(_failedMapSettings!=null&&_failedMapSettings.Value.Map!=HeightmapPath)

            { _failedMapSettings=null;TerrainSourceInfo=_heightmap==null?"Procedural fallback":$"Heightmap: {_heightmap.Width} x {_heightmap.Height}, {_heightmap.BitDepth}-bit PNG"; }

            return;

        }

        if(_failedMapSettings==settings)return;

        if(HeightmapPath.Length>0)

        {

            string mapFile;

            try{mapFile=Path.GetFullPath(Path.IsPathRooted(HeightmapPath)?HeightmapPath:Path.Combine(ProjectRoot??Environment.CurrentDirectory,HeightmapPath));}

            catch(ArgumentException e){TerrainSourceInfo="Heightmap error: "+e.Message;_failedMapSettings=settings;if(_settings!=null)return;mapFile="";}

            if(_heightmap==null||_loadedMapPath!=mapFile)

            {

                try{var image=HeightmapImage.Load(mapFile);_heightmap=image;_failedMapSettings=null;_loadedMapPath=mapFile;_mapWriteTime=File.GetLastWriteTimeUtc(mapFile);}

                catch(Exception e)when(e is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException)

                {

                    TerrainSourceInfo="Heightmap error: "+e.Message;_failedMapSettings=settings;

                    if(_settings!=null)return;

                    _heightmap=null;_loadedMapPath=mapFile;

                }

            }

            settings=settings with {MapHash=_heightmap?.ContentHash??""};

            TerrainSourceInfo=_heightmap==null?TerrainSourceInfo:$"Heightmap: {_heightmap.Width} x {_heightmap.Height}, {_heightmap.BitDepth}-bit PNG";

        }

        else {_heightmap=null;_loadedMapPath="";TerrainSourceInfo="Procedural fallback (set Heightmap Path to use an image)";settings=settings with {MapHash=""};}

        SimulationPatch?.Reset();ClearPatchCuts();DisposeChunks();_settings=settings;_cells=Cells;_spacing=Spacing;

        int n=_cells+1;_base=new float[n*n];_heights=new float[n*n];_packed=new float[n*n];

        _minimum=float.PositiveInfinity;_maximum=float.NegativeInfinity;

        float angle=WindAngle*MathF.PI/180,co=MathF.Cos(angle),si=MathF.Sin(angle),phase=Hash(Seed,0,0)*100;

        for(int z=0;z<n;z++)for(int x=0;x<n;x++)

        {

            float h;

            if(HeightmapPath.Length>0)

            {

                float u=x/(float)_cells,v=z/(float)_cells;if(FlipHeightmapX)u=1-u;if(FlipHeightmapZ)v=1-v;

                h=BaseHeight+(_heightmap?.Sample(u,v)??0)*HeightmapHeight;

            }

            else

            {

                float px=(x-_cells*.5f)*Spacing,pz=(z-_cells*.5f)*Spacing;

                float along=(px*co+pz*si)/DuneWavelength,cross=(-px*si+pz*co)/DuneWavelength;

                float ridge=MathF.Sin((along+.19f*MathF.Sin(cross*3.4f+phase))*MathF.Tau+phase);

                float dune=MathF.Pow((ridge+1)*.5f,2.8f);

                float broad=Noise(px/(DuneWavelength*1.8f),pz/(DuneWavelength*1.8f),Seed+1);

                float detail=Noise(px/12,pz/12,Seed+2)*.45f;

                float blend=Smooth(Math.Clamp((MathF.Sqrt(px*px+pz*pz)-FlatCenterRadius)/12,0,1));

                h=BaseHeight+(dune*DuneHeight+broad*DuneHeight*.2f+detail)*blend;

            }

            _base[z*n+x]=_heights[z*n+x]=h;_minimum=Math.Min(_minimum,h);_maximum=Math.Max(_maximum,h);

        }

        _initialPacked=new float[_packed.Length];

        if(!string.IsNullOrWhiteSpace(PackedSurfaceMaskPath))

        {

            string mask=Path.IsPathRooted(PackedSurfaceMaskPath)?PackedSurfaceMaskPath:Path.Combine(ProjectRoot??Environment.CurrentDirectory,PackedSurfaceMaskPath);

            // A configured source is required content, not an optional cosmetic layer.

            var image=HeightmapImage.Load(mask);

            for(int z=0;z<n;z++)for(int x=0;x<n;x++)_initialPacked[z*n+x]=_packed[z*n+x]=Math.Clamp(image.Sample(x/(float)_cells,z/(float)_cells),0,1);

        }

        ApplyAuthoredLayer();

        for(int z=0;z<_cells;z+=ChunkCells)for(int x=0;x<_cells;x+=ChunkCells)

        {

            int w=Math.Min(ChunkCells,_cells-x),d=Math.Min(ChunkCells,_cells-z);

            var vertices=new float[(w+1)*(d+1)*8];var indices=new uint[w*d*6];int t=0;

            for(int iz=0;iz<d;iz++)for(int ix=0;ix<w;ix++){uint a=(uint)(iz*(w+1)+ix),b=a+1,c=a+(uint)w+1,e=c+1;indices[t++]=a;indices[t++]=c;indices[t++]=b;indices[t++]=b;indices[t++]=c;indices[t++]=e;}

            FillVertices(x,z,w,d,vertices);_chunks.Add(new(x,z,w,d,vertices,new Mesh(vertices,indices,dynamicVertices:true)));

        }

        _dirty.Clear();DeformationRevision=0;

    }

    public void Regenerate()

    {

        _failedMapSettings=null;

        if(!string.IsNullOrWhiteSpace(HeightmapPath))

        {

            try

            {

                string path=Path.GetFullPath(Path.IsPathRooted(HeightmapPath)?HeightmapPath:Path.Combine(ProjectRoot??Environment.CurrentDirectory,HeightmapPath));

                var image=HeightmapImage.Load(path);_heightmap=image;_loadedMapPath=path;_mapWriteTime=File.GetLastWriteTimeUtc(path);

            }

            catch(Exception e)when(e is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException)

            {

                TerrainSourceInfo="Heightmap error: "+e.Message;

                if(_settings==null)EnsureGenerated();return;

            }

        }

        SimulationPatch?.Reset();ClearPatchCuts();_settings=null;EnsureGenerated();

    }

    public void ResetSand(){if(UseReferenceSandSurface)ReferenceSand.ResetTracks();SimulationPatch?.Reset();ClearPatchCuts();EnsureGenerated();Array.Copy(_base,_heights,_base.Length);Array.Copy(_initialPacked,_packed,_packed.Length);for(int i=0;i<_chunks.Count;i++)_dirty.Add(i);DeformationRevision++;}

    public override float HeightAt(int x,int z){EnsureGenerated();return _heights[z*(_cells+1)+x];}

    private void FillVertices(int ox,int oz,int w,int d,float[] v)

    {

        int n=_cells+1;float half=_cells*_spacing*.5f;

        for(int z=0;z<=d;z++)for(int x=0;x<=w;x++)

        {

            int gx=ox+x,gz=oz+z,i=(z*(w+1)+x)*8;

            int lx=Math.Max(0,gx-1),rx=Math.Min(_cells,gx+1),lz=Math.Max(0,gz-1),rz=Math.Min(_cells,gz+1);

            var normal=Vector3.Normalize(new Vector3(-(_heights[gz*n+rx]-_heights[gz*n+lx])/((rx-lx)*_spacing),1,-(_heights[rz*n+gx]-_heights[lz*n+gx])/((rz-lz)*_spacing)));

            v[i]=gx*_spacing-half;v[i+1]=_heights[gz*n+gx];v[i+2]=gz*_spacing-half;v[i+3]=normal.X;v[i+4]=normal.Y;v[i+5]=normal.Z;v[i+6]=gx*_spacing/8;v[i+7]=gz*_spacing/8;

        }

    }

    public int FlushMeshChanges()

    {

        EnsureGenerated();LastUpdatedChunks=_dirty.Count;

        foreach(int i in _dirty){var c=_chunks[i];FillVertices(c.X,c.Z,c.Width,c.Depth,c.Vertices);c.Mesh.UpdateVertices(c.Vertices);c.InvalidateLod();}

        _dirty.Clear();return LastUpdatedChunks;

    }

    public bool TryGetSand(Vector3 world,out SandSurface sample){if(UseReferenceSandSurface){if(ReferenceSand.TrySampleWorld(world,out var position,out var normal)){sample=new(position,normal,.4f,LooseGrip+(PackedGrip-LooseGrip)*.4f,LooseResistance*.72f);return true;}sample=default;return false;}if(SimulationPatch!=null&&SimulationPatch.Sample(world,out sample))return true;return TryGetBaseSand(world,out sample);}

    internal bool TryGetBaseSand(Vector3 world,out SandSurface sample)

    {

        sample=default;if(!TrySampleWorld(world,out var surface,out var normal)||!Matrix4x4.Invert(SurfaceMatrix,out var inv))return false;

        var p=Vector3.Transform(surface,inv);float pack=SampleLocalCompaction(p.X,p.Z);

        sample=new(surface,normal,pack,Math.Clamp(LooseGrip,0,3)+(Math.Clamp(PackedGrip,0,3)-Math.Clamp(LooseGrip,0,3))*pack,Math.Clamp(LooseResistance,0,2)*(1-pack*.7f));return true;

    }

    internal float SampleLocalCompaction(float localX,float localZ)

    {

        float half=_cells*_spacing*.5f;

        float gx=Math.Clamp((localX+half)/_spacing,0,_cells),gz=Math.Clamp((localZ+half)/_spacing,0,_cells);

        int x=Math.Min((int)gx,_cells-1),z=Math.Min((int)gz,_cells-1),n=_cells+1;float u=gx-x,v=gz-z;

        return (_packed[z*n+x]*(1-u)+_packed[z*n+x+1]*u)*(1-v)+(_packed[(z+1)*n+x]*(1-u)+_packed[(z+1)*n+x+1]*u)*v;

    }

    // Only shallow contact compaction is supported; there is no excavation or terrain-raising gameplay.

    private int CompactSand(Vector3 world,float radius,float amount)

    {

        EnsureGenerated();if(!float.IsFinite(world.X)||!float.IsFinite(world.Y)||!float.IsFinite(world.Z)||!SupportedTransform||!float.IsFinite(radius)||!float.IsFinite(amount)||radius<=0||amount<=0||!Matrix4x4.Invert(SurfaceMatrix,out var inv))return 0;

        var p=Vector3.Transform(world,inv);float half=_cells*_spacing*.5f;float rx=radius/Transform.WorldScale.X,rz=radius/Transform.WorldScale.Z;

        int minX=Math.Clamp((int)MathF.Floor((p.X-rx+half)/_spacing),0,_cells),maxX=Math.Clamp((int)MathF.Ceiling((p.X+rx+half)/_spacing),0,_cells);

        int minZ=Math.Clamp((int)MathF.Floor((p.Z-rz+half)/_spacing),0,_cells),maxZ=Math.Clamp((int)MathF.Ceiling((p.Z+rz+half)/_spacing),0,_cells);

        int changed=0,n=_cells+1;

        for(int z=minZ;z<=maxZ;z++)for(int x=minX;x<=maxX;x++)

        {

            float dx=(x*_spacing-half-p.X)/rx,dz=(z*_spacing-half-p.Z)/rz,q=dx*dx+dz*dz;if(q>=1)continue;

            float weight=(1-q)*(1-q),a=amount/Transform.WorldScale.Y*weight;int i=z*n+x;float old=_heights[i],oldPack=_packed[i];

            float h=old-Math.Min(a*(1-oldPack),Math.Max(0,old-(_base[i]-MaximumRutDepth)));

            _packed[i]=Math.Clamp(oldPack+amount*weight*2,0,1);

            if(h==old&&_packed[i]==oldPack)continue;

            _heights[i]=h;_minimum=Math.Min(_minimum,h);changed++;

        }

        if(changed>0){DeformationRevision++;for(int i=0;i<_chunks.Count;i++){var c=_chunks[i];if(c.X<=maxX+1&&c.X+c.Width>=minX-1&&c.Z<=maxZ+1&&c.Z+c.Depth>=minZ-1)_dirty.Add(i);}}

        return changed;

    }

    public void StampTrack(Vector3 from,Vector3 to,float width,float depth)

    {

        if(!TryGetSand(to,out var sand)||Math.Abs(to.Y-sand.Position.Y)>.35f)return;

        float length=Vector3.Distance(from,to);if(length<.02f||length>20)return;

        int steps=Math.Clamp((int)MathF.Ceiling(length/Math.Max(.12f,width*.3f)),1,128);

        for(int i=1;i<=steps;i++){var point=Vector3.Lerp(from,to,i/(float)steps);if(TryGetSand(point,out var contact)&&Math.Abs(point.Y-contact.Position.Y)<=.35f)CompactSand(contact.Position,Math.Max(width*.5f,_spacing*.8f),depth*length/(steps*.25f));}

    }

    /// <summary>Local soil-contact approximation: load compacts, slip shears loose material into bounded shoulders.</summary>

    public int StampWheelContact(Vector3 from,Vector3 to,float width,float load,float slip,float dt)

    {

        if(UseReferenceSandSurface){if(!float.IsFinite(load)||!float.IsFinite(dt)||load<=0||dt<=0||dt>.25f||!TryGetSand(to,out var contact)||Math.Abs(to.Y-contact.Position.Y)>.35f||Vector3.DistanceSquared(from,to)<.000001f)return 0;int changed=ReferenceSand.Stamp(from,to,Math.Clamp(width*.5f+.05f,.15f,.6f),Math.Clamp(load/3500,.4f,1.2f));if(changed>0)DeformationRevision++;return changed;}



        if(SimulationPatch!=null){if(!float.IsFinite(load)||!float.IsFinite(slip)||!float.IsFinite(dt)||dt<=0||dt>.25f||load<=0)return 0;int changed=SimulationPatch.Stamp(from,to,width,load,slip,dt);if(changed>0)DeformationRevision++;return changed;}

        if(!float.IsFinite(load)||!float.IsFinite(slip)||!float.IsFinite(dt)||load<=0||dt<=0||dt>.25f||!TryGetSand(to,out var sand)||Math.Abs(to.Y-sand.Position.Y)>.35f)return 0;

        float distance=Vector3.Distance(from,to);if(!float.IsFinite(distance)||distance>20)return 0;

        // The active footprint is limited to nearby grid nodes. No desert-wide per-frame solve.

        float radius=Math.Max(Math.Clamp(width,.1f,2)*.5f,_spacing*.8f);

        float pressure=Math.Clamp(load/Math.Max(.08f,width*.35f)/18000, .05f,2);

        float shear=Math.Clamp(slip/8,0,1);float amount=Math.Clamp((distance*.012f+dt*.012f+shear*dt*.06f)*pressure,0,.04f);

        int count=CompactSand(sand.Position,radius,amount);

        if(count==0||shear<.05f)return count;

        if(!Matrix4x4.Invert(SurfaceMatrix,out var inverse))return count;

        var local=Vector3.Transform(sand.Position,inverse);float half=_cells*_spacing*.5f;int n=_cells+1;

        int cx=(int)MathF.Round((local.X+half)/_spacing),cz=(int)MathF.Round((local.Z+half)/_spacing);

        // Sheared sand forms low shoulders, rather than disappearing. Packed roads shed very little.

        float displaced=amount*shear*(1-sand.Compaction)*.25f;

        foreach(var offset in new[]{(-1,0),(1,0),(0,-1),(0,1)}){

            int x=cx+offset.Item1,z=cz+offset.Item2;if(x<0||z<0||x>_cells||z>_cells)continue;int i=z*n+x;

            float h=Math.Min(_base[i]+.06f,_heights[i]+displaced);if(h<=_heights[i])continue;

            _heights[i]=h;_maximum=Math.Max(_maximum,h);count++;

            // One bounded relaxation against neighbours keeps shoulders within the angle of repose.

            float maxDifference=_spacing*.625f;

            foreach(var adjacent in new[]{(-1,0),(1,0),(0,-1),(0,1)}){int ax=x+adjacent.Item1,az=z+adjacent.Item2;if(ax<0||az<0||ax>_cells||az>_cells)continue;int j=az*n+ax;float excess=(_heights[i]-_heights[j])-Math.Max(maxDifference,_base[i]-_base[j]);if(excess<=0)continue;float transfer=Math.Min(excess*.25f,Math.Min(Math.Max(0,_heights[i]-_base[i]),Math.Max(0,_base[j]+.06f-_heights[j])));_heights[i]-=transfer;_heights[j]+=transfer;}

        }

        DeformationRevision++;foreach(var c in _chunks.Select((chunk,index)=>(chunk,index)))if(c.chunk.X<=cx+3&&c.chunk.X+c.chunk.Width>=cx-3&&c.chunk.Z<=cz+3&&c.chunk.Z+c.chunk.Depth>=cz-3)_dirty.Add(c.index);

        return count;

    }

    public string ExportSand()

    {

        EnsureGenerated();using var output=new MemoryStream();using(var gzip=new GZipStream(output,CompressionLevel.Fastest,true))using(var w=new BinaryWriter(gzip)){w.Write(SimulationPatch==null?2:3);w.Write(_cells);w.Write(_spacing);for(int i=0;i<_heights.Length;i++){w.Write(_heights[i]-_base[i]);w.Write(_packed[i]);}if(SimulationPatch!=null)SimulationPatch.Write(w);}

        return Convert.ToBase64String(output.ToArray());

    }

    public void ImportSand(string data)

    {

        EnsureGenerated();if(string.IsNullOrWhiteSpace(data))return;

        if(data.Length>10_000_000)throw new InvalidDataException("Terrain state exceeds the safety limit.");

        using var input=new MemoryStream(Convert.FromBase64String(data));using var gzip=new GZipStream(input,CompressionMode.Decompress);using var reader=new BinaryReader(gzip);

        int version=reader.ReadInt32();if(version is not (1 or 2 or 3)||reader.ReadInt32()!=_cells||Math.Abs(reader.ReadSingle()-_spacing)>.00001f)throw new InvalidDataException("Terrain state does not match the saved grid.");

        var heights=new float[_heights.Length];var packed=new float[_packed.Length];

        for(int i=0;i<heights.Length;i++){float delta=reader.ReadSingle(),p=reader.ReadSingle();if(!float.IsFinite(delta)||!float.IsFinite(p))throw new InvalidDataException("Non-finite sand data.");heights[i]=_base[i]+Math.Clamp(delta,-MaximumRutDepth,version>=2?.06f:0);packed[i]=Math.Max(_initialPacked[i],Math.Clamp(p,0,1));}

        _heights=heights;_packed=packed;SimulationPatch?.Reset();if(version==3){EnableSimulationPatch(Vector3.Transform(Vector3.Zero,SurfaceMatrix));SimulationPatch!.Read(reader);}

        if(gzip.ReadByte()!=-1)throw new InvalidDataException("Unexpected trailing sand data.");

        _heights=heights;_packed=packed;_minimum=_heights.Min();_maximum=_heights.Max();for(int i=0;i<_chunks.Count;i++)_dirty.Add(i);DeformationRevision++;

    }

    protected override void OnStart(){if(UseReferenceSandSurface){_=ReferenceSand;return;}EnsureGenerated();if(AutoLoadSavedSand)LoadSandFromDisk();}

    private string? SandFile=>ProjectRoot==null||AttachedGameObject==null?null:Path.Combine(ProjectRoot,"Saves","sand-"+GameObject.Id.ToString("N")+".json");

    private string SettingsKey=>System.Text.Json.JsonSerializer.Serialize(_settings)+(_hasSculpt?"|"+Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(ExportSculpt()))):"");

    public bool SaveSandToDisk()

    {

        EnsureGenerated();string? file=SandFile;if(file==null){LastSaveMessage="No project save directory";return false;}

        try{Directory.CreateDirectory(Path.GetDirectoryName(file)!);var json=new JsonObject{["format"]=1,["generation"]=SettingsKey,["sand"]=ExportSand()};File.WriteAllText(file+".tmp",json.ToJsonString());File.Move(file+".tmp",file,true);LastSaveMessage="Sand saved / persists after stopping Play";return true;}

        catch(Exception e)when(e is IOException or UnauthorizedAccessException){LastSaveMessage="Save failed: "+e.Message;return false;}

    }

    public bool LoadSandFromDisk()

    {

        EnsureGenerated();string? file=SandFile;if(file==null||!File.Exists(file))return false;

        try{if(new FileInfo(file).Length>12_000_000)throw new InvalidDataException("Save is too large.");var json=JsonNode.Parse(File.ReadAllText(file));

            if(json?["format"]?.GetValue<int>()!=1||json["generation"]?.GetValue<string>()!=SettingsKey){LastSaveMessage="Saved sand belongs to different terrain settings";return false;}

            ImportSand(json["sand"]?.GetValue<string>()??"");LastSaveMessage="Saved sand restored";return true;}

        catch(Exception e)when(e is IOException or UnauthorizedAccessException or FormatException or System.Text.Json.JsonException or InvalidOperationException){LastSaveMessage="Load failed: "+e.Message;return false;}

    }

    protected override void OnRender(RenderContext context)

    {

        if(!context.Has3DCamera)return;

        if(UseReferenceSandSurface){_=ReferenceSand;if(ResetDeformationRequested){ResetDeformationRequested=false;ReferenceSand.ResetTracks();}return;}

        if(ReloadHeightmapRequested){ReloadHeightmapRequested=false;Regenerate();}

        if(HeightmapPath.Length>0&&Environment.TickCount64>=_nextMapPoll)

        {

            _nextMapPoll=Environment.TickCount64+1000;

            try

            {

                string requested=Path.GetFullPath(Path.IsPathRooted(HeightmapPath)?HeightmapPath:Path.Combine(ProjectRoot??Environment.CurrentDirectory,HeightmapPath));

                if(File.Exists(requested)&&(requested!=_loadedMapPath||File.GetLastWriteTimeUtc(requested)!=_mapWriteTime))Regenerate();

            }catch(ArgumentException){ /* Inspector reports the invalid path; retain the previous landscape. */ }

        }

        if(ResetDeformationRequested){ResetDeformationRequested=false;ResetSand();}EnsureTextures();if(_dirty.Count>0)ClearPatchCuts();FlushMeshChanges();if(SimulationPatch!=null&&_patchCutRevision!=SimulationPatch.WindowRevision){ClearPatchCuts();_patchCutRevision=SimulationPatch.WindowRevision;}_material.BaseColor=SandColor;

        var camera=context.RenderWorld.View?.CameraPosition??context.Camera3D?.Transform.WorldPosition??Vector3.Zero;

        float shadowDistance=Math.Clamp(Finite(ReceiveShadowsDistance,36),0,200);

        foreach(var chunk in _chunks)

        {

            var bounds=chunk.Mesh.LocalBounds.Transform(SurfaceMatrix);

            var closest=Vector3.Clamp(camera,bounds.Minimum,bounds.Maximum);

            bool receive=shadowDistance>0&&Vector3.DistanceSquared(camera,closest)<=shadowDistance*shadowDistance;

            float distance=Vector3.Distance(camera,closest);

            int step=!UseDistanceLod||distance<Math.Clamp(Finite(LodNearDistance,100),32,500)?1:distance<Math.Max(LodNearDistance+32,Finite(LodFarDistance,240))?2:4;

            while(step>1&&(chunk.Width%step!=0||chunk.Depth%step!=0))step/=2;

            Mesh mesh=step==1?chunk.Mesh:step==2?chunk.MediumMesh??=CreateLod(chunk,2):chunk.FarMesh??=CreateLod(chunk,4);

            if(SimulationPatch!=null){var min=SimulationPatch.GridMinimum;float half=_cells*_spacing*.5f;float x=chunk.X*_spacing-half,z=chunk.Z*_spacing-half;if(x<min.X+32&&x+chunk.Width*_spacing>min.X&&z<min.Y+32&&z+chunk.Depth*_spacing>min.Y)mesh=CutPatch(chunk);}

            context.RenderWorld.Submit(mesh,_material,SurfaceMatrix,castShadows:CastShadows,receiveShadows:receive);

        }

        SimulationPatch?.RenderPatch(context,_material);

    }

    protected override void OnStop(){SimulationPatch?.Reset();ClearPatchCuts();DisposeChunks();_settings=null;_sandTexture?.Dispose();_normalTexture?.Dispose();_roughTexture?.Dispose();_sandTexture=_normalTexture=_roughTexture=null;_textureKey="";}

    protected override void OnDestroy(){ClearPatchCuts();DisposeChunks();_sandTexture?.Dispose();_normalTexture?.Dispose();_roughTexture?.Dispose();}

    private void EnsureTextures()

    {

        _material.SandSurface=SandShadingEnabled;_material.UvTiling=new Vector2(8/Math.Clamp(Finite(TextureWorldSize,8),.5f,64));_material.NormalStrength=Math.Clamp(Finite(NormalStrength,.65f),0,2);_material.Roughness=Math.Clamp(Finite(SandRoughness,1),.05f,1);

        string key=SandAlbedoPath+"|"+SandNormalPath+"|"+SandRoughnessPath;if(_sandTexture!=null&&key==_textureKey)return;

        _sandTexture?.Dispose();_normalTexture?.Dispose();_roughTexture?.Dispose();_sandTexture=_normalTexture=_roughTexture=null;_textureKey=key;

        string FilePath(string p)=>Path.IsPathRooted(p)?p:Path.Combine(ProjectRoot??Environment.CurrentDirectory,p);

        if(SandAlbedoPath.Length>0&&File.Exists(FilePath(SandAlbedoPath))){_sandTexture=new Texture2D(FilePath(SandAlbedoPath),TextureFilter.Linear);if(SandNormalPath.Length>0&&File.Exists(FilePath(SandNormalPath)))_normalTexture=new Texture2D(FilePath(SandNormalPath),TextureFilter.Linear);if(SandRoughnessPath.Length>0&&File.Exists(FilePath(SandRoughnessPath)))_roughTexture=new Texture2D(FilePath(SandRoughnessPath),TextureFilter.Linear);_sandTexture.EnableWorldSampling();_normalTexture?.EnableWorldSampling();_roughTexture?.EnableWorldSampling();_material.SandSurface=SandShadingEnabled;_material.MainTexture=_sandTexture;_material.NormalTexture=_normalTexture;_material.RoughnessTexture=_roughTexture;_material.DecodeColorTexturesSrgb=true;return;}

        _material.RoughnessTexture=null;_material.DecodeColorTexturesSrgb=false;

        const int n=128;var color=new byte[n*n*4];var normal=new byte[color.Length];

        for(int z=0;z<n;z++)for(int x=0;x<n;x++)

        {

            int i=(z*n+x)*4;float noise=Hash(902,x,z),wave=MathF.Sin(x*MathF.Tau*12/n+.2f*MathF.Sin(z*MathF.Tau*2/n));

            byte shade=(byte)(225+noise*20+wave*4);color[i]=shade;color[i+1]=shade;color[i+2]=shade;color[i+3]=255;

            var direction=Vector3.Normalize(new Vector3(MathF.Cos(x*MathF.Tau*12/n+.2f*MathF.Sin(z*MathF.Tau*2/n))*.22f,noise*.08f-.04f,1));

            normal[i]=(byte)((direction.X*.5f+.5f)*255);normal[i+1]=(byte)((direction.Y*.5f+.5f)*255);normal[i+2]=(byte)((direction.Z*.5f+.5f)*255);normal[i+3]=255;

        }

        _sandTexture=Texture2D.FromPixels(n,n,color);_normalTexture=Texture2D.FromPixels(n,n,normal);

        _material.SandSurface=SandShadingEnabled;_material.MainTexture=_sandTexture;_material.NormalTexture=_normalTexture;_material.NormalStrength=Math.Clamp(NormalStrength,0,2);

    }

    // Far rendering uses fewer vertices; collision and wheel ruts always retain the full grid.

    // Two-metre skirts hide tiny boundary gaps between neighbouring resolutions.

    private static Mesh CreateLod(Chunk c,int step)

    {

        int w=c.Width/step,d=c.Depth/step;var vertices=new List<float>();var indices=new List<uint>();

        uint Add(int x,int z,float drop=0){int i=(z*(c.Width+1)+x)*8;uint index=(uint)(vertices.Count/8);for(int j=0;j<8;j++)vertices.Add(c.Vertices[i+j]-(j==1?drop:0));return index;}

        for(int z=0;z<=d;z++)for(int x=0;x<=w;x++)Add(x*step,z*step);

        for(int z=0;z<d;z++)for(int x=0;x<w;x++){uint a=(uint)(z*(w+1)+x),b=a+1,n=a+(uint)w+1,e=n+1;indices.AddRange(new[]{a,n,b,b,n,e});}

        void Edge(int ax,int az,int bx,int bz){uint a=Add(ax,az),b=Add(bx,bz),n=Add(ax,az,2),e=Add(bx,bz,2);indices.AddRange(new[]{a,n,b,b,n,e});}

        for(int x=0;x<w;x++){Edge(x*step,0,(x+1)*step,0);Edge(x*step,c.Depth,(x+1)*step,c.Depth);}

        for(int z=0;z<d;z++){Edge(0,z*step,0,(z+1)*step);Edge(c.Width,z*step,c.Width,(z+1)*step);}

        return new Mesh(vertices.ToArray(),indices.ToArray());

    }

    private void DisposeChunks(){foreach(var c in _chunks){c.Mesh.Dispose();c.InvalidateLod();}_chunks.Clear();_dirty.Clear();}

    private static float Smooth(float t)=>t*t*(3-2*t);

    private static float Hash(int seed,int x,int z){uint h=unchecked((uint)(seed+x*374761393+z*668265263));h=(h^(h>>13))*1274126177;return (h^(h>>16))/(float)uint.MaxValue;}

    private static float Noise(float x,float z,int seed){int ix=(int)MathF.Floor(x),iz=(int)MathF.Floor(z);float u=Smooth(x-ix),v=Smooth(z-iz);return (Hash(seed,ix,iz)*(1-u)+Hash(seed,ix+1,iz)*u)*(1-v)+(Hash(seed,ix,iz+1)*(1-u)+Hash(seed,ix+1,iz+1)*u)*v;}

}
