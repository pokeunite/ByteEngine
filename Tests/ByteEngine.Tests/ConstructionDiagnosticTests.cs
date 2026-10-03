using ByteEngine.Core.Diagnostics;
using GoblinScrapper.Construction;
using System.Numerics;
namespace ByteEngine.Tests;
internal static class ConstructionDiagnosticTests
{
 private static void Check(bool condition,string label){if(!condition)throw new InvalidOperationException(label);}
 public static void Run(string project)
 {
  ConstructionDiagnostics.Enabled=false;ConstructionDiagnostics.Clear();
  ConstructionDiagnostics.Record("DISABLED","must-not-record");Check(!ConstructionDiagnostics.GetTrace().Contains("must-not-record"),"Disabled debug recorded data");
  ConstructionDiagnostics.Enabled=true;int generation=ConstructionDiagnostics.Generation;
  var c=VehiclePartCatalog.Load(Path.Combine(project,"Assets","GarageUI","parts-catalog.json"));var a=ContraptionTests.Build(c);
  a.Add(a.Parts[1].File,999,Vector3.Zero,Quaternion.Identity);
  string json=a.ToJson();VehicleAssembly.FromJson(c,json);
  using(var physics=new ContraptionPhysicsWorld(a,new(0,a.RideHeight,0),Quaternion.Identity))
  {
   for(int i=0;i<150;i++)physics.Step(1f/60,i<120?0:1,.5f,false,false);
   physics.Activate();physics.Fire();
   string trace=ConstructionDiagnostics.GetTrace();
   foreach(string term in new[]{"PLACE]","PLACE REJECT","RESTORE","PHYSICS SNAPSHOT","BODY SETUP","wobbleDeg=","hubGap=","attachmentGap=","rpm=","steering=","MECHANISMS","FIRE"})Check(trace.Contains(term),"Missing diagnostic: "+term);
   Check(!trace.Contains("NaN")&&!trace.Contains("Infinity"),"Nonfinite diagnostic sample");
   Directory.CreateDirectory("output/video-wheel");File.WriteAllText("output/video-wheel/construction-debug-validation.txt",trace);
   ConstructionDiagnostics.Enabled=false;string frozen=ConstructionDiagnostics.GetTrace();physics.Step(1f/60,1,1,false,false);Check(frozen==ConstructionDiagnostics.GetTrace(),"Unticking must freeze trace");
   ConstructionDiagnostics.Clear();Check(ConstructionDiagnostics.Generation>generation,"Clear did not request new snapshot");ConstructionDiagnostics.Enabled=true;
   physics.Step(1f/60,0,0,false,false);Check(ConstructionDiagnostics.GetTrace().Contains("PHYSICS SNAPSHOT"),"Recording did not resume with snapshot");
  }
  for(int i=0;i<2000;i++){ConstructionDiagnostics.Record("bound",new string('x',2048));ConstructionDiagnostics.Record("bound",new string('x',2048),true);}
  Check(ConstructionDiagnostics.GetTrace().Length<1100000,"Unbounded trace memory");
  ConstructionDiagnostics.Enabled=false;ConstructionDiagnostics.Clear();
  Console.WriteLine("PASS: opt-in construction events/physics, finite wheel metrics, frozen copy, clear/resume snapshot and bounded memory.");
 }
}
