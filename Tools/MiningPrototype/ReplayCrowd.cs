using System.Diagnostics;
using System.Numerics;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Gameplay;
using KarlsonDemo.Plugin;

internal sealed class ReplayCrowd : Component
{
    internal Vector3[] Positions = [];
    internal GameObject?[] Actors = [];
    internal float[] Health = [];
    internal Guid[] Targets = [];
    private readonly List<(Mesh mesh, Matrix4x4 local)>[] poses = [new(),new()];
    private readonly Material[] materials = [new(){BaseColor=new(.36f,.13f,.045f,1)},new(){BaseColor=new(.18f,.10f,.09f,1)}];
    internal static int Kind(int index) => index%3==0 ? 0 : 1;

    internal void Capture(int kind, GameObject actor)
    {
        if(poses[kind].Count>0)return;
        var renderer=actor.GetComponent<MiningSpiderAnimation3D>()!.Renderer!;
        Matrix4x4.Invert(Matrix4x4.CreateTranslation(actor.Transform.WorldPosition),out var inverse);
        foreach(var part in renderer.CapturePoseMeshes())
            poses[kind].Add((part.Mesh,part.World*inverse));
        if(poses[kind].Count==0)throw new InvalidOperationException("Spider pose missing");
    }
    protected override void OnRender(RenderContext context)
    {
        for(int i=0;i<Positions.Length;i++){
            if(Actors[i]!=null||Health[i]<=0)continue;
            var transform=Matrix4x4.CreateTranslation(Positions[i]);
            foreach(var part in poses[Kind(i)])
                context.RenderWorld.Submit(part.mesh,materials[Kind(i)],part.local*transform);
        }
    }
    protected override void OnDestroy(){foreach(var pose in poses)foreach(var part in pose)part.mesh.Dispose();}
}
