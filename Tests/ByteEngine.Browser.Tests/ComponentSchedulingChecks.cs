using System.Reflection;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.InputSystem;
using ByteEngine.Core.Runtime;
using ByteEngine.Core.Scene;

static class ComponentSchedulingChecks
{
    internal static void Run()
    {
        static void Check(bool ok, string text) { if (!ok) throw new Exception(text); Console.WriteLine("PASS: " + text); }
        var holder = new GameObject("Lookup cache");
        var empty1 = holder.AddComponent(new Empty()); var empty2 = holder.AddComponent(new Empty());
        Check(holder.GetComponent<LookupBase>() == null && holder.GetComponent<LookupBase>() == null, "missing component lookup is repeatable");
        var first = holder.AddComponent(new LookupDerived());
        Check(holder.GetComponent<LookupBase>() == first && holder.GetComponent<LookupDerived>() == first, "adding a component invalidates negative lookups and respects inheritance");
        var second = holder.AddComponent(new LookupOther());
        Check(holder.GetComponent<LookupBase>() == first, "lookup preserves first authored matching component");
        // This is the editor's internal reordering API, not a test-specific production API.
        typeof(GameObject).GetMethod("ReorderComponents", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(holder, [new Component[] { second, empty1, first, empty2 }]);
        Check(holder.GetComponent<LookupBase>() == second, "reordering invalidates cached first-match selection");
        holder.RemoveComponent(second);
        Check(holder.GetComponent<LookupBase>() == first, "removal retires a cached component");
        holder.RemoveComponent(first);
        Check(holder.GetComponent<LookupBase>() == null, "removing the last matching component invalidates a positive lookup");

        using var loop = new PortableGameLoop();
        var scene = new Scene("Cached callback schedules") { FixedSimulation = true };
        var owner = new GameObject("Callbacks"); var calls = new List<string>();
        var a = owner.AddComponent(new Counter("A", 5, calls));
        var b = owner.AddComponent(new Counter("B", -5, calls));
        var c = owner.AddComponent(new Counter("C", 5, calls));
        owner.AddComponent(new Empty()); scene.AddGameObject(owner); loop.Scenes.LoadScene(scene);
        var sink = new EmptySink(); var input = new RawInputSnapshot();
        void Tick() { calls.Clear(); loop.Tick(1d/60, input, 640, 360, new(.5f), true, false, sink); }
        string Phase(string prefix) => string.Join(',', calls.Where(s=>s.StartsWith(prefix)).Select(s=>s[2..]));
        Tick(); Check(Phase("U:") == "B,A,C" && Phase("F:") == "B,A,C" && Phase("L:") == "B,A,C", "all cached phases preserve priority and equal-order stability");
        Tick(); Check(Phase("U:") == "B,A,C" && a.Starts == 1 && b.Starts == 1 && c.Starts == 1, "reusing schedules does not restart components");
        a.Order = -10;
        Tick(); Check(Phase("U:") == "A,B,C" && Phase("F:") == "A,B,C" && Phase("L:") == "A,B,C", "runtime priority changes refresh every callback phase");
        b.Enabled = false;
        Tick(); Check(Phase("U:") == "A,C" && Phase("L:") == "A,C", "cached schedules still respect runtime disabling");
        b.Enabled = true; a.Order = 5;
        Counter? added = null; b.Once = () => added = owner.AddComponent(new Counter("D", -20, calls));
        Tick(); Check(Phase("U:") == "B,A,C" && Phase("L:") == "D,B,A,C" && Phase("F:") == "D,B,A,C", "component creation during Update preserves its snapshot and refreshes later phases");
        Tick(); Check(Phase("U:") == "D,B,A,C" && added!.Starts == 1, "new callbacks join the next Update without a repeated start");
        owner.RemoveComponent(added!);
        Tick(); Check(Phase("U:") == "B,A,C" && Phase("F:") == "B,A,C" && Phase("L:") == "B,A,C", "removing a callback invalidates all schedules");
        typeof(GameObject).GetMethod("ReorderComponents", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(owner, [new Component[] { c, b, a, owner.Components.Last() }]);
        Tick(); Check(Phase("U:") == "B,C,A" && Phase("L:") == "B,C,A", "editor reordering updates equal-priority callback order");
    }
    sealed class Empty : Component { }
    class LookupBase : Component { }
    sealed class LookupDerived : LookupBase { }
    sealed class LookupOther : LookupBase { }
    sealed class Counter(string name, int order, List<string> calls) : Component
    {
        public int Order = order, Starts;
        public Action? Once;
        public override int UpdateOrder => Order;
        protected override void OnStart() => Starts++;
        protected override void OnUpdate() { calls.Add("U:"+name); var once=Once; Once=null; once?.Invoke(); }
        protected override void OnFixedUpdate() => calls.Add("F:"+name);
        protected override void OnLateUpdate() => calls.Add("L:"+name);
    }
    sealed class EmptySink : IRenderFrameSink
    {
        public void Draw3D(RenderView3D? view, RenderLighting3D light, RenderEnvironment3D environment, IReadOnlyList<RenderSubmission> submissions) { }
        public void DrawText(string text,string? font,int size,System.Numerics.Vector2 position,System.Numerics.Vector4 color,float wrap,UiAnchor anchor) { }
        public void DrawQuad(System.Numerics.Vector2 position,System.Numerics.Vector2 size,System.Numerics.Vector4 color) { }
        public void DrawImage(Texture2D texture,System.Numerics.Vector2 position,System.Numerics.Vector2 size,System.Numerics.Vector4 color) { }
    }
}
