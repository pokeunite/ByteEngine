using System.Numerics;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Physics;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Graphics;

namespace ByteEngine.Core.Gameplay.Hydraulics;

/// <summary>Rectangular control volume. Water is conserved; immersed native bodies displace it.</summary>
public sealed class WaterBasin3D : Component
{
    public float MinX { get; set; }
    public float MaxX { get; set; } = 10;
    public float Floor { get; set; }
    public float Depth { get; set; } = 2.4f;
    public float CenterZ { get; set; }
    public bool AvailableForRecovery { get; set; } = true;
    public float Rim { get; set; } = 6.4f;
    public float InitialLevel { get; set; } = 1;
    public string SurfaceName { get; set; } = "";
    public string VolumeVisualName { get; set; } = "";
    public float VisualDepth { get; set; }
    public float VisualInset { get; set; }
    public string FlowMarkerPrefix { get; set; } = "";
    public double Volume { get; internal set; }
    public float Level { get; internal set; }
    public float Current { get; internal set; }
    internal float LeftFlux,RightFlux;
    public float CurrentAt(float x) => Math.Clamp(float.Lerp(LeftFlux,RightFlux,Math.Clamp((x-MinX)/Math.Max(.01f,MaxX-MinX),0,1))/Math.Max(.5f,Depth*(Level-Floor)),-3.5f,3.5f);
    public float Area => Math.Max(.01f, (MaxX - MinX) * Depth);
    internal float Displacement(float y, IEnumerable<BuoyantBody3D> bodies)
    {
        float result = 0;
        foreach (var b in bodies)
        {
            Vector3 p = b.Transform.WorldPosition, s = b.Size;
            float w = Math.Max(0, Math.Min(MaxX, p.X+s.X/2)-Math.Max(MinX,p.X-s.X/2));
            float d = Math.Max(0, Math.Min(CenterZ+Depth/2,p.Z+s.Z/2)-Math.Max(CenterZ-Depth/2,p.Z-s.Z/2));
            float h = Math.Clamp(y-(p.Y-s.Y/2),0,s.Y);
            result += w*d*h;
        }
        return result;
    }
    internal void Solve(IEnumerable<BuoyantBody3D> bodies)
    {
        float lo=Floor, hi=Rim;
        for(int i=0;i<22;i++)
        {
            float mid=(lo+hi)/2;
            if((mid-Floor)*Area-Displacement(mid,bodies)>Volume) hi=mid; else lo=mid;
        }
        Level=(lo+hi)/2;
    }
}

/// <summary>Gravity-driven sluice or a recovery pump connecting two finite basins.</summary>
public sealed class WaterGate3D : Component
{
    public string Source { get; set; } = "";
    public string Destination { get; set; } = "";
    public bool IsPump { get; set; }
    public float Conductance { get; set; } = .6f;
    public float JetCurrentFactor { get; set; }
    public float Opening { get; set; }
    public string BladeName { get; set; } = "";
    public bool RetractBlade { get; set; }
    public float ClosedY { get; set; } = 3;
    public float LiftDistance { get; set; } = 6;
    public float Flow { get; internal set; }
    public double TransferredVolume { get; private set; }
    internal void RecordTransfer(double amount)=>TransferredVolume+=Math.Abs(amount);
    public bool OneWay { get; set; }
    public bool Overflow { get; set; }
    public float CrestHeight { get; set; }
    internal WaterBasin3D? From, To;
}

