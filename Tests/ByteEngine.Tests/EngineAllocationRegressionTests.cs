using System.Numerics;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Navigation;
using ByteEngine.Core.Physics;
using ByteEngine.Core.Scene;
namespace ByteEngine.Tests;
internal static class EngineAllocationRegressionTests
{
    public static void Run()
    {
        var scene=new Scene("Engine allocation regressions");
        var first=scene.CreateGameObject("First");var marker=first.AddComponent(new DerivedMarker());
        Check(ReferenceEquals(first.GetComponent<Marker>(),marker),"Base-type lookup");
        var secondMarker=first.AddComponent(new DerivedMarker());
        Check(ReferenceEquals(first.GetComponent<Marker>(),marker),"First component ordering");
        first.RemoveComponent(marker);Check(ReferenceEquals(first.GetComponent<Marker>(),secondMarker),"Removed component lookup");
        Check(first.GetComponent<Rigidbody3D>()==null,"Absent component lookup");
        var trigger=first.AddComponent(new BoxCollider3D{IsTrigger=true});
        var second=scene.CreateGameObject("Second");second.Transform.WorldPosition=new(.5f,0,0);
        var collider=second.AddComponent(new BoxCollider3D());
        scene.Physics.Step(scene,1f/60);Check(scene.Physics.Contacts.Count==1,"Static trigger contacts preserved");
        first.AddComponent(new CapsuleCollider3D{IsTrigger=true});
        scene.Physics.Step(scene,1f/60);Check(scene.Physics.Contacts.Count==2,"All colliders on one object preserved");
        collider.Enabled=false;scene.Physics.Step(scene,1f/60);Check(scene.Physics.Contacts.Count==0,"Disabled collider excluded");
        collider.Enabled=true;second.Active=false;scene.Physics.Step(scene,1f/60);Check(scene.Physics.Contacts.Count==0,"Inactive object excluded");
        var dynamic=scene.CreateGameObject("Dynamic");dynamic.Transform.WorldPosition=new(100,10,100);
        dynamic.AddComponent(new Rigidbody3D());scene.Physics.Step(scene,1f/60);
        Check(dynamic.Transform.WorldPosition.Y<10,"Dynamic body integration preserved");
        var kinematic=scene.CreateGameObject("Kinematic");kinematic.Transform.WorldPosition=new(120,10,100);
        kinematic.AddComponent(new Rigidbody3D{BodyType=RigidbodyBodyType3D.Kinematic});scene.Physics.Step(scene,1f/60);
        Check(kinematic.Transform.WorldPosition.Y==10,"Kinematic body remains fixed");
        scene.Physics.Reset();Check(scene.Physics.Contacts.Count==0,"World reset clears contacts");
        CheckNavigationAllocation();
        CheckNavigationRegionLifecycle();
        CheckSolverCreatedOverlap();
        CheckCustomFixedStep();
        Console.WriteLine("PASS: general component lookup, compound/static trigger contacts, disabled/inactive colliders, dynamic/kinematic bodies, world reset and allocation-free navigation replans.");
    }
    /// <summary>
    /// A crowd replans continuously. A* used to allocate a score dictionary, a
    /// parent dictionary, a closed set and a priority queue for every query, so
    /// hundreds of agents produced megabytes of garbage per second. Only the
    /// returned path may allocate now.
    /// </summary>
    private static void CheckNavigationAllocation()
    {
        var grid=new NavigationGrid(48,48,1);
        for(int z=4;z<44;z++)if(z is not (>18 and <22))grid.SetCell(24,z,true);
        Vector3 start=new(1.5f,0,1.5f),goal=new(45.5f,0,45.5f);
        for(int i=0;i<20;i++)grid.FindPath(start,goal);
        var firstPath=grid.FindPath(start,goal);
        Check(firstPath.Count>2,"Navigation returns a path around the wall");
        var snapshot=firstPath.ToArray();
        grid.FindPath(new(2.5f,0,2.5f),new(3.5f,0,3.5f));
        Check(firstPath.SequenceEqual(snapshot),"Navigation paths are private copies");
        const int queries=200;
        long before=GC.GetAllocatedBytesForCurrentThread();
        for(int i=0;i<queries;i++)grid.FindPath(start,goal);
        long perQuery=(GC.GetAllocatedBytesForCurrentThread()-before)/queries;
        Check(perQuery<4096,"Navigation replan allocation budget: "+perQuery+" bytes/query");
    }
    private static void Check(bool value,string name){if(!value)throw new Exception(name);}
    private static void CheckNavigationRegionLifecycle()
    {
        var scene = new Scene("Navigation region lifecycle");
        var owner = scene.CreateGameObject("Region");
        var region = owner.AddComponent(new NavigationRegion3D { Width = 8, Height = 8 });
        region.Bake();
        var agent = scene.CreateGameObject("Agent").AddComponent(new NavigationAgent3D { Speed = 0 });
        agent.SetDestination(Vector3.One);
        agent.Tick(.1f);
        Check(agent.Status != NavigationAgentStatus.MissingRegion, "Initial region resolves");
        owner.Active = false;
        agent.Tick(.1f);
        Check(agent.Status == NavigationAgentStatus.MissingRegion, "Inactive cached region excluded");
        owner.Active = true;
        agent.Tick(.6f);
        agent.Tick(.1f);
        Check(agent.Status != NavigationAgentStatus.MissingRegion, "Reactivated region resolves");
        owner.RemoveComponent(region);
        agent.Tick(.1f);
        Check(agent.Status == NavigationAgentStatus.MissingRegion, "Removed cached region excluded");
    }

    private static void CheckSolverCreatedOverlap()
    {
        var scene = new Scene("Solver-created overlap");
        scene.CreateGameObject("Wall").AddComponent(new BoxCollider3D());
        var body = scene.CreateGameObject("Body");
        body.Transform.LocalPosition = new(.6f, 0, 0);
        body.AddComponent(new BoxCollider3D());
        body.AddComponent(new Rigidbody3D { UseGravity = false, AllowSleep = false });
        var trigger = scene.CreateGameObject("Trigger");
        trigger.Transform.LocalPosition = new(1.65f, 0, 0);
        trigger.AddComponent(new BoxCollider3D { IsTrigger = true });
        scene.Physics.Step(scene, 1f / 60);
        Check(scene.Physics.Contacts.Any(pair => pair.IsTrigger),
            "Contact correction discovers new overlaps within the same substep");
    }

    private static void CheckCustomFixedStep()
    {
        var scene = new Scene("Custom fixed step") { FixedSimulation = true };
        scene.SimulationClock.StepSeconds = .02;
        var probe = scene.CreateGameObject("Probe").AddComponent(new FixedDeltaProbe());
        scene.LoadInternal();
        ByteEngine.Core.Time.Update(.02);
        scene.UpdateInternal();
        Check(Math.Abs(probe.Seconds - .02) < .000001,
            "Fixed callbacks use the configured simulation step");
        scene.UnloadInternal();
    }

    private sealed class FixedDeltaProbe : Component
    {
        public double Seconds;
        protected override void OnFixedUpdate() => Seconds += ByteEngine.Core.Time.FixedDeltaTime;
    }
    private class Marker:Component { }
    private sealed class DerivedMarker:Marker { }
}
