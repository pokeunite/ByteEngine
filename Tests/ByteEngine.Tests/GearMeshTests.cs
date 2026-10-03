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
 }
}
