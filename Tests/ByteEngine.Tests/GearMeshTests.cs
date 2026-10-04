using System.Numerics;
using GoblinScrapper.Construction;
namespace ByteEngine.Tests;
internal static class GearMeshTests
{
 public static void Run(VehiclePartCatalog catalog)
 {
  var machine=new VehicleAssembly(catalog);string beam=catalog.Parts.Values.First(p=>p.ReferenceId==1).File;
  int[] ids=new int[2];int index=0;
  foreach(string face in new[]{"Front","Rear"})
  {
   int support=machine.AddAtSocket(beam,0,face,catalog[beam].Sockets[0].Name);
   var part=machine.Parts[support];
   var target=catalog[beam].Sockets.Where(s=>Vector3.Transform(s.Normal,part.Rotation).X>.999f)
    .OrderBy(s=>Math.Abs((part.Position+Vector3.Transform(s.Position,part.Rotation)).Z)).First();
   var gear=catalog.Parts.Values.First(p=>p.ReferenceId==(index==0?39:38));
   ids[index++]=machine.AddAtSocket(gear.File,support,target.Name,gear.Sockets.First(s=>s.Bone=="Root").Name);
   if(ids[index-1]<1)throw new InvalidOperationException("Working medium cogs must fit adjacent beam mounts");
  }
  var ca=machine.Parts[ids[0]];var cb=machine.Parts[ids[1]];var da=catalog[ca.File];var db=catalog[cb.File];
  if(!CogGeometry.Meshes(da,ca.Position,ca.Rotation,db,cb.Position,cb.Rotation))throw new Exception("Real beam-mounted gears should mesh");
  if(CogGeometry.Meshes(da,ca.Position,ca.Rotation,db,cb.Position+Vector3.Transform(db.Axis,cb.Rotation)*.1f,cb.Rotation))throw new Exception("Different gear planes must not mesh");
  if(CogGeometry.Meshes(da,ca.Position,ca.Rotation,db,cb.Position+(cb.Position-ca.Position)*.25f,cb.Rotation))throw new Exception("Gears too far apart must not mesh");
  var phases=CogGeometry.Phases(machine);var direction=CogGeometry.Centre(db,cb.Position,cb.Rotation)-CogGeometry.Centre(da,ca.Position,ca.Rotation);
  var va=Vector3.Transform(direction,Quaternion.Inverse(ca.Rotation));var vb=Vector3.Transform(-direction,Quaternion.Inverse(cb.Rotation));
  float alignment=CogGeometry.Teeth(da)*(MathF.Atan2(va.Y,va.X)+phases[ca.Id])+CogGeometry.Teeth(db)*(MathF.Atan2(vb.Y,vb.X)+phases[cb.Id])-MathF.PI;
  if(Math.Abs(MathF.IEEERemainder(alignment,MathF.Tau))>.001f)throw new Exception("Cog teeth phase must alternate");
  Console.WriteLine("PASS: tooth phase alignment and rejection of separated / non-coplanar gears.");
  // Keep the gear teeth off the ground; ground friction would oppose both spin directions.
  int stand=machine.AddAtSocket(beam,0,"Bottom",catalog[beam].Sockets[0].Name);
  string foot=catalog.Parts.Values.First(p=>p.ReferenceId==15).File;
  foreach(int side in new[]{-1,1})
  {
   var part=machine.Parts[stand];var slot=catalog[beam].Sockets.Where(s=>Vector3.Transform(s.Normal,part.Rotation).X*side>.999f)
    .OrderBy(s=>(part.Position+Vector3.Transform(s.Position,part.Rotation)).Y).First();
   if(machine.AddAtSocket(foot,stand,slot.Name,catalog[foot].Sockets[0].Name)<1)throw new InvalidOperationException("Gear stand foot placement");
  }
  using var physics=new ContraptionPhysicsWorld(machine,new(0,machine.RideHeight,0),Quaternion.CreateFromAxisAngle(Vector3.UnitY,.4f));
  for(int tick=0;tick<120;tick++)physics.Step(1f/60,0,0,false,false);
  for(int tick=0;tick<120;tick++)physics.Step(1f/60,0,0,false,true);
  float powered=physics.OutputAngularSpeed(ids[0]),free=physics.OutputAngularSpeed(ids[1]);
  Console.WriteLine($"Cog speeds: powered={powered}, free={free}");
  if(Math.Abs(powered)<5 || Math.Abs(free)<5 || powered*free>=0 || Math.Abs(powered+free)>5)
   throw new InvalidOperationException("Powered cog must drive the neighboring free cog in the opposite direction after chassis rotation");
  Console.WriteLine("PASS: beam-mounted powered/free cog mesh, correct radii and opposite rotation on a rotated machine.");
  var train=VehicleAssembly.FromJson(catalog,File.ReadAllText("Tests/ByteEngine.Tests/TestData/GoblinGearTrain.json"));
  using var trainPhysics=new ContraptionPhysicsWorld(train,new(0,train.RideHeight,0),Quaternion.Identity);
  for(int tick=0;tick<120;tick++)trainPhysics.Step(1f/60,0,0,false,false);
  for(int tick=0;tick<180;tick++)trainPhysics.Step(1f/60,0,0,false,true);
  var trainGears=train.Parts.Values.Where(p=>catalog[p.File].ReferenceId is 38 or 39).OrderBy(p=>p.Position.Z).Select(p=>trainPhysics.OutputAngularSpeed(p.Id)).ToArray();
  if(trainGears.Any(v=>Math.Abs(v)<5)||trainGears[0]*trainGears[1]>=0||trainGears[0]*trainGears[2]<=0)throw new InvalidOperationException("Nearby surface connectors locked the gear train");
  Console.WriteLine("PASS: three-gear train keeps the middle reversed and last gear matching input; surface connectors do not weld neighboring gears.");

 }
}
