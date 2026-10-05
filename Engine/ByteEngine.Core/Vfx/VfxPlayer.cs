using System.Numerics;
using ByteEngine.Core.Animation;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;

namespace ByteEngine.Core.Vfx;

/// <summary>Reusable effect playback. One draw per active layer, never one object per particle.</summary>
public sealed class VfxPlayer : Component
{
    public AssetReference Effect { get; set; } = AssetReference.Empty;
    public VfxPreset Preset { get; set; } = VfxPreset.Sparks;
    public bool PlayOnStart { get; set; } = true;
    public int Seed { get; set; } = 1;
    private float _size=1,_intensity=1,_speed=1,_distance=150;
    public float Size { get=>_size; set=>_size=VfxEffect.Safe(value,1,.01f,100); }
    public float Intensity { get=>_intensity; set=>_intensity=VfxEffect.Safe(value,1,0,8); }
    public float PlaybackSpeed { get=>_speed; set=>_speed=VfxEffect.Safe(value,1,0,4); }
    public float ViewDistance { get=>_distance; set=>_distance=VfxEffect.Safe(value,150,1,10000); }
    public bool Paused { get; set; }
    public bool DestroyWhenFinished { get; set; }
    public bool DistanceQuality { get; set; } = true;
    private float _qualityDistance=40;
    public float QualityDistance { get=>_qualityDistance; set=>_qualityDistance=VfxEffect.Safe(value,40,1,10000); }
    public int RenderedParticles { get; private set; }
    internal Action<VfxPlayer>? ReturnToPool;
    internal ByteEngine.Core.Scene.Scene? AttachedScene() => AttachedGameObject?.Scene;
    private Vector3? _beamTarget;
    public void SetBeamTarget(Vector3 worldPosition) => _beamTarget=VfxEffect.Finite(worldPosition,Transform.WorldPosition);
    public void ClearBeamTarget() => _beamTarget=null;
    public bool IsPlaying => _simulation?.IsPlaying==true;
    public int ActiveParticles => _simulation?.ActiveCount ?? 0;
    public int ParticleCapacity => _simulation?.Capacity ?? 0;
    public int DroppedParticles => _simulation?.DroppedParticles ?? 0;
    public string Status { get; private set; } = "Ready. Pick a preset or a VFX asset.";
    private VfxSimulation? _simulation;
    private AssetReference _loaded=AssetReference.Empty;
    private VfxPreset _loadedPreset;
    private int _loadedSeed;
    private int _revision=-1;
    private bool _startedPlayback;
    public bool HasPlayed => _startedPlayback;
    private Batch[] _batches=Array.Empty<Batch>();
    private Matrix4x4 Emitter => Matrix4x4.CreateScale(Size)*Transform.WorldMatrix;

