using System.Numerics;
using ByteEngine.Core.Characters;
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
        Console.WriteLine("PASS: general component lookup, compound/static trigger contacts, disabled/inactive colliders, dynamic/kinematic bodies and world reset.");
    }
    private static void Check(bool value,string name){if(!value)throw new Exception(name);}
    private class Marker:Component { }
    private sealed class DerivedMarker:Marker { }
}
