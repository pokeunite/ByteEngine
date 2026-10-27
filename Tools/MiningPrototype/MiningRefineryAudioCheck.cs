using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using ByteEngine.Core;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Audio;
using ByteEngine.Core.Runtime;
using ByteEngine.Core.Scene;
using KarlsonDemo.Plugin;
internal static class MiningRefineryAudioCheck
{
 public static void Author(Scene scene,GameProjectRuntime runtime)
 {
  string folder=Program.Root+"/.byteengine/Backups/refinery-audio-"+DateTime.Now.ToString("yyyyMMdd-HHmmss");Directory.CreateDirectory(folder);File.Copy(Program.Root+"/Scenes/Main.bytescene",folder+"/Main.bytescene");
  string target=Program.Root+"/Assets/Audio/Mining/Refinery";Directory.CreateDirectory(target);
  foreach(string file in Directory.GetFiles("artifacts/refinery-audio","*.wav")){if(Path.GetFileName(file)=="refinery-mix-preview.wav")continue;string dest=target+"/"+Path.GetFileName(file);if(File.Exists(dest))File.Copy(dest,folder+"/"+Path.GetFileName(file));File.Copy(file,dest,true);if(!File.Exists(dest+".meta"))File.WriteAllText(dest+".meta",JsonSerializer.Serialize(new{guid=Guid.NewGuid(),type="AudioClip"}));}
  string provenance=Program.Root+"/SourceAudio/Refinery";Directory.CreateDirectory(provenance);File.Copy("artifacts/refinery-audio/SOURCES.json",provenance+"/SOURCES.json",true);
  AssetReference Clip(string name){string path="Assets/Audio/Mining/Refinery/"+name+".wav";using var meta=JsonDocument.Parse(File.ReadAllText(Program.Root+"/"+path+".meta"));return new(meta.RootElement.GetProperty("guid").GetGuid(),path);}
  var audio=scene.GameObjects.SelectMany(o=>o.Components.OfType<MiningAudio3D>()).Single();
  audio.RefineryLoop=Clip("refinery-machine");audio.RefineryConveyorLoop=Clip("refinery-conveyor");audio.RefineryOreImpact1=Clip("refinery-ore-1");audio.RefineryOreImpact2=Clip("refinery-ore-2");audio.RefineryOreImpact3=Clip("refinery-ore-3");audio.RefineryMenuOpen=Clip("refinery-menu-open");audio.MenuClose=Clip("menu-close");audio.RefineryConveyorVolume=.55f;audio.RefineryOreVolume=.65f;
  runtime.Serializer.Save(scene,Program.Root+"/Scenes/Main.bytescene");Console.WriteLine("REFINERY LAYERED AUDIO INSTALLED. Backup: "+folder);
 }
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);Console.WriteLine("PASS REFINERY AUDIO "+message);}
 public static void Verify(Scene scene,MiningOperation3D op)
 {
  var hash=SHA256.HashData(File.ReadAllBytes(Program.Root+"/Scenes/Main.bytescene"));var host=new SceneManager();host.LoadScene(scene);
  var audio=op.GameObject.GetComponent<MiningAudio3D>()!;var ui=scene.GameObjects.SelectMany(o=>o.Components.OfType<MiningBusinessUi3D>()).Single();var output=scene.FindGameObject(audio.RefineryObjectId)!.GetComponent<MiningRefineryOutput3D>()!;
  op.GameObject.GetComponent<MiningTrade3D>()!.Enabled=false;
  void Set(string name,int value)=>typeof(MiningOperation3D).GetProperty(name)!.SetValue(op,value);
  void Frame(int n=1){for(int i=0;i<n;i++){typeof(Time).GetMethod("Update",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[.05d]);typeof(Scene).GetMethod("UpdateInternal",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(scene,null);}}
  AudioSource3D Loop(string name)=>scene.GameObjects.Single(o=>o.Name=="Mining loop - "+name).GetComponent<AudioSource3D>()!;
  Set("RawOre",40);Set("RefinedOre",0);Frame(4);
  Check(!Loop("Refinery").IsPlaying&&!Loop("Refinery conveyor").IsPlaying,"idle refinery has no machine or belt loop");
  ui.Open(MiningScreen.Refinery);Check(audio.LastCue==nameof(MiningSoundCue.RefineryOpen),"opening refinery uses mechanical click rather than notification");ui.Close();Check(audio.LastCue==nameof(MiningSoundCue.PanelClose),"close plays its own sound");int before=audio.PlayedCues;ui.Close();Check(audio.PlayedCues==before,"already-closed menu does not click again");Frame(4);
  Check(op.BeginRefining(),"actual batch starts");Frame(5);Check(Loop("Refinery").IsPlaying&&Loop("Refinery conveyor").IsPlaying,"machine and conveyor have independent live voices");
  Check(Loop("Refinery conveyor").Spatial&&Loop("Refinery conveyor").Loop,"belt loop is spatial and looping");Check(audio.RefineryImpactCues==0,"no ore impacts before any landing");
  bool tail=false;long landed=0;for(int i=0;i<260;i++){Frame();if(op.RefiningAmount==0&&output.HasDeliveryWork&&i>90){tail|=Loop("Refinery conveyor").IsPlaying&&!Loop("Refinery").IsPlaying;}if(audio.RefineryImpactCues>0)Check(output.LandedIngotSequence>0,"impact follows actual landing");landed=output.LandedIngotSequence;if(i>150&&!output.HasDeliveryWork)break;}
  Check(tail,"conveyor keeps sounding after processor stops while output is delivering");Check(landed>0&&audio.RefineryImpactCues>0,"bucket clatter follows completed delivery");Frame(25);Check(!Loop("Refinery").IsPlaying&&!Loop("Refinery conveyor").IsPlaying,"both loops fade out after delivery");
  long impacts=audio.RefineryImpactCues;Set("RefinedOre",0);Frame(5);Check(audio.RefineryImpactCues==impacts,"selling cargo cannot create a false landing sound");
  Check(audio.PooledVoices<=audio.MaximumOneShotVoices&&audio.LoopCount==6,"layering respects bounded one-shot and loop counts");
  var sources=scene.GameObjects.SelectMany(o=>o.Components.OfType<AudioSource3D>()).ToArray();host.UnloadScene();Check(sources.All(s=>!s.IsPlaying),"unloading stops every audio voice");Check(SHA256.HashData(File.ReadAllBytes(Program.Root+"/Scenes/Main.bytescene")).SequenceEqual(hash),"verification preserves the authored scene");Console.WriteLine("REFINERY AUDIO NATIVE VERIFICATION COMPLETE");
 }
}
