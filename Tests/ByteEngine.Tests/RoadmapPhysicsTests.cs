using System.Numerics;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Physics;
using ByteEngine.Core.Runtime;
namespace ByteEngine.Tests;
internal static class RoadmapPhysicsTests
{
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    public static void Run()
    {
        var positions = new List<Vector3>();
        foreach (int fps in new[] {30,60,144})
        {
            var scene = new Scene("Cadence");
            var o = scene.CreateGameObject("body");
            var body = o.AddComponent(new Rigidbody3D { UseGravity = false, LinearDamping = 0, Velocity = new(3,0,0) });
            var clock = new FixedStepClock();
            for (int i=0;i<fps*2;i++) clock.Advance(1d/fps, dt => scene.Physics.Step(scene,(float)dt));
            positions.Add(o.Transform.WorldPosition);
        }
        Check(positions.All(p => Vector3.Distance(p, positions[0]) < .0001f), "Actual rigid-body cadence differs between frame rates");
        foreach(int fps in new[]{30,60,144})
        {
            var fixedScene=new Scene("Fixed motor"){FixedSimulation=true};var obj=fixedScene.CreateGameObject("motor");var controller=obj.AddComponent(new ByteEngine.Core.Characters.CharacterController3D{Gravity=0});fixedScene.LoadInternal();
            for(int frame=0;frame<fps;frame++){controller.Move(Vector3.UnitX);ByteEngine.Core.Time.Update(1d/fps);fixedScene.UpdateInternal();}
            Check(obj.Transform.WorldPosition.X>0,"Fixed motor does not move at "+fps+" FPS");fixedScene.UnloadInternal();
        }
        var world = new Scene("Stack");
        var floor = world.CreateGameObject("floor"); floor.Transform.LocalPosition = new(0,-.5f,0); floor.AddComponent(new BoxCollider3D { Size = new(20,1,20) });
        var stack = new List<GameObject>();
        for(int i=0;i<5;i++) { var o=world.CreateGameObject("box"); o.Transform.LocalPosition=new(0,.5f+i*1.02f,0); o.AddComponent(new BoxCollider3D()); o.AddComponent(new Rigidbody3D{SimulateRotation=true}); stack.Add(o); }
        for(int i=0;i<600;i++) world.Physics.Step(world,1f/60);
        for(int i=0;i<stack.Count;i++) Check(Math.Abs(stack[i].Transform.WorldPosition.Y-(.5f+i))<.08f, "Resting stack collapsed at "+i+" y="+stack[i].Transform.WorldPosition.Y);
        Console.WriteLine("Stack sleep states: "+string.Join(",", stack.Select(o=>$"{o.GetComponent<Rigidbody3D>()!.IsSleeping}:{o.GetComponent<Rigidbody3D>()!.Velocity.Length():F3}")));
        Check(stack.All(o=>o.GetComponent<Rigidbody3D>()!.IsSleeping),"Resting contact island does not sleep");
        stack[^1].GetComponent<Rigidbody3D>()!.AddImpulse(new(2,1,0));Check(!stack[^1].GetComponent<Rigidbody3D>()!.IsSleeping,"Force does not wake sleeping body");
        world.Physics.Reset();Check(world.Physics.QueryBounds(world,new(-1),new(1)).Count>0,"Reset leaves stale spatial tree");
        var sparse = new Scene("Index");
        for(int i=0;i<1000;i++) {var o=sparse.CreateGameObject("static");o.Transform.LocalPosition=new(i*3,0,0);o.AddComponent(new BoxCollider3D());}
        sparse.Physics.Step(sparse,1f/60); int rebuilds=sparse.Physics.StaticIndexRebuilds;
        sparse.Physics.Step(sparse,1f/60); Check(sparse.Physics.StaticIndexRebuilds==rebuilds,"Unchanged static tree rebuilt");
        Check(sparse.Physics.QueryBounds(sparse,new(-1),new(1)).Count==1,"Index query candidates wrong");
        sparse.GameObjects[0].Transform.LocalPosition=new(10000,0,0);
        Check(sparse.Physics.QueryBounds(sparse,new(-1),new(1)).Count==0,"Query ignores authored move");
        var ccd=new Scene("CCD"); var wall=ccd.CreateGameObject("wall");wall.Transform.LocalPosition=new(0,0,0);wall.AddComponent(new BoxCollider3D{Size=new(.1f,10,10)});
        var projectile=ccd.CreateGameObject("projectile");projectile.Transform.LocalPosition=new(-5,0,0);projectile.AddComponent(new BoxCollider3D{Size=new(.2f)});var bullet=projectile.AddComponent(new Rigidbody3D{UseGravity=false,LinearDamping=0,Velocity=new(1000,0,0),ContinuousCollision=true});
        ccd.Physics.Step(ccd,1f/60);Check(projectile.Transform.WorldPosition.X<0&&bullet.Velocity.X<1,"Fast projectile tunneled through thin wall");
        var jointScene=new Scene("Joint");var anchor=jointScene.CreateGameObject("anchor");anchor.Transform.LocalPosition=new(0,2,0);var hanging=jointScene.CreateGameObject("hanging");hanging.Transform.LocalPosition=new(1,2,0);hanging.AddComponent(new Rigidbody3D());hanging.AddComponent(new DistanceJoint3D{ConnectedObject=anchor.Id,Length=1});
        for(int i=0;i<600;i++) jointScene.Physics.Step(jointScene,1f/60);
        Check(Math.Abs(Vector3.Distance(anchor.Transform.WorldPosition,hanging.Transform.WorldPosition)-1)<.04,"Joint length unstable");
        var hingeScene=new Scene("Hinge");var fixedBody=hingeScene.CreateGameObject("world anchor");var moving=hingeScene.CreateGameObject("rotor");var rotor=moving.AddComponent(new Rigidbody3D{UseGravity=false,SimulateRotation=true,AllowSleep=false});var hinge=moving.AddComponent(new HingeJoint3D{ConnectedObject=fixedBody.Id,MotorEnabled=true,MotorSpeed=40,LimitsEnabled=true,MinimumAngle=-30,MaximumAngle=30});
        for(int i=0;i<600;i++)hingeScene.Physics.Step(hingeScene,1f/60);
        Check(Math.Abs(hinge.CurrentAngle)<=31&&moving.Transform.WorldPosition.Length()<.01f,"Hinge limit/anchor drift "+hinge.CurrentAngle);
        Check(Math.Abs(hinge.CurrentAngle)>10,"Hinge motor does not articulate");
        var slopeScene=new Scene("Slope");var ramp=slopeScene.CreateGameObject("ramp");ramp.AddComponent(new MeshCollider3D{Vertices=[new(-20,-4,-10),new(20,4,-10),new(20,4,10),new(-20,-4,10)],Triangles=[0,1,2,0,2,3]});
        var slider=slopeScene.CreateGameObject("slider");slider.Transform.WorldPosition=new(0,2,0);slider.AddComponent(new CapsuleCollider3D());slider.AddComponent(new Rigidbody3D{Friction=0,LinearDamping=0,AllowSleep=false});
        for(int i=0;i<180;i++)slopeScene.Physics.Step(slopeScene,1f/60);
        var slid=slider.Transform.WorldPosition;
        Check(slid.X<-.5f&&slid.Y>=slid.X*.2f-.08f&&float.IsFinite(slid.X+slid.Y),"Slope contact does not slide or penetrates: "+slid);
        var platformScene=new Scene("Moving platform");var platform=platformScene.CreateGameObject("kinematic lift");platform.Transform.WorldPosition=new(0,-.5f,0);platform.AddComponent(new BoxCollider3D{Size=new(6,1,6)});platform.AddComponent(new Rigidbody3D{BodyType=RigidbodyBodyType3D.Kinematic,Velocity=new(0,.2f,0),UseGravity=false});
        var passenger=platformScene.CreateGameObject("passenger");passenger.Transform.WorldPosition=new(0,.5f,0);passenger.AddComponent(new BoxCollider3D());passenger.AddComponent(new Rigidbody3D());
        for(int i=0;i<240;i++){platform.Transform.WorldPosition+=new Vector3(0,.2f/60,0);platformScene.Physics.Step(platformScene,1f/60);}
        Check(Math.Abs(passenger.Transform.WorldPosition.Y-(platform.Transform.WorldPosition.Y+1))<.05f,"Moving platform loses passenger "+passenger.Transform.WorldPosition);
        Console.WriteLine("PASS triangle slope sliding/contact and moving kinematic platform support");
        Console.WriteLine("PASS actual fixed body trajectories, resting stack, static-index reuse and live queries, 1000-unit/s thin-wall projectile, articulated distance joint");
    }
}
