using System.Numerics;
using System.Reflection;
using ByteEngine.Core;
using ByteEngine.Core.Audio;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;
using KarlsonDemo.Plugin;
internal static class MiningPressVerify
{
 static void Check(bool ok,string text){if(!ok)throw new Exception(text);Console.WriteLine("PASS PRESS "+text);}
 public static void Run(Scene scene,MiningOperation3D op)
 {
  var host=new SceneManager();host.LoadScene(scene);
  var press=scene.FindGameObject("Ammo press station")!;var renderer=press.GetComponent<SkeletalMeshRenderer>()!;var bucket=press.GetComponent<MiningAmmoBucket3D>()!;
  var machine=press.GetComponent<MiningMachineAnimation3D>()!;var trade=op.GameObject.GetComponent<MiningTrade3D>()!;
  trade.Enabled=false;bucket.AutoPickup=false;
  void Frame(float dt=.05f){typeof(Time).GetMethod("Update",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[(double)dt]);typeof(Scene).GetMethod("UpdateInternal",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(scene,null);}
  void Stock(float value)=>typeof(MiningTrade3D).GetProperty("StoredAmmo")!.SetValue(trade,value);
  Stock(0);Frame();Frame();Check(renderer.Play("Operate",true,0),"exported Operate clip loads");
  Vector3 Anchor(string name){Check(renderer.TryGetBoneWorldMatrix(name,out var matrix),"imported marker/part "+name);return matrix.Translation;}
  var start=Anchor("BeltStart");var end=Anchor("BeltEnd");var floor=Anchor("BucketFloor");
  Console.WriteLine($"PRESS WORLD start={start} end={end} floor={floor}");
  Check(Vector3.Distance(start,end)>1.8f&&Vector3.Distance(start,end)<2.1f,"conveyor endpoints retain model scale");
  Check(start.Y>floor.Y+.9f&&end.Y>floor.Y+.9f,"tray is below belt");Check(bucket.ModelAnchorsResolved,"visual delivery uses imported model markers");
  var ram0=Anchor("Ram");renderer.TryGetBoneWorldMatrix("BeltSlat00",out var slat0);renderer.TryGetBoneWorldMatrix("Flywheel",out var wheel0);
  Frame(.18f);renderer.TryGetBoneWorldMatrix("Ram",out var ram1);renderer.TryGetBoneWorldMatrix("BeltSlat00",out var slat1);renderer.TryGetBoneWorldMatrix("Flywheel",out var wheel1);
  Check(Vector3.Distance(ram0,ram1.Translation)>.03f,"ram makes an actual stroke");Check(Vector3.Distance(slat0.Translation,slat1.Translation)>.05f,"belt slats translate");Check(wheel0!=wheel1,"flywheel rotates");
  for(int i=0;i<40;i++){Stock(Math.Min(60,i*1.5f));Frame(.05f);}Check(bucket.TravellingRounds>0,"new production travels on the conveyor");
  Check(bucket.VisiblePileRounds>0,"delivered rounds accumulate");
  Stock(trade.Capacity);for(int i=0;i<60;i++)Frame();Check(!machine.Running,"full hopper pauses mechanical production");Check(bucket.TravellingRounds==0,"in-flight rounds finish delivery after stopping");Check(bucket.VisiblePileRounds==Math.Min(bucket.MaximumVisibleRounds,(int)Math.Ceiling(trade.Capacity/(float)bucket.RoundsPerVisual)),"pile matches stored ammunition at capacity");
  Check(!scene.GameObjects.Any(o=>o.Name=="Produced ammo visual"),"no per-round GameObjects or physics bodies");
  var matrices=(Matrix4x4[])typeof(MiningAmmoBucket3D).GetField("poses",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(bucket)!;
  for(int i=0;i<bucket.VisiblePileRounds;i++){var p=matrices[i].Translation;Check(p.Y>floor.Y&&p.Y<floor.Y+.4f,"pile layer stays inside tray "+i);}
  Stock(0);Frame();Check(bucket.VisiblePileRounds==0&&bucket.TravellingRounds==0,"collecting all stock clears pile and transit");Check(machine.Running,"production resumes with free capacity");
  var player=scene.FindGameObject(op.PlayerObjectId)!;var weapon=player.GetComponent<FpsGunLab3D>()!.SelectedWeapon!;weapon.SetAmmo(weapon.CurrentAmmo,0);Stock(40);bucket.AutoPickup=true;player.Transform.WorldPosition=new(floor.X,player.Transform.WorldPosition.Y,floor.Z);Frame();Frame();
  Check(bucket.LastCollected==40&&weapon.ReserveAmmo==40&&trade.StoredAmmo==0,"walk-up pickup transfers the exact stock once");Check(bucket.VisiblePileRounds==0&&bucket.TravellingRounds==0,"walk-up collection clears visible stock");bucket.AutoPickup=false;
  var audio=op.GameObject.GetComponent<MiningAudio3D>()!;Check(audio.RefineryVolumeMultiplier==3,"refinery gain exposed and increased 3.75 times");
  typeof(MiningOperation3D).GetProperty("RawOre")!.SetValue(op,40);Check(op.BeginRefining(),"refinery starts a real batch");for(int i=0;i<8;i++)Frame();
  var source=scene.GameObjects.Single(o=>o.Name=="Mining loop - Refinery").GetComponent<AudioSource3D>()!;
  Check(source.IsPlaying&&source.Volume>.5f,"refinery native loop plays at raised gain");
  host.UnloadScene();Console.WriteLine("REFERENCE PRESS NATIVE VERIFICATION COMPLETE");
 }
}
