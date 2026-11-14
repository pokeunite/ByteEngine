using ByteEngine.Core.Scene;

namespace ByteEngine.Tests;
internal static class FixedHookRegressionTests
{
    private sealed class Decoration:Component { public int Starts; protected override void OnStart()=>Starts++; }
    private class Counter(List<int> calls,int id):Component
    {
        public int Rank;
        public override int UpdateOrder=>Rank;
        public Action? DuringTick;
        protected override void OnFixedUpdate(){calls.Add(id);DuringTick?.Invoke();}
    }
    private sealed class Inherited(List<int> calls,int id):Counter(calls,id);
    private class FrameCounter(List<int> calls,int id):Component
    {
        public int Rank;public override int UpdateOrder=>Rank;public Action? DuringUpdate;
        protected override void OnUpdate(){calls.Add(id);DuringUpdate?.Invoke();}
        protected override void OnLateUpdate()=>calls.Add(id+10);
    }
    private sealed class InheritedFrame(List<int> calls,int id):FrameCounter(calls,id);
    public static void Run()
    {
        void Check(bool v,string text){if(!v)throw new Exception(text);Console.WriteLine("PASS "+text);}
        var o=new GameObject("Fixed hooks");var decoration=o.AddComponent(new Decoration());o.FixedUpdateInternal();o.FixedUpdateInternal();
        Check(decoration.Starts==1&&!decoration.ImplementsFixedUpdate,"Skipping an empty fixed hook preserves startup");
        var calls=new List<int>();var a=o.AddComponent(new Counter(calls,1){Rank=2});var b=o.AddComponent(new Inherited(calls,2){Rank=1});
        o.FixedUpdateInternal();Check(calls.SequenceEqual(new[]{2,1})&&b.ImplementsFixedUpdate,"Inherited fixed hooks retain execution order");
        calls.Clear();a.Rank=0;o.FixedUpdateInternal();Check(calls.SequenceEqual(new[]{1,2}),"Live fixed-hook priority changes remain visible");
        calls.Clear();a.Enabled=false;o.FixedUpdateInternal();Check(calls.SequenceEqual(new[]{2}),"Disabled hooks are skipped");
        calls.Clear();o.Active=false;o.FixedUpdateInternal();Check(calls.Count==0,"Inactive objects remain inactive");o.Active=true;
        o.RemoveComponent(b);calls.Clear();o.FixedUpdateInternal();Check(calls.Count==0,"Removed hooks leave the cached inventory");
        a.Enabled=true;bool added=false;a.DuringTick=()=>{if(!added){added=true;o.AddComponent(new Counter(calls,3));}};
        o.FixedUpdateInternal();Check(calls.SequenceEqual(new[]{1}),"New hooks do not enter the current tick snapshot");
        calls.Clear();o.FixedUpdateInternal();Check(calls.SequenceEqual(new[]{1,3}),"New hooks enter the next tick");
        var frameCalls=new List<int>();var f=new GameObject("Frame hooks");var idle=f.AddComponent(new Decoration());
        f.UpdateInternal();f.LateUpdateInternal();Check(idle.Starts==1&&!idle.ImplementsUpdate&&!idle.ImplementsLateUpdate,"Empty frame hooks retain startup");
        var u=f.AddComponent(new FrameCounter(frameCalls,1){Rank=2});var v=f.AddComponent(new InheritedFrame(frameCalls,2){Rank=1});
        f.UpdateInternal();f.LateUpdateInternal();Check(frameCalls.SequenceEqual(new[]{2,1,12,11}),"Inherited update and late hooks preserve phase order");
        frameCalls.Clear();u.Rank=0;v.Enabled=false;f.UpdateInternal();f.LateUpdateInternal();Check(frameCalls.SequenceEqual(new[]{1,11}),"Live priorities and disabled frame hooks are honored");
        bool spawned=false;u.DuringUpdate=()=>{if(!spawned){spawned=true;f.AddComponent(new FrameCounter(frameCalls,3));}};
        frameCalls.Clear();f.UpdateInternal();Check(frameCalls.SequenceEqual(new[]{1}),"Added update hooks wait for the next update snapshot");
        frameCalls.Clear();f.LateUpdateInternal();Check(frameCalls.SequenceEqual(new[]{11,13}),"A later phase observes newly attached late hooks");
        Check(ReferenceEquals(f.FindComponentByName(nameof(FrameCounter)),u),"Component-name cache finds the first matching component");
        Check(f.FindComponentByName("missing")==null,"Component-name cache handles misses");
        f.RemoveComponent(u);Check(f.FindComponentByName(nameof(FrameCounter))!=u,"Component-name cache invalidates on removal");
        f.ReorderComponents(f.Components.Reverse().ToArray());Check(f.FindComponentByName(nameof(FrameCounter))!=null,"Component-name cache remains valid after reorder");
        var tickCalls=new List<int>();var scene=new ByteEngine.Core.Scene.Scene("Fixed dispatch"){FixedSimulation=true};scene.SimulationClock.StepSeconds=.01;
        var first=scene.CreateGameObject("First");var later=scene.CreateGameObject("Later");later.AddComponent(new Decoration());
        var driver=first.AddComponent(new Counter(tickCalls,1));Counter? attached=null;GameObject? spawnedObject=null;
        driver.DuringTick=()=>{if(attached!=null)return;attached=later.AddComponent(new Counter(tickCalls,2));spawnedObject=scene.CreateGameObject("Spawned");spawnedObject.AddComponent(new Counter(tickCalls,3));};
        scene.LoadInternal();
        try{
            ByteEngine.Core.Time.Update(.02);scene.UpdateInternal();
            Check(tickCalls.SequenceEqual(new[]{1,2,1,2}),"Fixed dispatch observes hooks attached to later objects within the same tick");
            tickCalls.Clear();ByteEngine.Core.Time.Update(.01);scene.UpdateInternal();
            Check(tickCalls.SequenceEqual(new[]{1,2,3}),"Objects created during a tick enter the next frame snapshot");
            later.RemoveComponent(attached!);tickCalls.Clear();ByteEngine.Core.Time.Update(.01);scene.UpdateInternal();
            Check(tickCalls.SequenceEqual(new[]{1,3}),"Fixed dispatch drops removed hooks");
            driver.DuringTick=()=>{spawnedObject!.Active=false;later.AddComponent(new Counter(tickCalls,4));driver.DuringTick=null;};
            tickCalls.Clear();ByteEngine.Core.Time.Update(.01);scene.UpdateInternal();
            Check(tickCalls.SequenceEqual(new[]{1,4}),"Fixed dispatch preserves live activity and scene traversal order");
        }finally{scene.UnloadInternal();}
    }
}