    private sealed class Batch : IDisposable
    {
        public readonly float[] Vertices, Depths;
        public readonly int[] Order;
        public readonly Mesh Mesh;
        public readonly Material Material=new() { Shading=MaterialShadingMode.Unlit, CullMode=CullMode3D.None, DepthWriteMode=DepthWriteMode3D.Disabled };
        public VfxAtlas.Lease? Atlas;
        public int PreviousCount;
        public int Frames=1;
        public Batch(int capacity)
        {
            Vertices=new float[capacity*32]; Depths=new float[capacity]; Order=new int[capacity];
            var indices=new uint[capacity*6];
            for(int i=0;i<capacity;i++) { uint v=(uint)i*4; int n=i*6; indices[n]=v; indices[n+1]=v+1; indices[n+2]=v+2; indices[n+3]=v; indices[n+4]=v+2; indices[n+5]=v+3; }
            Mesh=new Mesh(Vertices,indices,dynamicVertices:true);
        }
        public void Dispose() { Mesh.Dispose(); Atlas?.Dispose(); }
    }
    public void SetDefinition(VfxEffect definition)
    {
        Release(); _beamTarget=null; _simulation=new(definition,Seed); _loaded=Effect; _loadedPreset=Preset; _loadedSeed=Seed;
        _batches=new Batch[_simulation.Layers.Length];
        for(int i=0;i<_batches.Length;i++) _batches[i]=new(_simulation.Layers[i].Particles.Length);
        Status="Ready";
    }
    private bool Ensure()
    {
        AnimationRuntimeAssets.TryGet(out var manager);
        int revision=manager?.VfxRevision??-1;
        if(_simulation!=null && _loaded==Effect && _loadedPreset==Preset && _loadedSeed==Seed && (Effect.IsEmpty || _revision==revision)) return true;
        try
        {
            bool replay=IsPlaying;
            if(Effect.IsEmpty) SetDefinition(VfxPresets.Create(Preset));
            else if(manager!=null) SetDefinition(manager.LoadVfxEffect(Effect));
            else { Status="VFX project assets are not available."; return false; }
            _revision=revision;
            if(replay) _simulation!.Play(Emitter,true,Intensity);
            return true;
        }
        catch(Exception e) { Status="VFX: "+e.Message; return false; }
    }
    public void Play(bool restart=true)
    { if(Ensure()) { _startedPlayback=true; Paused=false; _simulation!.Play(Emitter,restart,Intensity); } }
    public void Stop(bool clear=false) => _simulation?.Stop(clear);
    public void EmitBurst(int count=30)
    { if(Ensure()) { _startedPlayback=true; _simulation!.Burst(Emitter,count,Intensity); } }
    public void Advance(float dt)
    {
        if(Paused || !Ensure()) return;
        _simulation!.Advance(dt*PlaybackSpeed,Emitter,Intensity);
        if(DestroyWhenFinished && HasPlayed && !IsPlaying && AttachedGameObject?.Scene is {} scene)
        {
            if(ReturnToPool!=null) ReturnToPool(this);
            else scene.DestroyGameObject(GameObject);
        }
    }
    protected override void OnStart() { if(PlayOnStart) Play(); }
    protected override void OnUpdate() => Advance((float)Time.DeltaTime);
    protected override void OnStop() { Stop(true); if(ReturnToPool!=null) Release(); }
    protected override void OnDestroy() => Release();
    private void Release()
    { foreach(var batch in _batches) batch.Dispose(); _batches=Array.Empty<Batch>(); _simulation=null; }

