using System.Numerics;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Classification;
using ByteEngine.Core.Physics;
using ByteEngine.Core.Scene;
namespace ByteEngine.Tests;
internal static class EngineOptimizationTests
{
    private static void Check(bool value,string message) { if(!value) throw new Exception(message); }
    private sealed class Events : Component
    {
        public int Enter,Stay,Exit,TriggerEnter,TriggerStay,TriggerExit;
        protected override void OnCollisionEnter(PhysicsContact3D c)=>Enter++;
        protected override void OnCollisionStay(PhysicsContact3D c)=>Stay++;
        protected override void OnCollisionExit(PhysicsContact3D c)=>Exit++;
        protected override void OnTriggerEnter(PhysicsContact3D c)=>TriggerEnter++;
        protected override void OnTriggerStay(PhysicsContact3D c)=>TriggerStay++;
        protected override void OnTriggerExit(PhysicsContact3D c)=>TriggerExit++;
    }
    public static void Run()
    {
        EngineAllocationRegressionTests.Run();
        var scene=new Scene("Cached contacts");
        var a=scene.CreateGameObject("A");var ca=a.AddComponent(new BoxCollider3D());var events=a.AddComponent(new Events());
        var b=scene.CreateGameObject("B");b.Transform.LocalPosition=new(.25f,0,0);var cb=b.AddComponent(new BoxCollider3D());
        void Tick()=>scene.Physics.Step(scene,1f/60);
        Tick();Tick();Check(events.Enter==1&&events.Stay==1&&scene.Physics.LastCachedContactHits>0,"Cached solid enter/stay");
        b.Transform.LocalPosition=new(3,0,0);Tick();Check(events.Exit==1&&scene.Physics.Contacts.Count==0,"Move invalidates cached solid");
        cb.Size=new(8,1,1);Tick();Check(events.Enter==2,"Resize invalidates cached miss");
        cb.UseProjectMatrix=false;cb.CollisionMask=LayerMask.None;Tick();Check(events.Exit==2,"Filter invalidates cached hit");
        cb.CollisionMask=LayerMask.All;cb.IsTrigger=true;Tick();Tick();Check(events.TriggerEnter==1&&events.TriggerStay==1,"Solid to trigger transitions");
        cb.Enabled=false;Tick();Check(events.TriggerExit==1,"Disabled cached trigger exits");cb.Enabled=true;Tick();
        b.RemoveComponent(cb);Tick();Check(events.TriggerExit==2,"Removed cached trigger exits");
        cb=b.AddComponent(new BoxCollider3D{Size=new(8,1,1)});Tick();
        var parent=scene.CreateGameObject("parent");b.SetParent(parent,false);parent.Transform.LocalPosition=new(10,0,0);Tick();Check(scene.Physics.Contacts.Count==0,"Parent move invalidates cached contact");
        var snapshot=scene.Physics.QueryBounds(scene,new(-1),new(1));scene.Physics.QueryBounds(scene,new(50),new(51));Check(snapshot.Contains(a),"Query snapshot survives another query");
        parent.Transform.LocalPosition=new(-3,0,0);Check(scene.Physics.QueryBounds(scene,new(-1),new(1)).Contains(b),"Live parent teleport query");
        // Compare swept capsule sampling against independently queried sphere casts.
        var sweep=new Scene("Capsule sweep");var wall=sweep.CreateGameObject("wall");wall.Transform.LocalPosition=new(2,1,0);wall.AddComponent(new BoxCollider3D{Size=new(.1f,4,2)});
        Check(GameplayQuery3D.CapsuleCast(sweep,new(0,.4f,0),new(0,1.6f,0),Vector3.UnitX,.3f,out var capsuleHit,5),"Capsule misses wall");
        Check(GameplayQuery3D.SphereCast(sweep,new(0,1,0),Vector3.UnitX,.3f,out var sphereHit,5)&&Math.Abs(capsuleHit.Distance-sphereHit.Distance)<.001f,"Swept capsule distance changed");
        wall.Transform.LocalPosition=new(20,1,0);Check(!GameplayQuery3D.CapsuleCast(sweep,new(0,.4f,0),new(0,1.6f,0),Vector3.UnitX,.3f,out _,5),"Capsule keeps teleported obstacle");
        var ordering=new Scene("Physics registry ordering");var earlier=ordering.CreateGameObject("earlier");var later=ordering.CreateGameObject("later");later.AddComponent(new BoxCollider3D());ordering.Physics.QueryBounds(ordering,new(-1),new(1));earlier.AddComponent(new BoxCollider3D());
        var ordered=ordering.Physics.QueryBounds(ordering,new(-1),new(1));Check(ordered[0]==earlier&&ordered[1]==later,"Registry loses scene order after late component add");later.MoveBefore(earlier);ordered=ordering.Physics.QueryBounds(ordering,new(-1),new(1));Check(ordered[0]==later,"Registry ignores hierarchy reorder");ordering.DestroyGameObject(later);Check(ordering.Physics.QueryBounds(ordering,new(-1),new(1)).Count==1,"Registry retains destroyed collider");
        TransformCache();
        BoundsIntervals();
        Console.WriteLine("PASS cached contacts/events/filter/resize/parent move/removal, stable query snapshots, capsule sweep and transform hierarchy invalidation");
    }
    private static void BoundsIntervals()
    {
        var random=new Random(1782);
        for(int i=0;i<1000;i++)
        {
            float F()=> (float)(random.NextDouble()*20-10);
            var bounds=new ByteEngine.Core.Graphics.ThreeD.BoundingBox3D(new(F(),F(),F()),new(F(),F(),F()));
            var matrix=new Matrix4x4(F(),F(),F(),0,F(),F(),F(),0,F(),F(),F(),0,F(),F(),F(),1);
            var expectedMinimum=new Vector3(float.PositiveInfinity);var expectedMaximum=new Vector3(float.NegativeInfinity);
            foreach(var corner in bounds.GetCorners()){var transformed=Vector3.Transform(corner,matrix);expectedMinimum=Vector3.Min(expectedMinimum,transformed);expectedMaximum=Vector3.Max(expectedMaximum,transformed);}
            var actual=bounds.Transform(matrix);
            Check(Vector3.Distance(expectedMinimum,actual.Minimum)<.0001f&&Vector3.Distance(expectedMaximum,actual.Maximum)<.0001f,"Interval bounds differ under rotation/shear/reflection");
        }
    }
    private static void TransformCache()
    {
        var scene=new Scene("Hierarchy cache");var parent=scene.CreateGameObject("parent");var child=scene.CreateGameObject("child");var grandchild=scene.CreateGameObject("grandchild");child.SetParent(parent,false);grandchild.SetParent(child,false);
        child.Transform.LocalPosition=new(1,2,3);grandchild.Transform.LocalPosition=new(.5f,1,0);
        void Verify(GameObject o)
        {
            var t=o.Transform;var local=Matrix4x4.CreateScale(t.LocalScale)*Matrix4x4.CreateFromQuaternion(t.LocalRotation)*Matrix4x4.CreateTranslation(t.LocalPosition);
            var expected=o.Parent==null?local:local*o.Parent.Transform.WorldMatrix;
            Check(MatrixNear(expected,t.WorldMatrix),"Cached hierarchy matrix stale");
            var expectedPosition=o.Parent==null?t.LocalPosition:o.Parent.Transform.WorldPosition+Vector3.Transform(t.LocalPosition*o.Parent.Transform.WorldScale,o.Parent.Transform.WorldRotation);
            Check(Vector3.Distance(expectedPosition,t.WorldPosition)<.0001f,"Cached world position stale");
            Check(MatrixNear(t.RenderMatrix,t.WorldMatrix),"Unsimulated render matrix stale");
        }
        Verify(grandchild);parent.Transform.LocalPosition=new(2,4,6);parent.Transform.EulerAngles=new(10,45,20);parent.Transform.LocalScale=new(2,3,4);Verify(child);Verify(grandchild);
        child.Transform.WorldPosition=new(5,8,2);Verify(grandchild);var before=grandchild.Transform.WorldPosition;grandchild.SetParent(parent,true);Verify(grandchild);Check(Vector3.Distance(grandchild.Transform.WorldPosition,before)<.001f,"Reparent world pose");
        grandchild.SetParent(null,false);Verify(grandchild);grandchild.Transform.Position=new(8,9);grandchild.Transform.Rotation=40;grandchild.Transform.WorldScale=new(3,2,1);Verify(grandchild);
    }
    private static bool MatrixNear(Matrix4x4 a,Matrix4x4 b)=>Math.Abs(a.M11-b.M11)+Math.Abs(a.M12-b.M12)+Math.Abs(a.M13-b.M13)+Math.Abs(a.M21-b.M21)+Math.Abs(a.M22-b.M22)+Math.Abs(a.M23-b.M23)+Math.Abs(a.M31-b.M31)+Math.Abs(a.M32-b.M32)+Math.Abs(a.M33-b.M33)+Math.Abs(a.M41-b.M41)+Math.Abs(a.M42-b.M42)+Math.Abs(a.M43-b.M43)<.001f;
}
