using System.Text.Json;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Audio;
using ByteEngine.Core.Runtime;
using ByteEngine.Core.Scene;
using KarlsonDemo.Plugin;
internal static class MiningPolishAuthor
{
 public static void Build(Scene scene,GameProjectRuntime runtime)
 {
  string backup=Program.Root+"/.byteengine/Backups/defence-hud-audio-balance-"+DateTime.Now.ToString("yyyyMMdd-HHmmss");Directory.CreateDirectory(backup);File.Copy(Program.Root+"/Scenes/Main.bytescene",backup+"/Main.bytescene");
  var op=scene.GameObjects.SelectMany(o=>o.Components).OfType<MiningOperation3D>().Single();op.HunterMoveSpeed=5;op.RaiderMoveSpeed=3.8f;op.HunterHealth=50;op.RaiderHealth=75;op.HealthGrowthPerOperation=.06f;op.MaximumHealthGrowth=2;var contracts=JsonSerializer.Deserialize<MiningContract[]>(op.ContractsJson)!;contracts[0].HealthMultiplier=1;contracts[1].HealthMultiplier=1.2f;contracts[2].HealthMultiplier=1.45f;op.ContractsJson=JsonSerializer.Serialize(contracts);
  var audio=op.GameObject.GetComponent<MiningAudio3D>()!;
  string path="Assets/Audio/Weapons/First Hunt/pistol-shot.wav";using var meta=JsonDocument.Parse(File.ReadAllText(Program.Root+"/"+path+".meta"));var original=new AssetReference(meta.RootElement.GetProperty("guid").GetGuid(),path);
  audio.PistolShot=original;audio.Confirm=audio.Notification;audio.Credits=audio.Notification;audio.DrillVolumeMultiplier=2.5f;audio.PressVolumeMultiplier=3;audio.MachineHearingDistance=8;audio.MachineRolloff=.7f;
  foreach(var source in scene.GameObjects.SelectMany(o=>o.Components).OfType<AudioSource3D>())if(source.GameObject.Name.StartsWith("pistol shot sound",StringComparison.OrdinalIgnoreCase)){source.ClipReference=original;source.Volume=.48f;}
  runtime.Serializer.Save(scene,Program.Root+"/Scenes/Main.bytescene");Console.WriteLine("POLISH INSTALLED: original pistol restored, notification kept, loud machinery, stronger enemies. Backup "+backup);
 }
}
