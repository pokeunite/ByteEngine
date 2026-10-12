using System.Numerics;
namespace DuneCompany;
public sealed partial class DuneWorkshop3D
{
 readonly HashSet<Guid> _relayStations=[];
 void UpdateRelayProgress()
 {
  if(!RelayMission||_physics==null||!WinchConnected)return;
  foreach(var anchor in GameObject.Scene!.GameObjects.SelectMany(o=>o.Components).OfType<DuneWinchAnchor3D>())
  {
   if(!anchor.Enabled||!anchor.GameObject.ActiveInHierarchy||Vector3.Distance(RecoveryPosition,anchor.Transform.WorldPosition)>14)continue;
   if(_blocks.Any(b=>IsWinch(b.Type)&&_physics.IsAnchorCable(b.Id)&&Vector3.Distance(_physics.WinchTarget(b.Id),anchor.Transform.WorldPosition)<.1f))
    _relayStations.Add(anchor.GameObject.Id);
  }
 }
}
