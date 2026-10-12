using System.Numerics;
using System.Reflection;
using System.Text.Json;
using DuneCompany;
namespace ByteEngine.Tests;
internal sealed partial class DunePolishDiagnostic
{
 void RunSpringStability(string root){
  var scene=_project!.Scenes.Load(Path.Combine(root,"Scenes/Workshop.bytescene"));Scenes.LoadScene(scene);Tick(scene,new(.5f),false);
  var w=scene.GameObjects.SelectMany(o=>o.Components).OfType<DuneWorkshop3D>().Single();
  var blocks=new List<PlacedBlock>();void Add(int type,int parent,Vector3 p,Quaternion q,bool moving=false){var b=new PlacedBlock{Id=blocks.Count,Type=type,Parent=parent,MovingMount=moving};b.Pose(p,q);blocks.Add(b);}
  Add(3,-1,Vector3.Zero,Quaternion.Identity);Add(3,0,new(0,0,2),Quaternion.Identity);Add(24,0,new(0,.5f,0),Quaternion.Identity);Add(25,1,new(0,.57f,2),Quaternion.Identity);
  foreach(int side in new[]{-1,1})foreach(float z in new[]{-.6f,2.6f}){var q=Quaternion.CreateFromAxisAngle(Vector3.UnitY,-side*MathF.PI*.5f);var p=new Vector3(side*.52f,0,z);Add(32,0,p,q);int parent=blocks[^1].Id;Add(13,parent,p+Vector3.Transform(new(0,.25f,-.94f),q),q,true);}
  if(Environment.GetEnvironmentVariable("DUNE_SPRING_VEHICLE") is {Length:>0} vehicle)blocks=BuildStore.Read(File.ReadAllText(vehicle),JsonSerializer.Deserialize<List<DunePart>>(File.ReadAllText(Path.Combine(root,"Assets/DuneParts/catalog.json")),BuildStore.Json)!.ToDictionary(d=>d.Id));
  var save=Path.Combine(root,"Saves/vehicle.json");File.WriteAllText(save,JsonSerializer.Serialize(blocks,BuildStore.Json));typeof(DuneWorkshop3D).GetField("_blocks",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(w,blocks);typeof(DuneWorkshop3D).GetMethod("BuildVisuals",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(w,null);typeof(DuneWorkshop3D).GetMethod("SetGarageEditing",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(w,new object[]{true});Capture(scene,Path.Combine(root,"Preview/spring-stability-build.png"),false);w.Command("Toggle drive");
  var physics=(LandVehiclePhysics)typeof(DuneWorkshop3D).GetField("_physics",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(w)!;physics.SetSolverProfile(6,2);if(physics.SolverSubsteps!=4)throw new Exception("Spring-arm minimum solver profile was not applied");physics.AddFlatGround(-1);
  float maxIdle=0,maxLateral=0,maxAxis=0;for(int i=0;i<600;i++){Tick(scene,new(.5f),false);if(i>300)maxIdle=Math.Max(maxIdle,physics.Velocity.Length());foreach(var b in blocks.Where(b=>b.Type==32))maxLateral=Math.Max(maxLateral,physics.SuspensionLateralError(b.Id));foreach(var b in blocks.Where(b=>b.Type==32))maxAxis=Math.Max(maxAxis,physics.SuspensionAngularError(b.Id));foreach(var b in blocks.Where(b=>b.Type==13))maxAxis=Math.Max(maxAxis,physics.WheelAxisError(b.Id));}
  Console.WriteLine($"SPRING idle={maxIdle} lateral={maxLateral} axis={maxAxis} position={physics.Frame.Position}");Capture(scene,Path.Combine(root,"Preview/spring-stability-idle.png"),false);
  if(maxIdle>.15f||maxLateral>.04f||maxAxis>6)throw new Exception("Spring-arm chassis shakes or joints lose alignment at rest");
  for(int i=0;i<900;i++){physics.Step(1f/60,1,i%240<120?1:-1,false,32);foreach(var b in blocks.Where(b=>b.Type==32))maxLateral=Math.Max(maxLateral,physics.SuspensionLateralError(b.Id));foreach(var b in blocks.Where(b=>b.Type==32))maxAxis=Math.Max(maxAxis,physics.SuspensionAngularError(b.Id));foreach(var b in blocks.Where(b=>b.Type is 13 or 15 or 37))maxAxis=Math.Max(maxAxis,physics.WheelAxisError(b.Id));}Console.WriteLine($"SPRING stress lateral={maxLateral} axis={maxAxis}");if(maxLateral>.06f||maxAxis>6)throw new Exception("Spring-arm joints lose alignment under driving load");Console.WriteLine($"SPRING moving={physics.Velocity} position={physics.Frame.Position}");Capture(scene,Path.Combine(root,"Preview/spring-stability-drive.png"),false);Close();
 }
}






