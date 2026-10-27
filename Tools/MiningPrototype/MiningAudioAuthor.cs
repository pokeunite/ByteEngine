using System.Security.Cryptography;
using System.Text.Json;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Audio;
using ByteEngine.Core.Runtime;
using ByteEngine.Core.Scene;
using KarlsonDemo.Plugin;
internal static class MiningAudioAuthor
{
 public static void Build(Scene scene,GameProjectRuntime runtime)
 {
  string backup=Program.Root+"/.byteengine/Backups/selected-audio-"+DateTime.Now.ToString("yyyyMMdd-HHmmss");Directory.CreateDirectory(backup);File.Copy(Program.Root+"/Scenes/Main.bytescene",backup+"/Main.bytescene");
  var reloads=Directory.GetFiles(Program.Root+"/Assets/Audio","*reload*",SearchOption.AllDirectories).Where(f=>f.EndsWith(".wav")).ToDictionary(f=>f,f=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(f))));
  string target=Program.Root+"/Assets/Audio/Mining/Selected";Directory.CreateDirectory(target);
  foreach(string file in Directory.GetFiles("artifacts/mining-audio"))if(!file.EndsWith("body-hit-source.wav"))
  {string dest=target+"/"+Path.GetFileName(file);if(File.Exists(dest))File.Copy(dest,backup+"/"+Path.GetFileName(file));File.Copy(file,dest,true);if(!File.Exists(dest+".meta"))File.WriteAllText(dest+".meta",JsonSerializer.Serialize(new{guid=Guid.NewGuid(),type=file.EndsWith(".wav")?"AudioClip":"Unknown"}));}
  string source=Program.Root+"/SourceAudio/Freesound Selected";Directory.CreateDirectory(source);foreach(string file in Directory.GetFiles("artifacts/selected-freesound"))if(!file.EndsWith(".html"))File.Copy(file,source+"/"+Path.GetFileName(file),true);
  AssetReference Clip(string name){string path="Assets/Audio/Mining/Selected/"+name+".wav";using var json=JsonDocument.Parse(File.ReadAllText(Program.Root+"/"+path+".meta"));return new(json.RootElement.GetProperty("guid").GetGuid(),path);}
  var op=scene.GameObjects.SelectMany(o=>o.Components).OfType<MiningOperation3D>().Single();var ui=scene.GameObjects.SelectMany(o=>o.Components).OfType<MiningBusinessUi3D>().Single();var audio=op.GameObject.GetComponent<MiningAudio3D>()??op.GameObject.AddComponent(new MiningAudio3D());
  audio.OperationObjectId=op.GameObject.Id;audio.WorkshopObjectId=ui.WorkshopObjectId;audio.PressObjectId=ui.AmmoMachineObjectId;audio.RefineryObjectId=ui.RefineryMachineObjectId;
  string originalPath="Assets/Audio/Weapons/First Hunt/pistol-shot.wav";using var originalMeta=JsonDocument.Parse(File.ReadAllText(Program.Root+"/"+originalPath+".meta"));audio.PistolShot=new(originalMeta.RootElement.GetProperty("guid").GetGuid(),originalPath);audio.DryTrigger=Clip("dry-trigger");audio.AmmoClink=Clip("ammo-clink");audio.AlienGrowl=Clip("alien-growl");audio.MetalHit=Clip("metal-hit");audio.BodyHit=Clip("body-hit-1");audio.BodyHit2=Clip("body-hit-2");audio.BodyHit3=Clip("body-hit-3");audio.PlayerHurt=Clip("player-hurt");
  audio.DrillLoop=Clip("drill-loop");audio.RefineryLoop=Clip("refinery-loop");audio.PressLoop=Clip("press-loop");audio.CriticalAlarm=Clip("critical-alarm");audio.Confirm=Clip("ui-notification");audio.Notification=Clip("ui-notification");audio.Alert=Clip("ui-alert");audio.Error=Clip("ui-error");audio.Credits=Clip("ui-notification");audio.Explosion=Clip("explosion");audio.Cannon=Clip("cannon");
  int shots=0;foreach(var src in scene.GameObjects.SelectMany(o=>o.Components).OfType<AudioSource3D>())if(src.ClipReference.ProjectPath.EndsWith("/pistol-shot.wav",StringComparison.OrdinalIgnoreCase)){src.ClipReference=audio.PistolShot;src.Volume=.48f;shots++;}
  if(shots==0)throw new InvalidDataException("Existing pistol shot emitter was not found; reloads not touched.");
  foreach(var pair in reloads)if(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(pair.Key)))!=pair.Value)throw new InvalidDataException("Reload asset unexpectedly changed");
  runtime.Serializer.Save(scene,Program.Root+"/Scenes/Main.bytescene");File.WriteAllText(backup+"/reload-hashes.json",JsonSerializer.Serialize(reloads));Console.WriteLine($"SELECTED AUDIO INSTALLED: {shots} shot emitters, {reloads.Count} reload assets preserved. Backup {backup}");
 }
}
