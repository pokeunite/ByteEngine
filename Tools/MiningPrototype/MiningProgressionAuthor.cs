using System.Text.Json;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Runtime;
using ByteEngine.Core.Scene;
using KarlsonDemo.Plugin;
internal static class MiningProgressionAuthor
{
 public static void Build(Scene s,GameProjectRuntime runtime)
 {
  var op=s.GameObjects.SelectMany(o=>o.Components).OfType<MiningOperation3D>().Single();var trade=op.GameObject.GetComponent<MiningTrade3D>()!;
  string backup=Program.Root+"/.byteengine/Backups/ranked-defences-"+DateTime.Now.ToString("yyyyMMdd-HHmmss");Directory.CreateDirectory(backup);File.Copy(Program.Root+"/Scenes/Main.bytescene",backup+"/Main.bytescene");
  var original=JsonSerializer.Deserialize<CompanyUpgrade[]>(trade.ResearchDefinitionsJson)!.ToDictionary(n=>n.Id);var defaults=JsonSerializer.Deserialize<CompanyUpgrade[]>(new MiningTrade3D().ResearchDefinitionsJson)!;
  foreach(var n in defaults)if(original.TryGetValue(n.Id,out var old)){n.Cost=old.Cost;}
  trade.ResearchDefinitionsJson=JsonSerializer.Serialize(defaults);
  var player=s.FindGameObject(op.PlayerObjectId)!;var gun=player.GetComponent<FpsGunLab3D>()!;
  var planner=player.GetComponent<MiningDefensePlanner3D>()??player.AddComponent(new MiningDefensePlanner3D());planner.WorkshopObjectId=trade.GameObject.Id;planner.CameraObjectId=gun.CameraObjectId;
  string icon="Assets/UI/Mining/Artwork/company-pistol.png";File.Copy("Plugins/KarlsonDemo/Samples/Hud/Art/company-pistol.png",Program.Root+"/"+icon,true);Guid iconId=Guid.NewGuid();if(File.Exists(Program.Root+"/"+icon+".meta")){using var meta=JsonDocument.Parse(File.ReadAllText(Program.Root+"/"+icon+".meta"));iconId=meta.RootElement.GetProperty("guid").GetGuid();}File.WriteAllText(Program.Root+"/"+icon+".meta",JsonSerializer.Serialize(new{guid=iconId,type="Texture2D"}));
  foreach(var weapon in s.GameObjects.SelectMany(o=>o.Components).OfType<WeaponDefinition3D>().Where(w=>w.Slot==0)){weapon.ReserveCapacity=72;weapon.HudIcon=new AssetReference(iconId,icon);if(weapon.GameObject.GetComponent<WeaponInstance3D>() is {} instance)instance.ReserveAmmo=Math.Min(instance.ReserveAmmo,36);}
  MiningUiAuthor.Build(s,runtime);var ui=s.GameObjects.SelectMany(o=>o.Components).OfType<MiningBusinessUi3D>().Single();player.GetComponent<MiningThirdPerson3D>()!.InterfaceObjectId=ui.GameObject.Id;
  runtime.Serializer.Save(s,Program.Root+"/Scenes/Main.bytescene");Console.WriteLine("RANKED DEFENCE PROGRESSION INSTALLED. Backup "+backup);
 }
}
