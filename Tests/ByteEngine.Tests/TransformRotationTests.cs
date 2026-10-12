using System.Numerics;
using ByteEngine.Core.Scene;

namespace ByteEngine.Tests;

internal static class TransformRotationTests
{
    public static void Run()
    {
        var scene = new Scene("Nested rotation consistency");
        var root=scene.CreateGameObject("Root");
        var pivot=scene.CreateGameObject("Pivot");pivot.SetParent(root,false);
        var child=scene.CreateGameObject("Camera");child.SetParent(pivot,false);
        root.Transform.LocalPosition=new(3,2,-5);
        pivot.Transform.LocalPosition=new(.3f,1.5f,-.2f);
        child.Transform.LocalPosition=new(.1f,.2f,.3f);
        foreach(float yaw in new[]{-179f,-90f,0f,45f,179f})
        foreach(float pitch in new[]{-89f,-45f,0f,45f,89f})
        {
            root.Transform.LocalRotation=Quaternion.CreateFromAxisAngle(Vector3.UnitY,yaw*MathF.PI/180);
            pivot.Transform.LocalRotation=Quaternion.CreateFromAxisAngle(Vector3.UnitX,pitch*MathF.PI/180);
            child.Transform.LocalRotation=Quaternion.CreateFromAxisAngle(Vector3.UnitZ,.2f);
            Check(child);
            Quaternion target=Quaternion.CreateFromYawPitchRoll(.7f,-.4f,.3f);
            child.Transform.WorldRotation=target;
            if(Math.Abs(Quaternion.Dot(target,child.Transform.WorldRotation))<.99999f)throw new Exception("World rotation setter failed.");
            Check(child);
            var world=child.Transform.WorldMatrix;
            child.SetParent(root,true);
            Check(child);
            if(Vector3.Distance(world.Translation,child.Transform.WorldMatrix.Translation)>.0001f)
                throw new Exception("World-preserving reparent changed rendered position.");
            foreach(var axis in new[]{Vector3.UnitX,Vector3.UnitY,Vector3.UnitZ})
                if(Vector3.Distance(Vector3.TransformNormal(axis,world),Vector3.TransformNormal(axis,child.Transform.WorldMatrix))>.0001f)
                    throw new Exception("World-preserving reparent changed rendered rotation.");
            child.SetParent(pivot,false);child.Transform.LocalPosition=new(.1f,.2f,.3f);
        }
        Console.WriteLine("PASS Nested quaternion/matrix agreement, world rotation assignment and rendered-pose preserving reparent.");
    }
    private static void Check(GameObject child)
    {
        foreach(var axis in new[]{Vector3.UnitX,Vector3.UnitY,-Vector3.UnitZ})
            if(Vector3.Distance(Vector3.Transform(axis,child.Transform.WorldRotation),Vector3.Normalize(Vector3.TransformNormal(axis,child.Transform.WorldMatrix)))>.0001f)
                throw new Exception("Nested world rotation disagrees with render matrix.");
        if(Vector3.Distance(child.Transform.WorldPosition,child.Transform.WorldMatrix.Translation)>.0001f)
            throw new Exception("Nested world position disagrees with rendered position.");
    }
}
