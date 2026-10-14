using System.Numerics;
using System.Reflection;
using System.Text.Json;
using ByteEngine.Core;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using OpenTK.Graphics.OpenGL4;
namespace KarlsonDemoTool;
internal sealed class MilkDepotOcclusionCheck : ByteEngineApplication
{
    DemoRuntime? _runtime;int _frame;readonly List<object> _results=new();
    public MilkDepotOcclusionCheck():base(1280,720,"Milk Depot visibility validation"){}
    protected override void OnLoad(){base.OnLoad();IsVisible=false;VSync=OpenTK.Windowing.Common.VSyncMode.Off;_runtime=new();Scenes.LoadScene(_runtime.Serializer.Load(Program.Root+"/Scenes/Milk Depot.bytescene"));}
    protected override void OnEngineUpdate(){if(++_frame==3)Validate();if(_frame==4)Close();}
    private void Validate()
    {
        var scene=Scenes.ActiveScene!;var player=scene.GameObjects.Single(o=>o.Name=="FPS Character");player.Transform.WorldPosition=new(-21,.08f,10);
        var referenceWorld=new RenderWorld{EnableOcclusionCulling=false};var culledWorld=new RenderWorld{EnableOcclusionCulling=true};
        byte[] Draw(RenderWorld world)
        {
            GL.BindFramebuffer(FramebufferTarget.Framebuffer,0);GL.Viewport(0,0,1280,720);GL.ClearColor(.055f,.065f,.085f,1);GL.Clear(ClearBufferMask.ColorBufferBit|ClearBufferMask.DepthBufferBit);
            var context=new RenderContext(Renderer,Renderer3D,scene,1280,720,camera3D:scene.ActiveCamera,renderWorld:world);
            typeof(Scene).GetMethod("RenderInternal",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(scene,[context]);typeof(RenderContext).GetMethod("FlushUi",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(context,null);GL.Finish();
            var pixels=new byte[1280*720*4];GL.ReadPixels(0,0,1280,720,PixelFormat.Rgba,PixelType.UnsignedByte,pixels);return pixels;
        }
        for(int angle=0;angle<360;angle+=30)
        {
            player.GetComponent<PlayerController3D>()!.ControlYaw=angle;
            typeof(Time).GetMethod("Update",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,[1.0/60]);typeof(Scene).GetMethod("UpdateInternal",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(scene,null);
            var expected=Draw(referenceWorld);var actual=Draw(culledWorld);int different=0;for(int i=0;i<actual.Length;i++)if(Math.Abs(expected[i]-actual[i])>1)different++;
            if(different!=0)throw new Exception("Visibility changes "+different+" channels at angle "+angle);
            _results.Add(new{angle,occluded=culledWorld.OccludedMeshes,referenceTriangles=referenceWorld.MainTriangles,triangles=culledWorld.MainTriangles,visibilityMs=culledWorld.OcclusionMilliseconds});
            Console.WriteLine("PASS full Milk Depot GPU image angle "+angle+"; occluded "+culledWorld.OccludedMeshes);
        }
        File.WriteAllText(".artifacts/ue-audit-level-visibility.json",JsonSerializer.Serialize(_results,new JsonSerializerOptions{WriteIndented=true}));
    }
    protected override void OnUnload(){Scenes.UnloadScene();_runtime?.Dispose();base.OnUnload();}
}
