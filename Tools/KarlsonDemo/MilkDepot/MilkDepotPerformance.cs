using System.Diagnostics;
using System.Numerics;
using System.Text.Json;
using ByteEngine.Core;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Diagnostics;
namespace KarlsonDemoTool;
internal sealed class MilkDepotPerformance:ByteEngineApplication
{
    DemoRuntime? _runtime;int _frame;long _last;readonly List<object> _samples=[];
    public MilkDepotPerformance():base(1280,720,"Milk Depot performance capture"){}
    protected override bool CloseOnEscape=>false;
    protected override void OnLoad()
    {
        base.OnLoad();IsVisible=false;VSync=Environment.GetEnvironmentVariable("BYTE_PERF_VSYNC_ON")=="1"?OpenTK.Windowing.Common.VSyncMode.On:OpenTK.Windowing.Common.VSyncMode.Off;_runtime=new();
        var scene=_runtime.Serializer.Load(Program.Root+"/Scenes/Milk Depot.bytescene");
        scene.GameObjects.Single(o=>o.Name=="FPS Character").Transform.WorldPosition=new(-21,.08f,10);
        scene.FindComponent<SkyEnvironment>()!.ProfileGraphicsGpu=true;
        Scenes.LoadScene(scene);_last=Stopwatch.GetTimestamp();
    }
    protected override void OnEngineUpdate()
    {
        _frame++;
        if(Environment.GetEnvironmentVariable("BYTE_PERF_PAN")=="1")
            Scenes.ActiveScene!.GameObjects.Single(o=>o.Name=="FPS Character").GetComponent<ByteEngine.Core.Gameplay.PlayerController3D>()!.ControlYaw=(_frame-30)*3f;
        long now=Stopwatch.GetTimestamp();double wall=Stopwatch.GetElapsedTime(_last,now).TotalMilliseconds;_last=now;
        if(_frame>30)_samples.Add(new{frame=_frame,wallMs=wall,simulation=Scenes.ActiveScene!.LastFrameMetrics,renderCpuMs=LastRenderCpuMilliseconds,swapMs=LastSwapMilliseconds,physics=new {stepMs=Diagnostic("LastStepMilliseconds"),gather=Scenes.ActiveScene.Physics.LastColliderGatherMs,index=Scenes.ActiveScene.Physics.LastIndexUpdateMs,candidates=Scenes.ActiveScene.Physics.LastCandidateQueryMs,hits=Diagnostic("LastCachedContactHits"),contacts=Scenes.ActiveScene.Physics.Contacts.Count},passes=GraphicsDiagnostics.Passes.ToArray()});
        if(_frame%10==0)Console.WriteLine("PERF frame "+_frame+" "+wall.ToString("F1")+" ms");
        if(_frame==150)
        {
            string label=Environment.GetEnvironmentVariable("BYTE_PERF_LABEL")??"baseline";
            File.WriteAllText(".artifacts/milk-performance-"+label+".json",JsonSerializer.Serialize(_samples,new JsonSerializerOptions{WriteIndented=true}));
            GraphicsDiagnostics.ExportCapture(".artifacts/milk-graphics-"+label+".json",Scenes.ActiveScene!);Close();
        }
    }
    double Diagnostic(string name)=>Convert.ToDouble(Scenes.ActiveScene!.Physics.GetType().GetProperty(name)?.GetValue(Scenes.ActiveScene.Physics)??0);
    protected override void OnUnload(){Scenes.UnloadScene();_runtime?.Dispose();base.OnUnload();}
}

