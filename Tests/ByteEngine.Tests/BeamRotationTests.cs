using System.Numerics;
using GoblinScrapper.Construction;
namespace ByteEngine.Tests;
internal static class BeamRotationTests {
 public static void Run(string project){
  var c=VehiclePartCatalog.Load(Path.Combine(project,"Assets","GarageUI","parts-catalog.json"));var beam=c.Parts.Values.First(p=>p.ReferenceId==1);
  foreach(var target in new[]{"Front","Rear","Top","Bottom","Left","Right"}) {
   var a=new VehicleAssembly(c);var targetNormal=c[VehicleAssembly.MasterBlock].Sockets.Single(s=>s.Name==target).Normal;
   int own=0;var pose=a.Snap(0,target,beam.File,beam.Sockets[own].Name);var original=pose.Rotation;
   for(int i=0;i<4;i++) {var next=VehicleBuilder3D.RotateBeamMount(beam.Sockets,own,pose.Rotation,targetNormal,90);own=next.Socket;pose=a.Snap(0,target,beam.File,beam.Sockets[own].Name,next.Twist);
    if(Vector3.Dot(Vector3.Transform(beam.Sockets[own].Normal,pose.Rotation),-targetNormal)<.999f)throw new Exception("Rotation lost attachment normal");
    var joint=pose.Position+Vector3.Transform(beam.Sockets[own].Position,pose.Rotation);if(Vector3.Distance(joint,c[VehicleAssembly.MasterBlock].Sockets.Single(s=>s.Name==target).Position)>.001f)throw new Exception("Rotation lost mounting point");
    if(i==0&&Math.Abs(Vector3.Dot(Vector3.Transform(Vector3.UnitZ,original),Vector3.Transform(Vector3.UnitZ,pose.Rotation)))>.01f)throw new Exception("R rolled the beam instead of turning it crosswise");
   }
  }
  Console.WriteLine("PASS: R turns beams crosswise on every master face while preserving socket alignment and mount position.");
 }
}
