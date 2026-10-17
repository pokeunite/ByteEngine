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
    }
}
