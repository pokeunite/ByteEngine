using System.Numerics;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;

namespace ByteEngine.Tests;

internal static class GraphicsQualityRenderTests
{
    public static void Run()
    {
        using var window=new NativeWindow(new NativeWindowSettings { ClientSize=new(128,128),StartVisible=false,API=ContextAPI.OpenGL,APIVersion=new(3,3),Profile=ContextProfile.Core,Title="Graphics offscreen checks" });
        window.Context.MakeCurrent(); GL.LoadBindings(new OpenTK.Windowing.GraphicsLibraryFramework.GLFWBindingsContext());
        Console.WriteLine("Offscreen GPU: "+GL.GetString(StringName.Renderer));
        using(var timer=new GeometryGpuTimer()){for(int i=0;i<8;i++){timer.Begin(true);GL.Clear(ClearBufferMask.ColorBufferBit);timer.End();GL.Finish();}if(GraphicsDiagnostics.GeometryGpuMs==null||GraphicsDiagnostics.GeometryGpuMs<0)throw new Exception("Geometry GPU query failed");timer.Begin(false);timer.End();if(GraphicsDiagnostics.GeometryGpuMs!=null)throw new Exception("Disabled GPU query stays visible");}
        using var post=new PostProcess3D();
        int source=GL.GenTexture(),depth=GL.GenTexture();
        try
        {
            var projection=Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI/3,1,.01f,100);
            const int size=128; float[] pixels=new float[size*size*4],depths=new float[size*size];
            float ProjectDepth(float z) { var p=Vector4.Transform(new Vector4(0,0,z,1),projection); return (p.Z/p.W)*.5f+.5f; }
            for(int y=0;y<size;y++) for(int x=0;x<size;x++)
            {
                int i=(y*size+x)*4; pixels[i]=.15f; pixels[i+1]=.08f; pixels[i+2]=.04f; pixels[i+3]=1;
                depths[y*size+x]=ProjectDepth(x<64?-2f:-2.15f);
                if(x>=58 && x<70 && y>=58 && y<70) pixels[i]=pixels[i+1]=pixels[i+2]=12;
            }
            GL.ActiveTexture(TextureUnit.Texture0); GL.BindTexture(TextureTarget.Texture2D,source);
            GL.TexImage2D(TextureTarget.Texture2D,0,PixelInternalFormat.Rgba16f,size,size,0,PixelFormat.Rgba,PixelType.Float,pixels);
            GL.TexParameter(TextureTarget.Texture2D,TextureParameterName.TextureMinFilter,(int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D,TextureParameterName.TextureMagFilter,(int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D,TextureParameterName.TextureWrapS,(int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D,TextureParameterName.TextureWrapT,(int)TextureWrapMode.ClampToEdge);
            GL.BindTexture(TextureTarget.Texture2D,depth);
            GL.TexImage2D(TextureTarget.Texture2D,0,PixelInternalFormat.DepthComponent32f,size,size,0,PixelFormat.DepthComponent,PixelType.Float,depths);
            GL.TexParameter(TextureTarget.Texture2D,TextureParameterName.TextureMinFilter,(int)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D,TextureParameterName.TextureMagFilter,(int)TextureMagFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D,TextureParameterName.TextureWrapS,(int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D,TextureParameterName.TextureWrapT,(int)TextureWrapMode.ClampToEdge);
            byte[] Read(int x,int y) { byte[] p=new byte[4]; GL.ReadPixels(x,y,1,1,PixelFormat.Rgba,PixelType.UnsignedByte,p); return p; }
            post.Render(source,0,size,size,true,1,false,depth,projection); var baseline=Read(78,64);
            if(GraphicsDiagnostics.ExtraPasses!=0) throw new Exception("Default adds passes.");
            post.Render(source,0,size,size,true,1,false,depth,projection,GraphicsLook.Default with { Bloom=.5f }); var glow=Read(78,64);
            if(glow[0]<=baseline[0]+3 || GraphicsDiagnostics.ExtraPasses!=3) throw new Exception("Bloom does not spread bright HDR pixels.");
            post.Render(source,0,size,size,true,1,false,depth,projection,GraphicsLook.Default with { Saturation=0 }); var grey=Read(20,20);
            if(Math.Abs(grey[0]-grey[1])>1 || Math.Abs(grey[1]-grey[2])>1) throw new Exception("Saturation zero should be grey.");
            post.Render(source,0,size,size,true,1,false,depth,projection,GraphicsLook.Default with { Occlusion=1,Radius=.5f });
            if(GraphicsDiagnostics.ExtraPasses!=2) throw new Exception("AO must use two bounded passes.");
            var occluded=Read(67,30);
            post.Render(source,0,size,size,true,1,false,depth,projection); var unoccluded=Read(67,30);
            if(occluded[0]>=unoccluded[0]) throw new Exception("Depth step did not produce contact shading.");
            var quality=new SkyEnvironment { Quality=GraphicsQuality.High,ProfileGraphicsGpu=true };
            for(int i=0;i<12;i++) { post.Render(source,0,size,size,true,1,true,depth,projection,quality.CaptureLook()); GL.Finish(); }
            if(GraphicsDiagnostics.ExtraPasses!=5 || GraphicsDiagnostics.PostGpuMs==null) throw new Exception("High pass budget/GPU timer failed.");
            Console.WriteLine($"Offscreen post GPU at 128x128: {GraphicsDiagnostics.PostGpuMs:0.###} ms (synthetic, not game FPS).");
            post.Render(source,0,size,size,false,5,true,depth,projection,quality.CaptureLook()); var legacy=Read(20,20);
            if(Math.Abs(legacy[0]-38)>1 || GraphicsDiagnostics.ExtraPasses!=0) throw new Exception("Legacy 2D must remain a raw copy.");
            post.Render(source,0,64,64,true,1,true,depth,projection,quality.CaptureLook());
            if(GL.GetError()!=ErrorCode.NoError) throw new Exception("OpenGL errors in graphics passes/resize.");
            // Exercise real depth/stencil attachment allocation and cleanup, not just synthetic textures.
            using var target=new RuntimePostProcessTarget3D(); target.Begin(64,64); target.Present(1,64,64,true,projection,quality.CaptureLook());
            if(GL.GetError()!=ErrorCode.NoError) throw new Exception("Runtime depth target error.");
            // Regional uploads must preserve neighbouring texels and caller pixel-store state.
            var map=new byte[4*4*4];Array.Fill(map,(byte)20);using(var texture=Texture2D.FromPixels(4,4,map)){
                var changed=(byte[])map.Clone();for(int channel=0;channel<4;channel++){changed[(1*4+2)*4+channel]=150;changed[(2*4+2)*4+channel]=170;}
                GL.PixelStore(PixelStoreParameter.UnpackRowLength,9);GL.PixelStore(PixelStoreParameter.UnpackSkipPixels,1);GL.PixelStore(PixelStoreParameter.UnpackSkipRows,2);
                texture.UpdateRegion(changed,2,1,1,2);
                GL.GetInteger(GetPName.UnpackRowLength,out int rows);GL.GetInteger(GetPName.UnpackSkipPixels,out int skipPixels);GL.GetInteger(GetPName.UnpackSkipRows,out int skipRows);
                if(rows!=9||skipPixels!=1||skipRows!=2)throw new Exception("Regional upload leaked unpack state");
                GL.PixelStore(PixelStoreParameter.UnpackRowLength,0);GL.PixelStore(PixelStoreParameter.UnpackSkipPixels,0);GL.PixelStore(PixelStoreParameter.UnpackSkipRows,0);
                var read=new byte[map.Length];texture.Bind();GL.GetTexImage(TextureTarget.Texture2D,0,PixelFormat.Rgba,PixelType.UnsignedByte,read);
                if(!read.SequenceEqual(changed))throw new Exception("Regional upload changed neighbouring pixels or skipped its intended region");
                Console.WriteLine("PASS Regional texture updates preserve neighbours and pixel-store state");
            }
            Benchmark(post,source,depth,projection);
            Console.WriteLine("Graphics offscreen: neutral defaults, bloom spread, colour grading, contact shading, resize, GPU queries and legacy 2D passed.");
        }
        finally { GL.DeleteTexture(source); GL.DeleteTexture(depth); }
    }
    private static void Benchmark(PostProcess3D post,int source,int depth,Matrix4x4 projection)
    {
        int fbo=GL.GenFramebuffer(),texture=GL.GenTexture();
        try
        {
            GL.ActiveTexture(TextureUnit.Texture0); GL.BindTexture(TextureTarget.Texture2D,texture);
            GL.TexImage2D(TextureTarget.Texture2D,0,PixelInternalFormat.Rgba8,1920,1080,0,PixelFormat.Rgba,PixelType.UnsignedByte,IntPtr.Zero);
            GL.BindFramebuffer(FramebufferTarget.Framebuffer,fbo);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer,FramebufferAttachment.ColorAttachment0,TextureTarget.Texture2D,texture,0);
            if(GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer)!=FramebufferErrorCode.FramebufferComplete) throw new Exception("Benchmark framebuffer.");
            foreach(var q in new[] { GraphicsQuality.Fast,GraphicsQuality.Balanced,GraphicsQuality.High })
            {
                var look=new SkyEnvironment { Quality=q,ProfileGraphicsGpu=true }.CaptureLook();
                double sum=0; int count=0;
                for(int i=0;i<40;i++)
                {
                    post.Render(source,fbo,1920,1080,true,1,true,depth,projection,look); GL.Finish();
                    if(i>=10 && GraphicsDiagnostics.PostGpuMs is {} ms) { sum+=ms; count++; }
                }
                Console.WriteLine($"Synthetic 1920x1080 {q}: post GPU average={sum/Math.Max(1,count):0.###} ms, extra passes={GraphicsDiagnostics.ExtraPasses}. Excludes geometry/shadows.");
            }
            if(GL.GetError()!=ErrorCode.NoError) throw new Exception("Benchmark GL errors.");
        }
        finally { GL.BindFramebuffer(FramebufferTarget.Framebuffer,0); GL.DeleteFramebuffer(fbo); GL.DeleteTexture(texture); }
    }
}