/// <summary>Finite-volume hydraulics with gravity head, reversible flow and closed-loop pumps.</summary>
public sealed class WaterNetwork3D : Component
{
    public float Density { get; set; } = 1000;
    public WaterBasin3D[] Basins { get; private set; } = [];
    public WaterGate3D[] Gates { get; private set; } = [];
    public BuoyantBody3D[] Bodies { get; private set; } = [];
    public WaterCurrentPath3D[] CurrentPaths { get; private set; } = [];
    public double InitialVolume { get; private set; }
    public double ElapsedSeconds { get; private set; }
    public double TotalVolume => Basins.Sum(b=>(double)b.Volume);
    public double RecoverableVolume => Basins.Where(b=>b.AvailableForRecovery).Sum(b=>b.Volume);
    readonly Dictionary<string,GameObject> visuals = new();
    protected override void OnStart()
    {
        var objects=GameObject.Scene!.GameObjects;
        visuals.Clear();
        foreach(var o in objects) visuals.TryAdd(o.Name,o);
        Basins=objects.SelectMany(o=>o.Components).OfType<WaterBasin3D>().ToArray();
        Gates=objects.SelectMany(o=>o.Components).OfType<WaterGate3D>().ToArray();
        Bodies=objects.SelectMany(o=>o.Components).OfType<BuoyantBody3D>().ToArray();
        CurrentPaths=objects.SelectMany(o=>o.Components).OfType<WaterCurrentPath3D>().ToArray();
        foreach(var b in Basins) { b.Volume=Math.Max(0,(b.InitialLevel-b.Floor)*b.Area-b.Displacement(b.InitialLevel,Bodies)); b.Solve(Bodies); }
        InitialVolume=TotalVolume;
        foreach(var body in Bodies) body.Network=this;
    }
    protected override void OnFixedUpdate()
    {
        float dt=(float)Time.FixedDeltaTime;
        ElapsedSeconds+=dt;
        foreach(var b in Basins) { b.Solve(Bodies); b.Current=0; b.LeftFlux=b.RightFlux=0; }
        foreach(var gate in Gates)
        {
            gate.From=Basins.FirstOrDefault(b=>b.GameObject.Name==gate.Source);
            gate.To=Basins.FirstOrDefault(b=>b.GameObject.Name==gate.Destination);
            gate.Flow=0;
            if(!gate.Enabled || !gate.GameObject.ActiveInHierarchy) continue;
            if(gate.From is not {} a || gate.To is not {} b) continue;
            gate.Opening=Math.Clamp(gate.Opening,0,1);
            float head=a.Level-b.Level;
            if(gate.Overflow)head=Math.Max(0,a.Level-Math.Max(b.Level,gate.CrestHeight));
            if(gate.OneWay)head=Math.Max(0,head);
            float q=gate.IsPump ? gate.Conductance*gate.Opening : gate.Conductance*gate.Opening*MathF.Sign(head)*MathF.Sqrt(19.62f*Math.Abs(head));
            var source=q>=0?a:b; var target=q>=0?b:a;
            double room=ReferenceEquals(source,target)?source.Volume:Math.Max(0,(target.Rim-target.Floor)*target.Area-target.Displacement(target.Rim,Bodies)-target.Volume);
            double amount=Math.Min(Math.Abs(q)*dt,Math.Min(source.Volume,room));
            if(!gate.IsPump) amount=Math.Min(amount,Math.Abs(head)/(1/a.Area+1/b.Area)*.48f);
            source.Volume-=amount; target.Volume+=amount;
            gate.RecordTransfer(amount);
            gate.Flow=(float)(MathF.Sign(q)*amount/Math.Max(.000001f,dt));
            if(!gate.IsPump)
            {
                float direction=Math.Sign((b.MinX+b.MaxX)-(a.MinX+a.MaxX));
                // Water supplied by a vertical lift pipe does not propel a boat sideways.
                if(Math.Abs(a.MaxX-b.MinX)<.1f || Math.Abs(b.MaxX-a.MinX)<.1f)
                {
                    if(direction>0){a.RightFlux+=gate.Flow;b.LeftFlux+=gate.Flow;}
                    else {a.LeftFlux-=gate.Flow;b.RightFlux-=gate.Flow;}
                }
                else b.LeftFlux+=gate.Flow*gate.JetCurrentFactor;
            }
            if(gate.BladeName.Length>0 && visuals.TryGetValue(gate.BladeName,out var blade))
            {
                float travel=gate.LiftDistance*gate.Opening;
                float height=blade.GetComponent<BoxCollider3D>()?.Size.Y??Math.Max(.01f,gate.ClosedY*2);
                if(gate.RetractBlade)travel=Math.Clamp(travel,0,height*.985f);
                blade.Transform.LocalPosition=new(blade.Transform.LocalPosition.X,gate.ClosedY+travel*(gate.RetractBlade?.5f:1),blade.Transform.LocalPosition.Z);
                if(gate.RetractBlade)blade.Transform.LocalScale=new(1,Math.Max(.015f,1-travel/height),1);
            }
        }
        foreach(var b in Basins)
        {
            b.Solve(Bodies); b.Current=b.CurrentAt((b.MinX+b.MaxX)/2);
            if(visuals.TryGetValue(b.SurfaceName,out var surface))
                surface.Transform.LocalPosition=new((b.MinX+b.MaxX)/2,b.Level,b.CenterZ);
            if(visuals.TryGetValue(b.VolumeVisualName,out var waterBody))
            {
                waterBody.Transform.LocalPosition=new((b.MinX+b.MaxX)/2,(b.Floor+b.Level)/2,b.CenterZ);
                waterBody.Transform.LocalScale=new(Math.Max(.001f,b.MaxX-b.MinX-2*b.VisualInset),Math.Max(.001f,b.Level-b.Floor),b.VisualDepth>0?b.VisualDepth:b.Depth);
            }
        }
    }
    protected override void OnLateUpdate()
    {
        var scene=GameObject.Scene!;
        float time=(float)scene.SimulationTime;
        foreach(var b in Basins)
        {
            var paths=CurrentPaths.Where(path=>path.Enabled&&path.GameObject.ActiveInHierarchy&&path.BasinName==b.GameObject.Name).ToArray();
            for(int i=0;i<9;i++) if(visuals.TryGetValue(b.FlowMarkerPrefix+i,out var marker))
            {
                if(paths.Length>0){
                    var point=marker.Transform.LocalPosition;Vector3 velocity=Vector3.Zero;
                    foreach(var path in paths)velocity+=path.VelocityAt(point,this,b);
                    point+=velocity*(float)Time.DeltaTime;
                    if(point.X>b.MaxX-.15f)point.X=b.MinX+.15f;if(point.X<b.MinX+.15f)point.X=b.MaxX-.15f;
                    if(point.Z>b.CenterZ+b.Depth/2-.10f)point.Z=b.CenterZ-b.Depth/2+.10f;if(point.Z<b.CenterZ-b.Depth/2+.10f)point.Z=b.CenterZ+b.Depth/2-.10f;
                    point.Y=b.Level+.035f;marker.Transform.LocalPosition=point;continue;
                }
                float width=b.MaxX-b.MinX-.5f;
                float travel=i*width/9+time*b.Current;
                float x=b.MinX+.25f+(travel%width+width)%width;
                marker.Transform.LocalPosition=new(x,b.Level+.035f+MathF.Sin(time*2+i)*.008f,b.CenterZ+(i%3-1)*Math.Min(.65f,b.Depth*.3f));
            }

        }
    }
}

