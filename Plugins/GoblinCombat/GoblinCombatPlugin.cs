using ByteEngine.Core.Plugins;
namespace GoblinCombat;
public sealed class GoblinCombatPlugin:IByteEnginePlugin
{
 public void Register(ByteEnginePluginContext c){
  c.RegisterSimpleComponent<CombatActor3D>("CombatActor3D",new("Combat Actor","Gameplay","Buffered, committed three-hit melee, dodge and enemy timing.","goblin melee combo"));
  c.RegisterSimpleComponent<CombatAnimation3D>("CombatAnimation3D",new("Combat Animation","Gameplay","Native skeletal combo playback and a stable right-hand sword socket.","goblin animation sword"));
  c.RegisterSimpleComponent<CombatRoom3D>("CombatRoom3D",new("Combat Test Room","Gameplay","Combat HUD, restart, pause and camera.","goblin test combat"));
 }
}


