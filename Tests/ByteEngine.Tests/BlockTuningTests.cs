using GoblinScrapper.Construction;
using System.Numerics;
namespace ByteEngine.Tests;
internal static class BlockTuningTests {
 public static void Run(string project){
  var c=VehiclePartCatalog.Load(Path.Combine(project,"Assets/GarageUI/parts-catalog.json"));var a=ContraptionTests.Build(c);var wheel=a.Parts.Values.First(p=>c[p.File].ReferenceId is 2 or 46);
  a.Parts[wheel.Id]=BlockTuning.Set(wheel,c[wheel.File].ReferenceId,"speed",2);
  var loaded=VehicleAssembly.FromJson(c,a.ToJson());if(BlockTuning.Value(loaded.Parts[wheel.Id],c[wheel.File].ReferenceId,"speed")!=2)throw new Exception("Tuning lost during reload");
  a.Parts[wheel.Id]=BlockTuning.Set(wheel,c[wheel.File].ReferenceId,"speed",100);if(BlockTuning.Value(a.Parts[wheel.Id],c[wheel.File].ReferenceId,"speed")!=4)throw new Exception("Unsafe speed not clamped");
  bool rejects=false;try{BlockTuning.Set(wheel,c[wheel.File].ReferenceId,"speed",float.NaN);}catch(ArgumentException){rejects=true;}if(!rejects)throw new Exception("NaN accepted");
  float Drive(float speed){var car=ContraptionTests.Build(c);foreach(var p in car.Parts.Values.Where(p=>c[p.File].ReferenceId is 2 or 46).ToArray()){var tuned=BlockTuning.Set(p,c[p.File].ReferenceId,"speed",speed);car.Parts[p.Id]=BlockTuning.Set(tuned,c[p.File].ReferenceId,"torque",400);}using var world=new ContraptionPhysicsWorld(car,new(0,car.RideHeight+.08f,0),Quaternion.Identity);for(int i=0;i<60;i++)world.Step(1f/60,0,0,false,false);for(int i=0;i<240;i++)world.Step(1f/60,1,0,false,false);return new Vector2(world.Velocity(0).X,world.Velocity(0).Z).Length();}
  float basic=Drive(1),fast=Drive(2);if(fast<basic*1.3f)throw new Exception($"Wheel tuning has no driving effect: {basic}/{fast}");Console.WriteLine($"PASS: tuning persistence, safety clamps and wheel drive: {basic:F2} -> {fast:F2} m/s.");
 }
}
