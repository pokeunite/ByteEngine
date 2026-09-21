using System.Reflection;
using System.Runtime.CompilerServices;
using ByteEngine.Core.Animation;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Variables;
using ByteEngine.Core.VisualLogic;

namespace ByteEngine.Tests;

internal static class C10FAnimationVisualLogicTests
{
    [ModuleInitializer]
    public static void Run()
    {
        Registry(); EventFiltering(); MultipleListeners(); Windows(); ResetLifecycle(); OrdinaryIsolation();
    }

    private static void Registry()
    {
        VisualLogicRegistry r=VisualLogicRegistry.CreateDefault();
        foreach(string id in new[]{"animation.eventFired","animation.windowEntered","animation.windowExited","animation.windowActive"})
            Assert(r.TryGetCondition(id,out _),$"registry exposes {id}");
    }

    private static void EventFiltering()
    {
        World w=Create(); VisualInstruction c=Signal("animation.eventFired","event","Footstep","Run","shoe");
        EventModuleDefinition m=Module(Rule(c,"hit")); w.Runtime.Update(m,w.Globals,w.Scene,w.Self);
        Assert(w.Count("hit")==0,"edge is false during polling");
        Raise(w.Controller,Event("Other","Run","run-key","shoe")); Assert(w.Count("hit")==0,"name mismatch blocked");
        Raise(w.Controller,Event("footstep","Run","run-key","shoe")); Assert(w.Count("hit")==1,"case-insensitive name match");
        c.Arguments["clip"]=EventValue.String("RUN-KEY");
        Raise(w.Controller,Event("Footstep","Other","run-key","shoe")); Assert(w.Count("hit")==2,"stable key match");
        Raise(w.Controller,Event("Footstep","Other","run-key","other")); Assert(w.Count("hit")==2,"payload exact");
        c.Arguments["payload"]=EventValue.String(""); Raise(w.Controller,Event("Footstep","Other","run-key","other"));
        Assert(w.Count("hit")==3,"empty payload is Any");
    }

    private static void MultipleListeners()
    {
        World a=Create(); EventModuleDefinition m=Module(Rule(Signal("animation.eventFired","event","Footstep"),"a",true),Rule(Signal("animation.eventFired","event","Footstep"),"b"));
        a.Runtime.Update(m,a.Globals,a.Scene,a.Self);
        World b=Create(a.Scene,a.Self,a.Controller); b.Runtime.Update(Module(Rule(Signal("animation.eventFired","event","Footstep"),"c")),b.Globals,b.Scene,b.Self);
        Raise(a.Controller,Event("Footstep")); Raise(a.Controller,Event("Footstep",loop:1));
        Assert(a.Count("a")==2,"Trigger Once is occurrence-local"); Assert(a.Count("b")==2,"two occurrences dispatch twice"); Assert(b.Count("c")==2,"two modules observe same occurrence");
    }

