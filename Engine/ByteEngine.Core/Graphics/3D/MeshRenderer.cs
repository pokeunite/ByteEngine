using ByteEngine.Core.Scene;
namespace ByteEngine.Core.Graphics.ThreeD;
public sealed class MeshRenderer:Component
{
    public Mesh? Mesh{get;set;} public PrimitiveMeshType Primitive{get;set;}=PrimitiveMeshType.Cube; public Material Material{get;set;}=new(); public bool Visible{get;set;}=true;
    protected override void OnRender(RenderContext context){if(!Visible||!context.Has3DCamera)return;Mesh mesh=Mesh??context.Renderer3D.GetPrimitive(Primitive);DirectionalLight? light=context.Scene.FindComponent<DirectionalLight>();context.Renderer3D.Draw(mesh,Material,Transform.WorldMatrix,context.GetViewMatrix3D(),context.GetProjectionMatrix3D(),light?.Direction??new(-.4f,-1f,-.3f),light?.Color??System.Numerics.Vector3.One,light?.Intensity??1f);}
}
