using System.Numerics;
using GoblinScrapper.Construction;
namespace ByteEngine.Tests;
internal static class BraceTests
{
 public static void Run(string game)
 {
  var c=VehiclePartCatalog.Load(Path.Combine(game,"Assets/GarageUI/parts-catalog.json"));
  var a=new VehicleAssembly(c);var beam=c.Parts.Values.First(p=>p.ReferenceId==1);
  int front=a.AddAtSocket(beam.File,0,"Front",beam.Sockets[0].Name),rear=a.AddAtSocket(beam.File,0,"Rear",beam.Sockets[0].Name);
  string Side(int id)=>c[a.Parts[id].File].Sockets.First(s=>s.Normal.X>.99f).Name;
  var first=new BraceEndpoint(front,Side(front));var second=new BraceEndpoint(rear,Side(rear));
  if(a.AddBrace(first,first)>=0)throw new Exception("Brace accepted same body");
  int brace=a.AddBrace(first,second);if(brace<1||a.AddBrace(second,first)>=0)throw new Exception("Brace validation failed");
  var restored=VehicleAssembly.FromJson(c,a.ToJson());if(restored.Braces.Count!=1||restored.Braces[brace]!=a.Braces[brace])throw new Exception("Brace save/reload lost endpoints");
  // Detached body becomes physically fixed to the first endpoint through the brace.
  restored.Parts[rear]=restored.Parts[rear] with{Parent=-1,ParentConnector="",OwnConnector=""};
  using var physics=new ContraptionPhysicsWorld(restored,new(0,3,0),Quaternion.CreateFromAxisAngle(Vector3.UnitY,.5f));
  float length=Vector3.Distance(physics.BracePoint(first),physics.BracePoint(second));
  for(int i=0;i<180;i++)physics.Step(1f/60,0,0,false,false);
  float after=Vector3.Distance(physics.BracePoint(first),physics.BracePoint(second));
  if(!float.IsFinite(after)||Math.Abs(after-length)>.03f)throw new Exception($"Brace did not hold detached bodies: {length} -> {after}");
  restored.Remove(front);if(restored.Braces.Count!=0)throw new Exception("Deleting endpoint left dangling brace");
  foreach(int reference in new[]{9,16,18,13,44}){
   var loop=new VehicleAssembly(c);var moving=c.Parts.Values.First(d=>d.ReferenceId==reference);
   int support=loop.AddAtSocket(beam.File,0,"Rear",beam.Sockets[0].Name);
   int joint=loop.AddAtSocket(moving.File,0,"Top",moving.Sockets.First(s=>s.Bone=="Root").Name);
   if(joint<0||support<0)throw new Exception("Moving brace fixture placement rejected: "+moving.Label);
   var ea=new BraceEndpoint(joint,moving.Sockets.First(s=>s.Bone=="Moving"&&!s.Name.StartsWith("SOCKET_Surface_")).Name);
   var eb=new BraceEndpoint(support,c[loop.Parts[support].File].Sockets.First(s=>s.Normal.X>.99f).Name);
   if(loop.AddBrace(ea,eb)<0)throw new Exception("Moving endpoint brace rejected");
   var loaded=VehicleAssembly.FromJson(c,loop.ToJson());using var dynamics=new ContraptionPhysicsWorld(loaded,new(0,loaded.RideHeight,0),Quaternion.Identity);
   float span=Vector3.Distance(dynamics.BracePoint(ea),dynamics.BracePoint(eb));float peakAngle=0;
   for(int tick=0;tick<360;tick++){
    dynamics.Step(1f/60,0,tick<180?1:-1,false,false);
    var pose=dynamics.Pose(joint);var deformation=dynamics.OutputDeformation(joint);
    if(!float.IsFinite(pose.Position.LengthSquared())||pose.Position.Length()>30||dynamics.Velocity(joint).Length()>50)throw new Exception("Brace caused unstable moving body: "+moving.Label);
    if(Math.Abs(Vector3.Distance(dynamics.BracePoint(ea),dynamics.BracePoint(eb))-span)>.12f)throw new Exception("Brace endpoint span drift: "+moving.Label);
    Matrix4x4.Decompose(deformation,out _,out var q,out _);peakAngle=Math.Max(peakAngle,2*MathF.Acos(Math.Clamp(Math.Abs(q.W),0,1)));
   }
   if(reference==13&&peakAngle<.1f)throw new Exception("Axial brace incorrectly locks steering rotation");
   Console.WriteLine($"PASS: {moving.Label} moving-output brace to wooden beam; finite six-second simulation; held endpoint span; relative rotation={peakAngle:0.000} rad.");
  }
  Console.WriteLine($"PASS: two-point brace validation; duplicate rejection; snapshot reload; detached endpoint held ({length:0.000} -> {after:0.000} m); endpoint deletion cleanup.");
 }
}