/// <summary>Archimedes buoyancy and fluid drag applied through the native rigid-body solver.</summary>
public sealed class BuoyantBody3D : Component
{
    public Vector3 Size { get; set; } = Vector3.One;
    public float Bottom => Transform.WorldPosition.Y-Size.Y/2;
    public float Top => Transform.WorldPosition.Y+Size.Y/2;
    public float Back => Transform.WorldPosition.X-Size.X/2;
    public float Front => Transform.WorldPosition.X+Size.X/2;
    public float WaterDrag { get; set; } = 2.4f;
    public float VerticalDrag { get; set; } = 5;
    internal WaterNetwork3D? Network;
    protected override void OnFixedUpdate()
    {
        if(Network is not {} network || GameObject.GetComponent<Rigidbody3D>() is not {} body) return;
        Vector3 p=Transform.WorldPosition;
        float volume=0;Vector3 weightedCurrent=Vector3.Zero;
        foreach(var b in network.Basins)
        {
            float width=Math.Max(0,Math.Min(b.MaxX,p.X+Size.X/2)-Math.Max(b.MinX,p.X-Size.X/2));
            float depth=Math.Max(0,Math.Min(b.CenterZ+b.Depth/2,p.Z+Size.Z/2)-Math.Max(b.CenterZ-b.Depth/2,p.Z-Size.Z/2));
            float height=Math.Clamp(b.Level-(p.Y-Size.Y/2),0,Size.Y);
            float submerged=width*height*depth;
            float mid=(Math.Min(b.MaxX,p.X+Size.X/2)+Math.Max(b.MinX,p.X-Size.X/2))/2;
            Vector3 velocity=new(b.CurrentAt(mid),0,0);
            bool guided=false;Vector3 guidedVelocity=Vector3.Zero;
            foreach(var path in network.CurrentPaths)if(path.Enabled&&path.GameObject.ActiveInHierarchy&&path.BasinName==b.GameObject.Name){guided=true;guidedVelocity+=path.VelocityAt(p,network,b);}
            if(guided)velocity=guidedVelocity;
            volume+=submerged; weightedCurrent+=velocity*submerged;
        }
        if(volume<=0) return;
        float fraction=Math.Clamp(volume/(Size.X*Size.Y*Size.Z),0,1);
        Vector3 current=weightedCurrent/volume;
        Vector3 force=new((current.X-body.Velocity.X)*body.Mass*WaterDrag*fraction,
            network.Density*9.81f*volume-body.Velocity.Y*body.Mass*VerticalDrag*fraction,(current.Z-body.Velocity.Z)*body.Mass*WaterDrag*fraction);
        body.AddForce(force);
    }
}