    protected override void OnRender(RenderContext context)
    {
        RenderedParticles=0;
        if(!context.Has3DCamera || _simulation==null || ActiveParticles==0) return;
        Matrix4x4.Invert(context.GetViewMatrix3D(),out var inverse);
        Vector3 camera=inverse.Translation, right=Vector3.Normalize(new Vector3(inverse.M11,inverse.M12,inverse.M13)), up=Vector3.Normalize(new Vector3(inverse.M21,inverse.M22,inverse.M23));
        Matrix4x4 emitter=Emitter;
        var worldScale=Vector3.Abs(Transform.WorldScale);
        float objectScale=Math.Max(worldScale.X,Math.Max(worldScale.Y,worldScale.Z));
        float distance=Vector3.Distance(camera,Transform.WorldPosition);
        if(distance>ViewDistance) return;
        int stride=DistanceQuality ? Math.Clamp((int)(distance/QualityDistance),1,4) : 1;
        for(int l=0;l<_batches.Length;l++)
        {
            var layer=_simulation.Layers[l]; var s=layer.Settings; var b=_batches[l];
            if(layer.Count==0 || !s.Enabled) continue;
            if(b.Atlas==null)
            {
                Texture2D? texture=null;
                try
                {
                    if(!s.Texture.IsEmpty && AnimationRuntimeAssets.TryGet(out var assets) && assets!=null) texture=assets.LoadTexture(s.Texture);
                    b.Atlas=VfxAtlas.Acquire(s,texture);
                    b.Frames=texture==null ? 1 : s.FlipbookColumns*s.FlipbookRows;
                }
                catch(Exception e) { Status="VFX sprite fallback: "+e.Message; b.Atlas=VfxAtlas.Acquire(s,null); b.Frames=1; }
                b.Material.MainTexture=b.Atlas.Texture;
                b.Material.BlendMode=s.Additive ? BlendMode3D.Additive : BlendMode3D.AlphaBlend;
            }
            int drawCount=0;
            for(int i=0;i<layer.Count;i++)
            {
                // Stable thinning; never remove ribbon segments or beams.
                if(s.RenderMode is not (VfxRenderMode.Trail or VfxRenderMode.Beam) && layer.Particles[i].Sequence%stride!=0) continue;
                b.Order[drawCount]=i;
                var position=layer.Particles[i].Position;
                if(s.LocalSpace) position=Vector3.Transform(position,emitter);
                b.Depths[drawCount++]=-Vector3.DistanceSquared(position,camera);
            }
            if(drawCount==0) continue;
            if(!s.Additive && s.RenderMode!=VfxRenderMode.Trail) Array.Sort(b.Depths,b.Order,0,drawCount);
            Vector3 minimum=new(float.PositiveInfinity), maximum=new(float.NegativeInfinity);
            for(int i=0;i<drawCount;i++)
            {
                var p=layer.Particles[b.Order[i]];
                float age=Math.Clamp(p.Age/p.Lifetime,0,1), size=(s.StartSize+(s.EndSize-s.StartSize)*age)*p.SizeFactor*Size*objectScale;
                size*=s.SizeOverLife?.Evaluate(age)??1;
                Vector3 center=p.Position, previous=p.PreviousPosition, velocity=p.Velocity;
                if(s.LocalSpace) { center=Vector3.Transform(center,emitter); previous=Vector3.Transform(previous,emitter); velocity=Vector3.TransformNormal(velocity,emitter); }
                Vector3 a=right*size*.5f,c=up*size*.5f;
                if(s.RenderMode==VfxRenderMode.Billboard)
                { float cos=MathF.Cos(p.Rotation),sin=MathF.Sin(p.Rotation); a=(right*cos+up*sin)*size*.5f; c=(-right*sin+up*cos)*size*.5f; }
                else if(s.RenderMode==VfxRenderMode.Stretched && velocity.LengthSquared()>.00001f)
                { c=Vector3.Normalize(velocity)*(size+velocity.Length()*s.Stretch)*.5f; a=Side(c,center-camera,right)*size*.5f; }
                else if(s.RenderMode is VfxRenderMode.Trail or VfxRenderMode.Beam)
                {
                    Vector3 end=s.RenderMode==VfxRenderMode.Beam ? _beamTarget??Vector3.Transform(s.BeamEnd,emitter) : previous;
                    c=(end-center)*.5f; center=(center+end)*.5f; a=Side(c,center-camera,right)*size*.5f;
                }
                Vector3 extent=Vector3.Abs(a)+Vector3.Abs(c);
                minimum=Vector3.Min(minimum,center-extent); maximum=Vector3.Max(maximum,center+extent);
                int frames=b.Frames;
                int frame=Math.Min(frames-1,(int)(age*frames));
                int row=Math.Min(VfxAtlas.Ages-1,(int)(age*(VfxAtlas.Ages-1)));
                float u0=(frame+.5f/VfxAtlas.Tile)/frames,u1=(frame+1-.5f/VfxAtlas.Tile)/frames;
                float v0=(row+.5f/VfxAtlas.Tile)/VfxAtlas.Ages,v1=(row+1-.5f/VfxAtlas.Tile)/VfxAtlas.Ages;
                Vertex(b.Vertices,i*32,center-a-c,u0,v0); Vertex(b.Vertices,i*32+8,center+a-c,u1,v0);
                Vertex(b.Vertices,i*32+16,center+a+c,u1,v1); Vertex(b.Vertices,i*32+24,center-a+c,u0,v1);
            }
            if(b.PreviousCount>drawCount) Array.Clear(b.Vertices,drawCount*32,(b.PreviousCount-drawCount)*32);
            b.PreviousCount=drawCount; b.Mesh.UpdateVertices(b.Vertices,updateBounds:false,knownBounds:new BoundingBox3D(minimum,maximum));
            b.Mesh.SetDrawIndexCount(drawCount*6); RenderedParticles+=drawCount;
            context.RenderWorld.Submit(b.Mesh,b.Material,Matrix4x4.Identity,frustumCulling:true,castShadows:false,receiveShadows:false);
        }
    }
    private static Vector3 Side(Vector3 direction,Vector3 view,Vector3 fallback)
    { var side=Vector3.Cross(direction,view); return side.LengthSquared()<.000001f ? fallback : Vector3.Normalize(side); }
    private static void Vertex(float[] v,int n,Vector3 p,float u,float w)
    { v[n]=p.X; v[n+1]=p.Y; v[n+2]=p.Z; v[n+3]=0; v[n+4]=1; v[n+5]=0; v[n+6]=u; v[n+7]=w; }
}
