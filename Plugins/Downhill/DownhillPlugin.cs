using ByteEngine.Core.Plugins;
namespace Downhill;
public sealed class DownhillPlugin:IByteEnginePlugin
{
 public void Register(ByteEnginePluginContext c){
  c.RegisterSimpleComponent<BoardFeedback3D>("BoardFeedback3D",new("Board Feedback","Audio","Optional wheel rotation, rolling audio and wind driven by physical speed.","longboard sound wheel wind"));
  c.RegisterSimpleComponent<BoardController3D>("BoardController3D",new("Downhill Board","Character","Gravity-driven board with speed-sensitive carving, lateral grip and slides. All tuning and bindings are editable.","longboard downhill skateboard carve slide"));
  c.RegisterSimpleComponent<BoardRider3D>("BoardRider3D",new("Board Rider Animation","Animation","Native blend spaces and stance IK driven by measured board motion. Clip names and anchors are editable.","longboard rider feet animation"));
  c.RegisterSimpleComponent<BoardCamera3D>("BoardCamera3D",new("Downhill Follow Camera","Camera","Speed-based look-ahead, obstruction avoidance and restrained FOV.","downhill camera follow"));
  c.RegisterSimpleComponent<DownhillRun3D>("DownhillRun3D",new("Downhill Run","Gameplay","Editable start/finish, native HUD targets, pause, retry and finish state.","downhill run finish timer"));
 }
}
