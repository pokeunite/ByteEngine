using ByteEngine.Core;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Runtime;
using ByteEngine.Core.Physics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.InputSystem;

static class PerformanceProfilerChecks
{
    public static void Run()
    {
        using var loop=new PortableGameLoop();var scene=new Scene("Profiler lifecycle");
        scene.CreateGameObject("Camera").AddComponent(new Camera3D{ActiveGameCamera=true});
        scene.CreateGameObject("Measured worker").AddComponent(new Worker());loop.Scenes.LoadScene(scene);
        void Tick()=>loop.Tick(.016,new RawInputSnapshot(),320,200,new(.5f),true,false,new Sink());
        Tick();if(scene.ComponentProfiler.Snapshot().Length!=0)throw new Exception("Disabled profiler recorded work.");
        scene.ComponentProfiler.Enabled=true;
        scene.CreateGameObject("Self-removing callback").AddComponent(new RemoveSelf());
        Tick();var profiler=scene.ComponentProfiler;
        if(!profiler.ObjectSnapshot().Any(s=>s.Object=="Self-removing callback"))throw new Exception("Detached callback identity was lost.");
        if(!profiler.Snapshot().Any(s=>s.AllocatedBytes>=4096))throw new Exception("Callback allocations were not recorded.");
        if(!profiler.SystemSnapshot().Any(s=>s.Category=="Physics.Raycast"&&s.Calls==1))throw new Exception("Physics query timing missing.");
        if(!profiler.SystemSnapshot().Any(s=>s.Category=="Fixture.Work"))throw new Exception("System scope missing.");
        for(int i=0;i<1100;i++){using var scope=profiler.MeasureSystem("Bounded",i.ToString());}
        if(profiler.SystemRows>1024||profiler.DroppedDetailRows<76)throw new Exception("System profiler budget was not enforced.");
        Tick();if(profiler.ObjectSnapshot().Any(s=>s.Object=="Self-removing callback"))throw new Exception("Old frame retained removed object timing.");
        profiler.ClearSamples();profiler.AccumulateFrames=true;Tick();Tick();
        if(profiler.Frames!=2||!profiler.Snapshot().Any(s=>s.Phase=="Update"&&s.Calls==2))throw new Exception("Accumulated window lost callback frames.");
        profiler.Enabled=false;Tick();if(profiler.ObjectRows!=0||profiler.SystemRows!=0)throw new Exception("Disabled frame retained detail.");
        if(!double.IsFinite(loop.LastRenderMilliseconds)||loop.LastRenderAllocatedBytes<0)throw new Exception("Portable render metrics invalid.");
        Console.WriteLine("PASS: deep profiler identity, self-removal, timing/allocation scopes, physics query counters, budgets and reset lifecycle");
    }
    sealed class Worker:Component {
        protected override void OnUpdate(){using var scope=GameObject.Scene!.ComponentProfiler.MeasureSystem("Fixture.Work");
            GC.KeepAlive(new byte[4096]);GameplayQuery3D.Raycast(GameObject.Scene!,System.Numerics.Vector3.Zero,System.Numerics.Vector3.UnitZ,out _,1);}
    }
    sealed class RemoveSelf:Component {protected override void OnUpdate()=>GameObject.RemoveComponent(this);}
}
