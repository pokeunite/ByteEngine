using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Scene;
namespace GoblinCombat;
public sealed class CombatRoom3D:Component {
 bool paused;CombatActor3D? hero;
 protected override void OnStart(){hero=GameObject.Scene!.GameObjects.Select(o=>o.GetComponent<CombatActor3D>()).FirstOrDefault(a=>a?.Player==true);}
 protected override void OnUpdate(){var s=GameObject.Scene!;if(Input.IsKeyPressed(Key.R)){s.SimulationTimeScale=1;s.RequestRestart();return;}if(Input.IsKeyPressed(Key.Escape)){paused=!paused;s.SimulationTimeScale=paused?0:1;}
  if(hero==null)return;void Text(string name,string value){var t=s.FindGameObject(name)?.GetComponent<UiText>();if(t!=null)t.Text=value;}
  Text("Vitals",$"HEALTH {hero.Health.CurrentHealth:0}    STAMINA {hero.Timing.Stamina:0}");
  Text("Timing",$"{hero.Timing.State}    COMBO {(hero.Timing.Attacking?hero.Timing.Stage+1:0)}/3    HITS {hero.HitsLanded}");
  int remaining=s.GameObjects.Count(o=>o.GetComponent<CombatActor3D>() is {Player:false} a&&!a.Health.IsDead);
  Text("Status",paused?"PAUSED / ESC TO RESUME":hero.Health.IsDead?"DEFEATED / R TO RETRY":remaining==0?"ARENA CLEARED / R TO GO AGAIN":"READ THE WINDUP. STRIKE, THEN DODGE.");
 }
 protected override void OnLateUpdate(){if(hero==null)return;var camera=GameObject.Scene!.FindGameObject("Camera");if(camera==null)return;var target=hero.Transform.WorldPosition+new Vector3(0,.85f,0);var position=target+new Vector3(0,3.8f,5.4f);camera.Transform.WorldPosition=position;Matrix4x4.Invert(Matrix4x4.CreateLookAt(position,target,Vector3.UnitY),out var basis);camera.Transform.WorldRotation=Quaternion.CreateFromRotationMatrix(basis);}
}

