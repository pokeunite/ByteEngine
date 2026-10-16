using System.Numerics;
using ByteEngine.Core.Scene;
namespace ByteEngine.Core.Graphics.ThreeD;
/// <summary>Reusable bounded tube mesh. Slack bows under gravity; taut cables retain their round section.</summary>
public sealed class CableRenderer3D : Component
{
    public Vector3 Start {get;set;}
    public Vector3 End {get;set;}
    public bool DrawEndHook {get;set;}=true;
    public bool UseControlPoint {get;set;}
    public Vector3 ControlPoint {get;set;}
    public float Length {get;set;}=1;
    public float Radius {get;set;}=.025f;
    public Vector4 Color {get;set;}=new(.18f,.16f,.12f,1);
    internal Func<bool>? VisibilityGuard {get;set;}
    const int Segments=24,Sides=6;
    Mesh? _mesh,_hook;float[]? _vertices;
    readonly Material _material=new(){Roughness=.8f};
    readonly Material _hookMaterial=new(){BaseColor=new(.45f,.46f,.43f,1),Roughness=.45f,Metallic=.7f};
    static Mesh HookMesh(){var vertices=new List<float>();var indices=new List<uint>();const int rings=12,sides=6;for(int i=0;i<=rings;i++){float a=(30+300f*i/rings)*MathF.PI/180;var center=new Vector3(MathF.Cos(a)*.075f,MathF.Sin(a)*.075f,0);for(int j=0;j<sides;j++){float b=MathF.Tau*j/sides;var normal=new Vector3(MathF.Cos(a)*MathF.Cos(b),MathF.Sin(a)*MathF.Cos(b),MathF.Sin(b));var p=center+normal*.018f;vertices.AddRange([p.X,p.Y,p.Z,normal.X,normal.Y,normal.Z,0,0]);if(i<rings){uint x=(uint)(i*sides+j),y=(uint)(i*sides+(j+1)%sides);indices.AddRange([x,y,x+sides,y,y+sides,x+sides]);}}}return new(vertices.ToArray(),indices.ToArray());}
    protected override void OnRender(RenderContext context)
    {
        if(VisibilityGuard?.Invoke()==false||!context.Has3DCamera||!float.IsFinite(Start.LengthSquared())||!float.IsFinite(End.LengthSquared()))return;
        var direction=End-Start;float distance=direction.Length();if(distance<.001f)return;
        if(_mesh==null){_vertices=new float[(Segments+1)*Sides*8];var indices=new uint[Segments*Sides*6];int n=0;for(int i=0;i<Segments;i++)for(int j=0;j<Sides;j++){uint a=(uint)(i*Sides+j),b=(uint)(i*Sides+(j+1)%Sides),c=a+Sides,d=b+Sides;foreach(var k in new[]{a,b,c,b,d,c})indices[n++]=k;}_mesh=new(_vertices,indices,true);}
        var forward=direction/distance;var side=Vector3.Normalize(Vector3.Cross(forward,Math.Abs(forward.Y)>.95f?Vector3.UnitX:Vector3.UnitY));var up=Vector3.Cross(side,forward);
        float slack=Math.Max(0,(float.IsFinite(Length)?Length:distance)-distance);float sag=Math.Min(distance*.3f,MathF.Sqrt(slack*Math.Max(distance,.1f))*.5f);
        for(int i=0;i<=Segments;i++){float t=(float)i/Segments;var center=UseControlPoint?((1-t)*(1-t)*Start+2*(1-t)*t*ControlPoint+t*t*End):Vector3.Lerp(Start,End,t)-Vector3.UnitY*(4*sag*t*(1-t));for(int j=0;j<Sides;j++){float a=MathF.Tau*j/Sides;var normal=side*MathF.Cos(a)+up*MathF.Sin(a);var p=center+normal*Math.Clamp(Radius,.005f,.2f);int k=(i*Sides+j)*8;_vertices![k]=p.X;_vertices[k+1]=p.Y;_vertices[k+2]=p.Z;_vertices[k+3]=normal.X;_vertices[k+4]=normal.Y;_vertices[k+5]=normal.Z;_vertices[k+6]=t*distance;_vertices[k+7]=(float)j/Sides;}}
        _mesh.UpdateVertices(_vertices!);_material.BaseColor=Color;context.RenderWorld.Submit(_mesh,_material,Matrix4x4.Identity,RenderQueue3D.Opaque,true,true,true);
        if(!DrawEndHook)return;
        _hook??=HookMesh();float dot=Vector3.Dot(Vector3.UnitZ,forward);var rotation=dot<-.999f?Quaternion.CreateFromAxisAngle(Vector3.UnitY,MathF.PI):Quaternion.Normalize(new Quaternion(Vector3.Cross(Vector3.UnitZ,forward),1+dot));context.RenderWorld.Submit(_hook,_hookMaterial,Matrix4x4.CreateFromQuaternion(rotation)*Matrix4x4.CreateTranslation(End),RenderQueue3D.Opaque,true,true,true);
    }
    protected override void OnDestroy(){_mesh?.Dispose();_hook?.Dispose();_mesh=_hook=null;}
}
