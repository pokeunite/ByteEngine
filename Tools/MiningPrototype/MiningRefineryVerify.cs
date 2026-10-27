using System.Numerics;
using System.Reflection;
using ByteEngine.Core;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;
using KarlsonDemo.Plugin;
internal static class MiningRefineryVerify
{
 static void Check(bool ok,string text){if(!ok)throw new Exception(text);Console.WriteLine("PASS REFINERY "+text);}
 public static void Run(Scene scene,MiningOperation3D op)
 {
  var host=new SceneManager();host.LoadScene(scene);var station=scene.FindGameObject("Refinery station")!;var renderer=station.GetComponent<SkeletalMeshRenderer>()!;var output=station.GetComponent<MiningRefineryOutput3D>()!;var driver=station.GetComponent<MiningMachineAnimation3D>()!;
  op.GameObject.GetComponent<MiningTrade3D>()!.Enabled=false;
  void Set(string name,int value)=>typeof(MiningOperation3D).GetProperty(name)!.SetValue(op,value);
  void Frame(float dt=.05f){typeof(Time).GetMethod("Update",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[(double)dt]);typeof(Scene).GetMethod("UpdateInternal",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(scene,null);}
  Set("RefinedOre",0);Set("RawOre",30);Frame();Check(renderer.HasAnimation("Operate"),"dedicated refinery clip loads");Check(output.ModelAnchorsResolved,"outlet and bulk-bin anchors resolve");
  Check(op.BeginRefining(),"actual raw ore batch starts");Frame();renderer.TryGetBoneWorldMatrix("RefinerySlat00",out var before);renderer.TryGetBoneWorldMatrix("Drum",out var drumBefore);for(int i=0;i<5;i++)Frame();renderer.TryGetBoneWorldMatrix("RefinerySlat00",out var after);renderer.TryGetBoneWorldMatrix("Drum",out var drumAfter);
  Check(Vector3.Distance(before.Translation,after.Translation)>.1f,"segmented conveyor advances");Check(drumBefore!=drumAfter&&driver.Running,"processing rotor turns during refining");
  renderer.TryGetBoneWorldMatrix("OutputStart",out var start);renderer.TryGetBoneWorldMatrix("OutputEnd",out var end);renderer.TryGetBoneWorldMatrix("BucketFloor",out var floor);
  Console.WriteLine($"REFINERY WORLD start={start.Translation} end={end.Translation} bin={floor.Translation}");
  Check(Vector3.Distance(start.Translation,end.Translation)>1.5f,"continuous conveyor reaches out from chamber");Check(end.Translation.Y>floor.Translation.Y+1.2f,"outlet is above collection bin");
  var belt=end.Translation-start.Translation;belt.Y=0;belt=Vector3.Normalize(belt);var toBin=floor.Translation-end.Translation;toBin.Y=0;
  Check(Vector3.Cross(belt,toBin).Length()<.02f,"bin is centred on conveyor discharge line");Check(Vector3.Dot(belt,toBin)>.5f&&Vector3.Dot(belt,toBin)<1.2f,"belt lip overhangs inside the bin opening");
  bool sawFlight=false;for(int i=0;i<160;i++){Frame();sawFlight|=output.TravellingIngots>0;}
  Check(sawFlight,"completed batch visibly moves across belt and drops");Check(op.RefinedOre==op.RefiningOutput&&op.RefinedOre>0,"display does not create extra refined cargo");Check(output.VisiblePileIngots>0,"real refined stock creates an ingot pile");
  for(int i=0;i<150;i++)Frame();Check(!output.HasDeliveryWork&&!driver.Running,"refinery stops after processing and output delivery finish");
  Set("RefinedOre",op.CargoCapacity);for(int i=0;i<600;i++){Frame(.1f);if(i>0&&!output.HasDeliveryWork)break;}
  float units=Math.Max(output.UnitsPerIngot,op.CargoCapacity/(float)output.MaximumVisibleIngots);int expected=(int)Math.Ceiling(op.CargoCapacity/units);
  Check(output.VisiblePileIngots==expected&&output.TravellingIngots==0,"full cargo produces a bounded settled pile");
  var poses=(Matrix4x4[])typeof(MiningRefineryOutput3D).GetField("poses",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(output)!;
  Check(poses.Take(output.VisiblePileIngots).All(m=>m.Translation.Y>floor.Translation.Y&&m.Translation.Y<floor.Translation.Y+.85f),"all pile layers remain inside deep bin");
  int old=output.VisiblePileIngots;Set("RefinedOre",op.CargoCapacity/2);Frame();Check(output.VisiblePileIngots<old,"market stock reduction removes ingots");Set("RefinedOre",0);Frame();Check(output.VisiblePileIngots==0&&!output.HasDeliveryWork,"selling all refined cargo empties the bin");
  Check(!scene.GameObjects.Any(o=>o.Name.StartsWith("Refined ingot visual")),"bounded batch uses no per-ingot scene objects");host.UnloadScene();Console.WriteLine("REFINERY NATIVE VERIFICATION COMPLETE");
 }
}
