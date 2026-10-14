using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using ByteEngine.Core;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Diagnostics;
using ByteEngine.Editor;
namespace KarlsonDemoTool;
internal static class MilkDepotEditorPerformance
{
    private static Dictionary<string,object> PanelSnapshot()
    {
        var result=new Dictionary<string,object>();
        var type=typeof(EditorApplication).Assembly.GetType("ByteEngine.Editor.EditorFrameProfile");
        if(type?.GetProperty("Panels")?.GetValue(null) is System.Collections.IDictionary panels)
            foreach(System.Collections.DictionaryEntry panel in panels)
            {
                var value=panel.Value!;var metric=value.GetType();
                result[(string)panel.Key]=new{Milliseconds=Convert.ToDouble(metric.GetProperty("Milliseconds")!.GetValue(value)),AllocatedBytes=Convert.ToInt64(metric.GetProperty("AllocatedBytes")!.GetValue(value))};
            }
        return result;
    }
    internal static void Run()
    {
        using var editor=new EditorApplication(Program.Root+"/karlson demo.byteproject");
        int frames=0;long last=0;var samples=new List<object>();
        editor.Load+=()=>{editor.IsVisible=Environment.GetEnvironmentVariable("BYTE_PERF_VISIBLE")=="1";editor.VSync=OpenTK.Windowing.Common.VSyncMode.Off;last=Stopwatch.GetTimestamp();};
        editor.UpdateFrame+=_=>
        {
            frames++;long now=Stopwatch.GetTimestamp();double wall=Stopwatch.GetElapsedTime(last,now).TotalMilliseconds;last=now;
            if(frames==1 && Environment.GetEnvironmentVariable("BYTE_PERF_VSYNC_OFF")=="1")editor.VSync=OpenTK.Windowing.Common.VSyncMode.Off;
            if(frames==2)
            {
                typeof(EditorApplication).GetMethod("EnterPlayMode",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(editor,null);
                editor.Scenes.ActiveScene!.GameObjects.Single(o=>o.Name=="FPS Character").Transform.WorldPosition=new(-21,.08f,10);
                editor.Scenes.ActiveScene.FindComponent<SkyEnvironment>()!.ProfileGraphicsGpu=Environment.GetEnvironmentVariable("BYTE_PERF_GPU_PROFILE")!="0";
            }
            if(frames>=60 && Environment.GetEnvironmentVariable("BYTE_PERF_PAN")=="1")
                editor.Scenes.ActiveScene!.GameObjects.Single(o=>o.Name=="FPS Character").GetComponent<ByteEngine.Core.Gameplay.PlayerController3D>()!.ControlYaw=(frames-60)*1.2f;
            if(frames==60 && Environment.GetEnvironmentVariable("BYTE_PERF_SCENE_FOCUS")=="1")
            {
                var sceneView=typeof(EditorApplication).GetField("_sceneView",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(editor)!;
                sceneView.GetType().GetMethod("RequestFocus")!.Invoke(sceneView,null);
            }
            if(frames>60)samples.Add(new{frame=frames,wallMs=wall,simulation=editor.Scenes.ActiveScene!.LastFrameMetrics,renderCpuMs=ByteEngineApplication.LastRenderCpuMilliseconds,swapMs=ByteEngineApplication.LastSwapMilliseconds,panels=PanelSnapshot(),vsync=editor.VSync.ToString(),passes=GraphicsDiagnostics.Passes.ToArray()});
            if(frames==300 && Environment.GetEnvironmentVariable("BYTE_PERF_CAPTURE")=="1")
            {
                var pixels=new byte[editor.FramebufferSize.X*editor.FramebufferSize.Y*4];
                OpenTK.Graphics.OpenGL4.GL.BindFramebuffer(OpenTK.Graphics.OpenGL4.FramebufferTarget.Framebuffer,0);
                OpenTK.Graphics.OpenGL4.GL.ReadBuffer(OpenTK.Graphics.OpenGL4.ReadBufferMode.Front);
                OpenTK.Graphics.OpenGL4.GL.ReadPixels(0,0,editor.FramebufferSize.X,editor.FramebufferSize.Y,OpenTK.Graphics.OpenGL4.PixelFormat.Rgba,OpenTK.Graphics.OpenGL4.PixelType.UnsignedByte,pixels);
                OpenTK.Graphics.OpenGL4.GL.ReadBuffer(OpenTK.Graphics.OpenGL4.ReadBufferMode.Back);
                DemoApplication.Png(".artifacts/editor-colliders-hidden.png",pixels,editor.FramebufferSize.X,editor.FramebufferSize.Y);
            }
            if(frames%30==0)Console.WriteLine("EDITOR PERF "+frames+" "+wall.ToString("F1")+" ms; shadows="+typeof(Renderer3D).GetProperty("DirectionalShadowCacheStatus")?.GetValue(editor.Renderer3D));
            if(frames==360)
            {
                var label=Environment.GetEnvironmentVariable("BYTE_PERF_LABEL")??"final";
                File.WriteAllText(".artifacts/milk-editor-performance-"+label+".json",JsonSerializer.Serialize(samples,new JsonSerializerOptions{WriteIndented=true}));
                GraphicsDiagnostics.ExportCapture(".artifacts/milk-editor-graphics-"+label+".json",editor.Scenes.ActiveScene!);
                typeof(EditorApplication).GetMethod("StopPlayMode",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(editor,null);editor.Close();
            }
        };
        editor.Run();
    }
}
