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
    private Vector4 _tint=Vector4.One;
    private float _brightness=1;
    public Vector4 Tint { get=>_tint; set=>_tint=new(VfxEffect.Safe(value.X,1,0,1),VfxEffect.Safe(value.Y,1,0,1),VfxEffect.Safe(value.Z,1,0,1),VfxEffect.Safe(value.W,1,0,1)); }
    public float Brightness { get=>_brightness; set=>_brightness=VfxEffect.Safe(value,1,0,16); }
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
    public bool IsEmitting => _simulation?.IsEmitting==true;
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
        public readonly float[]? Template;
        public readonly int VerticesPerParticle = 4, IndicesPerParticle = 6;
        public Batch(int capacity, VfxLayer settings)
        {
            uint[] templateIndices = [0, 1, 2, 0, 2, 3];
            if (settings.RenderMode == VfxRenderMode.Mesh)
            {
                using var primitive = PrimitiveMesh.Create(settings.MeshShape);
                Template = primitive.VertexData.ToArray();
                templateIndices = primitive.IndexData.ToArray();
                // Legacy primitives were normally double-sided. Normalize winding against their
                // authored normals before using back-face culling on a solid particle core.
                for (int n = 0; n < templateIndices.Length; n += 3)
                {
                    int a = (int)templateIndices[n] * 8, b = (int)templateIndices[n + 1] * 8, c = (int)templateIndices[n + 2] * 8;
                    Vector3 Position(int at) => new(Template[at], Template[at + 1], Template[at + 2]);
                    Vector3 Normal(int at) => new(Template[at + 3], Template[at + 4], Template[at + 5]);
                    if (Vector3.Dot(Vector3.Cross(Position(b) - Position(a), Position(c) - Position(a)), Normal(a) + Normal(b) + Normal(c)) < 0)
                        (templateIndices[n + 1], templateIndices[n + 2]) = (templateIndices[n + 2], templateIndices[n + 1]);
                }
                VerticesPerParticle = Template.Length / 8;
                IndicesPerParticle = templateIndices.Length;
                Material.Shading = MaterialShadingMode.Lit;
                Material.CullMode = CullMode3D.Back;
            }
            Vertices=new float[capacity*VerticesPerParticle*8]; Depths=new float[capacity]; Order=new int[capacity];
            var indices=new uint[capacity*IndicesPerParticle];
            for (int i = 0; i < capacity; i++)
                for (int n = 0; n < IndicesPerParticle; n++) indices[i * IndicesPerParticle + n] = templateIndices[n] + (uint)(i * VerticesPerParticle);
            Mesh=new Mesh(Vertices,indices,dynamicVertices:true);
        }
        public void Dispose() { Mesh.Dispose(); Atlas?.Dispose(); }
    }
    public void SetDefinition(VfxEffect definition)
    {
        Release(); _beamTarget=null; _simulation=new(definition,Seed); _loaded=Effect; _loadedPreset=Preset; _loadedSeed=Seed;
        _batches=new Batch[_simulation.Layers.Length];
        for(int i=0;i<_batches.Length;i++) _batches[i]=new(_simulation.Layers[i].Particles.Length, _simulation.Layers[i].Settings);
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
    /// <summary>Builds GPU batches and sprite atlases before combat. Retain this player to reuse them.</summary>
    public void Prewarm()
    {
        if(!Ensure())return;
        for(int i=0;i<_batches.Length;i++)
        {
            var b=_batches[i];var s=_simulation!.Layers[i].Settings;if(!s.Enabled||b.Atlas!=null)continue;
            Texture2D? texture=null;
            if(!s.Texture.IsEmpty&&AnimationRuntimeAssets.TryGet(out var assets)&&assets!=null)texture=assets.LoadTexture(s.Texture);
            b.Atlas=VfxAtlas.Acquire(s,texture);b.Material.MainTexture=b.Atlas.Texture;b.Material.BlendMode=s.Additive?BlendMode3D.Additive:BlendMode3D.AlphaBlend;
        }
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
            if (ReturnToPool != null && GameObject.GetComponent<ByteEngine.Core.Audio.AudioSource3D>() is { IsPlaying: true }) return;
            if(ReturnToPool!=null) ReturnToPool(this);
            else scene.DestroyGameObject(GameObject);
        }
    }
    protected override void OnStart() { if(PlayOnStart) Play(); }
    protected override void OnUpdate() => Advance((float)Time.DeltaTime);
    protected override void OnStop() { Stop(true); Release(); }
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
                }
                catch(Exception e) { Status="VFX sprite fallback: "+e.Message; b.Atlas=VfxAtlas.Acquire(s,null); }
                b.Material.MainTexture=b.Atlas.Texture;
                b.Material.BlendMode=s.Additive ? BlendMode3D.Additive : BlendMode3D.AlphaBlend;
            }
            b.Material.BaseColor=new Vector4(new Vector3(Tint.X,Tint.Y,Tint.Z)*(Brightness*s.Brightness),Tint.W);
            if (s.RenderMode == VfxRenderMode.Mesh)
            {
                b.Material.EmissionEnabled = s.MeshEmission > 0;
                b.Material.EmissionColor = new Vector3(Tint.X, Tint.Y, Tint.Z);
                b.Material.EmissionIntensity = s.MeshEmission * Brightness * s.Brightness;
                b.Material.EmissionTexture = b.Material.MainTexture;
                b.Material.VfxSurface = new(s.MeshSurfaceFlow, _simulation.Elapsed, b.Atlas!.Layout.Columns, b.Atlas.Layout.Rows);
                b.Material.Roughness = .55f;
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
                if (s.RenderMode == VfxRenderMode.Mesh)
                {
                    var bounds = FillMesh(b, i, p, s, center, size, age, s.LocalSpace ? Transform.WorldRotation : p.Orientation);
                    minimum = Vector3.Min(minimum, bounds.Minimum);
                    maximum = Vector3.Max(maximum, bounds.Maximum);
                    continue;
                }
                Vector3 a=right*size*.5f,c=up*size*.5f;
                if(s.RenderMode is VfxRenderMode.Billboard or VfxRenderMode.Horizontal or VfxRenderMode.EmitterPlane or VfxRenderMode.SurfacePlane)
                {
                    var basisRight=right;var basisUp=up;
                    if(s.RenderMode==VfxRenderMode.Horizontal){basisRight=Vector3.UnitX;basisUp=-Vector3.UnitZ;}
                    else if(s.RenderMode==VfxRenderMode.EmitterPlane){basisRight=Vector3.Normalize(Vector3.TransformNormal(Vector3.UnitX,emitter));basisUp=Vector3.Normalize(Vector3.TransformNormal(Vector3.UnitY,emitter));}
                    else if (s.RenderMode == VfxRenderMode.SurfacePlane)
                    {
                        var orientation = s.LocalSpace ? Transform.WorldRotation : p.Orientation;
                        basisRight = Vector3.Transform(Vector3.UnitX, orientation);
                        basisUp = -Vector3.Transform(Vector3.UnitZ, orientation);
                    }
                    float cos=MathF.Cos(p.Rotation),sin=MathF.Sin(p.Rotation);a=(basisRight*cos+basisUp*sin)*size*.5f;c=(-basisRight*sin+basisUp*cos)*size*.5f;
                }
                else if (s.RenderMode == VfxRenderMode.TravelAligned)
                {
                    // The source image points along +U. Keep that axis in the actual
                    // flight direction, facing the camera around that axis only.
                    Vector3 travel = velocity.LengthSquared() > .00001f ? velocity :
                        Vector3.TransformNormal(-Vector3.UnitZ, emitter);
                    var axes = ResolveTravelAxes(travel, camera - center, up, right);
                    a = axes.Right * size * .5f;
                    c = axes.Up * size * .5f;
                }
                else if(s.RenderMode==VfxRenderMode.Stretched && velocity.LengthSquared()>.00001f)
                { c=Vector3.Normalize(velocity)*(size+velocity.Length()*s.Stretch)*.5f; a=Side(c,center-camera,right)*size*.5f; }
                else if(s.RenderMode is VfxRenderMode.Trail or VfxRenderMode.Beam)
                {
                    Vector3 end=s.RenderMode==VfxRenderMode.Beam ? _beamTarget??Vector3.Transform(s.BeamEnd,emitter) : previous;
                    c=(end-center)*.5f; center=(center+end)*.5f; a=Side(c,center-camera,right)*size*.5f;
                }
                Vector3 extent=Vector3.Abs(a)+Vector3.Abs(c);
                minimum=Vector3.Min(minimum,center-extent); maximum=Vector3.Max(maximum,center+extent);
                var uv=b.Atlas!.Layout.Uv(age);
                float u0=uv.X,u1=uv.Z,v0=uv.Y,v1=uv.W;
                Vertex(b.Vertices,i*32,center-a-c,u0,v0); Vertex(b.Vertices,i*32+8,center+a-c,u1,v0);
                Vertex(b.Vertices,i*32+16,center+a+c,u1,v1); Vertex(b.Vertices,i*32+24,center-a+c,u0,v1);
            }
            if(b.PreviousCount>drawCount) Array.Clear(b.Vertices,drawCount*b.VerticesPerParticle*8,(b.PreviousCount-drawCount)*b.VerticesPerParticle*8);
            b.PreviousCount=drawCount; b.Mesh.UpdateVertices(b.Vertices,updateBounds:false,knownBounds:new BoundingBox3D(minimum,maximum));
            b.Mesh.SetDrawIndexCount(drawCount*b.IndicesPerParticle); RenderedParticles+=drawCount;
            context.RenderWorld.Submit(b.Mesh,b.Material,Matrix4x4.Identity,RenderQueue3D.Transparent,frustumCulling:true,castShadows:false,receiveShadows:false);
        }
    }
    private static BoundingBox3D FillMesh(Batch batch, int index, VfxParticle particle, VfxLayer layer, Vector3 center, float size, float age, Quaternion rotation)
    {
        Vector3 minimum = new(float.PositiveInfinity), maximum = new(float.NegativeInfinity);
        var uv = batch.Atlas!.Layout.Uv(age);
        float pulse = 1 + layer.MeshPulse * MathF.Sin(particle.Age * 12 + particle.Sequence);
        var spin = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, particle.Rotation);
        rotation = Quaternion.Normalize(spin * rotation);
        for (int vertex = 0; vertex < batch.VerticesPerParticle; vertex++)
        {
            int source = vertex * 8, at = (index * batch.VerticesPerParticle + vertex) * 8;
            var template = batch.Template!;
            var local = new Vector3(template[source], template[source + 1], template[source + 2]);
            float noise = MathF.Sin(local.X * 11 + particle.Age * 8) * MathF.Sin(local.Y * 13 - particle.Age * 7) * MathF.Sin(local.Z * 17 + particle.Age * 6);
            Vector3 point = center + Vector3.Transform(local * layer.MeshScale * (size * pulse * (1 + noise * layer.MeshDeformation)), rotation);
            Vector3 normal = Vector3.Normalize(Vector3.Transform(new Vector3(template[source + 3], template[source + 4], template[source + 5]) / layer.MeshScale, rotation));
            minimum = Vector3.Min(minimum, point); maximum = Vector3.Max(maximum, point);
            batch.Vertices[at] = point.X; batch.Vertices[at + 1] = point.Y; batch.Vertices[at + 2] = point.Z;
            batch.Vertices[at + 3] = normal.X; batch.Vertices[at + 4] = normal.Y; batch.Vertices[at + 5] = normal.Z;
            batch.Vertices[at + 6] = uv.X + template[source + 6] * (uv.Z - uv.X);
            batch.Vertices[at + 7] = uv.Y + template[source + 7] * (uv.W - uv.Y);
        }
        return new(minimum, maximum);
    }

    internal bool TryGetEffectLight(out RenderPointLight3D light)
    {
        light = default;
        if (!Enabled || AttachedGameObject?.ActiveInHierarchy != true || _simulation == null || !IsPlaying || ActiveParticles == 0) return false;
        var effect = _simulation.Effect;
        if (effect.Loop && !_simulation.IsEmitting) return false;
        if (!effect.Light.Enabled) return false;
        float time = effect.Loop ? _simulation.Elapsed % effect.Duration : _simulation.Elapsed;
        float intensity = effect.Light.Intensity * Intensity * effect.Light.IntensityOverLife.Evaluate(Math.Clamp(time / effect.Duration, 0, 1));
        if (intensity <= .001f) return false;
        light = new(Transform.WorldPosition, effect.Light.Color, intensity, effect.Light.Range, false, 0, 0, 0, 0);
        return true;
    }
    internal static (Vector3 Right, Vector3 Up) ResolveTravelAxes(
        Vector3 travel, Vector3 toCamera, Vector3 cameraUp, Vector3 cameraRight)
    {
        Vector3 axis = travel.LengthSquared() > .000001f ? Vector3.Normalize(travel) : -Vector3.UnitZ;
        Vector3 perpendicular = Vector3.Cross(toCamera, axis);
        if (perpendicular.LengthSquared() < .000001f)
            perpendicular = cameraUp - axis * Vector3.Dot(cameraUp, axis);
        if (perpendicular.LengthSquared() < .000001f)
            perpendicular = cameraRight - axis * Vector3.Dot(cameraRight, axis);
        if (perpendicular.LengthSquared() < .000001f)
            perpendicular = Vector3.Cross(axis, Math.Abs(axis.Y) < .9f ? Vector3.UnitY : Vector3.UnitX);
        perpendicular = Vector3.Normalize(perpendicular);
        if (Vector3.Dot(perpendicular, cameraUp) < 0) perpendicular = -perpendicular;
        return (axis, perpendicular);
    }

    private static Vector3 Side(Vector3 direction,Vector3 view,Vector3 fallback)
    { var side=Vector3.Cross(direction,view); return side.LengthSquared()<.000001f ? fallback : Vector3.Normalize(side); }
    private static void Vertex(float[] v,int n,Vector3 p,float u,float w)
    { v[n]=p.X; v[n+1]=p.Y; v[n+2]=p.Z; v[n+3]=0; v[n+4]=1; v[n+5]=0; v[n+6]=u; v[n+7]=w; }
}
