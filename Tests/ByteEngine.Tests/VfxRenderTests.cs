using System.Numerics;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Vfx;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;

namespace ByteEngine.Tests;

/// <summary>Invisible offscreen GL check, not editor UI automation.</summary>
internal static class VfxRenderTests
{
    public static void Run(string root)
    {
        using var window=new NativeWindow(new NativeWindowSettings {
            ClientSize=new OpenTK.Mathematics.Vector2i(128,128), StartVisible=false,
            API=ContextAPI.OpenGL, APIVersion=new Version(3,3), Profile=ContextProfile.Core, Title="VFX offscreen test"
        });
        window.Context.MakeCurrent(); GL.LoadBindings(new OpenTK.Windowing.GraphicsLibraryFramework.GLFWBindingsContext());
        using var renderer=new Renderer3D(); using var renderer2D=new Renderer2D();
        var scene=new Scene("VFX offscreen");
        var player=scene.CreateGameObject("VFX").AddComponent(new VfxPlayer { PlayOnStart=false });
        var effect=new VfxEffect { Layers=new() { new VfxLayer {
            Burst=1,Rate=0,Speed=0,Gravity=Vector3.Zero,Lifetime=1,StartSize=1,EndSize=1,StartColor=new(1,0,0,1),EndColor=new(1,0,0,1)
        }} };
        player.SetDefinition(effect); player.Play();
        var context=new RenderContext(renderer2D,renderer,scene,128,128,
            viewMatrix3D:Matrix4x4.CreateLookAt(new(0,0,3),Vector3.Zero,Vector3.UnitY),
            projectionMatrix3D:Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI/3,1,.01f,100),
            prepareEnvironmentLighting3D:false,renderShadows3D:false);
        GL.Viewport(0,0,128,128); GL.ClearColor(0,0,0,1); GL.Clear(ClearBufferMask.ColorBufferBit|ClearBufferMask.DepthBufferBit);
        context.Begin3DFrame(); player.RenderEditorInternal(context);
        if(context.RenderWorld.SubmissionCount!=1) throw new Exception("Expected one VFX draw submission.");
        context.Flush3D(); GL.Finish();
        byte[] pixel=new byte[4]; GL.ReadPixels(64,64,1,1,PixelFormat.Rgba,PixelType.UnsignedByte,pixel);
        if(pixel[0]<80 || pixel[1]>30) throw new Exception($"VFX sprite not rendered: {string.Join(',',pixel)}");
        if(GL.GetError()!=ErrorCode.NoError) throw new Exception("OpenGL error during VFX rendering.");
        // A desktop-loaded BMP exercises the one-time decode (PixelData is not retained on desktop).
        string path=Path.Combine(root,"Assets","vfx-sprite.bmp");
        byte[] bmp=new byte[70]; bmp[0]=66; bmp[1]=77;
        BitConverter.GetBytes(70).CopyTo(bmp,2); BitConverter.GetBytes(54).CopyTo(bmp,10); BitConverter.GetBytes(40).CopyTo(bmp,14);
        BitConverter.GetBytes(2).CopyTo(bmp,18); BitConverter.GetBytes(2).CopyTo(bmp,22); bmp[26]=1; bmp[28]=24;
        for(int y=0;y<2;y++) for(int x=0;x<2;x++) bmp[54+y*8+x*3+1]=255;
        File.WriteAllBytes(path,bmp);
        var texture=new Texture2D(path);
        try
        {
            var pixels=VfxAtlas.GeneratePixels(new VfxLayer { StartColor=Vector4.One,EndColor=Vector4.One },texture,out int w,out _);
            int n=(16*w+16)*4;
            if(pixels[n+1]<240 || pixels[n]>10) throw new Exception("Desktop custom VFX sprite decode failed.");
        }
        finally { texture.Dispose(); scene.DestroyGameObject(player.GameObject); }
        Console.WriteLine("VFX offscreen GL: billboard pixels, batched draw, and custom desktop texture passed.");
    }
}
