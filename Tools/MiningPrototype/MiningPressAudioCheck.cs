using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using ByteEngine.Core;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Audio;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Runtime;
using ByteEngine.Core.Scene;
using KarlsonDemo.Plugin;
internal static class MiningPressAudioCheck
{
 public static void Author(Scene scene,GameProjectRuntime runtime)
 {
  string backup=Program.Root+"/.byteengine/Backups/press-audio-"+DateTime.Now.ToString("yyyyMMdd-HHmmss");Directory.CreateDirectory(backup);File.Copy(Program.Root+"/Scenes/Main.bytescene",backup+"/Main.bytescene");
  string target=Program.Root+"/Assets/Audio/Mining/Ammo Press";Directory.CreateDirectory(target);
  foreach(string file in Directory.GetFiles("artifacts/press-audio","*.wav")){
   if(Path.GetFileName(file)=="press-mix-preview.wav")continue;string dest=target+"/"+Path.GetFileName(file);if(File.Exists(dest))File.Copy(dest,backup+"/"+Path.GetFileName(file));File.Copy(file,dest,true);if(!File.Exists(dest+".meta"))File.WriteAllText(dest+".meta",JsonSerializer.Serialize(new{guid=Guid.NewGuid(),type="AudioClip"}));
  }
  string provenance=Program.Root+"/SourceAudio/Ammo Press";Directory.CreateDirectory(provenance);File.Copy("artifacts/press-audio/SOURCES.json",provenance+"/SOURCES.json",true);
  AssetReference Clip(string name){string path="Assets/Audio/Mining/Ammo Press/"+name+".wav";using var meta=JsonDocument.Parse(File.ReadAllText(Program.Root+"/"+path+".meta"));return new(meta.RootElement.GetProperty("guid").GetGuid(),path);}
  var audio=scene.GameObjects.SelectMany(o=>o.Components.OfType<MiningAudio3D>()).Single();audio.PressLoop=Clip("press-motor");audio.PressConveyorLoop=Clip("press-conveyor");audio.PressStroke=Clip("press-stroke");audio.PressBucketImpact1=Clip("press-bucket-1");audio.PressBucketImpact2=Clip("press-bucket-2");audio.PressBucketImpact3=Clip("press-bucket-3");audio.PressMenuOpen=Clip("press-menu-open");audio.PressConveyorVolume=.48f;audio.PressStrokeVolume=.48f;audio.PressBucketVolume=.58f;audio.PressStrokesPerClip=3;audio.PressStrokeContactPhase=.5f;
  runtime.Serializer.Save(scene,Program.Root+"/Scenes/Main.bytescene");Console.WriteLine("AMMO PRESS LAYERED AUDIO INSTALLED. Backup: "+backup);
 }
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);Console.WriteLine("PASS PRESS AUDIO "+message);}
 public static void Verify(Scene scene,MiningOperation3D op)
 {
  var hash=SHA256.HashData(File.ReadAllBytes(Program.Root+"/Scenes/Main.bytescene"));var host=new SceneManager();host.LoadScene(scene);var audio=op.GameObject.GetComponent<MiningAudio3D>()!;var trade=op.GameObject.GetComponent<MiningTrade3D>()!;trade.Enabled=false;
  var press=scene.FindGameObject(audio.PressObjectId)!;var bucket=press.GetComponent<MiningAmmoBucket3D>()!;bucket.AutoPickup=false;var renderer=press.GetComponent<SkeletalMeshRenderer>()!;var machine=press.GetComponent<MiningMachineAnimation3D>()!;
  void Stock(float amount)=>typeof(MiningTrade3D).GetProperty("StoredAmmo")!.SetValue(trade,amount);
  void Frame(int n=1){for(int i=0;i<n;i++){typeof(Time).GetMethod("Update",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[.05d]);typeof(Scene).GetMethod("UpdateInternal",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(scene,null);}}
  AudioSource3D Loop(string name)=>scene.GameObjects.Single(o=>o.Name=="Mining loop - "+name).GetComponent<AudioSource3D>()!;
  Stock(0);Frame(5);Check(Loop("Press").IsPlaying&&Loop("Press conveyor").IsPlaying,"motor and belt have separate live voices");Check(audio.PressImpactCues==0&&bucket.LandedRoundSequence==0,"empty output does not invent landing sounds");
  var ui=scene.GameObjects.SelectMany(o=>o.Components.OfType<MiningBusinessUi3D>()).Single();ui.Open(MiningScreen.AmmoPress);Check(audio.LastCue==nameof(MiningSoundCue.PressOpen),"press opens with its mechanical click");ui.Close();Check(audio.LastCue==nameof(MiningSoundCue.PanelClose),"press closes with latch cue");
  long stroke=audio.PressStrokeCues;Frame(65);Check(audio.PressStrokeCues>=stroke+2,"stamp cues follow multiple ram strokes and animation wrap");
  Frame(4);Stock(10);Frame(2);Check(bucket.TravellingRounds>0&&audio.PressImpactCues==0,"production starts visible delivery before any clink");
  bool sawLanding=false;for(int i=0;i<60;i++){Frame();if(audio.PressImpactCues>0){Check(bucket.LandedRoundSequence>0,"clinks follow actual tray landings");sawLanding=true;break;}}
  Check(sawLanding,"delivered rounds play tray clatter");Check(!scene.GameObjects.Any(o=>o.Name=="Mining audio voice - AmmoClink"),"old production-time clink is not doubled");
  Stock(trade.Capacity);Frame(20);Check(!machine.Running&&!Loop("Press").IsPlaying,"full storage stops motor and ram");Check(bucket.TravellingRounds>0&&Loop("Press conveyor").IsPlaying,"belt audio continues while final rounds travel");long stoppedStrokes=audio.PressStrokeCues;Frame(65);Check(audio.PressStrokeCues==stoppedStrokes,"paused ram cannot keep stamping");Check(bucket.TravellingRounds==0&&!Loop("Press conveyor").IsPlaying,"belt fades out after last round lands");
  long impacts=audio.PressImpactCues;Stock(0);Frame(3);Check(audio.PressImpactCues==impacts,"collecting stock does not create false impacts");Check(Loop("Press").IsPlaying&&Loop("Press conveyor").IsPlaying,"production sounds resume when storage has room");
  audio.AutomaticCues=false;Frame();Check(!Loop("Press").IsPlaying&&!Loop("Press conveyor").IsPlaying,"automatic audio toggle silences all press layers");
  Check(audio.PooledVoices<=audio.MaximumOneShotVoices&&audio.LoopCount==6,"audio voices and all loop sources stay bounded");var sources=scene.GameObjects.SelectMany(o=>o.Components.OfType<AudioSource3D>()).ToArray();host.UnloadScene();Check(sources.All(s=>!s.IsPlaying),"unload stops every voice");Check(SHA256.HashData(File.ReadAllBytes(Program.Root+"/Scenes/Main.bytescene")).SequenceEqual(hash),"checks preserve scene and saved progression");Console.WriteLine("PRESS AUDIO NATIVE VERIFICATION COMPLETE");
 }
}
