using System.Numerics;
using System.Text.RegularExpressions;
using GoblinScrapper.Construction;
using ByteEngine.Core.Diagnostics;
namespace ByteEngine.Tests;
internal static class RecordedBuildTests
{
 public static void Run(string project)
 {
  var c=VehiclePartCatalog.Load(Path.Combine(project,"Assets","GarageUI","parts-catalog.json"));
  foreach(float dt in new[]{1f/60,1f/45})
  for(int build=0;build<2;build++)
  {
   var a=VehicleAssembly.FromJson(c,File.ReadAllText($"Tests/ByteEngine.Tests/TestData/GoblinRecordedBuild{build}.json"));
   ConstructionDiagnostics.Clear();ConstructionDiagnostics.Enabled=true;
   using var physics=new ContraptionPhysicsWorld(a,new(0,a.RideHeight,0),Quaternion.Identity);
   for(int i=0;i<(int)(5/dt);i++)physics.Step(dt,0,0,false,false);
   var trace=ConstructionDiagnostics.GetTrace();File.WriteAllText($"output/video-wheel/recorded-{build}-rest.txt",trace);
   string settled=string.Join("\n",trace.Split("[PHYSICS]").Where(t=>Regex.Match(t,@" t=([\d.]+)") is var match&&match.Success&&float.Parse(match.Groups[1].Value,System.Globalization.CultureInfo.InvariantCulture)>=2));
   float wobble=Regex.Matches(settled,@"wobbleDeg=([\d.]+)").Select(m=>float.Parse(m.Groups[1].Value,System.Globalization.CultureInfo.InvariantCulture)).Max();
   float gap=Regex.Matches(settled,@"hubGap=([\d.]+)").Select(m=>float.Parse(m.Groups[1].Value,System.Globalization.CultureInfo.InvariantCulture)).Max();
   Console.WriteLine($"Recorded build {build}: rest wobble={wobble}deg hub gap={gap}m");
   if(wobble>.6f||gap>.002f)throw new InvalidOperationException("Axles deflected at rest");
   var start=physics.Pose(0);
   for(int i=0;i<(int)(4/dt);i++)physics.Step(dt,1,0,false,false);
   var travel=physics.Pose(0).Position-start.Position;
   Console.WriteLine($"Recorded build {build}: straight travel={travel}");
   if(Math.Abs(travel.X)>.25f||-travel.Z<2)throw new InvalidOperationException("Recorded vehicle straight propulsion failed");
   for(int i=0;i<(int)(3/dt);i++)physics.Step(dt,1,1,false,false);
   var drivingTrace=ConstructionDiagnostics.GetTrace();
   File.WriteAllText($"output/video-wheel/recorded-{build}-drive.txt",drivingTrace);
   float loadedWobble=Regex.Matches(drivingTrace,@"wobbleDeg=([\d.]+)").Select(m=>float.Parse(m.Groups[1].Value,System.Globalization.CultureInfo.InvariantCulture)).Max();
   float loadedGap=Regex.Matches(drivingTrace,@"hubGap=([\d.]+)").Select(m=>float.Parse(m.Groups[1].Value,System.Globalization.CultureInfo.InvariantCulture)).Max();
   Console.WriteLine($"Recorded {build} at {1/dt:F0}Hz: loaded wobble={loadedWobble}deg hubGap={loadedGap}m");
   float mountAngle=Regex.Matches(drivingTrace,@"mountAngleDeg=([\d.]+)").Select(m=>float.Parse(m.Groups[1].Value,System.Globalization.CultureInfo.InvariantCulture)).Max();
   Console.WriteLine($"Maximum rigid mount rotation={mountAngle}deg");
   if(mountAngle>2)throw new InvalidOperationException("Rigid wheel mount rocked relative to frame");
   if(loadedWobble>3||loadedGap>.02f)throw new InvalidOperationException("Recorded user's vehicle axle deflection exceeded stability limit");
   if(physics.Pose(0).Position.Y<.2f||Vector3.Transform(Vector3.UnitY,physics.Pose(0).Rotation).Y<.85f)throw new InvalidOperationException("Recorded user's vehicle overturned");
   ConstructionDiagnostics.Enabled=false;
  }
 }
}
