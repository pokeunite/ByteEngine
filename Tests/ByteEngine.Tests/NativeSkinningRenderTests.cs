using System.Numerics;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Assets;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
namespace ByteEngine.Tests;
internal static class NativeSkinningRenderTests {
 public static void Run(){
  using var window=new NativeWindow(new NativeWindowSettings{ClientSize=new(128,128),StartVisible=false,API=ContextAPI.OpenGL,APIVersion=new(3,3),Profile=ContextProfile.Core});
  window.Context.MakeCurrent();GL.LoadBindings(new OpenTK.Windowing.GraphicsLibraryFramework.GLFWBindingsContext());
  using var renderer=new Renderer3D();renderer.Initialize();
  float[] vertices=[-.4f,-.4f,0,0,0,1,0,0,.4f,-.4f,0,0,0,1,1,0,0,.4f,0,0,0,1,.5f,1];
  float[] weights=[0,299,0,0,.25f,.75f,0,0,0,299,0,0,.6f,.4f,0,0,0,299,0,0,.4f,.6f,0,0];
  using var gpu=new Mesh(vertices.ToArray(),[0,1,2],true);using var cpu=new Mesh(vertices.ToArray(),[0,1,2],true);
  var skin=new GpuSkinningData(vertices,weights){Bones=Enumerable.Repeat(Matrix4x4.Identity,300).ToArray()};gpu.SetGpuSkinning(skin);
  var material=new Material{BaseColor=new(.7f,.2f,.1f,1),Shading=MaterialShadingMode.Unlit,CullMode=CullMode3D.None};
  var lighting=new RenderLighting3D([new(new(0,0,-1),Vector3.One,1,0,true,256,5,.001f,1,0)],[new(new(0,0,2),Vector3.One,1,5,true,256,.001f,1,0)],1,1,1);
  var view=new RenderView3D(Matrix4x4.CreateLookAt(new(0,0,3),Vector3.Zero,Vector3.UnitY),Matrix4x4.CreatePerspectiveFieldOfView(1,1,.1f,10),new(0,0,3),128,128);
  byte[] Image(Mesh mesh){GL.BindFramebuffer(FramebufferTarget.Framebuffer,0);GL.Viewport(0,0,128,128);GL.ClearColor(0,0,0,1);GL.Clear(ClearBufferMask.ColorBufferBit|ClearBufferMask.DepthBufferBit);renderer.Draw(mesh,material,Matrix4x4.Identity,view.ViewMatrix,view.ProjectionMatrix,lighting);var data=new byte[128*128*4];GL.ReadPixels(0,0,128,128,PixelFormat.Rgba,PixelType.UnsignedByte,data);return data;}
  float[] Depth(int texture,bool cube){GL.BindTexture(cube?TextureTarget.TextureCubeMap:TextureTarget.Texture2D,texture);var data=new float[256*256*(cube?6:1)];for(int face=0;face<(cube?6:1);face++){var part=new float[256*256];GL.GetTexImage(cube?(TextureTarget)((int)TextureTarget.TextureCubeMapPositiveX+face):TextureTarget.Texture2D,0,PixelFormat.DepthComponent,PixelType.Float,part);part.CopyTo(data,face*part.Length);}return data;}
  RenderSubmission Item(Mesh mesh)=>new(mesh,material,Matrix4x4.Identity,mesh.LocalBounds,RenderQueue3D.Opaque,false,true,true,0,0);
  void Equal(float[] a,float[] b,string stage){if(!a.Any(x=>x<.999f)||a.Zip(b).Any(x=>Math.Abs(x.First-x.Second)>.00002f))throw new Exception(stage+" GPU/CPU depths differ");}
  for(int pose=0;pose<3;pose++){
   skin.Bones[0]=Matrix4x4.CreateRotationZ(.1f*pose)*Matrix4x4.CreateTranslation(.08f*pose,0,0);
   skin.Bones[299]=Matrix4x4.CreateRotationY(.25f*(pose+1))*Matrix4x4.CreateTranslation(0,.05f*pose,0);
   var output=vertices.ToArray();for(int v=0;v<3;v++){var p=new Vector3(vertices[v*8],vertices[v*8+1],vertices[v*8+2]);var q=Vector3.Transform(p,skin.Bones[0])*weights[v*8+4]+Vector3.Transform(p,skin.Bones[299])*weights[v*8+5];output[v*8]=q.X;output[v*8+1]=q.Y;output[v*8+2]=q.Z;}cpu.UpdateVertices(output);
   var expected=Image(cpu);var actual=Image(gpu);if(!actual.Where((v,i)=>i%4!=3&&v>10).Any()||expected.Zip(actual).Any(x=>Math.Abs(x.First-x.Second)>1))throw new Exception("Native skinned color pixels differ");
   var a=renderer.RenderDirectionalShadowMap([Item(cpu)],view,lighting);var da=Depth(a.Shadow!.Value.DepthTextureId,false);var b=renderer.RenderDirectionalShadowMap([Item(gpu)],view,lighting);Equal(da,Depth(b.Shadow!.Value.DepthTextureId,false),"Directional");
   var pa=renderer.RenderPointShadowMaps([Item(cpu)],view,lighting);var pda=Depth(pa.Shadows[0].DepthCubeTextureId,true);var pb=renderer.RenderPointShadowMaps([Item(gpu)],view,lighting);Equal(pda,Depth(pb.Shadows[0].DepthCubeTextureId,true),"Point");
   var after=Image(cpu);if(!expected.SequenceEqual(after))throw new Exception("Skin state leaked into static draw");
  }
  if(GL.GetError()!=ErrorCode.NoError)throw new Exception("Native skinning GL error");
  Console.WriteLine("PASS native GPU skinning: 300-bone palettes, changed poses, CPU-equivalent main/directional/six-face point shadows and static state isolation");
 }
}
