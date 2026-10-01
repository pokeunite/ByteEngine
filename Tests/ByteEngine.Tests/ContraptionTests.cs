using System.Numerics;
using ByteEngine.Core.Construction;
namespace ByteEngine.Tests;
internal static class ContraptionTests
{
 public static VehicleAssembly Build(VehiclePartCatalog c)
 {
  var a=new VehicleAssembly(c);string beam=c.Parts.Values.First(p=>p.ReferenceId==1).File;string wheel=c.Parts.Values.First(p=>p.ReferenceId==46).File;
  foreach(var face in new[]{"Front","Rear"})
  {
   int id=a.AddAtSocket(beam,0,face,c[beam].Sockets[0].Name);Check(id>0,"Beam placement: "+face);
   foreach(int side in new[]{-1,1})
   {
    var socket=c[beam].Sockets.First(s=>s.Position.Z==0&&s.Normal.X==side);
    var pose=a.Snap(id,socket.Name,wheel,c[wheel].Sockets[0].Name);string issue=a.PlacementIssue(wheel,id,pose.Position,pose.Rotation,socket.Name);Check(issue=="",issue);
    Check(a.AddAtSocket(wheel,id,socket.Name,c[wheel].Sockets[0].Name)>0,"Wheel failed");
   }
  }
  return a;
 }
 public static void Run(string project)
 {
  var c=VehiclePartCatalog.Load(Path.Combine(project,"Assets","GarageUI","parts-catalog.json"));Check(c.Standard&&c.Parts.Count==66,"Wrong catalogue");
  int[] excluded=[7,45,49,57,58,65,66,67,68,69,70,74,75,87];Check(!c.Parts.Values.Any(p=>excluded.Contains(p.ReferenceId)),"Excluded blocks survived");
  var a=Build(c);Check(a.CanDrive,"Engine or cab gate survived");VehicleAssembly.FromJson(c,a.ToJson());
  using(var physics=new ContraptionPhysicsWorld(a,new(0,a.RideHeight,0),Quaternion.Identity))
  {
   for(int i=0;i<120;i++)physics.Step(1f/60,0,0,false,false);var start=physics.Pose(0);
   for(int i=0;i<300;i++)physics.Step(1f/60,1,0,false,false);var end=physics.Pose(0);Console.WriteLine($"Powered machine displacement {end.Position-start.Position}; orientation {end.Rotation}");Check(Vector3.Distance(start.Position,end.Position)>1,"Wheel torque failed to propel machine");
   for(int i=0;i<180;i++)physics.Step(1f/60,1,1,false,false);Check(Math.Abs(Quaternion.Dot(end.Rotation,physics.Pose(0).Rotation))<.995f,"Differential steering failed");
  }
  var passive=Build(c);string freeWheel=c.Parts.Values.First(p=>p.ReferenceId==60).File;
  foreach(var p in passive.Parts.Values.Where(p=>c[p.File].ReferenceId==46).ToArray())passive.Parts[p.Id]=p with {File=freeWheel};
  using(var physics=new ContraptionPhysicsWorld(passive,new(0,passive.RideHeight,0),Quaternion.Identity))
  {
   for(int i=0;i<120;i++)physics.Step(1f/60,0,0,false,false);var start=physics.Pose(0).Position;
   for(int i=0;i<180;i++)physics.Step(1f/60,1,0,false,false);Check(Vector3.Distance(start,physics.Pose(0).Position)<.5f,"Passive wheels supplied motor power");
  }
  var cannonMachine=new VehicleAssembly(c);var cannon=c.Parts.Values.First(p=>p.ReferenceId==11);Check(cannonMachine.AddAtSocket(cannon.File,0,"Front",cannon.Sockets[0].Name)>0,"Cannon placement failed");
  using(var physics=new ContraptionPhysicsWorld(cannonMachine,new(0,3,0),Quaternion.Identity))
  {
   Check(physics.Fire()==1&&physics.Projectiles.Count()==1,"Cannon failed to spawn a physical shot");var start=physics.Projectiles.Single().Position;
   for(int i=0;i<20;i++)physics.Step(1f/60,0,0,false,false);Check(physics.Projectiles.Any(p=>Vector3.Distance(start,p.Position)>5),"Projectile failed to travel");
  }
  foreach(int flightId in new[]{14,43})
  {
   var flight=new VehicleAssembly(c);var def=c.Parts.Values.First(d=>d.ReferenceId==flightId);Check(flight.AddAtSocket(def.File,0,"Top",def.Sockets[0].Name)>0,"Flight attachment failed");
   using var physics=new ContraptionPhysicsWorld(flight,new(0,3,0),Quaternion.Identity);for(int i=0;i<120;i++)physics.Step(1f/60,1,0,false,false);
   Check(physics.Pose(0).Position.Y>3.5f,"Flight block failed to lift root: "+def.Label);
  }
  foreach(var def in c.Parts.Values.Where(p=>p.ReferenceId!=0))
  {
   var machine=new VehicleAssembly(c);var socket=def.Sockets.First(s=>s.Bone=="Root");int id=-1;
   foreach(var face in new[]{"Front","Rear","Top","Bottom","Left","Right"})foreach(int twist in new[]{0,90,180,270}){if(id>0)break;id=machine.AddAtSocket(def.File,0,face,socket.Name,twist);}
   if(id<0)
   {
    string stem=c.Parts.Values.First(p=>p.ReferenceId==15).File;int support=machine.AddAtSocket(stem,0,"Front",c[stem].Sockets[0].Name);
    foreach(var target in c[stem].Sockets.Skip(1))foreach(int twist in new[]{0,90,180,270}){if(id>0)break;id=machine.AddAtSocket(def.File,support,target.Name,socket.Name,twist);}
   }
   Check(id>0,"Part cannot attach to master: "+def.Label);
   using var physics=new ContraptionPhysicsWorld(machine,new(0,3,0),Quaternion.Identity);
   physics.Activate();physics.Fire();for(int i=0;i<90;i++)physics.Step(1f/60,1,.5f,false,true);
   var position=physics.Pose(0).Position;Check(float.IsFinite(position.X)&&float.IsFinite(position.Y),"Invalid physics: "+def.Label);
   if(def.ReferenceId==18)Check(physics.OutputDeformation(id).Translation.Length()>.2f,"Piston did not physically extend");
  }
  Console.WriteLine("PASS: all 65 parts can attach and simulate; piston output physically extends; projectile and mechanism actions remain finite.");
  var simple=new VehicleAssembly(c);string beam=c.Parts.Values.First(p=>p.ReferenceId==15).File;Check(simple.AddAtSocket(beam,0,"Right",c[beam].Sockets[0].Name)>0,"Short beam failed");Check(simple.Remove(1)&&!simple.Remove(0),"Delete protection failed");
  Console.WriteLine("PASS: selected catalogue; arbitrary beams and wheels; save/load; deletion; physical wheel propulsion and steering without engine/cab.");
 }
 private static void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}
}
