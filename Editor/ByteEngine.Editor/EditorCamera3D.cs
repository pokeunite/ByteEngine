using System.Numerics;
namespace ByteEngine.Editor;

internal sealed class EditorCamera3D
{
    public Vector3 Position{get;set;}=new(5,4,7);
    public float Yaw{get;set;}=-135f;
    public float Pitch{get;set;}=-22f;
    public float FieldOfView{get;set;}=60f;
    public Vector3 Forward { get { float y=Yaw*MathF.PI/180f,p=Pitch*MathF.PI/180f;return Vector3.Normalize(new(MathF.Cos(p)*MathF.Cos(y),MathF.Sin(p),MathF.Cos(p)*MathF.Sin(y))); } }
    public Vector3 Right=>Vector3.Normalize(Vector3.Cross(Forward,Vector3.UnitY));
    public Matrix4x4 View=>Matrix4x4.CreateLookAt(Position,Position+Forward,Vector3.UnitY);
    public Matrix4x4 Projection(float aspect)=>Matrix4x4.CreatePerspectiveFieldOfView(FieldOfView*MathF.PI/180f,Math.Max(aspect,.001f),.05f,2000f);
    public void Reset(){Position=new(5,4,7);Yaw=-135;Pitch=-22;}
    public void Frame(ByteEngine.Core.Scene.GameObject target){Vector3 size=target.Transform.WorldScale;float distance=Math.Max(Math.Max(Math.Abs(size.X),Math.Abs(size.Y)),Math.Abs(size.Z))*3f+2f;Position=target.Transform.WorldPosition-Forward*distance;}
}
