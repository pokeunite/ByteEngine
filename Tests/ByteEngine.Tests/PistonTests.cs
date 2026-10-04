using System.Numerics;
using GoblinScrapper.Construction;
namespace ByteEngine.Tests;
internal static class PistonTests {
 public static void Run(string project) {
  var catalog=VehiclePartCatalog.Load(Path.Combine(project,"Assets","GarageUI","parts-catalog.json"));var piston=catalog.Parts.Values.Single(p=>p.ReferenceId==18);var beam=catalog.Parts.Values.Single(p=>p.ReferenceId==15);var a=new VehicleAssembly(catalog);
  int id=a.AddAtSocket(piston.File,0,"Top",piston.Sockets.Single(s=>s.Bone=="Root").Name);if(id<0)throw new Exception("Piston mount rejected");
  int child=a.AddAtSocket(beam.File,id,piston.Sockets.Single(s=>s.Bone=="Moving").Name,beam.Sockets[0].Name);if(child<0)throw new Exception("Piston payload rejected");
  using var physics=new ContraptionPhysicsWorld(a,new(0,.4f,0),Quaternion.Identity);
  void Step(){for(int i=0;i<180;i++)physics.Step(1f/60,0,0,false,false);}
  void CheckChild(){var root=physics.Pose(id);var socket=piston.Sockets.Single(s=>s.Bone=="Moving");var expected=root.Position+Vector3.Transform(Vector3.Transform(socket.Position,physics.OutputDeformation(id)),root.Rotation);var pose=physics.Pose(child);var actual=pose.Position+Vector3.Transform(beam.Sockets[0].Position,pose.Rotation);if(Vector3.Distance(expected,actual)>.025f)throw new Exception("Payload separated from piston output");}
  Step();CheckChild();var rest=physics.OutputDeformation(id).Translation;physics.Activate();Step();CheckChild();var extended=physics.OutputDeformation(id).Translation;float travel=Vector3.Distance(rest,extended);if(travel<.3f||travel>.45f)throw new Exception("Piston stroke incorrect: "+travel);physics.Activate();Step();CheckChild();if(Vector3.Distance(rest,physics.OutputDeformation(id).Translation)>.025f)throw new Exception("Piston did not retract");Console.WriteLine($"PASS: piston extends {travel:F3} m, retracts, and keeps its attached beam on the moving output.");
 }
}
