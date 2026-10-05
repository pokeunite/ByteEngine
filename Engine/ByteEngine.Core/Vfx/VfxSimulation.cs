using System.Numerics;

namespace ByteEngine.Core.Vfx;

public struct VfxParticle
{
    public Vector3 Position, PreviousPosition, Velocity;
    public float Age, Lifetime, SizeFactor, Rotation;
    public long Sequence;
}

/// <summary>Dense bounded pools. Simulation contains no scene objects, GL calls, or frame allocations.</summary>
public sealed class VfxSimulation
{
    public sealed class Layer
    {
        public VfxLayer Settings { get; }
        public VfxParticle[] Particles { get; }
        public int Count { get; internal set; }
        internal float EmissionRemainder;
        internal bool BurstEmitted;
        internal Vector3 LastSpawn;
        internal bool HasLastSpawn;
        internal Layer(VfxLayer settings, int capacity) { Settings=settings; Particles=new VfxParticle[capacity]; }
    }
    public VfxEffect Effect { get; }
    public Layer[] Layers { get; }
    public int ActiveCount { get; private set; }
    public int Capacity { get; }
    public int DroppedParticles { get; private set; }
    public bool IsEmitting { get; private set; }
    public bool IsPlaying => IsEmitting || ActiveCount > 0;
    public float Elapsed { get; private set; }
    private Random _random;
    private readonly int _seed;
    private float _accumulator;
    private long _sequence;
    public VfxSimulation(VfxEffect definition, int seed=1)
    {
        // Own normalized settings: runtime never mutates the author's shared definition.
        Effect=VfxEffectSerializer.Clone(definition); Effect.Validate();
        _seed=seed; _random=new(seed);
        Layers=new Layer[Effect.Layers.Count];
        int remaining=Effect.ParticleBudget;
        for(int i=0;i<Layers.Length;i++)
        {
            int capacity=Math.Min(Effect.Layers[i].MaxParticles,remaining);
            Layers[i]=new(Effect.Layers[i],capacity); remaining-=capacity; Capacity+=capacity;
        }
    }
    public void Play(Matrix4x4 emitter, bool restart=true, float intensity=1)
    {
        if(restart) Clear();
        IsEmitting=true;
        foreach(var layer in Layers)
            if(layer.Settings.Delay<=0 && !layer.BurstEmitted) EmitInitial(layer,emitter,intensity);
    }
    public void Stop(bool clear=false) { IsEmitting=false; if(clear) Clear(); }
    public void Clear()
    {
        IsEmitting=false; ActiveCount=0; Elapsed=0; _accumulator=0; _sequence=0; DroppedParticles=0; _random=new(_seed);
        foreach(var layer in Layers) { layer.Count=0; layer.EmissionRemainder=0; layer.BurstEmitted=false; layer.HasLastSpawn=false; }
    }
    public void Burst(Matrix4x4 emitter, int count, float intensity=1)
    {
        count=Math.Clamp(count,0,8192);
        count=(int)(count*VfxEffect.Safe(intensity,1,0,8));
        foreach(var layer in Layers) if(layer.Settings.Enabled) Emit(layer,emitter,count,intensity);
    }
    public void Advance(float dt, Matrix4x4 emitter, float intensity=1)
    {
        if(!float.IsFinite(dt) || dt<=0 || !IsPlaying) return;
        intensity=VfxEffect.Safe(intensity,1,0,8);
        // At most 12 substeps after a hitch; never spiral into catch-up work.
        _accumulator=Math.Min(_accumulator+dt,.2f);
        const float step=1f/60f;
        int steps=0;
        while(_accumulator>=step && steps++<12) { Step(step,emitter,intensity); _accumulator-=step; }
    }
    private void Step(float dt, Matrix4x4 emitter,float intensity)
    {
        float previous=Elapsed; Elapsed+=dt;
        foreach(var layer in Layers)
        {
            var s=layer.Settings;
            for(int i=layer.Count-1;i>=0;i--)
            {
                ref var p=ref layer.Particles[i];
                p.Age+=dt;
                if(p.Age>=p.Lifetime)
                { layer.Particles[i]=layer.Particles[--layer.Count]; ActiveCount--; continue; }
                p.Velocity=(p.Velocity+s.Gravity*dt)/(1+s.Drag*dt);
                p.Position+=p.Velocity*dt;
                if(s.GroundBounce && p.Position.Y<s.GroundHeight && p.Velocity.Y<0)
                { p.Position.Y=s.GroundHeight; p.Velocity.Y=-p.Velocity.Y*s.Bounciness; p.Velocity.X*=.8f; p.Velocity.Z*=.8f; }
                p.Rotation+=s.RotationSpeed*(MathF.PI/180)*dt;
            }
            if(!IsEmitting || !s.Enabled || Elapsed<s.Delay) continue;
            if(!layer.BurstEmitted) EmitInitial(layer,emitter,intensity);
            if(s.RenderMode==VfxRenderMode.Beam)
            { if(layer.Count==0 && intensity>0) Emit(layer,emitter,1,intensity); continue; }
            float emissionTime=Effect.Loop ? dt : Math.Max(0,Math.Min(Elapsed,Effect.Duration)-Math.Max(previous,s.Delay));
            layer.EmissionRemainder+=s.Rate*intensity*emissionTime;
            int emit=(int)layer.EmissionRemainder; layer.EmissionRemainder-=emit;
            Emit(layer,emitter,emit,intensity);
        }
        if(!Effect.Loop && Elapsed>=Effect.Duration) IsEmitting=false;
    }
    private void EmitInitial(Layer layer,Matrix4x4 emitter,float intensity)
    {
        layer.BurstEmitted=true;
        if(layer.Settings.Enabled) Emit(layer,emitter,(int)(layer.Settings.Burst*VfxEffect.Safe(intensity,1,0,8)),intensity);
    }
    private float Rand() => (float)_random.NextDouble();
    private Vector3 Unit()
    {
        float y=Rand()*2-1, a=Rand()*MathF.Tau, r=MathF.Sqrt(Math.Max(0,1-y*y));
        return new(r*MathF.Cos(a),y,r*MathF.Sin(a));
    }
    private void Emit(Layer layer,Matrix4x4 emitter,int count,float intensity)
    {
        if(count<=0) return;
        int accepted=Math.Min(count,layer.Particles.Length-layer.Count);
        DroppedParticles=(int)Math.Min(int.MaxValue,(long)DroppedParticles+count-accepted);
        var s=layer.Settings;
        for(int i=0;i<accepted;i++)
        {
            Vector3 offset=s.Shape switch
            {
                VfxShape.Sphere=>Unit()*s.Extents*MathF.Cbrt(Rand()),
                VfxShape.Box=>new Vector3(Rand()*2-1,Rand()*2-1,Rand()*2-1)*s.Extents,
                VfxShape.Ring=>Ring(s.Extents),
                _=>Vector3.Zero
            };
            Vector3 direction=s.Direction;
            float cosMin=MathF.Cos(s.Spread*MathF.PI/180), cos=1-Rand()*(1-cosMin), angle=Rand()*MathF.Tau;
            Vector3 tangent=Vector3.Normalize(Vector3.Cross(MathF.Abs(direction.Y)<.99f ? Vector3.UnitY : Vector3.UnitX,direction));
            Vector3 bitangent=Vector3.Cross(direction,tangent);
            direction=direction*cos+(tangent*MathF.Cos(angle)+bitangent*MathF.Sin(angle))*MathF.Sqrt(Math.Max(0,1-cos*cos));
            if(s.Shape==VfxShape.Cone) offset=(tangent*(Rand()*2-1)+bitangent*(Rand()*2-1))*s.Extents.X;
            Vector3 position=offset+s.Offset;
            Vector3 velocity=direction*s.Speed*(1+(Rand()*2-1)*s.SpeedVariation);
            if(!s.LocalSpace) { position=Vector3.Transform(position,emitter); velocity=Vector3.TransformNormal(velocity,emitter); }
            layer.Particles[layer.Count++]=new VfxParticle
            {
                Position=position, PreviousPosition=layer.HasLastSpawn ? layer.LastSpawn : position, Velocity=velocity,
                Lifetime=s.Lifetime*(1+(Rand()*2-1)*s.LifetimeVariation), SizeFactor=1+(Rand()*2-1)*s.SizeVariation,
                Rotation=s.RenderMode==VfxRenderMode.Billboard ? Rand()*MathF.Tau : 0, Sequence=++_sequence
            };
            layer.LastSpawn=position; layer.HasLastSpawn=true; ActiveCount++;
        }
    }
    private Vector3 Ring(Vector3 extents) { float a=Rand()*MathF.Tau; return new(MathF.Cos(a)*extents.X,0,MathF.Sin(a)*extents.Z); }
}
