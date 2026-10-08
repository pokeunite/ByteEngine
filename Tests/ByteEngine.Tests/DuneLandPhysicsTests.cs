using System.Numerics;
using DuneCompany;
using System.Text.Json;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;
namespace ByteEngine.Tests;
internal static class DuneLandPhysicsTests
{
 public static void Run(string catalogue,int simulationHz=0)
 {
  var snap=DuneWorkshop3D.SnapFace(new(.38f,.5f,-.12f),Vector3.UnitY,new(-1,0,-1),new(1,.5f,1));Check(snap==new Vector3(.5f,.5f,0),"Structural mounts snap accurately to a centred quarter-metre grid");var offsetSnap=DuneWorkshop3D.SnapFace(new(2.37f,1,-.12f),Vector3.UnitY,new(1,0,-1),new(3,1,1));Check(offsetSnap==new Vector3(2.25f,1,0),"Grid alignment remains stable on offset faces");
  var catalog=JsonSerializer.Deserialize<List<DunePart>>(File.ReadAllText(catalogue),BuildStore.Json)!.ToDictionary(p=>p.Id);
  var defs=catalog.Values.ToDictionary(d=>d.Id,d=>new VehiclePartPhysics(new(d.Low,d.High),new(d.Low,d.High),Vector3.Zero,Vector3.UnitY,false));
  defs[3]=new(new(new(-.6f,0,-1.2f),new(.6f,.3f,1.2f)),default,Vector3.Zero,Vector3.UnitY,false);
  foreach(int id in new[]{13,14,15})defs[id]=new(new(new(-.06f,-.06f,.005f),new(.06f,.06f,.075f)),new(catalog[id].Low,catalog[id].High),new(0,0,catalog[id].WheelCenterZ),Vector3.UnitZ,true);
  defs[18]=new(new(new(-.1f,0,-.1f),new(.1f,.2f,.1f)),new(new(-.08f,.18f,-.08f),new(.18f,.26f,.08f)),new(0,.2f,0),Vector3.UnitY,true);
  defs[16]=new(new(new(-.1f,0,-.1f),new(.1f,.2f,.1f)),new(new(-.1f,.4f,-.1f),new(.1f,.6f,.1f)),new(0,.5f,0),Vector3.UnitY,true);
  List<PlacedBlock> Cart(bool hinges){var list=new List<PlacedBlock>{new(){Id=0,Type=3}};void Add(int type,int parent,Vector3 pos,Quaternion q,bool moving=false){var p=new PlacedBlock{Id=list.Count,Type=type,Parent=parent,MovingMount=moving};p.Pose(pos,q);list.Add(p);}Add(23,0,new(0,.3f,.5f),Quaternion.Identity);Add(25,0,new(0,.3f,-.5f),Quaternion.Identity);foreach(int z in new[]{-1,1})foreach(int x in new[]{-1,1}){int parent=0;bool moving=false;if(hinges&&z<0){Add(18,0,new(x*.6f,.1f,z*.85f),Quaternion.Identity);parent=list.Last().Id;moving=true;}Add(13,parent,new(x*.9f,.1f,z*.85f),Quaternion.CreateFromAxisAngle(Vector3.UnitY,-x*MathF.PI*.5f),moving);}return list;}
  using(var budget=new LandVehiclePhysics(Cart(false),catalog,defs,new(0,2,0),Quaternion.Identity,simulationHz:60,maximumCatchUpSteps:2)){
   budget.AddFlatGround();for(int i=0;i<120;i++)budget.Step(1f/60,0,0,false,24);
   Check(budget.DroppedSimulationSeconds==0,"Normal-rate frames preserve all simulation time");
   budget.Step(.2f,0,0,false,24);Check(budget.LastStepCount==2&&budget.DroppedSimulationSeconds>.15,"A long stall has bounded catch-up work");
   budget.Step(1f/60,0,0,false,24);Check(budget.LastStepCount==1&&float.IsFinite(budget.RenderFrame.Position.Y),"The frame after a stall resumes normally without old catch-up debt");
  }
  using(var world=new LandVehiclePhysics(Cart(false),catalog,defs,new(0,2,0),Quaternion.Identity,simulationHz:simulationHz)){
   world.AddFlatGround();world.Step(1f/120,0,0,false,24);Check(world.Frame.Position.Y>1.99f,"Gravity starts with continuous free fall, not terrain snapping");for(int i=0;i<240;i++)world.Step(1f/120,0,0,false,24);Check(world.Frame.Position.Y<1&&world.Frame.Position.Y>-.2f&&world.ContactCount>=2,"Tyres physically support the chassis");
   var start=world.Frame.Position;for(int i=0;i<480;i++)world.Step(1f/120,1,0,false,24);Console.WriteLine(world.DebugSnapshot());Check(world.Frame.Position.Z<start.Z-5,"Bounded axle motors produce forward tyre traction");float speed=world.Velocity.Length();for(int i=0;i<360;i++)world.Step(1f/120,0,0,true,24);Console.WriteLine("BRAKE "+world.Velocity);Check(world.Velocity.Length()<Math.Max(.5f,speed*.25f),"Wheel brake torque slows the physical assembly");
  }
  using(var flat=new LandVehiclePhysics(Cart(false),catalog,defs,new(0,1,0),Quaternion.Identity,simulationHz:simulationHz)){
   flat.AddFlatGround();for(int i=0;i<300;i++)flat.Step(1f/60,0,0,false,24);
   float low=float.MaxValue,high=float.MinValue,maxVy=0;for(int i=0;i<300;i++){flat.Step(1f/60,0,0,false,24);low=Math.Min(low,flat.Frame.Position.Y);high=Math.Max(high,flat.Frame.Position.Y);maxVy=Math.Max(maxVy,Math.Abs(flat.Velocity.Y));}
   Console.WriteLine($"FLAT IDLE height range={high-low:F6}m maximum vertical speed={maxVy:F6}m/s");Check(high-low<.02f,"Settled vehicle does not hop on flat ground");
   int held=0;var prior=flat.RenderFrame.Position;for(int i=0;i<360;i++){flat.Step(1f/90,1,0,false,24);var next=flat.RenderFrame.Position;if(i>90&&Vector3.DistanceSquared(prior,next)<1e-10f)held++;prior=next;}Check(held==0,$"90 Hz presentation interpolates motion between {flat.SimulationHz} Hz physics ticks");
  }
  using(var world=new LandVehiclePhysics(Cart(true),catalog,defs,new(0,1,0),Quaternion.Identity,simulationHz:simulationHz)){
   world.AddFlatGround();for(int i=0;i<240;i++)world.Step(1f/120,0,0,false,24);var initial=world.Frame.Rotation;for(int i=0;i<480;i++)world.Step(1f/120,1,1,false,24);Console.WriteLine(world.DebugSnapshot());Check(Math.Abs(Quaternion.Dot(initial,world.Frame.Rotation))<.98f,"Steering joints turn the chassis through tyre forces");Check(float.IsFinite(world.Velocity.LengthSquared())&&world.Frame.Position.Y>-.5f,"Articulated steering remains stable under acceleration");
  }
  using(var world=new LandVehiclePhysics(Cart(false),catalog,defs,new(0,1,0),Quaternion.Identity,simulationHz:simulationHz)){
   world.AddFlatGround();world.AddObstacle(10,new(0,3,-7),new(20,6,1),Quaternion.Identity);for(int i=0;i<720;i++)world.Step(1f/120,1,0,false,24);Check(world.Frame.Position.Z>-7.5f,"Vehicle collides with an obstacle instead of passing through it");
  }
  using(var drift=new LandVehiclePhysics(Cart(true),catalog,defs,new(0,1,0),Quaternion.Identity,simulationHz:simulationHz)){
   drift.AddFlatGround();for(int i=0;i<480;i++)drift.Step(1f/120,1,0,false,24);
   for(int i=0;i<60;i++)drift.Step(1f/120,1,.7f,false,24,handbrake:true);
   var rear=Cart(true).Where(b=>catalog[b.Type].Wheel&&b.P.Z>0).ToArray();var front=Cart(true).Where(b=>catalog[b.Type].Wheel&&b.P.Z<0).ToArray();
   Check(drift.DriftBlend>.95f&&rear.All(b=>drift.WheelGrip(b.Id)<front.Min(f=>drift.WheelGrip(f.Id))*.5f),"Handbrake releases rear traction while front tyres retain steering grip");
   Check(float.IsFinite(drift.Velocity.LengthSquared())&&drift.Frame.Position.Y>-.5f,"Handbrake drift remains physically supported and finite");
   for(int i=0;i<180;i++)drift.Step(1f/120,1,0,false,24);
   Check(drift.DriftBlend<.02f&&rear.All(b=>Math.Abs(drift.WheelGrip(b.Id)-front[0].Grip*.8f)<.02f),"Rear grip progressively recovers after releasing drift");
  }
  float Coast(float resistance){using var world=new LandVehiclePhysics(Cart(false),catalog,defs,new(0,1,0),Quaternion.Identity,simulationHz:simulationHz);world.AddFlatGround();for(int i=0;i<360;i++)world.Step(1f/120,1,0,false,24);for(int i=0;i<120;i++)world.Step(1f/120,0,0,false,24,sandResistance:_=>resistance);return world.Velocity.Length();}
  Check(Coast(.18f)<Coast(0)-.3f,"Loose-soil rolling resistance measurably slows coasting through wheel contacts");
  var suspended=new List<PlacedBlock>{new(){Id=0,Type=3},new(){Id=1,Type=23,Parent=0,Position=[0,.3f,.5f]},new(){Id=2,Type=25,Parent=0,Position=[0,.3f,-.5f]}};
  foreach(int z in new[]{-1,1})foreach(int x in new[]{-1,1}){int joint=suspended.Count;var suspension=new PlacedBlock{Id=joint,Type=16,Parent=0};suspension.Pose(new(x*.65f,.3f,z*.85f),Quaternion.CreateFromAxisAngle(Vector3.UnitX,MathF.PI));suspended.Add(suspension);var wheel=new PlacedBlock{Id=suspended.Count,Type=13,Parent=joint,MovingMount=true};wheel.Pose(new(x*.9f,-.2f,z*.85f),Quaternion.CreateFromAxisAngle(Vector3.UnitY,-x*MathF.PI*.5f));suspended.Add(wheel);}
  using(var world=new LandVehiclePhysics(suspended,catalog,defs,new(0,1.5f,0),Quaternion.Identity,simulationHz:simulationHz)){world.AddFlatGround();for(int i=0;i<360;i++)world.Step(1f/120,0,0,false,24);Check(suspended.Where(b=>b.Type==16).All(b=>Math.Abs(world.SuspensionOffset(b.Id))<=b.Travel+.025f),"Loaded suspension obeys its travel limits");Check(suspended.Where(b=>b.Type==16).All(b=>world.SuspensionLateralError(b.Id)<.01f),"Suspension holds its lateral mount under vehicle load");world.Impulse(new(0,350,0));for(int i=0;i<600;i++)world.Step(1f/120,1,0,false,24);Check(world.Frame.Position.Y>-.5f&&float.IsFinite(world.Velocity.LengthSquared())&&suspended.Where(b=>b.Type==13).All(b=>world.WheelAxisError(b.Id)<.5f),"Suspension and tyre joints remain aligned after a jump and powered landing");Check(suspended.Where(b=>b.Type==16).All(b=>world.SuspensionLateralError(b.Id)<.015f),"Suspension cannot slide fore/aft after powered landing");}
  using(var left=new LandVehiclePhysics(Cart(false),catalog,defs,new(0,1,0),Quaternion.Identity,simulationHz:simulationHz))using(var right=new LandVehiclePhysics(Cart(false),catalog,defs,new(0,1,0),Quaternion.Identity,simulationHz:simulationHz)){left.AddFlatGround();right.AddFlatGround();for(int i=0;i<480;i++){left.Step(1f/120,1,-1,false,24);right.Step(1f/120,1,1,false,24);}Check(Vector3.Distance(left.Frame.Position,right.Frame.Position)<.001f&&Math.Abs(Quaternion.Dot(left.Frame.Rotation,right.Frame.Rotation))>.99999f,"A/D cannot alter driving without a steering mechanism");}
  Vector3 AtRate(int rate){using var world=new LandVehiclePhysics(Cart(false),catalog,defs,new(0,1,0),Quaternion.Identity,simulationHz:simulationHz);world.AddFlatGround();for(int i=0;i<rate*4;i++)world.Step(1f/rate,1,0,false,24);return world.Frame.Position;}
  Check(Vector3.Distance(AtRate(30),AtRate(120))<.01f,"Fixed-timestep simulation agrees at 30 and 120 render FPS");
  var scene=new Scene("Terrain regression");var terrain=scene.CreateGameObject("Uneven sand").AddComponent(new TestTerrain());
  using(var world=new LandVehiclePhysics(Cart(false),catalog,defs,new(0,1,0),Quaternion.Identity,terrain,simulationHz:simulationHz)){
   for(int i=0;i<240;i++)world.Step(1f/120,0,0,false,24);Check(world.Frame.Position.Y>-.2f&&world.ContactCount>=2,"One-sided sand mesh supports tyres from above across chunk seams");
   terrain.Depth=.12f;world.MarkAllTerrainChanged();for(int i=0;i<480;i++)world.Step(1f/120,1,0,false,24);Check(world.Frame.Position.Y>-.35f&&world.Frame.Position.Z<-5,"Rut collider rebuilds preserve tyre traction and ground support");
  }
  // Tunable suspension must alter the physical response, not just a UI number.
  float Compression(float rate,float damping){var blocks=BuildStore.Read(JsonSerializer.Serialize(suspended,BuildStore.Json),catalog);foreach(var b in blocks.Where(b=>b.Type==16)){b.SpringRate=rate;b.Damping=damping;}using var w=new LandVehiclePhysics(blocks,catalog,defs,new(0,1.5f,0),Quaternion.Identity,simulationHz:simulationHz);w.AddFlatGround();for(int i=0;i<600;i++)w.Step(1f/120,0,0,false,24);return blocks.Where(b=>b.Type==16).Average(b=>Math.Abs(w.SuspensionOffset(b.Id)));}
  float soft=Compression(2,1),stiff=Compression(9,1);Console.WriteLine($"Spring load: soft={soft:0.0000} m / stiff={stiff:0.0000} m");Check(soft>stiff+.005f,"Spring stiffness changes measured loaded suspension compression");
  var tuned=suspended[3];tuned.SpringRate=7;tuned.Damping=.4f;tuned.Preload=-.05f;tuned.SpeedLimit=17;tuned.BrakeStrength=1.7f;var roundtrip=BuildStore.Read(JsonSerializer.Serialize(suspended,BuildStore.Json),catalog).Single(b=>b.Id==tuned.Id);Check(roundtrip.SpringRate==7&&roundtrip.Damping==.4f&&roundtrip.Preload==-.05f&&roundtrip.SpeedLimit==17&&roundtrip.BrakeStrength==1.7f,"Mechanical customization survives vehicle save/reload");

  defs[20]=new(new(new(-.1f,0,-.1f),new(.1f,.2f,.1f)),new(new(-.1f,.15f,-.1f),new(.1f,.25f,.1f)),new(0,.2f,0),Vector3.UnitY,true);
  var towingCart=Cart(true);var hitch=new PlacedBlock{Id=towingCart.Count,Type=20,Parent=0};hitch.Pose(new(0,0,1.7f),Quaternion.CreateFromAxisAngle(Vector3.UnitY,MathF.PI));towingCart.Add(hitch);
  var buggy=Cart(false).Where(b=>b.Type!=23&&b.Type!=25).ToList();
  using(var tow=new LandVehiclePhysics(towingCart,catalog,defs,new(0,1,0),Quaternion.Identity,simulationHz:simulationHz)){
   tow.AddFlatGround();tow.AddRecoveryVehicle(buggy,defs,new(0,1,8),Quaternion.Identity,new(0,.2f,-1.4f));
   Check(!tow.ConnectRecovery(0)&&!tow.ConnectRecovery(hitch.Id),"Reject self/non-hinge and distant attachment");
   for(int i=0;i<180;i++)tow.Step(1f/60,0,0,true,24);
   var hp=tow.TrailerHitch(hitch.Id);tow.ResetRecovery(hp+new Vector3(0,-.2f,1.65f),Quaternion.Identity);
   for(int i=0;i<180;i++)tow.Step(1f/60,0,0,true,24);
   Console.WriteLine("HITCH CHECK "+tow.HitchAvailability(hitch.Id)+" / gap="+Vector3.Distance(tow.TrailerHitch(hitch.Id),tow.TargetHitch));
   var beforeHitch=tow.RecoveryFrame().Position;Check(tow.ConnectRecovery(hitch.Id),"Nearby aligned stationary target attaches without pose snapping");Check(Vector3.Distance(beforeHitch,tow.RecoveryFrame().Position)<.00001f,"Hitching does not teleport the target");Check(!tow.ConnectRecovery(hitch.Id),"Duplicate attachment is rejected");
   var start=tow.RecoveryFrame().Position;for(int i=0;i<360;i++)tow.Step(1f/60,1,0,false,24);
   Check(tow.RecoveryFrame().Position.Z<start.Z-3&&float.IsFinite(tow.RecoveryVelocity.LengthSquared()),"Physical truck pulls separate rolling buggy forward");
   tow.DisconnectRecovery();Check(!tow.RecoveryConnected,"Deliberate detachment removes joint");
   hp=tow.TrailerHitch(hitch.Id);tow.ResetTruck(new(0,1,0),Quaternion.Identity);hp=tow.TrailerHitch(hitch.Id);var invalidRotation=Quaternion.CreateFromAxisAngle(Vector3.UnitY,MathF.PI/3);tow.ResetRecovery(hp-Vector3.Transform(new Vector3(0,.2f,-1.4f),invalidRotation),invalidRotation);
   Check(tow.HitchAvailability(hitch.Id)=="Align vehicles within 40 degrees"&&!tow.ConnectRecovery(hitch.Id),"Close but incorrectly aligned hitch is rejected");tow.ResetRecovery(hp+new Vector3(0,-.2f,1.6f),Quaternion.Identity);for(int i=0;i<180;i++)tow.Step(1f/60,0,0,true,24);Check(tow.ConnectRecovery(hitch.Id),"Aligned hitch can be reattached after reset");tow.ResetTruck(new(0,1,0),Quaternion.Identity);Check(!tow.RecoveryConnected,"Reset safely removes an active hitch joint");
  }
  float EngineAcceleration(int count){var truck=Cart(false);for(int i=1;i<count;i++)truck.Add(new(){Id=truck.Max(b=>b.Id)+1,Type=23,Parent=0,Position=[0,.3f,2+i]});using var world=new LandVehiclePhysics(truck,catalog,defs,new(0,1,0),Quaternion.Identity,simulationHz:simulationHz);world.AddFlatGround();for(int i=0;i<180;i++)world.Step(1f/60,0,0,false,32);for(int i=0;i<120;i++)world.Step(1f/60,1,0,false,32);return -world.Velocity.Z;}
  Console.WriteLine($"CONFIGURATION acceleration after 2s: one engine={EngineAcceleration(1):F3}m/s two engines={EngineAcceleration(2):F3}m/s (traction capped)");
  Console.WriteLine("Dune land physics tests passed.");
 }
 sealed class TestTerrain:HeightfieldCollider3D
 {
  public override Vector3 Size {get=>LocalTerrainBounds.Size;set{}} public float Depth;public override int Columns=>70;public override int Rows=>70;public override float CellSpacing=>2;public override Vector2 GridMinimum=>new(-64);
  public override float HeightAt(int x,int z)=>-Depth;public override BoundingBox3D LocalTerrainBounds=>new(new(-64,-1,-64),new(74,0,74));
 }
 static void Check(bool okay,string message){if(!okay)throw new InvalidOperationException(message);Console.WriteLine("PASS "+message);}
}
