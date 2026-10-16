using System.Numerics;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;

namespace ByteEngine.Core.Gameplay.Hydraulics;

/// <summary>Optional, sparse direction markers driven by delivered flux and wet basin depth.
/// Never renders a suggested flow over a dry reach or changes the hydraulic simulation.</summary>
public sealed class WaterFlowIndicator3D : Component
{
    public string FlowGate { get; set; } = "";
    public string BasinName { get; set; } = "";
    public string BasinPrefix { get; set; } = "";
    public int BasinCount { get; set; }
    public Vector3 Start { get; set; }
    public Vector3 End { get; set; }
    public int Count { get; set; } = 2;
    public float Size { get; set; } = .65f;
    public bool Show { get; set; } = true;
    public bool IsFlowing => gate is not null&&Math.Abs(gate.DeliveredFlow)>.015f;
    WaterGate3D? gate;WaterBasin3D? basin;WaterBasin3D[] cells=[];Mesh? mesh;
    readonly Material material=new(){Shading=MaterialShadingMode.Unlit,BaseColor=new(.67f,.94f,.91f,.65f),BlendMode=BlendMode3D.AlphaBlend,CullMode=CullMode3D.None,DepthWriteMode=DepthWriteMode3D.Disabled};
    protected override void OnStart(){var s=GameObject.Scene!;gate=s.FindGameObject(FlowGate)?.GetComponent<WaterGate3D>();basin=s.FindGameObject(BasinName)?.GetComponent<WaterBasin3D>();cells=Enumerable.Range(0,Math.Clamp(BasinCount,0,64)).Select(i=>s.FindGameObject(BasinPrefix+i)?.GetComponent<WaterBasin3D>()).OfType<WaterBasin3D>().ToArray();}
    protected override void OnRender(RenderContext context)
    {
        if(!Show||!context.Has3DCamera||!IsFlowing)return;
        mesh??=new Mesh(new float[]{-.18f,0,-.4f,0,1,0,0,0,.18f,0,-.4f,0,1,0,1,0,.18f,0,.05f,0,1,0,1,1,-.18f,0,.05f,0,1,0,0,1,-.4f,0,.05f,0,1,0,0,0,.4f,0,.05f,0,1,0,1,0,0,0,.52f,0,1,0,.5f,1},new uint[]{0,1,2,0,2,3,4,5,6});
        Vector3 direction=End-Start;direction.Y=0;if(direction.LengthSquared()<.001f)return;
        float yaw=MathF.Atan2(direction.X,direction.Z)+(gate!.DeliveredFlow<0?MathF.PI:0),time=(float)GameObject.Scene!.SimulationTime;
        int count=Math.Clamp(Count,1,4);
        for(int i=0;i<count;i++){
            float t=(i+.5f)/count+MathF.Sin(time*.7f+i)*.025f;var b=cells.Length>0?cells[Math.Clamp((int)(t*cells.Length),0,cells.Length-1)]:basin;
            if(b==null||b.Level-b.Floor<.025f)continue;
            var p=Vector3.Lerp(Start,End,t);p.Y=b.Level+.13f;
            context.RenderWorld.Submit(mesh,material,Matrix4x4.CreateScale(Math.Clamp(Size,.2f,2))*Matrix4x4.CreateRotationY(yaw)*Matrix4x4.CreateTranslation(p),RenderQueue3D.Transparent,true,false,false);
        }
    }
    protected override void OnDestroy()=>mesh?.Dispose();
}
