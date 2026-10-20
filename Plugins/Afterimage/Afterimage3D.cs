using System.Numerics;
using System.ComponentModel;
using ByteEngine.Core;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;

namespace ByteEngine.Afterimage;

/// <summary>Bounded frozen-pose ghosts; no duplicated AI, animation, colliders or scene objects.</summary>
public sealed class Afterimage3D : ByteEngine.Core.Scene.Component
{
    public bool Emitting { get; set; }
    public float Interval { get; set; } = .09f;
    public float Lifetime { get; set; } = .42f;
    public float MinimumDistance { get; set; } = .06f;
    public int MaximumGhosts { get; set; } = 5;
    public Vector4 Color { get; set; } = new(.12f,.8f,1,.22f);
    [Browsable(false)] public int ActiveGhosts => _ghosts.Count;
    private float _elapsed;
    private Vector3? _last;
    private readonly List<Ghost> _ghosts = [];
    private sealed class Ghost : IDisposable
    {
        public required IReadOnlyList<(Mesh Mesh, Matrix4x4 World)> Parts;
        public required Material Material;
        public float Remaining, Duration;
        public void Dispose() { foreach (var part in Parts) part.Mesh.Dispose(); }
    }
    public void Clear() { foreach (var ghost in _ghosts) ghost.Dispose(); _ghosts.Clear(); _last=null; }
    protected override void OnStop() => Clear();
    protected override void OnDestroy() => Clear();
    protected override void OnLateUpdate()
    {
        // Real-time visual cadence remains smooth when the source uses a slow local clock.
        float dt=Math.Clamp((float)Time.UnscaledDeltaTime,0,.1f);
        for(int n=_ghosts.Count-1;n>=0;n--) if((_ghosts[n].Remaining-=dt)<=0){_ghosts[n].Dispose();_ghosts.RemoveAt(n);}
        if(!Emitting){_last=null;_elapsed=0;return;}
        _elapsed+=dt;
        if(_elapsed<Math.Clamp(Interval,.03f,2))return;
        if(_last is {} last&&Vector3.DistanceSquared(last,Transform.WorldPosition)<MathF.Pow(Math.Clamp(MinimumDistance,.01f,10),2))return;
        var renderer=GameObject.GetComponent<SkeletalMeshRenderer>();
        if(renderer is not {Visible:true})return;
        var parts=renderer.CapturePoseMeshes();if(parts.Count==0)return;
        while(_ghosts.Count>=Math.Clamp(MaximumGhosts,1,8)){_ghosts[0].Dispose();_ghosts.RemoveAt(0);}
        float duration=Math.Clamp(Lifetime,.05f,3);
        _ghosts.Add(new(){Parts=parts,Remaining=duration,Duration=duration,Material=new(){
            Shading=MaterialShadingMode.Unlit,BlendMode=BlendMode3D.Additive,
            DepthWriteMode=DepthWriteMode3D.Disabled,DepthTest=true,CullMode=CullMode3D.None}});
        _elapsed=0;_last=Transform.WorldPosition;
    }
    protected override void OnRender(RenderContext context)
    {
        foreach(var ghost in _ghosts){ghost.Material.BaseColor=new(Color.X,Color.Y,Color.Z,Math.Clamp(Color.W,0,1)*ghost.Remaining/ghost.Duration);
            foreach(var part in ghost.Parts)context.RenderWorld.Submit(part.Mesh,ghost.Material,part.World,RenderQueue3D.Transparent,true,false,false);}
    }
}
