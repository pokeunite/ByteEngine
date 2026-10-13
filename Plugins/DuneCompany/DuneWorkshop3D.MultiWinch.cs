using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.Scene;
namespace DuneCompany;
public sealed partial class DuneWorkshop3D
{
 readonly Dictionary<int,GameObject> _cableVisuals=[];
 string _winchNotice="";float _winchNoticeRemaining;
 public bool ConnectBlockWinch(int id){
  if(Building||!_contractRun||!WinchMission||_contractComplete||_physics==null||(NoBrakesMission&&_runawayFailed))return false;
  var targets=new List<Vector3?>{null};if(AnchorMission)targets.AddRange(GameObject.Scene!.GameObjects.SelectMany(o=>o.Components).OfType<DuneWinchAnchor3D>().Where(a=>a.Enabled&&a.GameObject.ActiveInHierarchy).Select(a=>(Vector3?)a.Transform.WorldPosition));
  if((RelayMission||NoBrakesMission)&&WinchConnected)targets.Remove(null);
  foreach(var target in targets.OrderBy(a=>Vector3.DistanceSquared(_physics.WinchPoint(id),a??_physics.TargetHitch)))if(_physics.ConnectCable(id,target)){if(target==null)_activeWinch=id;return true;}
  return false;
 }
 void UpdateAllWinches(){
  var physics=_physics!;physics.MaximumCableLength=24;_winchNoticeRemaining=Math.Max(0,_winchNoticeRemaining-(float)Time.DeltaTime);
  var lines=new List<string>();
  foreach(var b in _blocks.Where(b=>IsWinch(b.Type)).OrderBy(b=>b.Id)){
   var attach=BlockKey(b,"Attach");var reel=BlockKey(b,"Reel in");var pay=BlockKey(b,"Pay out");
   if(!_contractComplete&&Input.IsKeyPressed(attach)){if(physics.IsWinchConnected(b.Id)){physics.DisconnectWinch(b.Id);JobSound("hitch-release",.45f);}else if(!ConnectBlockWinch(b.Id)){int reach=b.Type==35?22:12;_winchNotice=NoBrakesMission?$"Match the truck speed and get within {reach} m.":$"No clear hook within {reach} m. Stop or reposition.";_winchNoticeRemaining=3;JobNotice(_winchNotice);}else{JobSound("hitch-lock",.5f);JobNotice(physics.IsAnchorCable(b.Id)?"ANCHOR SECURED":"VEHICLE CABLE SECURED");}}
   physics.SetWinchInput(b.Id,!_contractComplete?(Input.IsKeyDown(reel)?1:Input.IsKeyDown(pay)?-1:0):0);
   bool connected=physics.IsWinchConnected(b.Id);
   lines.Add($"#{b.Id} [{attach}] attach / [{reel}] in / [{pay}] out"+(connected?$" / {(physics.IsAnchorCable(b.Id)?"ANCHOR":"VEHICLE")} {physics.WinchLength(b.Id):0.0}m":""));
   if(!_cableVisuals.TryGetValue(b.Id,out var cable)){cable=GameObject.Scene!.CreateGameObject("Winch cable "+b.Id);cable.AddComponent(new ByteEngine.Core.Graphics.ThreeD.CableRenderer3D());_cableVisuals[b.Id]=cable;}
   cable.Active=connected;if(connected){var renderer=cable.GetComponent<ByteEngine.Core.Graphics.ThreeD.CableRenderer3D>()!;renderer.Start=physics.WinchPoint(b.Id);renderer.End=physics.WinchVisualTarget(b.Id);renderer.Length=Math.Max(Vector3.Distance(renderer.Start,renderer.End),physics.WinchLength(b.Id));}

  }
  RecoveryConnectionHint=string.Join("\n",lines.Take(3))+(_winchNoticeRemaining>0?"\n"+_winchNotice:"");if(_towLink!=null)_towLink.Active=false;
 }
 void ClearCableVisuals(){foreach(var cable in _cableVisuals.Values)if(cable.Scene!=null)GameObject.Scene!.DestroyGameObject(cable);_cableVisuals.Clear();}
}
