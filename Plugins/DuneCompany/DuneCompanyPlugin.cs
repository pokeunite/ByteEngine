using System.Text.Json.Nodes;
using ByteEngine.Core.Plugins;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Serialization;
using ByteEngine.Core.Serialization.SerializationModels;
namespace DuneCompany;
public sealed class DuneCompanyPlugin : IByteEnginePlugin
{
 public void Register(ByteEnginePluginContext c)
 {
  c.RegisterComponent(new MainMenuCodec(),new("Dune Main Menu","UI","Native menu state, saved vehicle continuation and preferences.","menu"));
  foreach(var command in new[]{"Continue","New Game","Settings","Credits","Close Settings","Close Credits","Volume Up","Volume Down","Quality"}){string action=command;c.RegisterAction(new(){Id="bytebard.dunecompany.menu."+command.ToLowerInvariant().Replace(' ','_'),Category="Dune Company / Menu",DisplayName=command,Execute=(_,e)=>e.Self.GetComponent<DuneMainMenu3D>()?.Command(action)});}
  c.RegisterCondition(new(){Id="bytebard.dunecompany.building",Category="Dune Company",DisplayName="Workshop Is Building",Evaluate=(_,e)=>e.Self.GetComponent<DuneWorkshop3D>()?.Building==true});
  c.RegisterSimpleComponent<DuneRecoveryContract3D>("DuneRecoveryContract3D",new("Dune Recovery Contract","Gameplay","Place the stranded buggy anchor; edit payout, delivery size and shallow sand depression.","recovery hitch towing contract"));
  c.RegisterSimpleComponent<DuneMinimap3D>("DuneMinimap3D",new("Dune Mission Minimap","UI","North-up world map with a live vehicle position marker.","minimap mission map"));
  c.RegisterSimpleComponent<DuneSalvageRoute3D>("DuneSalvageRoute3D",new("Dune Salvage Route 3D","Gameplay","Garage-to-salvage navigation and arrival feedback.","dune mission route salvage garage"));
  c.RegisterSimpleComponent<DuneWorldObstacle3D>("DuneWorldObstacle3D",new("Dune World Obstacle 3D","Physics","Editable static prop collision for vehicle driving.","dune obstacle garage rock collision"));
  c.RegisterSimpleComponent<DuneBlock3D>("DuneBlock3D",new("Dune Vehicle Block 3D","Gameplay","Authored block type, connectivity and wheel settings. Move the object to edit its build pose.","dune block vehicle part"));
  c.RegisterSimpleComponent<DuneBountyTarget3D>("DuneBountyTarget3D",new("Dune Bounty Target 3D","Gameplay","Destructible proving-ground contract target.","target combat bounty"));
  c.RegisterComponent(new WorkshopCodec(),new("Dune Vehicle Workshop","Gameplay","Editable block construction and desert vehicle test driving.","dune desert vehicle builder workshop"));
  foreach(var command in new[]{"Toggle drive","Save session","Save vehicle","Load vehicle","Undo","Redo","Delete selected","Recover vehicle","Tune selected","Quit mission","Return to base","Open contracts","Open blueprints","Close garage overlay","Deploy contract","Connect recovery","Disconnect recovery"})
  {string action=command;c.RegisterAction(new(){Id="bytebard.dunecompany."+command.ToLowerInvariant().Replace(' ','_'),Category="Dune Company",DisplayName=command,Execute=(_,e)=>e.Self.GetComponent<DuneWorkshop3D>()?.Command(action)});}
 }
}
internal sealed class WorkshopCodec:IComponentCodec
{
 public string TypeName=>"bytebard.dunecompany.DuneWorkshop3D";public Type ComponentType=>typeof(DuneWorkshop3D);
 public ComponentData Serialize(Component c,ComponentSerializationContext x){var w=(DuneWorkshop3D)c;return new(){Type=TypeName,Properties=new JsonObject{["build"]=w.BuildJson,["maximumSpeed"]=w.MaximumSpeed,["automaticWeapons"]=w.AutomaticWeapons,["vehicleSfxVolume"]=w.VehicleSfxVolume,["dustAmount"]=w.DustAmount,["exhaustAmount"]=w.ExhaustAmount}};}
 public Component Deserialize(ComponentData d,ComponentSerializationContext x)=>new DuneWorkshop3D{Assets=x.Assets,ProjectRoot=x.ProjectRoot,BuildJson=d.Properties["build"]?.GetValue<string>()??"",MaximumSpeed=d.Properties["maximumSpeed"]?.GetValue<float>()??32,VehicleSfxVolume=d.Properties["vehicleSfxVolume"]?.GetValue<float>()??.8f,DustAmount=d.Properties["dustAmount"]?.GetValue<float>()??1,ExhaustAmount=d.Properties["exhaustAmount"]?.GetValue<float>()??1,AutomaticWeapons=d.Properties["automaticWeapons"]?.GetValue<bool>()??true};
}


internal sealed class MainMenuCodec:IComponentCodec
{
 public string TypeName=>"bytebard.dunecompany.DuneMainMenu3D";public Type ComponentType=>typeof(DuneMainMenu3D);
 public ComponentData Serialize(Component c,ComponentSerializationContext x)=>new(){Type=TypeName};
 public Component Deserialize(ComponentData d,ComponentSerializationContext x)=>new DuneMainMenu3D{ProjectRoot=x.ProjectRoot};
}
