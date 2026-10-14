using System.Numerics;
using ByteEngine.Core.Graphics.ThreeD;
using OpenTK.Graphics.OpenGL4;
namespace ByteEngine.Tests;
internal static class OcclusionTests
{
    private static void Check(bool value,string message){if(!value)throw new Exception(message);}
    internal static void Run(Renderer3D renderer)
    {
        using var wall=new Mesh(new float[]{-3,-3,0,0,0,1,0,0,3,-3,0,0,0,1,1,0,3,3,0,0,0,1,1,1,-3,3,0,0,0,1,0,1},new uint[]{0,1,2,0,2,3});
        var cube=renderer.GetPrimitive(PrimitiveMeshType.Cube);
        var solid=new Material{Shading=ByteEngine.Core.Assets.MaterialShadingMode.Unlit,BaseColor=new(.2f,.3f,.4f,1)};
        var target=new Material{Shading=ByteEngine.Core.Assets.MaterialShadingMode.Unlit,BaseColor=new(1,.1f,.1f,1)};
        var vp=Matrix4x4.CreatePerspectiveFieldOfView(1,1,.1f,20);var view=new RenderView3D(Matrix4x4.Identity,vp,Vector3.Zero,128,128);
        RenderSubmission Item(Mesh mesh,Material material,Vector3 position,float scale=1){var model=Matrix4x4.CreateScale(scale)*Matrix4x4.CreateTranslation(position);return new(mesh,material,model,mesh.LocalBounds.Transform(model),RenderQueue3D.Opaque,true,true,true,0,0);}
        var occluder=Item(wall,solid,new(0,0,-2));var hidden=Item(cube,target,new(1,-1,-5),.25f);
        var culler=new SoftwareOcclusionCuller();var items=new List<RenderSubmission>{occluder,hidden};culler.Cull(items,view);
        Check(culler.LastCulledCount==1&&items.Count==1,"Opaque wall fails to reject hidden geometry");
        solid.CullMode=CullMode3D.Back;items=[occluder,hidden];culler.Cull(items,view);Check(items.Count==1,"Backface culling prevents a valid front-facing occluder");
        solid.FrontFace=FrontFaceWinding3D.Clockwise;items=[occluder,hidden];culler.Cull(items,view);Check(items.Count==2,"Culled winding writes occlusion depth");solid.FrontFace=FrontFaceWinding3D.CounterClockwise;solid.CullMode=CullMode3D.None;
        var front=Item(cube,target,new(.2f,0,-1),.1f);items=[occluder,front];culler.Cull(items,view);Check(items.Count==2,"Front geometry falsely occluded");
        var near=Item(cube,target,new(0,0,-.05f),.2f);items=[occluder,near];culler.Cull(items,view);Check(items.Count==2,"Near-plane object rejected");
        solid.BlendMode=BlendMode3D.Cutout;items=[occluder,hidden];culler.Cull(items,view);Check(items.Count==2,"Cutout treated as solid occluder");solid.BlendMode=BlendMode3D.Opaque;
        var moved=Item(wall,solid,new(8,0,-2));items=[moved,hidden];culler.Cull(items,view);Check(items.Count==2,"Moving occluder leaves stale depth");
        target.DepthTest=false;items=[occluder,hidden];culler.Cull(items,view);Check(items.Count==2,"Depth-disabled draw rejected");target.DepthTest=true;
        byte[] Draw(IReadOnlyList<RenderSubmission> draws){GL.Viewport(0,0,128,128);GL.ClearColor(0,0,0,1);GL.Clear(ClearBufferMask.ColorBufferBit|ClearBufferMask.DepthBufferBit);foreach(var d in draws)renderer.Draw(d.Mesh,d.Material,d.ModelMatrix,Matrix4x4.Identity,vp,RenderLighting3D.Default);GL.Finish();var pixels=new byte[128*128*4];GL.ReadPixels(0,0,128,128,PixelFormat.Rgba,PixelType.UnsignedByte,pixels);return pixels;}
        var random=new Random(116);
        for(int frame=0;frame<24;frame++)
        {
            var all=new List<RenderSubmission>{occluder};
            for(int i=0;i<20;i++)all.Add(Item(cube,target,new((float)random.NextDouble()*6-3,(float)random.NextDouble()*6-3,-1-(float)random.NextDouble()*8),.1f+(float)random.NextDouble()*.4f));
            var expected=Draw(all);items=new(all);culler.Cull(items,view);var actual=Draw(items);
            Check(expected.Zip(actual).All(p=>Math.Abs(p.First-p.Second)<=1),"Occlusion changes visible GPU pixels at frame "+frame);
        }
        Check(GL.GetError()==ErrorCode.NoError,"Occlusion regression GL errors");
        Console.WriteLine("PASS conservative occlusion, front/near/depth-disabled objects, cutout/moving walls and 24 randomized GPU image comparisons");
    }
}
