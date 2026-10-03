using System.Numerics;
using GoblinScrapper.Construction;
namespace ByteEngine.Tests;
internal static class SteeringJointTests
{
    private static void Check(bool condition,string label) { if(!condition)throw new InvalidOperationException(label); }
    public static VehicleAssembly Build(VehiclePartCatalog catalog)
    {
        string beam=catalog.Parts.Values.First(p=>p.ReferenceId==1).File;
        var hinge=catalog.Parts.Values.First(p=>p.ReferenceId==28);var wheel=catalog.Parts.Values.First(p=>p.ReferenceId==46);
        Check(Vector3.Dot(hinge.Axis,Vector3.UnitY)>.999f,"Steering hinge must yaw around its upright pivot");
        var machine=new VehicleAssembly(catalog);
        foreach(string face in new[]{"Front","Rear"})
        {
            // Large tires need room to swing without striking the rear tires.
            int spacer=machine.AddAtSocket(beam,0,face,catalog[beam].Sockets[0].Name); Check(spacer>0,"Steering wheelbase spacer"); int support=machine.AddAtSocket(beam,spacer,catalog[beam].Sockets[1].Name,catalog[beam].Sockets[0].Name);Check(support>0,"Steering test beam placement");
            foreach(int side in new[]{-1,1})
            {
                var slot=catalog[beam].Sockets.First(s=>Math.Abs(s.Position.Z)<1e-5f&&s.Normal.X==side);
                int parent=support;string target=slot.Name;
                if(face=="Front")
                {
                    parent=machine.AddAtSocket(hinge.File,support,slot.Name,hinge.Sockets.First(s=>s.Bone=="Root").Name);
                    Check(parent>0,"Steering hinge placement");target=hinge.Sockets.First(s=>s.Bone=="Moving").Name;
                }
                int id=machine.AddAtSocket(wheel.File,parent,target,wheel.Sockets.First(s=>s.Bone=="Root").Name);
                Check(id>0,"Wheel must fit the hinge output without a gap");
                if(face=="Front")Check(machine.Parts[id].ParentBone=="Moving","Wheel must attach to the moving hinge body");
            }
        }
        return machine;
    }
    public static void Run(VehiclePartCatalog catalog)
    {
        var machine=Build(catalog);
        var hinge=catalog.Parts.Values.First(p=>p.ReferenceId==28);var wheel=catalog.Parts.Values.First(p=>p.ReferenceId==46);
        var hingeIds=machine.Parts.Values.Where(p=>catalog[p.File].ReferenceId==28).Select(p=>p.Id).ToArray();
        foreach(int id in hingeIds)Console.WriteLine($"Hinge mount {id}: axis={Vector3.Transform(hinge.Axis,machine.Parts[id].Rotation)}, position={machine.Parts[id].Position}"); VehicleAssembly.FromJson(catalog,machine.ToJson());
        using var physics=new ContraptionPhysicsWorld(machine,new(0,machine.RideHeight,0),Quaternion.Identity);
        for(int tick=0;tick<120;tick++)physics.Step(1f/60,0,0,false,false);
        for(int tick=0;tick<90;tick++)physics.Step(1f/60,0,1,false,false);
        foreach(int id in hingeIds)
        {
            var delta=physics.OutputDeformation(id);var direction=Vector3.Normalize(Vector3.TransformNormal(-Vector3.UnitZ,delta));
            float angle=MathF.Acos(Math.Clamp(Vector3.Dot(direction,-Vector3.UnitZ),-1,1));
            Console.WriteLine($"Steering hinge {id}: angle={angle}, axis={direction}"); Check(angle>.3f&&angle<.8f,"Hinge steering must reach its requested angle under wheel load");
            var forwardInHinge=Vector3.Transform(-Vector3.UnitZ,Quaternion.Inverse(machine.Parts[id].Rotation));
            var steeredWorld=Vector3.Transform(Vector3.TransformNormal(forwardInHinge,delta),physics.Pose(id).Rotation);
            var steeredMachine=Vector3.Transform(steeredWorld,Quaternion.Inverse(physics.Pose(0).Rotation));
            Check(steeredMachine.X>.3f,"D input must steer both upright hinges to the machine's right");
            var output=hinge.Sockets.First(s=>s.Bone=="Moving");var root=physics.Pose(id);
            var expected=root.Position+Vector3.Transform(Vector3.Transform(output.Position,delta),root.Rotation);
            int child=machine.Parts.Values.Single(p=>p.Parent==id).Id;var childPose=physics.Pose(child);
            var input=wheel.Sockets.First(s=>s.Bone=="Root");var actual=childPose.Position+Vector3.Transform(input.Position,childPose.Rotation);
            Check(Vector3.Distance(expected,actual)<.04f,"Powered wheel disconnected from the moving hinge during steering");
        }
        var start=physics.Pose(0);
        float greatestTurn=0; var initialHeading=Vector3.Transform(-Vector3.UnitZ,start.Rotation); initialHeading.Y=0; initialHeading=Vector3.Normalize(initialHeading); for(int tick=0;tick<180;tick++){physics.Step(1f/60,1,1,false,false);foreach(int id in hingeIds){var d=Vector3.Normalize(Vector3.TransformNormal(-Vector3.UnitZ,physics.OutputDeformation(id)));Check(MathF.Acos(Math.Clamp(Vector3.Dot(d,-Vector3.UnitZ),-1,1))<.8f,"Steering hinge exceeded its travel under drive torque");}var heading=Vector3.Transform(-Vector3.UnitZ,physics.Pose(0).Rotation); heading.Y=0; greatestTurn=Math.Max(greatestTurn,MathF.Acos(Math.Clamp(Vector3.Dot(initialHeading,Vector3.Normalize(heading)),-1,1)));}
        var end=physics.Pose(0);Check(Vector3.Distance(start.Position,end.Position)>1,"Hinge-equipped vehicle must drive");
        Console.WriteLine($"Steering drive delta={end.Position-start.Position}, greatest turn={greatestTurn}, end orientation={end.Rotation}"); Check(greatestTurn>.25f,"Front steering hinges must turn the vehicle");
        if(catalog.Legacy is {} old)
        {
            var legacy=ContraptionTests.Build(old);var v4=legacy.ToJson().Replace("\"Version\": 5","\"Version\": 4");
            var migrated=VehicleAssembly.FromJson(catalog,v4);
            Check(migrated.Parts.Count==legacy.Parts.Count,"Saved machine migration lost parts");
            foreach(var part in migrated.Parts.Values.Where(p=>p.Id!=0))
            {
                var snapped=migrated.Snap(part.Parent,part.ParentConnector,part.File,part.OwnConnector);
                Check(Vector3.Distance(snapped.Position,part.Position)<1e-4f,"Legacy sockets were not resnapped to new geometry");
            }
        }
        Console.WriteLine("PASS: wheel-to-hinge attachment, loaded upright steering, vehicle turning, and saved-machine socket migration.");
    }
}