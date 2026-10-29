using System.Text.Json;
using ByteEngine.Core.Animation;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Navigation;
using ByteEngine.Core.Vfx;
using System.Runtime.InteropServices.JavaScript;

public static partial class BrowserGame
{
    private static bool _captureComponents;
    private static bool _captureAccumulate;

    [JSExport]
    public static void CaptureProfileComponents(bool enabled)
    {
        _captureComponents=enabled;_captureAccumulate=enabled;
        if(Loop.Scenes.ActiveScene is {} scene){scene.ComponentProfiler.Enabled=enabled;scene.ComponentProfiler.AccumulateFrames=enabled;scene.ComponentProfiler.ClearSamples();}
    }

    [JSExport]
    public static string PerformanceDetails()
    {
        var scene=Loop.Scenes.ActiveScene;
        if(scene==null)return "{}";
        var components=scene.GameObjects.SelectMany(o=>o.Components).ToArray();
        var skins=components.OfType<SkeletalMeshRenderer>().Where(s=>s.Enabled&&s.GameObject.ActiveInHierarchy).ToArray();
        var effects=components.OfType<VfxPlayer>().ToArray();
        var agents=components.OfType<NavigationAgent3D>().ToArray();
        var p=scene.Physics;
        var details=new {
            scene=scene.Name,scene.SimulationTime,scene.SimulationTimeScale,scene.FixedSimulation,
            frame=scene.LastFrameMetrics,
            bridge=Sink.CaptureStats(),
            clock=new {scene.SimulationClock.StepSeconds,scene.SimulationClock.DroppedSeconds,scene.SimulationClock.InterpolationAlpha},
            memory=new {managedBytes=GC.GetTotalMemory(false),totalAllocatedBytes=GC.GetTotalAllocatedBytes(false),
                gc0=GC.CollectionCount(0),gc1=GC.CollectionCount(1),gc2=GC.CollectionCount(2)},
            objects=new {total=scene.GameObjects.Count,active=scene.GameObjects.Count(o=>o.ActiveInHierarchy),components=components.Length,
                enabledComponents=components.Count(c=>c.Enabled&&c.GameObject.ActiveInHierarchy)},
            physics=new {p.LastStepMilliseconds,p.LastColliderCount,p.LastCandidateCount,p.LastColliderGatherMs,p.LastIndexUpdateMs,p.LastCandidateQueryMs,
                p.LastCachedContactHits,p.LastStaticSolveReuseCount,contacts=p.Contacts.Count,p.SolverIterations,p.MaximumSubsteps},
            animation=new {renderers=skins.Length,playing=skins.Count(s=>s.IsPlaying),meshes=skins.Sum(s=>s.SkinnedMeshCount),
                modelVertices=skins.Sum(s=>s.ModelLoaded?s.ResolvedModel!.Meshes.Sum(m=>(long)m.Vertices.Length/8):0)},
            vfx=new {players=effects.Length,playing=effects.Count(v=>v.IsPlaying),particles=effects.Sum(v=>v.ActiveParticles),
                rendered=effects.Sum(v=>v.RenderedParticles),capacity=effects.Sum(v=>v.ParticleCapacity),dropped=effects.Sum(v=>v.DroppedParticles)},
            navigation=new {agents=agents.Length,moving=agents.Count(a=>a.Moving),pathQueries=agents.Sum(a=>(long)a.PathQueries),pathPoints=agents.Sum(a=>a.Path.Count),
                unreachable=agents.Count(a=>a.Status==NavigationAgentStatus.Unreachable)},
            profiler=new {enabled=scene.ComponentProfiler.Enabled,frames=scene.ComponentProfiler.Frames,droppedRows=scene.ComponentProfiler.DroppedDetailRows,
                objectRowsAvailable=scene.ComponentProfiler.ObjectRows,objectRowsOmitted=Math.Max(0,scene.ComponentProfiler.ObjectRows-256),systemRows=scene.ComponentProfiler.SystemRows,
                components=scene.ComponentProfiler.Snapshot(),objects=scene.ComponentProfiler.ObjectSnapshot(256),systems=scene.ComponentProfiler.SystemSnapshot()},
            componentCounts=components.GroupBy(c=>c.GetType().FullName??c.GetType().Name).Select(g=>new {type=g.Key,count=g.Count(),active=g.Count(c=>c.Enabled&&c.GameObject.ActiveInHierarchy)}).ToArray()
        };
        var result=JsonSerializer.Serialize(details);
        if(_captureAccumulate)scene.ComponentProfiler.ClearSamples();
        return result;
    }

    [JSExport]
    public static string PerformanceInventory()
    {
        var scene=Loop.Scenes.ActiveScene;if(scene==null)return "{}";
        // Inventory is explicit/on demand, not a repeated per-frame scene dump.
        return JsonSerializer.Serialize(new {scene=scene.Name,total=scene.GameObjects.Count,limit=20000,
            objects=scene.GameObjects.Take(20000).Select(o=>new {id=o.Id,name=o.Name,parent=o.Parent?.Id,active=o.ActiveInHierarchy,
                components=o.Components.Select(c=>new {type=c.GetType().FullName,c.Enabled,order=c.UpdateOrder}).ToArray()}).ToArray(),
            animations=scene.GameObjects.SelectMany(o=>o.Components.OfType<SkeletalMeshRenderer>()).Where(s=>s.ModelLoaded).Select(s=>new {
                id=s.GameObject.Id,name=s.GameObject.Name,s.CurrentAnimation,s.Speed,s.AnimationUpdateInterval,s.Visible,s.SkinnedMeshCount,
                model=s.ResolvedModel!.Name,nodes=s.ResolvedModel.Nodes.Count,vertices=s.ResolvedModel.Meshes.Sum(m=>(long)m.Vertices.Length/8),
                triangles=s.ResolvedModel.Meshes.Sum(m=>(long)m.Indices.Length/3)}).ToArray()});
    }
}
