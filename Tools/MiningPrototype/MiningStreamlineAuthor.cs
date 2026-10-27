using ByteEngine.Core.Animation;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Runtime;
using ByteEngine.Core.Scene;
using ByteEngine.Core.VisualLogic;
using KarlsonDemo.Plugin;
internal static class MiningStreamlineAuthor
{
 public static void Build(Scene scene,GameProjectRuntime runtime)
 {
  string backup=Program.Root+"/.byteengine/Backups/no-dash-"+DateTime.Now.ToString("yyyyMMdd-HHmmss");Directory.CreateDirectory(backup);File.Copy(Program.Root+"/Scenes/Main.bytescene",backup+"/Main.bytescene");
  var op=scene.GameObjects.SelectMany(o=>o.Components).OfType<MiningOperation3D>().Single();var player=scene.FindGameObject(op.PlayerObjectId)!;
  if(player.GetComponent<AnimationController>() is {} animation)animation.TurnInPlaceEnabled=false;

  if(player.GetComponent<DashAbility3D>() is {} dash){dash.Cancel();dash.Enabled=false;}
  if(player.GetComponent<ResourceMeter3D>() is {} energy)energy.Enabled=false;
  foreach(var ui in scene.GameObjects.SelectMany(o=>o.Components).OfType<MiningBusinessUi3D>())ui.ShowEnergy=false;
  foreach(var trade in scene.GameObjects.SelectMany(o=>o.Components).OfType<MiningTrade3D>())
  {
   var nodes=System.Text.Json.JsonSerializer.Deserialize<CompanyUpgrade[]>(trade.ResearchDefinitionsJson)??[];
   if(nodes.FirstOrDefault(n=>n.Id=="survival2") is {} node){node.Name="Trauma plating";node.Description="Increase maximum health by another 25.";node.Effect=CompanyUpgradeEffect.Health;node.Amount=25;node.DashCooldownReduction=0;}
   trade.ResearchDefinitionsJson=System.Text.Json.JsonSerializer.Serialize(nodes);
  }
  const string relative="Assets/Events/Combat HUD and Dash.byteevents";string file=Program.Root+"/"+relative;
  File.Copy(file,backup+"/Combat HUD and Dash.byteevents");var codec=new EventModuleSerializer();var module=codec.Load(file);
  foreach(var rule in module.Rules)if(rule.DisplayName.Contains("dash",StringComparison.OrdinalIgnoreCase)||rule.DisplayName.Contains("energy",StringComparison.OrdinalIgnoreCase))rule.Enabled=false;
  codec.Save(module,file);runtime.Serializer.Save(scene,Program.Root+"/Scenes/Main.bytescene");Console.WriteLine("Disabled turn animations, dash input and energy HUD. Backup "+backup);
 }
}
