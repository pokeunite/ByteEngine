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
  float controlsHeight=_blocks.Count(b=>IsWinch(b.Type))>1?100:72;
  PlaceWidget("Recovery interaction background",UiAnchor.TopLeft,new(20,height-controlsHeight-18),new(width-268,controlsHeight));
  PlaceText("Recovery interaction",UiAnchor.TopLeft,new(184,height-controlsHeight-6),16);
  if(_ui.TryGetValue("Recovery interaction",out var hint)){var t=hint.GetComponent<UiText>()!;t.WrapWidth=width-460;}
  PlaceWidget("Recover machine",UiAnchor.BottomLeft,new(20,-18),new(142,36));
  foreach(string n in new[]{"Return workshop","Quit contract"})PlaceWidget(n,UiAnchor.TopRight,new(-20,99),new(154,34));
  PlaceWidget("Mission balance card",UiAnchor.TopRight,new(-20,20),new(154,68));
  PlaceText("Mission balance caption",UiAnchor.TopRight,new(-42,25),12);
  if(_speedDial!=null){_speedDial.Offset=new(-20,-20);_speedDial.Size=new(208,148);}
  if(_speedNumber!=null){_speedNumber.Offset=new(-84,-34);_speedNumber.FontSize=24;}
  PlaceWidget("Contract result",UiAnchor.TopCenter,new(0,36),new(340,102));
  PlaceWidget("Contract result stripe",UiAnchor.TopLeft,new(0,0),new(4,102));
  PlaceText("Contract result heading",UiAnchor.TopLeft,new(18,10),16);PlaceText("Contract result reward",UiAnchor.TopLeft,new(18,32),28);PlaceText("Contract result balance",UiAnchor.TopLeft,new(18,76),12);
 }
 float _resultRemaining;bool _resultWasComplete;
 void RefreshMissionPresentation(){
  bool completed=!Building&&_contractRun&&_contractComplete;
  if(completed&&!_resultWasComplete)_resultRemaining=2.5f;
  if(!completed)_resultRemaining=0;else _resultRemaining=Math.Max(0,_resultRemaining-(float)ByteEngine.Core.Time.DeltaTime);
  _resultWasComplete=completed;GarageActive("Contract result",completed&&_resultRemaining>0);
  if(completed&&_resultRemaining<=0){Command("Return to base");_resultWasComplete=false;return;}
  if(!WorkshopV3)return;
  Text("Mission title",_contractRun?ContractTitle(CurrentContract).ToUpperInvariant():"VEHICLE TEST");
  if(!Building){
   GarageActive("Recovery interaction",true);
   Text("Battle objective",completed?"Contract complete. Payment received.":_contractRun?RecoveryObjective:"Test your build in the sand.");
   if(completed)Text("Recovery interaction","Return to the garage to choose your next job.");
   else if(!_contractRun)Text("Recovery interaction",$"{DriveKey("Forward")}/{DriveKey("Reverse")} drive  /  {DriveKey("Left")}/{DriveKey("Right")} steer\n{DriveKey("Brake")} brake  /  {DriveKey("Drift")} drift  /  {DriveKey("Recover")} recover");
   Text("Contract result reward",$"+${JobPayment:N0}");
   Text("Contract result balance","PAID / RETURNING TO GARAGE");
  }
 }
}
