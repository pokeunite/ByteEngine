using System.Numerics;
using ByteEngine.Core.Diagnostics;
using GoblinScrapper.Construction;
namespace ByteEngine.Tests;
internal static class WheelHandlingTests
{
 public static void Run(VehiclePartCatalog c)
 {
  foreach(int wheelReference in new[]{2,46})
  foreach(float dt in new[]{1f/30,1f/60,1f/120})
  foreach(int poweredAxle in new[]{0,1,2})
  {
   var a=ContraptionTests.Build(c,wheelReference);var free=c.Parts.Values.First(p=>p.ReferenceId==(wheelReference==2?40:60)).File;
   var wheels=a.Parts.Values.Where(p=>c[p.File].ReferenceId==wheelReference).ToArray();
   foreach(var p in wheels)if(poweredAxle!=2 && (p.Position.Z>0?1:0)!=poweredAxle)a.Parts[p.Id]=p with{File=free};
   using var physics=new ContraptionPhysicsWorld(a,new(0,a.RideHeight,0),Quaternion.Identity);
   for(int i=0;i<(int)(2/dt);i++)physics.Step(dt,0,0,false,false);
   var start=physics.Pose(0);float minUp=1;
   for(int i=0;i<(int)(4/dt);i++){physics.Step(dt,1,0,false,false);minUp=Math.Min(minUp,Vector3.Transform(Vector3.UnitY,physics.Pose(0).Rotation).Y);}
   var end=physics.Pose(0);var delta=end.Position-start.Position;
   Console.WriteLine($"Wheel {wheelReference} layout {poweredAxle} at {1/dt:F0}Hz: straight delta={delta}; up={minUp}; yaw={Vector3.Transform(-Vector3.UnitZ,end.Rotation)}");
   if(poweredAxle==2)
   {
    float expectedRollingSpeed=(wheelReference==2?10.472f*.484f:10.193f*.687f);
    float groundSpeed=new Vector2(physics.Velocity(0).X,physics.Velocity(0).Z).Length();
    Console.WriteLine($"Traction wheel {wheelReference}: ground speed={groundSpeed:F3} nominal tire surface speed={expectedRollingSpeed:F3}");
    if(groundSpeed<expectedRollingSpeed*.85f)throw new InvalidOperationException("Four powered wheels kept spinning without reaching rolling speed");
   }
   if(minUp<.85f||Math.Abs(delta.X)>Math.Abs(delta.Z)*.2f||Math.Abs(delta.Z)<2)throw new InvalidOperationException("Mixed-wheel straight driving unstable");
   using(var reference=new ContraptionPhysicsWorld(a,new(0,a.RideHeight,0),Quaternion.Identity))
   {
    for(int i=0;i<(int)(2/dt);i++)reference.Step(dt,0,0,false,false);
    for(int i=0;i<(int)(4/dt);i++)reference.Step(dt,1,0,false,false);
    for(int i=0;i<(int)(3/dt);i++){physics.Step(dt,1,1,false,false);reference.Step(dt,1,0,false,false);}
    if(Vector3.Distance(physics.Pose(0).Position,reference.Pose(0).Position)>.0001f||Math.Abs(Quaternion.Dot(physics.Pose(0).Rotation,reference.Pose(0).Rotation))<.99999f)throw new InvalidOperationException("A/D must not implicitly drive/steer wheels without a steering block");
   }
   using var reverse=new ContraptionPhysicsWorld(a,new(0,a.RideHeight,0),Quaternion.Identity);
   for(int i=0;i<(int)(2/dt);i++)reverse.Step(dt,0,0,false,false);
   var reverseStart=reverse.Pose(0).Position;
   for(int i=0;i<(int)(4/dt);i++)reverse.Step(dt,-1,0,false,false);
   if(reverse.Pose(0).Position.Z-reverseStart.Z<2)throw new InvalidOperationException("Reverse motor propulsion failed");
   float brakingStartSpeed=reverse.Velocity(0).Length();
   for(int i=0;i<(int)(4/dt);i++)reverse.Step(dt,0,0,true,false);
   Console.WriteLine($"Brake velocity={reverse.Velocity(0)}; angular speeds={string.Join(",",wheels.Select(p=>reverse.OutputAngularSpeed(p.Id)))}; up={Vector3.Transform(Vector3.UnitY,reverse.Pose(0).Rotation)}");
   if(reverse.Velocity(0).Length()>brakingStartSpeed*.5f)throw new InvalidOperationException("Powered-wheel braking did not decelerate the machine");
   foreach(var p in a.Parts.Values.Where(p=>c[p.File].ReferenceId==wheelReference))if(Math.Abs(reverse.OutputAngularSpeed(p.Id))>.1f)throw new InvalidOperationException("Powered wheel auto-brake did not lock its axle");
   for(int i=0;i<(int)(8/dt);i++)reverse.Step(dt,0,0,false,false);
   if(reverse.Velocity(0).Length()>.3f)throw new InvalidOperationException("Auto-braked machine continued sliding indefinitely");
   var restart=reverse.Pose(0).Position;
   for(int i=0;i<(int)(2/dt);i++)reverse.Step(dt,1,0,false,false);
   if(Vector3.Distance(restart,reverse.Pose(0).Position)<1)throw new InvalidOperationException("Sleeping machine did not wake when powered again");
  }
  Console.WriteLine("PASS: small/large front/rear/all-powered mixed wheels drive straight, ignore A/D without steering blocks, reverse and brake at 30/60/120Hz.");
 }
}
