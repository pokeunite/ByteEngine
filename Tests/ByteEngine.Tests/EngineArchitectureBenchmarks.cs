using System.Diagnostics;
using System.Numerics;
using System.Text.Json;
using ByteEngine.Core;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Physics;
using ByteEngine.Core.Scene;
namespace ByteEngine.Tests;

// Engine-only workloads: no game assets, plugins, AI, native graphics or special-case tuning.
internal static class EngineArchitectureBenchmarks
{
    public static void Run(string output)
    {
        EngineAllocationRegressionTests.Run();
        var results=new List<Sample>();
        foreach(int count in new[]{1000,10000})
        {
            var scene=new Scene("Idle components");
            for(int i=0;i<count;i++)scene.CreateGameObject("Object").AddComponent(new IdleProbe());
            Measure("idle-components",count,scene,results);
        }
        foreach(int count in new[]{1000,5000})
        {
            var scene=new Scene("Hierarchy transforms");
            var root=scene.CreateGameObject("Root");
            root.Transform.LocalRotation=Quaternion.CreateFromAxisAngle(Vector3.UnitY,.3f);
            for(int i=0;i<count;i++)
            {
                var group=scene.CreateGameObject("Group");group.SetParent(root,false);
                var obj=scene.CreateGameObject("Child");obj.SetParent(group,false);
                obj.Transform.LocalPosition=new(i%50,0,i/50);obj.AddComponent(new TransformProbe());
            }
            Measure("hierarchy-transform-reads",count,scene,results);
        }
        foreach(int count in new[]{100,500,1000})
        {
            var scene=new Scene("Separated static colliders");
            for(int i=0;i<count;i++)
            {
                var obj=scene.CreateGameObject("Box");obj.Transform.LocalPosition=new((i%40)*3,0,(i/40)*3);
                obj.AddComponent(new BoxCollider3D());
            }
            Measure("separated-static-colliders",count,scene,results);
        }
        foreach(int count in new[]{100,500})
        {
            var scene=new Scene("UI buttons");
            for(int i=0;i<count;i++)scene.CreateGameObject("Button").AddComponent(new UiWidget{Kind=UiWidgetKind.Button});
            Measure("ui-buttons-update-only",count,scene,results);
        }
        string path=Path.GetFullPath(output);Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path,JsonSerializer.Serialize(new{capturedUtc=DateTime.UtcNow,framework=System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,logicalProcessors=Environment.ProcessorCount,scope="CPU scene updates only; no rendering, GPU, or game-specific code",warmupFrames=24,measuredFrames=120,results},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine("Saved engine-only benchmark: "+path);
    }
    private static void Measure(string scenario,int count,Scene scene,List<Sample> results)
    {
        scene.LoadInternal();
        for(int i=0;i<24;i++){Time.Update(1.0/60);scene.UpdateInternal();}
        var times=new double[120];long allocated=GC.GetAllocatedBytesForCurrentThread();
        int gen0=GC.CollectionCount(0);
        for(int i=0;i<times.Length;i++)
        {
            Time.Update(1.0/60);long start=Stopwatch.GetTimestamp();scene.UpdateInternal();
            times[i]=Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        }
        long bytes=GC.GetAllocatedBytesForCurrentThread()-allocated;int collections=GC.CollectionCount(0)-gen0;
        Array.Sort(times);var result=new Sample(scenario,count,scene.GameObjectCount,times.Average(),times[60],times[113],bytes/120.0,collections);results.Add(result);
        Console.WriteLine($"{scenario,-30} {count,6}: median {result.MedianMs,7:F3} ms, p95 {result.P95Ms,7:F3} ms, allocated {result.AllocatedBytesPerFrame/1024,8:F1} KiB/frame");
        scene.UnloadInternal();
    }
    private sealed class IdleProbe:Component { }
    private sealed class TransformProbe:Component
    {
        public Matrix4x4 Observed;
        protected override void OnUpdate(){Observed=Transform.WorldMatrix;_ = Transform.WorldPosition;}
    }
    private sealed record Sample(string Scenario,int Count,int SceneObjects,double MeanMs,double MedianMs,double P95Ms,double AllocatedBytesPerFrame,int Gen0Collections);
}