    private static void Windows()
    {
        World w=Create(); EventModuleDefinition m=Module(Rule(Signal("animation.windowEntered","window","Damage","Attack","hit"),"enter"),Rule(Signal("animation.windowExited","window","Damage","Attack","hit"),"exit"));
        w.Runtime.Update(m,w.Globals,w.Scene,w.Self); AnimationWindowOccurrence o=new(Guid.NewGuid(),"attack-key","Attack",Guid.NewGuid(),"Damage","hit",.1f,.4f,0);
        RaiseWindow(w.Controller,"WindowEntered",o); RaiseWindow(w.Controller,"WindowExited",o);
        Assert(w.Count("enter")==1&&w.Count("exit")==1,"window enter and exit dispatch");
        Guid id=Guid.NewGuid(); var window=new AnimationWindow{Id=id,Name="CanCombo",StartTime=.1f,EndTime=.4f};
        typeof(AnimationController).GetField("_animationWindowSnapshot",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(w.Controller,AnimationWindowCrossing.BuildSnapshot(new[]{window},1f));
        ((HashSet<Guid>)typeof(AnimationController).GetField("_activeAnimationWindows",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(w.Controller)!).Add(id);
        VisualLogicRegistry r=VisualLogicRegistry.CreateDefault(); r.TryGetCondition("animation.windowActive",out VisualConditionDefinition? active);
        VisualInstruction i=Signal("animation.windowActive","window","cancombo"); Assert(active!.Evaluate(i,Context(w)),"window active polls controller");
    }

    private static void ResetLifecycle()
    {
        World w=Create(); EventModuleDefinition m=Module(Rule(Signal("animation.eventFired","event","Ping"),"hit"));
        w.Runtime.Update(m,w.Globals,w.Scene,w.Self); w.Runtime.Update(m,w.Globals,w.Scene,w.Self); Raise(w.Controller,Event("Ping")); Assert(w.Count("hit")==1,"no duplicate subscription");
        w.Runtime.Reset(); Raise(w.Controller,Event("Ping")); Assert(w.Count("hit")==1,"Reset unsubscribes");
        w.Runtime.Update(m,w.Globals,w.Scene,w.Self); Raise(w.Controller,Event("Ping")); Assert(w.Count("hit")==2,"safe resubscribe");
    }

    private static void OrdinaryIsolation()
    {
        World w=Create(); EventModuleDefinition m=Module(Rule(new VisualInstruction{Id="system.always"},"ordinary"),Rule(Signal("animation.eventFired","event","Ping"),"signal"));
        w.Runtime.Update(m,w.Globals,w.Scene,w.Self); Raise(w.Controller,Event("Ping"));
        Assert(w.Count("ordinary")==1&&w.Count("signal")==1,"callback skips unrelated ordinary rule");
    }

    private static World Create(Scene? scene=null,GameObject? self=null,AnimationController? controller=null)
    {
        scene??=new Scene("C10-F"); self??=scene.CreateGameObject("Character"); controller??=self.AddComponent(new AnimationController());
        var counts=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase); VisualLogicRegistry registry=VisualLogicRegistry.CreateDefault();
        registry.RegisterAction(new VisualActionDefinition{Id="test.count",Category="Test",DisplayName="Count",Execute=(i,c)=>{string key=EventValueResolver.GetString(i,"key",c);counts[key]=counts.GetValueOrDefault(key)+1;}});
        return new World(scene,self,controller,new VariableStore(),new EventModuleRuntime(registry),counts);
    }
    private static EventRuleDefinition Rule(VisualInstruction c,string key,bool once=false){var r=new EventRuleDefinition();r.Conditions.Add(c);if(once)r.Conditions.Add(new VisualInstruction{Id="system.triggerOnce"});r.Actions.Add(new VisualInstruction{Id="test.count",Arguments={{"key",EventValue.String(key)}}});return r;}
    private static EventModuleDefinition Module(params EventRuleDefinition[] rules){var m=new EventModuleDefinition();m.Rules.AddRange(rules);return m;}
    private static VisualInstruction Signal(string id,string key,string name,string clip="",string payload="")=>new(){Id=id,Arguments={{"target",EventValue.String("Self")},{key,EventValue.String(name)},{"clip",EventValue.String(clip)},{"payload",EventValue.String(payload)}}};
    private static AnimationEventOccurrence Event(string name,string clip="Run",string key="run-key",string payload="",long loop=0)=>new(Guid.NewGuid(),key,clip,Guid.NewGuid(),name,payload,.25f,loop);
    private static void Raise(AnimationController c,AnimationEventOccurrence o)=>Delegate<AnimationEventOccurrence>(c,"AnimationEventFired")?.Invoke(o);
    private static void RaiseWindow(AnimationController c,string name,AnimationWindowOccurrence o)=>Delegate<AnimationWindowOccurrence>(c,name)?.Invoke(o);
    private static Action<T>? Delegate<T>(AnimationController c,string name)=>(Action<T>?)typeof(AnimationController).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)?.GetValue(c);
    private static EventExecutionContext Context(World w)=>new(){Globals=w.Globals,Scene=w.Scene,Self=w.Self};
    private static void Assert(bool value,string name){if(!value)throw new InvalidOperationException("FAILED C10-F: "+name);}
    private sealed record World(Scene Scene,GameObject Self,AnimationController Controller,VariableStore Globals,EventModuleRuntime Runtime,Dictionary<string,int> Counts){public int Count(string key)=>Counts.GetValueOrDefault(key);}
}