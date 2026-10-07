using GoblinScrapper.Construction;
using System.Numerics;
namespace ByteEngine.Tests;
internal static class SuspensionStressTests {
 public static void Run(string project){
  var c=VehiclePartCatalog.Load(Path.Combine(project,"Assets/GarageUI/parts-catalog.json"));var suspension=c.Parts.Values.Single(d=>d.ReferenceId==16);
  // Verify that reinforcement preserves the slider's intended axial travel.
  var slider=new VehicleAssembly(c);slider.Parts[1]=new(1,suspension.File,0,new(0,1,0),Quaternion.Identity);
  using(var spring=new ContraptionPhysicsWorld(slider,new(0,10,0),Quaternion.Identity,false)){
   spring.Activate();float moved=0;for(int i=0;i<240;i++){spring.Step(1f/60,0,0,false,false);Matrix4x4.Decompose(spring.OutputDeformation(1),out _,out _,out var delta);moved=Math.Max(moved,Math.Abs(Vector3.Dot(delta,suspension.Axis)));}
   if(moved<.1f||moved>.43f)throw new Exception("Suspension lost its intended spring-axis travel: "+moved);Console.WriteLine($"PASS: suspension retains axial motion ({moved:F3} m).");
  }
  foreach(bool braced in new[]{false,true})foreach(float speed in new[]{1f,4f}){
   var car=ContraptionTests.Build(c);int next=car.Parts.Keys.Max()+1;
   var axis=Vector3.Normalize(suspension.Axis);var q=Vector3.Dot(axis,Vector3.UnitY)>.999f?Quaternion.Identity:Vector3.Dot(axis,Vector3.UnitY)<-.999f?Quaternion.CreateFromAxisAngle(Vector3.UnitX,MathF.PI):Quaternion.Normalize(new Quaternion(Vector3.Cross(axis,Vector3.UnitY),1+Vector3.Dot(axis,Vector3.UnitY)));
   var joints=new List<int>();
   foreach(var wheel in car.Parts.Values.Where(p=>c[p.File].ReferenceId==46).ToArray()){
    int id=next++;joints.Add(id);car.Parts[id]=new(id,suspension.File,wheel.Parent,wheel.Position+Vector3.UnitY*.2f,q);
    var tuned=wheel with{Parent=id,ParentBone="Moving"};tuned=BlockTuning.Set(tuned,46,"speed",speed);tuned=BlockTuning.Set(tuned,46,"grip",1.5f);tuned=BlockTuning.Set(tuned,46,"torque",600);car.Parts[wheel.Id]=tuned;
   }
   if(braced){for(int i=0;i<joints.Count-2;i++){
    var e1=new BraceEndpoint(joints[i],suspension.Sockets.First(s=>s.Bone=="Root").Name);var e2=new BraceEndpoint(joints[i+2],suspension.Sockets.First(s=>s.Bone=="Root").Name);if(car.AddBrace(e1,e2)<0)throw new Exception("Stress brace fixture rejected");
   }}
   using var world=new ContraptionPhysicsWorld(car,new(0,car.RideHeight+.25f,0),Quaternion.Identity);
   float lateral=0,tilt=0,travel=0,braceError=0;var spans=car.Braces.Values.ToDictionary(b=>b.Id,b=>Vector3.Distance(world.BracePoint(b.A),world.BracePoint(b.B)));
   for(int tick=0;tick<720;tick++){
    float throttle=tick<120?0:tick<420?1:tick<480?0:-1;world.Step(1f/60,throttle,0,tick>=420&&tick<480,false);
    foreach(int id in joints){Matrix4x4.Decompose(world.OutputDeformation(id),out _,out var rot,out var delta);var slide=suspension.Axis*Vector3.Dot(delta,suspension.Axis);lateral=Math.Max(lateral,(delta-slide).Length());tilt=Math.Max(tilt,2*MathF.Acos(Math.Clamp(Math.Abs(rot.W),0,1)));travel=Math.Max(travel,Math.Abs(Vector3.Dot(delta,suspension.Axis)));}
    foreach(var b in car.Braces.Values)braceError=Math.Max(braceError,Math.Abs(Vector3.Distance(world.BracePoint(b.A),world.BracePoint(b.B))-spans[b.Id]));
    if(!float.IsFinite(world.Pose(0).Position.LengthSquared()))throw new Exception("Suspension produced invalid simulation");
   }
   Console.WriteLine($"braced={braced} speed={speed} grip=1.5 torque=600: lateral={lateral:F4}m tilt={tilt:F4}rad travel={travel:F4}m braceError={braceError:F4}m");
   if(lateral>.03f||tilt>.05f||travel>.43f||braceError>.03f)throw new Exception("Suspension/brace stress exceeded stable joint limits");
  }
 }
}
