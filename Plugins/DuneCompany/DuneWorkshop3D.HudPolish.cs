using System.Numerics;
using ByteEngine.Core.Graphics;
namespace DuneCompany;
public sealed partial class DuneWorkshop3D
{
 void ResizeMissionHud(float width,float height){
  PlaceWidget("Drive objective background",UiAnchor.TopLeft,new(20,20),new(460,104));
  PlaceText("Mission title",UiAnchor.TopLeft,new(36,34),15);
  PlaceText("Battle objective",UiAnchor.TopLeft,new(36,61),22);
  if(_ui.TryGetValue("Battle objective",out var objective)){var t=objective.GetComponent<UiText>()!;t.WrapWidth=422;}
  PlaceWidget("Recovery interaction background",UiAnchor.TopLeft,new(0,height-72),new(width,72));
  PlaceText("Recovery interaction",UiAnchor.TopLeft,new(184,height-52),16);
  if(_ui.TryGetValue("Recovery interaction",out var hint)){var t=hint.GetComponent<UiText>()!;t.WrapWidth=width-460;}
  PlaceWidget("Recover machine",UiAnchor.BottomLeft,new(20,-18),new(142,36));
  foreach(string n in new[]{"Return workshop","Quit contract"})PlaceWidget(n,UiAnchor.BottomRight,new(-20,-18),new(216,36));
  PlaceWidget("Mission balance card",UiAnchor.TopRight,new(-20,20),new(154,68));
  PlaceText("Mission balance caption",UiAnchor.TopRight,new(-42,25),12);
  if(_speedDial!=null){_speedDial.Offset=new(-20,-88);_speedDial.Size=new(208,148);}
  if(_speedNumber!=null){_speedNumber.Offset=new(-84,-102);_speedNumber.FontSize=24;}
 }
 float _resultRemaining;bool _resultWasComplete;
 void RefreshMissionPresentation(){
  if(!WorkshopV3)return;
  bool completed=!Building&&_contractRun&&_contractComplete;
  if(completed&&!_resultWasComplete)_resultRemaining=2.5f;
  if(!completed)_resultRemaining=0;else _resultRemaining=Math.Max(0,_resultRemaining-(float)ByteEngine.Core.Time.DeltaTime);
  _resultWasComplete=completed;GarageActive("Contract result",completed&&_resultRemaining>0);
  Text("Mission title",_contractRun?(WinchMission?"THE WELL ATE MY TRUCK":"STRANDED IN THE SAND"):"VEHICLE TEST");
  if(!Building){
   GarageActive("Recovery interaction",true);
   Text("Battle objective",completed?"Recovery delivered. Job paid.":_contractRun?RecoveryObjective:"Test your build in the sand.");
   if(completed)Text("Recovery interaction","Return to the garage to choose your next job.");
   else if(!_contractRun)Text("Recovery interaction","WASD drive  /  Space brake  /  R recover");
   Text("Contract result reward",$"+${JobPayment:N0}");
   Text("Contract result balance","PAYMENT RECEIVED / FILED UNDER COMPLETED");
  }
 }
}
