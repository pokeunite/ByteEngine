using System.Numerics;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Vfx;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;

namespace ByteEngine.Tests;

internal static class VfxGallery
{
    internal static void Run()
    {
        const int size=320;
        using var window=new NativeWindow(new NativeWindowSettings{ClientSize=new(size,size),StartVisible=false,API=ContextAPI.OpenGL,APIVersion=new(3,3),Profile=ContextProfile.Core});
        window.Context.MakeCurrent();GL.LoadBindings(new OpenTK.Windowing.GraphicsLibraryFramework.GLFWBindingsContext());
        using var renderer=new Renderer3D();using var sprites=new Renderer2D();
        using var gallery=new System.Drawing.Bitmap(size*3,size*2);using var canvas=System.Drawing.Graphics.FromImage(gallery);
        using var font=new System.Drawing.Font(System.Drawing.FontFamily.GenericSansSerif,12);
        var scene=new Scene("VFX gallery");var player=scene.CreateGameObject("Preview").AddComponent(new VfxPlayer{PlayOnStart=false,DistanceQuality=false});
        var examples=new[]{(VfxPreset.Fireball,.55f),(VfxPreset.WaterBolt,.55f),(VfxPreset.LightningStrike,.075f),(VfxPreset.ClawSlash,.085f),(VfxPreset.HealingPulse,.4f),(VfxPreset.PoisonCloud,1.2f)};
        for(int index=0;index<examples.Length;index++)
        {
            var (preset,time)=examples[index];player.SetDefinition(VfxPresets.Create(preset));player.Play();
            float width=preset is VfxPreset.Fireball or VfxPreset.WaterBolt?1.8f:preset==VfxPreset.ClawSlash?2.5f:4.2f;
            float center=preset==VfxPreset.LightningStrike?1.25f:.35f;
            var context=new RenderContext(sprites,renderer,scene,size,size,viewMatrix3D:Matrix4x4.CreateLookAt(new(0,center+2.5f,5),new(0,center,0),Vector3.UnitY),projectionMatrix3D:Matrix4x4.CreateOrthographic(width,width,.01f,100),prepareEnvironmentLighting3D:false,renderShadows3D:false);
            for(int i=0;i<(int)(time*60);i++)player.Advance(1f/60);
            GL.Viewport(0,0,size,size);GL.ClearColor(.035f,.045f,.065f,1);GL.Clear(ClearBufferMask.ColorBufferBit|ClearBufferMask.DepthBufferBit);
            context.Begin3DFrame();player.RenderEditorInternal(context);context.Flush3D();GL.Finish();
            if(GL.GetError()!=ErrorCode.NoError)throw new Exception("VFX gallery OpenGL failure: "+preset);
            var pixels=new byte[size*size*4];GL.ReadPixels(0,0,size,size,PixelFormat.Bgra,PixelType.UnsignedByte,pixels);
            using var tile=new System.Drawing.Bitmap(size,size,System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            var locked=tile.LockBits(new(0,0,size,size),System.Drawing.Imaging.ImageLockMode.WriteOnly,tile.PixelFormat);System.Runtime.InteropServices.Marshal.Copy(pixels,0,locked.Scan0,pixels.Length);tile.UnlockBits(locked);tile.RotateFlip(System.Drawing.RotateFlipType.RotateNoneFlipY);
            canvas.DrawImageUnscaled(tile,index%3*size,index/3*size);canvas.DrawString(preset.ToString(),font,System.Drawing.Brushes.White,index%3*size+14,index/3*size+size-30);
            Console.WriteLine("PASS native layered effect rendered: "+preset);
        }
        scene.DestroyGameObject(player.GameObject);
        string directory=Path.GetFullPath("artifacts/vfx-upgrade");Directory.CreateDirectory(directory);string output=Path.Combine(directory,"native-vfx-gallery.png");gallery.Save(output);Console.WriteLine(output);
    }
}
