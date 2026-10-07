using System.Numerics;
using GoblinScrapper.Construction;
namespace ByteEngine.Tests;
internal static class SteeringDirectionTests
{
 public static void Run(string project)
 {
  var c=VehiclePartCatalog.Load(Path.Combine(project,"Assets","GarageUI","parts-catalog.json"));
  foreach(bool invertedAxis in new[]{false,true})foreach(bool recorded in new[]{true,false})foreach(int direction in new[]{1,-1})foreach(int throttle in new[]{1,-1})
  {
   var a=recorded?VehicleAssembly.FromJson(c,File.ReadAllText("Tests/ByteEngine.Tests/TestData/GoblinSteeringUser025.json")):SteeringJointTests.Build(c);
   var hinge=c.Parts.Values.Single(p=>p.ReferenceId==28);
   var originalAxis=hinge.Axis;
   if(invertedAxis)typeof(AssemblyPartDefinition).GetProperty("Axis")!.SetValue(hinge,-originalAxis);
   using var physics=new ContraptionPhysicsWorld(a,new(0,a.RideHeight,0),Quaternion.Identity);
   for(int i=0;i<120;i++)physics.Step(1f/60,0,0,false,false);
   for(int i=0;i<60;i++)physics.Step(1f/60,throttle,0,false,false);
   var start=physics.Pose(0);float yaw=0;
   for(int i=0;i<180;i++)physics.Step(1f/60,throttle,direction,false,false);
   var forward=Vector3.Transform(-Vector3.UnitZ,physics.Pose(0).Rotation);yaw=MathF.Atan2(-forward.X,-forward.Z);
   Console.WriteLine($"InvertedAxis={invertedAxis} Recorded={recorded} input={direction} throttle={throttle}: yaw={yaw} delta={physics.Pose(0).Position-start.Position} velocity={physics.Velocity(0)}");
   if(throttle*direction*yaw>-.15f)throw new InvalidOperationException("Wheel steering and physical vehicle turning disagree");
   var reversalStart=physics.Pose(0);
   for(int i=0;i<240;i++)physics.Step(1f/60,throttle,-direction,false,false);
   var reversedForward=Vector3.Transform(Vector3.Transform(-Vector3.UnitZ,physics.Pose(0).Rotation),Quaternion.Inverse(reversalStart.Rotation));
   float reversedYaw=MathF.Atan2(-reversedForward.X,-reversedForward.Z);
   Console.WriteLine($"Counter-steer yaw={reversedYaw}");
   if(throttle*direction*reversedYaw<.15f)throw new InvalidOperationException("Counter-steering did not reverse the mechanical turn direction");
   typeof(AssemblyPartDefinition).GetProperty("Axis")!.SetValue(hinge,originalAxis);
  }
 }
}
